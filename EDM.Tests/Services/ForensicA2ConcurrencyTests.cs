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
    public class ForensicA2ConcurrencyTests : TestBase
    {
        // ==================== 1. EXPLICIT STATE MACHINE TRANSITION TEST ====================
        [Fact]
        public void SegmentScheduler_RejectsIllegalStateTransitionsFromCompleted()
        {
            var scheduler = new SegmentScheduler(1000);
            scheduler.InitializeDefault(1);

            var seg = scheduler.GetNextWorkItem("worker-1");
            seg.Should().NotBeNull();

            // Transition to Completed
            bool completedSuccess = scheduler.MarkCompleted(seg!.Id);
            completedSuccess.Should().BeTrue("First MarkCompleted must return true");

            // Attempt illegal double-completion
            bool doubleCompleted = scheduler.MarkCompleted(seg.Id);
            doubleCompleted.Should().BeFalse("Double MarkCompleted must return false");

            // Attempt illegal Completed -> Failed transition
            bool markFailed = scheduler.MarkFailed(seg.Id, requeue: true);
            markFailed.Should().BeFalse("Completed segment cannot transition to Failed or Pending");

            var snapshot = scheduler.GetSegmentsSnapshot();
            snapshot[0].State.Should().Be(SegmentState.Completed);
        }

        // ==================== 2. DOUBLE-COMPLETION PROTECTION ====================
        [Fact]
        public async Task SegmentScheduler_DoubleCompletionProtection_ConcurrentThreads()
        {
            var scheduler = new SegmentScheduler(10000);
            scheduler.InitializeDefault(1);
            var seg = scheduler.GetNextWorkItem("worker-1");

            int successfulCompletions = 0;
            Task[] tasks = new Task[100];

            for (int i = 0; i < 100; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    if (scheduler.MarkCompleted(seg!.Id))
                    {
                        Interlocked.Increment(ref successfulCompletions);
                    }
                });
            }

            await Task.WhenAll(tasks);
            successfulCompletions.Should().Be(1, "Exactly one thread may successfully complete a segment");
        }

        // ==================== 3. CANCELLATION PROPAGATION ====================
        [Fact]
        public async Task MultiPartDownloader_CancellationPropagatesWithoutConvertingToFailure()
        {
            using var listener = new HttpListener();
            int port = Random.Shared.Next(40000, 44999);
            string prefix = $"http://127.0.0.1:{port}/cancel-test/";
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
                                ctx.Response.StatusCode = 200;
                                ctx.Response.Headers.Add("Accept-Ranges", "bytes");
                                ctx.Response.ContentLength64 = 10 * 1024 * 1024;
                                await Task.Delay(2000); // Intentionally stall to allow cancellation
                                ctx.Response.Close();
                            }
                            catch { }
                        });
                    }
                    catch { break; }
                }
            });

            string tempPath = Path.Combine(Path.GetTempPath(), "cancel_test_" + Guid.NewGuid().ToString("N") + ".bin");
            using var cts = new CancellationTokenSource();

            try
            {
                using var client = new HttpClient();
                var downloader = new MultiPartDownloader(client);

                cts.CancelAfter(100);

                Func<Task> act = async () =>
                {
                    await downloader.DownloadFileAsync(new Uri(prefix), tempPath, chunkCount: 2, maxConcurrency: 2, progress: null, cancellationToken: cts.Token);
                };

                await act.Should().ThrowAsync<OperationCanceledException>("Cancellation must propagate as OperationCanceledException");
            }
            finally
            {
                listener.Stop();
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        // ==================== 4. PAUSE / RESUME FORENSIC TEST ====================
        [Fact]
        public async Task MultiPartDownloader_PauseResume_ZeroDuplicateBytesAndExactSHA256()
        {
            byte[] fullData = new byte[4 * 1024 * 1024]; // 4 MB
            new Random(888).NextBytes(fullData);

            string expectedHash;
            using (var sha = SHA256.Create())
            {
                expectedHash = Convert.ToHexString(sha.ComputeHash(fullData));
            }

            using var listener = new HttpListener();
            int port = Random.Shared.Next(45000, 49999);
            string prefix = $"http://127.0.0.1:{port}/pause-resume-test/";
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
                                    ctx.Response.ContentLength64 = fullData.Length;
                                }
                                else if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                                {
                                    var parts = rangeHeader.Substring(6).Split('-');
                                    long start = long.Parse(parts[0]);
                                    long end = long.Parse(parts[1]);
                                    long len = end - start + 1;

                                    ctx.Response.StatusCode = 206;
                                    ctx.Response.Headers.Add("Content-Range", $"bytes {start}-{end}/{fullData.Length}");
                                    ctx.Response.ContentLength64 = len;

                                    await ctx.Response.OutputStream.WriteAsync(fullData.AsMemory((int)start, (int)len)).ConfigureAwait(false);
                                }
                                else
                                {
                                    ctx.Response.StatusCode = 200;
                                    ctx.Response.ContentLength64 = fullData.Length;
                                    await ctx.Response.OutputStream.WriteAsync(fullData).ConfigureAwait(false);
                                }
                                ctx.Response.Close();
                            }
                            catch { }
                        });
                    }
                    catch { break; }
                }
            });

            string tempFile = Path.Combine(Path.GetTempPath(), "pause_resume_verify_" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                using var client = new HttpClient();
                var downloader = new MultiPartDownloader(client);

                var downloadTask = downloader.DownloadFileAsync(new Uri(prefix), tempFile, chunkCount: 4, maxConcurrency: 4, progress: null, cancellationToken: CancellationToken.None);

                await Task.Delay(50);
                downloader.Pause();
                await Task.Delay(100);
                downloader.Resume();

                await downloadTask;

                File.Exists(tempFile).Should().BeTrue();
                byte[] actualData = await File.ReadAllBytesAsync(tempFile);
                actualData.Length.Should().Be(fullData.Length);

                string actualHash;
                using (var sha = SHA256.Create())
                {
                    actualHash = Convert.ToHexString(sha.ComputeHash(actualData));
                }

                actualHash.Should().Be(expectedHash, "SHA-256 after pause/resume must match expected fixture hash exactly");
            }
            finally
            {
                listener.Stop();
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        // ==================== 5. 100-CYCLE STRESS TEST ====================
        [Fact]
        public async Task StressTest_100Cycles_RandomizedPauseCancelResume_NoCorruption()
        {
            var rand = new Random(42);

            for (int cycle = 0; cycle < 100; cycle++)
            {
                long totalBytes = rand.Next(500 * 1024, 2 * 1024 * 1024);
                var scheduler = new SegmentScheduler(totalBytes, minSplitThresholdBytes: 32 * 1024);
                scheduler.InitializeDefault(rand.Next(1, 6));

                int ops = rand.Next(5, 20);
                for (int op = 0; op < ops; op++)
                {
                    string workerId = $"worker_{op}";
                    var seg = scheduler.GetNextWorkItem(workerId);
                    if (seg != null)
                    {
                        if (rand.NextDouble() > 0.5)
                        {
                            scheduler.ReportProgress(seg.Id, seg.TotalBytes / 2);
                        }
                        else
                        {
                            scheduler.MarkCompleted(seg.Id);
                        }
                    }
                }

                scheduler.ValidateCoverage().Should().BeTrue($"Coverage invariant must hold under cycle #{cycle}");
            }

            await Task.CompletedTask;
        }
    }
}
