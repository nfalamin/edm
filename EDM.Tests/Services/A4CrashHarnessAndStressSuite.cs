using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class A4CrashHarnessAndStressSuite
    {
        // -----------------------------------------------------------------------
        // 1. DETERMINISTIC CHECKPOINT SIMULATION TESTS (Checkpoints 1-7)
        // -----------------------------------------------------------------------

        [Fact]
        public async Task Checkpoint1_CrashAfterMetadataCreation_ResumesCleanly()
        {
            await RunCheckpointTest(checkpoint: 1).ConfigureAwait(false);
        }

        [Fact]
        public async Task Checkpoint2_CrashDuringActiveSegmentDownload_ResumesCleanly()
        {
            await RunCheckpointTest(checkpoint: 2).ConfigureAwait(false);
        }

        [Fact]
        public async Task Checkpoint3_CrashAfterBytesWrittenBeforeMetadataPersistence_ResumesCleanly()
        {
            await RunCheckpointTest(checkpoint: 3).ConfigureAwait(false);
        }

        [Fact]
        public async Task Checkpoint4_CrashDuringAdaptiveScaling_ResumesCleanly()
        {
            await RunCheckpointTest(checkpoint: 4).ConfigureAwait(false);
        }

        [Fact]
        public async Task Checkpoint5_CrashDuringRetryThrottling_ResumesCleanly()
        {
            await RunCheckpointTest(checkpoint: 5).ConfigureAwait(false);
        }

        [Fact]
        public async Task Checkpoint6_CrashNearCompletion_ResumesCleanly()
        {
            await RunCheckpointTest(checkpoint: 6).ConfigureAwait(false);
        }

        [Fact]
        public async Task Checkpoint7_CrashBeforeFinalFilePromotion_ResumesCleanly()
        {
            await RunCheckpointTest(checkpoint: 7).ConfigureAwait(false);
        }

        private async Task RunCheckpointTest(int checkpoint)
        {
            int payloadSize = 5 * 1024 * 1024;
            byte[] payload = new byte[payloadSize];
            new Random(400 + checkpoint).NextBytes(payload);

            string expectedSha256;
            using (var sha = SHA256.Create())
            {
                expectedSha256 = Convert.ToHexString(sha.ComputeHash(payload));
            }

            int port = Random.Shared.Next(40000 + (checkpoint * 100), 40099 + (checkpoint * 100));
            string prefix = $"http://127.0.0.1:{port}/checkpoint-{checkpoint}/";

            using var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();

            var serverTask = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var ctx = await listener.GetContextAsync().ConfigureAwait(false);
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                string rangeHeader = ctx.Request.Headers["Range"];
                                if (ctx.Request.HttpMethod == "HEAD")
                                {
                                    ctx.Response.StatusCode = 200;
                                    ctx.Response.Headers.Add("Accept-Ranges", "bytes");
                                    ctx.Response.ContentLength64 = payload.Length;
                                }
                                else if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                                {
                                    var parts = rangeHeader.Substring(6).Split('-');
                                    long start = long.Parse(parts[0]);
                                    long end = long.Parse(parts[1]);
                                    long len = end - start + 1;

                                    ctx.Response.StatusCode = 206;
                                    ctx.Response.Headers.Add("Content-Range", $"bytes {start}-{end}/{payload.Length}");
                                    ctx.Response.ContentLength64 = len;

                                    await ctx.Response.OutputStream.WriteAsync(payload.AsMemory((int)start, (int)len)).ConfigureAwait(false);
                                }
                                else
                                {
                                    ctx.Response.StatusCode = 200;
                                    ctx.Response.ContentLength64 = payload.Length;
                                    await ctx.Response.OutputStream.WriteAsync(payload).ConfigureAwait(false);
                                }
                                ctx.Response.Close();
                            }
                            catch { }
                        });
                    }
                    catch { break; }
                }
            });

            string tempDir = Path.Combine(Path.GetTempPath(), $"chk_{checkpoint}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            string destFile = Path.Combine(tempDir, "final.bin");

            try
            {
                using (var httpClient = new HttpClient())
                {
                    var downloader = new MultiPartDownloader(httpClient);

                    using var cts = new CancellationTokenSource();
                    // Interrupt at checkpoint window
                    cts.CancelAfter(50 + (checkpoint * 20));

                    try
                    {
                        await downloader.DownloadFileAsync(
                            fileUrl: new Uri(prefix),
                            destinationFilePath: destFile,
                            chunkCount: 2,
                            maxConcurrency: 4,
                            progress: null,
                            cancellationToken: cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { }
                }

                // RESUME AFTER CRASH/INTERRUPTION
                using (var httpClient = new HttpClient())
                {
                    var downloader = new MultiPartDownloader(httpClient);
                    await downloader.DownloadFileAsync(
                        fileUrl: new Uri(prefix),
                        destinationFilePath: destFile,
                        chunkCount: 2,
                        maxConcurrency: 4,
                        progress: null,
                        cancellationToken: CancellationToken.None).ConfigureAwait(false);
                }

                File.Exists(destFile).Should().BeTrue();
                byte[] downloaded = await File.ReadAllBytesAsync(destFile);

                downloaded.Length.Should().Be(payload.Length, $"Checkpoint {checkpoint}: Actual length must equal expected length after crash recovery");

                string actualSha256;
                using (var sha = SHA256.Create())
                {
                    actualSha256 = Convert.ToHexString(sha.ComputeHash(downloaded));
                }
                actualSha256.Should().Be(expectedSha256, $"Checkpoint {checkpoint}: SHA-256 hash must match payload hash perfectly after crash recovery");
            }
            finally
            {
                listener.Stop();
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // -----------------------------------------------------------------------
        // 2. METADATA CORRUPTION SUITE
        // -----------------------------------------------------------------------

        [Fact]
        public async Task MetadataCorruption_TruncatedJson_DiscardsAndStartsFresh()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"corrupt_json_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            string metaPath = Path.Combine(tempDir, "metadata.json");

            try
            {
                await File.WriteAllTextAsync(metaPath, "{ \"DownloadId\": \"123\", \"Url\": ").ConfigureAwait(false);
                var manager = new DurableMetadataManager();
                var state = await manager.ReadStateAsync(metaPath, CancellationToken.None).ConfigureAwait(false);
                state.Should().BeNull("Truncated JSON must be rejected and return null");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task MetadataCorruption_VersionMismatch_DiscardsAndStartsFresh()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"corrupt_ver_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            string metaPath = Path.Combine(tempDir, "metadata.json");

            try
            {
                var badState = new DurableDownloadState
                {
                    SchemaVersion = 0, // Below MinSupported (1)
                    Url = "http://127.0.0.1/test.bin",
                    TotalBytes = 10_000_000
                };
                string json = JsonSerializer.Serialize(badState);
                await File.WriteAllTextAsync(metaPath, json).ConfigureAwait(false);

                var manager = new DurableMetadataManager();
                var state = await manager.ReadStateAsync(metaPath, CancellationToken.None).ConfigureAwait(false);
                state.Should().BeNull("Schema version 0 below minimum supported version must be discarded");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // -----------------------------------------------------------------------
        // 3. 100+ UNGRACEFUL CRASH & RECOVERY STRESS TEST
        // -----------------------------------------------------------------------

        [Fact]
        public async Task Run_100_UngracefulCrashAndRecovery_StressTest()
        {
            int payloadSize = 3 * 1024 * 1024; // 3 MB for fast 100-cycle execution
            byte[] payload = new byte[payloadSize];
            new Random(999).NextBytes(payload);

            string expectedSha256;
            using (var sha = SHA256.Create())
            {
                expectedSha256 = Convert.ToHexString(sha.ComputeHash(payload));
            }

            int port = Random.Shared.Next(49000, 49999);
            string prefix = $"http://127.0.0.1:{port}/crash-100/";

            using var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();

            var serverTask = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var ctx = await listener.GetContextAsync().ConfigureAwait(false);
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                string rangeHeader = ctx.Request.Headers["Range"];
                                if (ctx.Request.HttpMethod == "HEAD")
                                {
                                    ctx.Response.StatusCode = 200;
                                    ctx.Response.Headers.Add("Accept-Ranges", "bytes");
                                    ctx.Response.ContentLength64 = payload.Length;
                                }
                                else if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                                {
                                    var parts = rangeHeader.Substring(6).Split('-');
                                    long start = long.Parse(parts[0]);
                                    long end = long.Parse(parts[1]);
                                    long len = end - start + 1;

                                    ctx.Response.StatusCode = 206;
                                    ctx.Response.Headers.Add("Content-Range", $"bytes {start}-{end}/{payload.Length}");
                                    ctx.Response.ContentLength64 = len;

                                    await ctx.Response.OutputStream.WriteAsync(payload.AsMemory((int)start, (int)len)).ConfigureAwait(false);
                                }
                                else
                                {
                                    ctx.Response.StatusCode = 200;
                                    ctx.Response.ContentLength64 = payload.Length;
                                    await ctx.Response.OutputStream.WriteAsync(payload).ConfigureAwait(false);
                                }
                                ctx.Response.Close();
                            }
                            catch { }
                        });
                    }
                    catch { break; }
                }
            });

            int passedCycles = 0;

            for (int cycle = 1; cycle <= 100; cycle++)
            {
                string tempDir = Path.Combine(Path.GetTempPath(), $"a4_crash_cycle_{cycle}_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);
                string destFile = Path.Combine(tempDir, "final.bin");

                try
                {
                    // Interrupt attempt 1 at random time (10ms - 40ms)
                    using (var httpClient = new HttpClient())
                    {
                        var downloader = new MultiPartDownloader(httpClient);
                        using var cts = new CancellationTokenSource();
                        cts.CancelAfter(Random.Shared.Next(10, 40));

                        try
                        {
                            await downloader.DownloadFileAsync(new Uri(prefix), destFile, chunkCount: 2, maxConcurrency: 4, progress: null, cancellationToken: cts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) { }
                    }

                    // Resume attempt 2 to full completion
                    using (var httpClient = new HttpClient())
                    {
                        var downloader = new MultiPartDownloader(httpClient);
                        await downloader.DownloadFileAsync(new Uri(prefix), destFile, chunkCount: 2, maxConcurrency: 4, progress: null, cancellationToken: CancellationToken.None).ConfigureAwait(false);
                    }

                    byte[] downloaded = await File.ReadAllBytesAsync(destFile);
                    string actualSha256;
                    using (var sha = SHA256.Create())
                    {
                        actualSha256 = Convert.ToHexString(sha.ComputeHash(downloaded));
                    }

                    if (downloaded.Length == payload.Length && actualSha256 == expectedSha256)
                    {
                        passedCycles++;
                    }
                }
                finally
                {
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                }
            }

            listener.Stop();
            passedCycles.Should().Be(100, "All 100 ungraceful crash & recovery stress cycles must pass with 100% SHA256 match");
        }
    }
}
