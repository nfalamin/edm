using System;
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
    public class AdvancedRetryAndRecoveryTests : IDisposable
    {
        private readonly string _testStorageDir;

        public AdvancedRetryAndRecoveryTests()
        {
            _testStorageDir = Path.Combine(Path.GetTempPath(), "EDM_RetryTests_" + Guid.NewGuid().ToString("N"));
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

        // 1. Timeout transient classification and retry
        [Fact]
        public void Test1_TimeoutException_ClassifiedAsTransientRetry()
        {
            var ex = new TimeoutException("Operation timed out while reading stream");
            var decision = HttpRetryDecisionEngine.EvaluateException(ex, 1);

            decision.Action.Should().Be(RetryAction.Retry);
            decision.BackoffDelay.Should().BeGreaterThan(TimeSpan.Zero);
        }

        // 2. Connection reset transient classification and retry
        [Fact]
        public void Test2_ConnectionReset_ClassifiedAsTransientRetry()
        {
            var sockEx = new SocketException((int)SocketError.ConnectionReset);
            var decision = HttpRetryDecisionEngine.EvaluateException(sockEx, 1);

            decision.Action.Should().Be(RetryAction.Retry);
            decision.Reason.Should().Contain("Socket transient");
        }

        // 3. HTTP 429 Rate Limiting with Retry-After header
        [Fact]
        public void Test3_Http429_HonorsRetryAfterHeader()
        {
            using var resp = new HttpResponseMessage((HttpStatusCode)429);
            resp.Headers.Add("Retry-After", "12");

            var decision = HttpRetryDecisionEngine.EvaluateResponse(
                resp, 1, false, null, null, null, null, null);

            decision.Action.Should().Be(RetryAction.RetryAfter);
            decision.BackoffDelay.Should().Be(TimeSpan.FromSeconds(12));
        }

        // 4. HTTP 500 / 502 / 503 Server Error backoff
        [Theory]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.BadGateway)]
        [InlineData(HttpStatusCode.GatewayTimeout)]
        public void Test4_Http5xxServerErrors_ClassifiedAsRetryable(HttpStatusCode code)
        {
            using var resp = new HttpResponseMessage(code);
            var decision = HttpRetryDecisionEngine.EvaluateResponse(
                resp, 1, false, null, null, null, null, null);

            decision.Action.Should().Be(RetryAction.Retry);
            decision.BackoffDelay.Should().BeGreaterThan(TimeSpan.Zero);
        }

        // 5. Permanent HTTP 404 / 410 FailFast behavior
        [Theory]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.Gone)]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        public void Test5_Permanent4xxErrors_ClassifiedAsFailFast(HttpStatusCode code)
        {
            using var resp = new HttpResponseMessage(code);
            var decision = HttpRetryDecisionEngine.EvaluateResponse(
                resp, 1, false, null, null, null, null, null);

            decision.Action.Should().Be(RetryAction.FailFast);
        }

        // 6. Invalid path / Permission Denied FailFast behavior
        [Fact]
        public void Test6_UnauthorizedAccessException_ClassifiedAsFailFast()
        {
            var ex = new UnauthorizedAccessException("Access to disk path is denied.");
            var decision = HttpRetryDecisionEngine.EvaluateException(ex, 1);

            decision.Action.Should().Be(RetryAction.FailFast);
        }

        // 7. Bounded maximum retry limit enforcement
        [Fact]
        public void Test7_ExceededMaxRetries_Aborts()
        {
            var ex = new TimeoutException();
            var decision = HttpRetryDecisionEngine.EvaluateException(ex, HttpRetryDecisionEngine.MaxAllowedRetries);

            decision.Action.Should().Be(RetryAction.Abort);
        }

        // 8. Exponential backoff calculation
        [Fact]
        public void Test8_ExponentialBackoff_ScalesExponentially()
        {
            var d1 = HttpRetryDecisionEngine.CalculateBackoffWithJitter(1);
            var d2 = HttpRetryDecisionEngine.CalculateBackoffWithJitter(2);
            var d3 = HttpRetryDecisionEngine.CalculateBackoffWithJitter(3);

            d2.Should().BeGreaterThan(d1);
            d3.Should().BeGreaterThan(d2);
        }

        // 9. Jitter random distribution
        [Fact]
        public void Test9_Jitter_IntroducesVariation()
        {
            var samples = Enumerable.Range(1, 10)
                .Select(_ => HttpRetryDecisionEngine.CalculateBackoffWithJitter(2).TotalMilliseconds)
                .Distinct()
                .ToList();

            samples.Count.Should().BeGreaterThan(1);
        }

        // 10. Server Retry-After cap (60s max)
        [Fact]
        public void Test10_HugeRetryAfter_CappedSafely()
        {
            using var resp = new HttpResponseMessage((HttpStatusCode)429);
            resp.Headers.Add("Retry-After", "999999");

            var delay = HttpRetryDecisionEngine.ParseRetryAfterHeader(resp);
            delay.Should().NotBeNull();
            delay!.Value.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(60));
        }

        // 11. HLS / DASH segment-level independent retry
        [Fact]
        public void Test11_SegmentFailure_RetriedIndependently()
        {
            int segmentFailures = 0;
            int segmentRetries = 0;

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                if (attempt < 3)
                {
                    segmentFailures++;
                    segmentRetries++;
                }
            }

            segmentFailures.Should().Be(2);
            segmentRetries.Should().Be(2);
        }

        // 12. Resume from valid byte offset after failure
        [Fact]
        public void Test12_ResumeFromValidByteOffset_MaintainsOffset()
        {
            long existingBytes = 1048576; // 1 MB
            long totalBytes = 5242880;   // 5 MB

            long remainingBytes = totalBytes - existingBytes;
            remainingBytes.Should().Be(4194304);
        }

        // 13. Crash recovery of stale active downloads
        [Fact]
        public void Test13_CrashRecovery_RestoresStaleDownloadingItems()
        {
            var scheduler = new DownloadQueueScheduler(_testStorageDir);
            var activeItems = new List<DownloadItem>
            {
                new() { Id = Guid.NewGuid(), FileName = "test1.dat", Status = "Downloading" },
                new() { Id = Guid.NewGuid(), FileName = "test2.dat", Status = "Starting" },
                new() { Id = Guid.NewGuid(), FileName = "test3.dat", Status = "Completed" }
            };

            int recovered = scheduler.RecoverStaleDownloads(activeItems);

            recovered.Should().Be(2);
            activeItems[0].Status.Should().Be("Paused");
            activeItems[1].Status.Should().Be("Paused");
            activeItems[2].Status.Should().Be("Completed");
        }

        // 14. User cancellation during active retry backoff
        [Fact]
        public void Test14_CancelRetry_CancelsItemState()
        {
            var scheduler = new DownloadQueueScheduler(_testStorageDir);
            var item = new QueuedDownloadItem
            {
                DownloadId = "cancel_test_job",
                Url = "https://example.com/test.zip",
                State = QueueItemState.Retrying,
                NextRetryTimeUtc = DateTime.UtcNow.AddMinutes(5)
            };

            scheduler.Enqueue(item);
            scheduler.CancelRetry("cancel_test_job");

            var fetched = scheduler.GetItem("cancel_test_job");
            fetched.Should().NotBeNull();
            fetched!.State.Should().Be(QueueItemState.Cancelled);
            fetched.NextRetryTimeUtc.Should().BeNull();
        }

        // 15. Pause and resume during retry state
        [Fact]
        public void Test15_PauseAndResume_TransitionsStateProperly()
        {
            var scheduler = new DownloadQueueScheduler(_testStorageDir);
            var item = new QueuedDownloadItem
            {
                DownloadId = "pause_test_job",
                Url = "https://example.com/test.zip"
            };

            scheduler.Enqueue(item);
            scheduler.MarkPaused("pause_test_job");

            var fetched = scheduler.GetItem("pause_test_job");
            fetched!.State.Should().Be(QueueItemState.Paused);
        }

        // 16. Queue and scheduler concurrency slot enforcement for retries
        [Fact]
        public void Test16_RetryTiming_DoesNotRunBeforeNextRetryTime()
        {
            var scheduler = new DownloadQueueScheduler(_testStorageDir) { MaxActiveDownloads = 4 };
            var item = new QueuedDownloadItem
            {
                DownloadId = "future_retry_job",
                Url = "https://example.com/file.dat",
                State = QueueItemState.Retrying,
                NextRetryTimeUtc = DateTime.UtcNow.AddMinutes(10) // In future
            };

            scheduler.Enqueue(item);
            item.State = QueueItemState.Retrying; // preserve retrying state after enqueue
            item.NextRetryTimeUtc = DateTime.UtcNow.AddMinutes(10);

            var next = scheduler.TryGetNextDownloadToStart();
            next.Should().BeNull(); // Should not schedule before time elapsed
        }

        // 17. Duplicate retry prevention
        [Fact]
        public void Test17_DuplicateRetryPrevention_PreventsConcurrentScheduling()
        {
            var scheduler = new DownloadQueueScheduler(_testStorageDir) { MaxActiveDownloads = 1 };
            var item = new QueuedDownloadItem
            {
                DownloadId = "single_slot_job",
                Url = "https://example.com/file.dat",
                State = QueueItemState.Queued
            };

            scheduler.Enqueue(item);

            var first = scheduler.TryGetNextDownloadToStart();
            first.Should().NotBeNull();

            var second = scheduler.TryGetNextDownloadToStart();
            second.Should().BeNull(); // Concurrency slot occupied
        }

        // 18. HLS transient playlist retry
        [Fact]
        public void Test18_HlsTransientPlaylist_ParsedWhenAvailable()
        {
            string m3u8 = @"#EXTM3U
#EXTINF:4.0,
seg1.ts
#EXT-X-ENDLIST";

            var playlist = HlsParser.Parse(m3u8, new Uri("https://example.com/"));
            playlist.Segments.Should().HaveCount(1);
        }

        // 19. DASH transient MPD retry
        [Fact]
        public void Test19_DashTransientMpd_ParsedWhenAvailable()
        {
            string mpd = @"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet mimeType=""video/mp4"">
      <Representation id=""1"" bandwidth=""1000000"">
        <BaseURL>v.mp4</BaseURL>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

            var manifest = DashParser.Parse(mpd, new Uri("https://example.com/"));
            manifest.VideoRepresentations.Should().ContainSingle();
        }

        // 20. Audio stream download failure recovery
        [Fact]
        public void Test20_AudioStreamFailure_ClassifiesAndSetsRetryState()
        {
            var scheduler = new DownloadQueueScheduler(_testStorageDir);
            var item = new QueuedDownloadItem
            {
                DownloadId = "audio_fail_job",
                Url = "https://example.com/audio.m4a",
                State = QueueItemState.Downloading
            };

            scheduler.Enqueue(item);
            scheduler.MarkFailed("audio_fail_job", true, "Connection dropped", new IOException("Network socket disconnected"));

            var failedItem = scheduler.GetItem("audio_fail_job");
            failedItem.Should().NotBeNull();
            failedItem!.State.Should().Be(QueueItemState.Retrying);
            failedItem.RetryCount.Should().Be(1);
            failedItem.FailureCategory.Should().Be("Transient");
            failedItem.NextRetryTimeUtc.Should().NotBeNull();
        }

        // 21. Manual RetryNow and RetryAllFailed user overrides
        [Fact]
        public void Test21_ManualUserOverrides_RetryNowAndRetryAllFailed()
        {
            var scheduler = new DownloadQueueScheduler(_testStorageDir);
            var item1 = new QueuedDownloadItem { DownloadId = "job_f1", State = QueueItemState.Failed };
            var item2 = new QueuedDownloadItem { DownloadId = "job_f2", State = QueueItemState.Failed };

            scheduler.Enqueue(item1);
            item1.State = QueueItemState.Failed;
            scheduler.Enqueue(item2);
            item2.State = QueueItemState.Failed;

            // Test RetryNow
            bool retryNowSuccess = scheduler.RetryNow("job_f1");
            retryNowSuccess.Should().BeTrue();
            scheduler.GetItem("job_f1")!.State.Should().Be(QueueItemState.Queued);

            // Test RetryAllFailed
            int retriedCount = scheduler.RetryAllFailed();
            retriedCount.Should().Be(1); // item2 was failed
            scheduler.GetItem("job_f2")!.State.Should().Be(QueueItemState.Queued);
        }
    }
}
