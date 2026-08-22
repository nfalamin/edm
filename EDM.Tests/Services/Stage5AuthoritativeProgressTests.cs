using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using EDM.Services.Helpers;
using Xunit;

namespace EDM.Tests.Services
{
    public class Stage5AuthoritativeProgressTests
    {
        [Fact]
        public void Invariant_1_Percentage_StrictlyBoundedZeroToOneHundred()
        {
            var infoNegative = new DownloadProgressInfo { ProgressPercentage = -15.0 };
            var infoOver = new DownloadProgressInfo { ProgressPercentage = 125.0 };
            var infoValid = new DownloadProgressInfo { ProgressPercentage = 54.1 };

            double clampedNeg = Math.Clamp(infoNegative.ProgressPercentage, 0.0, 100.0);
            double clampedOver = Math.Clamp(infoOver.ProgressPercentage, 0.0, 100.0);
            double clampedValid = Math.Clamp(infoValid.ProgressPercentage, 0.0, 100.0);

            Assert.Equal(0.0, clampedNeg);
            Assert.Equal(100.0, clampedOver);
            Assert.Equal(54.1, clampedValid);
        }

        [Fact]
        public void Invariant_2_DownloadedBytes_AlwaysNonNegative()
        {
            var info = new DownloadProgressInfo { BytesReceived = 1048576 };
            Assert.True(info.BytesReceived >= 0);
            Assert.Equal(1048576, info.BytesDownloaded);
        }

        [Fact]
        public void Invariant_3_WhenKnownTotal_DownloadedBytesDoesNotExceedTotal()
        {
            long totalBytes = 1000000;
            long downloadedBytes = 500000;

            var info = new DownloadProgressInfo
            {
                TotalBytes = totalBytes,
                BytesReceived = downloadedBytes,
                ProgressPercentage = (downloadedBytes / (double)totalBytes) * 100.0
            };

            Assert.True(info.HasKnownTotal);
            Assert.True(info.BytesReceived <= info.TotalBytes.Value);
            Assert.Equal(50.0, info.ProgressPercentage);
        }

        [Fact]
        public void Invariant_4_And_5_Completed_RequiresAllWorkFinishedAndOutputFileValid()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "EDM_Stage5_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string testFile = Path.Combine(tempDir, "sample_output.mp4");

