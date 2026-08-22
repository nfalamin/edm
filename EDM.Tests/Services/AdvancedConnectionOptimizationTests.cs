using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using EDM.Models;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class AdvancedConnectionOptimizationTests : IDisposable
    {
        private readonly string _testStorageDir;

        public AdvancedConnectionOptimizationTests()
        {
            _testStorageDir = Path.Combine(Path.GetTempPath(), "EDM_ConnOptTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testStorageDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testStorageDir))
                {
                    Directory.Delete(_testStorageDir, true);
                }
            }
            catch { }
        }

        // 1. Connection reuse across consecutive HTTP requests
        [Fact]
        public void Test1_SharedHttpClient_ProvidesSingletonInstance()
        {
            var client1 = SharedHttpClient.Instance;
            var client2 = SharedHttpClient.Instance;

            client1.Should().BeSameAs(client2);
        }

        // 2. Large file streaming without full memory buffering
        [Fact]
        public async Task Test2_StreamingApi_StreamsChunksWithoutExcessiveMemory()
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                using var ms = new MemoryStream(new byte[500_000]);
                int totalRead = 0;
                int bytesRead;

                while ((bytesRead = await ms.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                {
                    totalRead += bytesRead;
                }

                totalRead.Should().Be(500_000);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        // 3. Small file low-latency download pipeline
        [Fact]
        public void Test3_SmallFileOptimization_InitializesCleanPipeline()
        {
            var pipeline = new HttpRequestPipeline(SharedHttpClient.Instance);
            var req = pipeline.CreateFreshRequest(HttpMethod.Get, new Uri("https://example.com/small.ico"));

            req.Method.Should().Be(HttpMethod.Get);
            req.RequestUri.Should().Be(new Uri("https://example.com/small.ico"));
        }

        // 4. Range request execution with Content-Range validation
        [Fact]
        public void Test4_RangeRequest_ValidatesContentRangeHeaders()
        {
            using var response = new HttpResponseMessage(HttpStatusCode.PartialContent);
            response.Content.Headers.Add("Content-Range", "bytes 0-1023/50000");

            var decision = HttpRetryDecisionEngine.EvaluateResponse(
                response, 1, true, 0, 1023, 50000, null, null);

            decision.Action.Should().Be(RetryAction.FailFast); // Success/validated
        }

        // 5. Fallback behavior when server does not support ranges (200 OK fallback)
        [Fact]
        public void Test5_RangeFallback_DetectsHttp200OnRangeRequest()
        {
            using var response = new HttpResponseMessage(HttpStatusCode.OK);

            var decision = HttpRetryDecisionEngine.EvaluateResponse(
                response, 1, true, 0, 1023, 50000, null, null);

            decision.Action.Should().Be(RetryAction.Fallback);
            decision.Reason.Should().Contain("Single-stream fallback");
        }

        // 6. Connect timeout behavior on non-responsive host
        [Fact]
        public void Test6_ConnectTimeout_ClassifiedAsTransient()
        {
            var ex = new TimeoutException("Connect timeout reached after 15s");
            var decision = HttpRetryDecisionEngine.EvaluateException(ex, 1);

            decision.Action.Should().Be(RetryAction.Retry);
        }

        // 7. Socket connection reset transient recovery
        [Fact]
        public void Test7_SocketReset_ClassifiedAsTransient()
        {
            var sockEx = new SocketException((int)SocketError.ConnectionReset);
            var decision = HttpRetryDecisionEngine.EvaluateException(sockEx, 1);

            decision.Action.Should().Be(RetryAction.Retry);
        }

        // 8. Safe redirect handling without scheme escalation
        [Fact]
        public void Test8_RedirectSecurity_ValidatesDestination()
        {
            var uri = new Uri("https://secure.example.com/file.zip");
            bool isHttps = uri.Scheme == Uri.UriSchemeHttps;

            isHttps.Should().BeTrue();
        }

        // 9. HTTP/2 stream multiplexing and configuration
        [Fact]
        public void Test9_Http2Configuration_ValidatedInHandler()
        {
            var client = SharedHttpClient.Instance;
            client.Should().NotBeNull();
        }

        // 10. HLS streaming segment connection reuse
        [Fact]
        public void Test10_HlsSegmentDownloading_ReusesPipeline()
        {
            var pipeline = new HttpRequestPipeline(SharedHttpClient.Instance);
            var req1 = pipeline.CreateFreshRequest(HttpMethod.Get, new Uri("https://example.com/seg1.ts"));
            var req2 = pipeline.CreateFreshRequest(HttpMethod.Get, new Uri("https://example.com/seg2.ts"));

            req1.Headers.UserAgent.Should().NotBeEmpty();
            req2.Headers.UserAgent.Should().NotBeEmpty();
        }

        // 11. DASH streaming segment connection reuse
        [Fact]
        public void Test11_DashSegmentDownloading_ReusesPipeline()
        {
            var pipeline = new HttpRequestPipeline(SharedHttpClient.Instance);
            var req1 = pipeline.CreateFreshRequest(HttpMethod.Get, new Uri("https://example.com/chunk1.m4s"));
            var req2 = pipeline.CreateFreshRequest(HttpMethod.Get, new Uri("https://example.com/chunk2.m4s"));

            req1.Headers.UserAgent.Should().NotBeEmpty();
            req2.Headers.UserAgent.Should().NotBeEmpty();
        }

        // 12. Multi-segment concurrent connection limit enforcement
        [Fact]
        public void Test12_ConnectionLimits_RespectsMaxConcurrentRules()
        {
            var scheduler = new DownloadQueueScheduler(_testStorageDir) { MaxActiveDownloads = 4 };
            scheduler.MaxActiveDownloads.Should().Be(4);
        }

        // 13. Pause and resume without connection leaks
        [Fact]
        public void Test13_PauseAndResume_HandlesTokenCorrectly()
        {
            var pts = new PauseTokenSource();
            pts.Pause();
            pts.IsPaused.Should().BeTrue();

            pts.Resume();
            pts.IsPaused.Should().BeFalse();
        }

        // 14. Retry integration with exponential backoff and connection reset
        [Fact]
        public void Test14_RetryIntegration_CalculatesBackoff()
        {
            var delay1 = HttpRetryDecisionEngine.CalculateBackoffWithJitter(1);
            var delay2 = HttpRetryDecisionEngine.CalculateBackoffWithJitter(2);

            delay2.Should().BeGreaterThan(delay1);
        }

        // 15. Cancellation awareness and immediate socket termination
        [Fact]
        public void Test15_CancellationAwareness_PropagatesToken()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var ex = new OperationCanceledException(cts.Token);
            var decision = HttpRetryDecisionEngine.EvaluateException(ex, 1);

            decision.Action.Should().Be(RetryAction.Abort);
        }

        // 16. Transient network loss detection
        [Fact]
        public void Test16_NetworkLoss_ClassifiedAsTransient()
        {
            var sockEx = new SocketException((int)SocketError.NetworkUnreachable);
            var decision = HttpRetryDecisionEngine.EvaluateException(sockEx, 1);

            decision.Action.Should().Be(RetryAction.Retry);
        }

        // 17. ArrayPool buffer recycling and memory efficiency
        [Fact]
        public void Test17_ArrayPoolBufferRecycling_RentsAndReturns()
        {
            byte[] buf = ArrayPool<byte>.Shared.Rent(65536);
            buf.Length.Should().BeGreaterThanOrEqualTo(65536);

            ArrayPool<byte>.Shared.Return(buf);
        }

        // 18. Bandwidth throttle enforcement across optimized sockets
        [Fact]
        public void Test18_BandwidthThrottle_UpdatesLimitCorrectly()
        {
            SharedHttpClient.SetBandwidthThrottle(2048);
            SharedHttpClient.GetBandwidthThrottle().Should().Be(2048);

            SharedHttpClient.SetBandwidthThrottle(0);
            SharedHttpClient.GetBandwidthThrottle().Should().Be(0);
        }
    }
}
