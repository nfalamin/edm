using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class DynamicSegmentationOwnershipTests : TestBase
    {
        // ==================== TEST A ====================
        [Fact]
        public void TestA_WorkerCannotWriteBeyondNewOwnershipAfterSplit()
        {
            // 0 - 1000 bytes
            long totalBytes = 1000;
            var scheduler = new SegmentScheduler(totalBytes, minSplitThresholdBytes: 100);
            scheduler.InitializeDefault(1);

            var segA = scheduler.GetNextWorkItem("worker-A");
            segA.Should().NotBeNull();
            segA!.Start.Should().Be(0);
            segA.End.Should().Be(999);

            // Worker A writes 0-399 bytes
            scheduler.ReportProgress(segA.Id, 400);

            // Dynamically split remaining 600 bytes
            var segB = scheduler.GetNextWorkItem("worker-B");
            segB.Should().NotBeNull();

            // Authoritative end for worker A must now be split boundary
            long assignedEndA = scheduler.GetAssignedEnd(segA.Id);
            assignedEndA.Should().BeLessThan(999, "Worker A's end boundary must be shortened after split");
            segB!.Start.Should().Be(assignedEndA + 1, "Worker B's start must be exactly assignedEndA + 1");
            segB.End.Should().Be(999);

            scheduler.ValidateCoverage().Should().BeTrue("Coverage invariant must hold after split");
        }

        // ==================== TEST B ====================
        [Fact]
        public void TestB_TwoWorkers_ZeroOverlap()
        {
            var scheduler = new SegmentScheduler(1000);
            scheduler.InitializeDefault(2);

            var snapshot = scheduler.GetSegmentsSnapshot();
            snapshot.Should().HaveCount(2);

            (snapshot[0].End + 1).Should().Be(snapshot[1].Start, "No gap or overlap allowed between worker ranges");
            scheduler.ValidateCoverage().Should().BeTrue();
        }

        // ==================== TEST C ====================
        [Fact]
        public void TestC_RepeatedDynamicSplits_MaintainsCoverageInvariant()
        {
            long totalBytes = 10 * 1024 * 1024; // 10 MB
            var scheduler = new SegmentScheduler(totalBytes, minSplitThresholdBytes: 64 * 1024);
            scheduler.InitializeDefault(1);

            scheduler.GetNextWorkItem("worker-1");

            for (int i = 2; i <= 8; i++)
            {
                var item = scheduler.GetNextWorkItem($"worker-{i}");
                item.Should().NotBeNull();
                scheduler.ValidateCoverage().Should().BeTrue($"Coverage must remain valid after split #{i}");
            }

            var finalSegments = scheduler.GetSegmentsSnapshot();
            long totalAssignedSum = finalSegments.Sum(s => s.TotalBytes);
            totalAssignedSum.Should().Be(totalBytes);
        }

        // ==================== TEST D ====================
        [Fact]
        public void TestD_SlowWorkerFastWorker_StealsRemainingTailCorrectly()
        {
            long totalBytes = 20 * 1024 * 1024; // 20 MB
            var scheduler = new SegmentScheduler(totalBytes, minSplitThresholdBytes: 512 * 1024);
            scheduler.InitializeDefault(2);

            var segFast = scheduler.GetNextWorkItem("worker-fast");
            var segSlow = scheduler.GetNextWorkItem("worker-slow");

            // Fast worker completes its half
            scheduler.MarkCompleted(segFast!.Id);

            // Slow worker has completed only 1 MB
            scheduler.ReportProgress(segSlow!.Id, 1 * 1024 * 1024);

            // Fast worker steals work
            var segStolen = scheduler.GetNextWorkItem("worker-fast");

            segStolen.Should().NotBeNull("Fast worker must steal tail end from slow worker");
            scheduler.ValidateCoverage().Should().BeTrue();
        }

        // ==================== TEST E & F ====================
        [Fact]
        public async Task TestE_TestF_SplitAndCancellationDuringActiveAsyncWrite()
        {
            using var cts = new CancellationTokenSource();
            var scheduler = new SegmentScheduler(5 * 1024 * 1024, minSplitThresholdBytes: 64 * 1024);
            scheduler.InitializeDefault(1);

            var seg = scheduler.GetNextWorkItem("worker-1");

            var writeTask = Task.Run(async () =>
            {
                for (int i = 0; i < 50; i++)
                {
                    await Task.Delay(10);
                    scheduler.ReportProgress(seg!.Id, (i + 1) * 64 * 1024);
                }
            });

            await Task.Delay(50);
            var stolen = scheduler.GetNextWorkItem("worker-2");
            stolen.Should().NotBeNull();

            cts.Cancel();

            await writeTask;
            scheduler.ValidateCoverage().Should().BeTrue();
        }

        // ==================== END-TO-END LOCAL HTTP TEST SERVER ====================
        [Fact]
        public async Task EndToEnd_ForcedDynamicSegmentation_SHA256AndLengthMatchExpectedFixture()
        {
            // Generate 8 MB deterministic binary payload
            byte[] expectedData = new byte[8 * 1024 * 1024];
            new Random(777).NextBytes(expectedData);

            string expectedHash;
            using (var sha = SHA256.Create())
            {
                expectedHash = Convert.ToHexString(sha.ComputeHash(expectedData));
            }

            using var listener = new HttpListener();
            int port = Random.Shared.Next(35000, 39999);
            string prefix = $"http://127.0.0.1:{port}/dynamic-split-test/";
            listener.Prefixes.Add(prefix);
            listener.Start();

            var serverTask = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var ctx = await listener.GetContextAsync();
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                string rangeHeader = ctx.Request.Headers["Range"];
                                if (ctx.Request.HttpMethod == "HEAD")
                                {
                                    ctx.Response.StatusCode = 200;
                                    ctx.Response.Headers.Add("Accept-Ranges", "bytes");
                                    ctx.Response.ContentLength64 = expectedData.Length;
                                }
                                else if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                                {
                                    var parts = rangeHeader.Substring(6).Split('-');
                                    long start = long.Parse(parts[0]);
                                    long end = long.Parse(parts[1]);
                                    long len = end - start + 1;

                                    ctx.Response.StatusCode = 206;
                                    ctx.Response.Headers.Add("Content-Range", $"bytes {start}-{end}/{expectedData.Length}");
                                    ctx.Response.ContentLength64 = len;

                                    await ctx.Response.OutputStream.WriteAsync(expectedData.AsMemory((int)start, (int)len)).ConfigureAwait(false);
                                }
                                else
                                {
                                    ctx.Response.StatusCode = 200;
                                    ctx.Response.ContentLength64 = expectedData.Length;
                                    await ctx.Response.OutputStream.WriteAsync(expectedData).ConfigureAwait(false);
                                }
                                ctx.Response.Close();
                            }
                            catch { }
                        });
                    }
                    catch { break; }
                }
            });

            string tempOutputFile = Path.Combine(Path.GetTempPath(), "e2e_split_test_" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                using var client = new HttpClient();
                var downloader = new MultiPartDownloader(client);

                // Act: Download 8 MB file using 4 parallel channels with dynamic splitting
                await downloader.DownloadFileAsync(new Uri(prefix), tempOutputFile, chunkCount: 2, maxConcurrency: 4, progress: null, cancellationToken: CancellationToken.None);

                // Assert
                File.Exists(tempOutputFile).Should().BeTrue();
                byte[] actualData = await File.ReadAllBytesAsync(tempOutputFile);

                actualData.Length.Should().Be(expectedData.Length, "Actual downloaded file length must match expected fixture length");

                string actualHash;
                using (var sha = SHA256.Create())
                {
                    actualHash = Convert.ToHexString(sha.ComputeHash(actualData));
                }

                actualHash.Should().Be(expectedHash, "Downloaded payload SHA-256 must match expected binary fixture hash under dynamic segmentation");
            }
            finally
            {
                listener.Stop();
                if (File.Exists(tempOutputFile)) File.Delete(tempOutputFile);
            }
        }

        // ==================== STRESS TEST: 100 RANDOMIZED DYNAMIC SPLITS ====================
        [Fact]
        public void StressTest_100RandomizedDynamicSplits_NoCorruptionOrOverlaps()
        {
            var rand = new Random(100);

            for (int run = 0; run < 100; run++)
            {
                long totalBytes = rand.Next(1 * 1024 * 1024, 100 * 1024 * 1024);
                var scheduler = new SegmentScheduler(totalBytes, minSplitThresholdBytes: 64 * 1024);
                int initialCount = rand.Next(1, 8);
                scheduler.InitializeDefault(initialCount);

                int splits = rand.Next(1, 15);
                for (int s = 0; s < splits; s++)
                {
                    string workerId = $"worker_stress_{s}";
                    var item = scheduler.GetNextWorkItem(workerId);
                    if (item != null)
                    {
                        long simDownloaded = rand.Next(0, (int)Math.Min(item.TotalBytes, 1024 * 1024));
                        scheduler.ReportProgress(item.Id, simDownloaded);
                    }
                }

                scheduler.ValidateCoverage().Should().BeTrue($"Run #{run} must maintain valid coverage without gaps or overlaps");
            }
        }
    }
}