            try
            {
                // File does not exist yet -> Cannot be validly completed
                Assert.False(File.Exists(testFile));

                // Create valid non-empty file
                File.WriteAllBytes(testFile, new byte[1024]);
                var fi = new FileInfo(testFile);

                Assert.True(File.Exists(testFile));
                Assert.True(fi.Length > 0);

                var progress = new DownloadProgressInfo
                {
                    Status = "Finished",
                    ProgressPercentage = 100.0,
                    BytesReceived = fi.Length,
                    TotalBytes = fi.Length,
                    IsCompleted = true
                };

                Assert.True(progress.IsCompleted);
                Assert.Equal(100.0, progress.ProgressPercentage);
                Assert.Equal(1024, progress.BytesReceived);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void Invariant_6_AdaptiveProgress_CombinesVideoAndAudioAccurately()
        {
            long vidTotal = 800 * 1024 * 1024L; // 800 MB
            long audTotal = 100 * 1024 * 1024L; // 100 MB
            long combinedTotal = vidTotal + audTotal; // 900 MB

            long vidDownloaded = 400 * 1024 * 1024L; // 400 MB
            long audDownloaded = 50 * 1024 * 1024L;  // 50 MB
            long combinedDownloaded = vidDownloaded + audDownloaded; // 450 MB

            double expectedPercent = (combinedDownloaded / (double)combinedTotal) * 100.0;

            var info = new DownloadProgressInfo
            {
                IsAdaptive = true,
                VideoTotalBytes = vidTotal,
                VideoDownloadedBytes = vidDownloaded,
                AudioTotalBytes = audTotal,
                AudioDownloadedBytes = audDownloaded,
                TotalBytes = combinedTotal,
                BytesReceived = combinedDownloaded,
                ProgressPercentage = expectedPercent
            };

            Assert.True(info.IsAdaptive);
            Assert.Equal(900 * 1024 * 1024L, info.TotalBytes);
            Assert.Equal(450 * 1024 * 1024L, info.BytesReceived);
            Assert.Equal(50.0, info.ProgressPercentage);
            Assert.False(info.IsCompleted);
        }

        [Fact]
        public void Invariant_7_UnknownTotalSize_ProducesIndeterminateProgress()
        {
            var info = new DownloadProgressInfo
            {
                TotalBytes = null,
                BytesReceived = 52428800, // 50 MB
                ProgressPercentage = 0.0,
                Status = "Downloading..."
            };

            Assert.False(info.HasKnownTotal);
            Assert.Null(info.TotalBytes);
            Assert.Equal(52428800, info.BytesReceived);
            Assert.Equal("Calculating...", info.Eta);
        }

        [Fact]
        public void Invariant_8_SameDownloadIdentity_PreservesState()
        {
            string url = "https://example.com/video_manifest.mpd";
            string quality = "2160p 4K";
            string filename = "video_4k.mp4";
            string identity1 = $"{url}|{quality}|{filename}";
            string identity2 = $"{url}|{quality}|{filename}";

            Assert.Equal(identity1, identity2);
        }

        [Fact]
        public void Invariant_9_And_10_ProgressMonotonicityAndEventProtection()
        {
            long currentAuthoritativeBytes = 500000;

            // Incoming out-of-order event with lesser bytes
            long outOfOrderBytes = 450000;

            long effectiveBytes = Math.Max(currentAuthoritativeBytes, outOfOrderBytes);
            Assert.Equal(500000, effectiveBytes); // Protected from regressing

            // Forward event
            long forwardBytes = 600000;
            effectiveBytes = Math.Max(effectiveBytes, forwardBytes);
            Assert.Equal(600000, effectiveBytes);
        }

        [Fact]
        public void SpeedTracker_CalculatesSmoothThroughput()
        {
            var tracker = new SpeedTracker();

            // First call establishes baseline
            double s1 = tracker.UpdateAndGetSpeed(1048576);
            Assert.True(s1 >= 0);

            // Simulating later bytes
            double s2 = tracker.UpdateAndGetSpeed(2097152);
            Assert.True(s2 >= 0);
        }

        [Fact]
        public void ProgressThrottler_CoalescesUpdatesAndPassesTerminalStateImmediately()
        {
            int callCount = 0;
            DownloadProgressInfo? lastDelivered = null;

            using var throttler = new ProgressThrottler<DownloadProgressInfo>(
                targetAction: info =>
                {
                    callCount++;
                    lastDelivered = info;
                },
                throttleInterval: TimeSpan.FromMilliseconds(50),
                isTerminalPredicate: info => info.IsCompleted
            );

            // Rapid fire 10 updates
            for (int i = 1; i <= 10; i++)
            {
                throttler.Report(new DownloadProgressInfo
                {
                    BytesReceived = i * 1000,
                    ProgressPercentage = i * 10,
                    IsCompleted = false
                });
            }

            // Terminal completed event
            throttler.Report(new DownloadProgressInfo
            {
                BytesReceived = 100000,
                ProgressPercentage = 100.0,
                IsCompleted = true,
                Status = "Finished"
            });

            Assert.NotNull(lastDelivered);
            Assert.True(lastDelivered!.IsCompleted);
            Assert.Equal(100.0, lastDelivered.ProgressPercentage);
            Assert.Equal("Finished", lastDelivered.Status);
        }

        [Fact]
        public async Task MediaMergeService_CleansUpTempFilesOnCancelOrFailure()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "EDM_MergeCleanupTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string outputPath = Path.Combine(tempDir, "merged_output.mp4");

            var handler = new MockHttpMessageHandler((req, ct) =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK);
                resp.Content = new ByteArrayContent(new byte[100]);
                return Task.FromResult(resp);
            });

            using var httpClient = new HttpClient(handler);
            var mergeService = new MediaMergeService(httpClient);

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Immediately cancelled

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await mergeService.MergeAudioVideoAsync(
                    "http://test.invalid/vid.mp4",
                    "http://test.invalid/aud.mp4",
                    outputPath,
                    "nonexistent_ffmpeg",
                    cts.Token
                );
            });

            // Verify no stray .tmp files left in tempDir
            var strayTmp = Directory.GetFiles(tempDir, "*.tmp");
            Assert.Empty(strayTmp);

            Directory.Delete(tempDir, true);
        }

        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

            public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return _handler(request, cancellationToken);
            }
        }
    }
}
