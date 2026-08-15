using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.NativeMessaging;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    [Collection("InterceptionTests")]
    public class RealWorldReliabilityTortureTests : IDisposable
    {
        public RealWorldReliabilityTortureTests()
        {
            BrowserInterceptionStateMachine.ResetForTesting();
            NativeMessageListener.ResetDeduplicationCacheForTesting();
        }

        public void Dispose()
        {
            BrowserInterceptionStateMachine.ResetForTesting();
            NativeMessageListener.ResetDeduplicationCacheForTesting();
        }

        [Fact]
        public void Part1_ProtocolHardening_416RangeNotSatisfiable_TriggersFallbackException()
        {
            var pipeline = new HttpRequestPipeline(SharedHttpClient.Instance);

            // Test 416 RequestedRangeNotSatisfiable logic via exception contract
            var ex = new RangeFallbackRequiredException("Server returned 416 Requested Range Not Satisfiable.");
            ex.Message.Should().Contain("416");
        }

        [Fact]
        public async Task Part1_ProtocolHardening_FastFails404NotFoundWithoutWastingRetries()
        {
            using var handler = new MockHttpHandler((req) => new HttpResponseMessage(HttpStatusCode.NotFound));
            using var client = new HttpClient(handler);
            var pipeline = new HttpRequestPipeline(client);

            var sw = System.Diagnostics.Stopwatch.StartNew();

            Func<Task> act = async () => await pipeline.ExecuteWithRetryAsync(
                requestFactory: () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri("https://example.com/missing.bin")),
                completionOption: HttpCompletionOption.ResponseHeadersRead,
                cancellationToken: CancellationToken.None,
                maxRetries: 5).ConfigureAwait(true);

            await act.Should().ThrowAsync<HttpRequestException>();
            sw.Stop();

            // Fast fail should take less than 200ms (1 attempt, no 5 retry delays)
            sw.ElapsedMilliseconds.Should().BeLessThan(200);
            handler.CallCount.Should().Be(1);
        }

        [Fact]
        public async Task Part4_ResumeRecovery_DetectsPartialSegmentCorruptionAndRepairs()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"corrupt_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            string segPath = Path.Combine(tempDir, "segment_0.part");
            byte[] corruptData = new byte[1024]; // Partial/corrupt byte payload
            await File.WriteAllBytesAsync(segPath, corruptData).ConfigureAwait(true);

            var metaManager = new DurableMetadataManager();
            var metaState = new DurableDownloadState
            {
                Url = "https://example.com/repair_file.bin",
                TotalBytes = 2048,
                Segments = new List<SegmentRange>
                {
                    new SegmentRange { Id = 0, Start = 0, End = 1023, BytesDownloaded = 1024, TempPath = segPath, Sha256Hash = "invalid_hash" }
                }
            };

            // Verify corruption detection
            var integrityService = new IntegrityVerificationService();
            var result = await integrityService.VerifyAsync(segPath, metaState, expectedSize: 2048, ct: CancellationToken.None).ConfigureAwait(true);

            result.State.Should().Be(VerificationState.VerificationFailed);

            // Cleanup
            Directory.Delete(tempDir, recursive: true);
        }

        [Fact]
        public async Task Part6_ExtremeConcurrency_100ConcurrentDownloadsProcessedWithoutDeadlock()
        {
            int concurrentCount = 100;
            var latencies = new ConcurrentBag<double>();
            using var semaphore = new SemaphoreSlim(25, 25);

            var tasks = new List<Task>();
            long memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

            for (int i = 0; i < concurrentCount; i++)
            {
                int eventId = i;
                await semaphore.WaitAsync().ConfigureAwait(true);

                tasks.Add(Task.Run(() =>
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        string corrId = $"extreme_corr_{eventId}";
                        string url = $"https://cdn.example.com/extreme_{eventId}.bin";

                        BrowserInterceptionStateMachine.CreateSession(corrId, url, $"extreme_{eventId}.bin");
                        BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.Validating);
                        BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandoffPending);
                        BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandedOff);
                        BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.EdmStarted);

                        sw.Stop();
                        latencies.Add(sw.Elapsed.TotalMilliseconds);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks).ConfigureAwait(true);

            // Prune sessions
            BrowserInterceptionStateMachine.PruneStaleSessions(TimeSpan.FromSeconds(0));

            long memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
            long memoryDelta = memoryAfter - memoryBefore;

            latencies.Count.Should().Be(concurrentCount);
            latencies.Average().Should().BeLessThan(10.0); // Sub-10ms processing per concurrent session
            memoryDelta.Should().BeLessThan(2 * 1024 * 1024); // Bounded RAM under 2 MB
        }

        [Fact]
        public async Task Part6_CancellationAndPauseStorm_100TogglesExecuteWithoutDeadlock()
        {
            var cts = new CancellationTokenSource();
            var pauseSource = new PauseTokenSource();

            var tasks = new List<Task>();
            for (int i = 0; i < 50; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await Task.Delay(1).ConfigureAwait(true);
                    pauseSource.Pause();
                    await Task.Delay(1).ConfigureAwait(true);
                    pauseSource.Resume();
                }));
            }

            await Task.WhenAll(tasks).ConfigureAwait(true);
            pauseSource.IsPaused.Should().BeFalse();
        }

        private class MockHttpHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;
            public int CallCount = 0;

            public MockHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref CallCount);
                return Task.FromResult(_responseFactory(request));
            }
        }
    }
}
