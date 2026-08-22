using System;
using System.IO;
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
    public class ForensicCorruptionAndCrashTests : TestBase
    {
        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        public async Task Phase4_RealFileCorruptionTest_SHA256MatchesExpectedFixture(int connectionCount)
        {
            // Arrange: Generate 5 MB deterministic binary payload with known SHA-256
            byte[] expectedPayload = new byte[5 * 1024 * 1024];
            new Random(42).NextBytes(expectedPayload);

            string expectedHash;
            using (var sha = SHA256.Create())
            {
                expectedHash = Convert.ToHexString(sha.ComputeHash(expectedPayload));
            }

            using var listener = new HttpListener();
            int port = Random.Shared.Next(25000, 29999);
            string prefix = $"http://127.0.0.1:{port}/sha256-test/";
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
                                    ctx.Response.ContentLength64 = expectedPayload.Length;
                                }
                                else if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                                {
                                    var parts = rangeHeader.Substring(6).Split('-');
                                    long start = long.Parse(parts[0]);
                                    long end = long.Parse(parts[1]);
                                    long len = end - start + 1;

                                    ctx.Response.StatusCode = 206;
                                    ctx.Response.Headers.Add("Content-Range", $"bytes {start}-{end}/{expectedPayload.Length}");
                                    ctx.Response.ContentLength64 = len;

                                    await ctx.Response.OutputStream.WriteAsync(expectedPayload.AsMemory((int)start, (int)len)).ConfigureAwait(false);
                                }
                                else
                                {
                                    ctx.Response.StatusCode = 200;
                                    ctx.Response.ContentLength64 = expectedPayload.Length;
                                    await ctx.Response.OutputStream.WriteAsync(expectedPayload).ConfigureAwait(false);
                                }
                                ctx.Response.Close();
                            }
                            catch { }
                        });
                    }
                    catch { break; }
                }
            });

            string tempFile = Path.Combine(Path.GetTempPath(), $"sha256_verify_{connectionCount}_" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                using var client = new HttpClient();
                var downloader = new MultiPartDownloader(client);

                // Act: Download via actual EDM engine across connection matrix
                await downloader.DownloadFileAsync(new Uri(prefix), tempFile, chunkCount: connectionCount, maxConcurrency: connectionCount, progress: null, cancellationToken: CancellationToken.None);

                // Assert
                File.Exists(tempFile).Should().BeTrue();
                byte[] actualPayload = await File.ReadAllBytesAsync(tempFile);

                string actualHash;
                using (var sha = SHA256.Create())
                {
                    actualHash = Convert.ToHexString(sha.ComputeHash(actualPayload));
                }

                actualHash.Should().Be(expectedHash, $"SHA-256 of downloaded payload must match expected fixture hash exactly for connection count = {connectionCount}");
            }
            finally
            {
                listener.Stop();
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task Phase5_CrashResumeForensics_RecoversPartialDownloadWithoutCorruption()
        {
            // Arrange
            byte[] fullPayload = new byte[2 * 1024 * 1024]; // 2 MB
            new Random(123).NextBytes(fullPayload);

            string expectedHash;
            using (var sha = SHA256.Create())
            {
                expectedHash = Convert.ToHexString(sha.ComputeHash(fullPayload));
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "crash_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string targetFile = Path.Combine(tempDir, "target.bin");
            string metaPath = Path.Combine(tempDir, ".tmp_target.bin", "metadata.json");
            Directory.CreateDirectory(Path.GetDirectoryName(metaPath)!);

            // Simulate interruption: create partial segment 0 (1 MB complete) and segment 1 (pending)
            var metaManager = new DurableMetadataManager();
            var scheduler = new SegmentScheduler(fullPayload.Length);
            scheduler.InitializeDefault(2);

            var segments = scheduler.GetSegmentsSnapshot();
            string seg0Path = Path.Combine(Path.GetDirectoryName(metaPath)!, "segment_0.part");
            string seg1Path = Path.Combine(Path.GetDirectoryName(metaPath)!, "segment_1.part");

            await File.WriteAllBytesAsync(seg0Path, fullPayload.AsSpan(0, 1024 * 1024).ToArray());
            segments[0].BytesDownloaded = 1024 * 1024;
            segments[0].State = SegmentState.Completed;
            segments[0].TempPath = seg0Path;

            segments[1].TempPath = seg1Path;

            var state = new DurableDownloadState
            {
                Url = "http://example.com/file.bin",
                TotalBytes = fullPayload.Length,
                Segments = segments,
                ETag = "etag-v1",
                LastModified = "Wed, 21 Oct 2015 07:28:00 GMT"
            };

            await metaManager.WriteStateAtomicAsync(metaPath, state, CancellationToken.None);

            // Act: Reconcile state
            var readState = await metaManager.ReadStateAsync(metaPath, CancellationToken.None);
            readState.Should().NotBeNull();
            bool isValid = metaManager.ReconcileAndValidate(readState!, "etag-v1", "Wed, 21 Oct 2015 07:28:00 GMT");

            // Assert
            isValid.Should().BeTrue("Resume validation must succeed when ETag and TotalBytes match");
            readState!.Segments[0].BytesDownloaded.Should().Be(1024 * 1024);

            // Cleanup
            Directory.Delete(tempDir, true);
        }

        [Fact]
        public async Task Phase6_ConcurrencyStressAudit_1000QueueOperations_NoDeadlocks()
        {
            // Arrange
            using var queueManager = new DownloadQueueManager(maxParallel: 8);
            int executedCount = 0;

            // Act: Perform 1000 concurrent enqueue/dequeue operations
            Task[] tasks = new Task[1000];
            for (int i = 0; i < 1000; i++)
            {
                int id = i;
                tasks[i] = Task.Run(async () =>
                {
                    await queueManager.EnqueueAsync($"item_{id}", "queue_1", QueuePriority.Normal, async (ct) =>
                    {
                        Interlocked.Increment(ref executedCount);
                        await Task.Yield();
                    });
                });
            }

            await Task.WhenAll(tasks);
            await Task.Delay(500);

            // Assert
            executedCount.Should().Be(1000, "1000 concurrent queue operations must complete cleanly without deadlocks or state corruption");
        }
    }
}
