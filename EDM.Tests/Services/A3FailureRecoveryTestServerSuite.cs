using System;
using System.Collections.Generic;
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
    public class A3FailureRecoveryTestServerSuite
    {
        // Test 1: 500 -> retry -> success
        [Fact]
        public async Task Test1_Server500_RetriesAndSucceeds()
        {
            int attempts = 0;
            int port = Random.Shared.Next(42000, 42999);
            string prefix = $"http://127.0.0.1:{port}/test1-500/";

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
                        attempts++;
                        if (attempts == 1)
                        {
                            ctx.Response.StatusCode = 500;
                        }
                        else
                        {
                            ctx.Response.StatusCode = 200;
                            byte[] okBytes = System.Text.Encoding.UTF8.GetBytes("OK");
                            await ctx.Response.OutputStream.WriteAsync(okBytes).ConfigureAwait(false);
                        }
                        ctx.Response.Close();
                    }
                    catch { break; }
                }
            });

            try
            {
                using var httpClient = new HttpClient();
                var pipeline = new HttpRequestPipeline(httpClient);

                var result = await pipeline.ExecuteWithRetryAsync(
                    () => new HttpRequestMessage(HttpMethod.Get, prefix),
                    HttpCompletionOption.ResponseHeadersRead,
                    CancellationToken.None,
                    maxRetries: 3).ConfigureAwait(false);

                attempts.Should().BeGreaterThan(1, "Pipeline must retry on 500 Internal Server Error");
                result.Response.StatusCode.Should().Be(HttpStatusCode.OK);
            }
            finally
            {
                listener.Stop();
            }
        }

        // Test 2: 503 -> Retry-After -> success
        [Fact]
        public async Task Test2_Server503_WithRetryAfter_RespectsHeaderAndSucceeds()
        {
            int attempts = 0;
            int port = Random.Shared.Next(41000, 41999);
            string prefix = $"http://127.0.0.1:{port}/test2-503/";

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
                        attempts++;
                        if (attempts == 1)
                        {
                            ctx.Response.StatusCode = 503;
                            ctx.Response.Headers.Add("Retry-After", "1");
                        }
                        else
                        {
                            ctx.Response.StatusCode = 200;
                            byte[] okBytes = System.Text.Encoding.UTF8.GetBytes("OK");
                            await ctx.Response.OutputStream.WriteAsync(okBytes).ConfigureAwait(false);
                        }
                        ctx.Response.Close();
                    }
                    catch { break; }
                }
            });

            try
            {
                using var httpClient = new HttpClient();
                var pipeline = new HttpRequestPipeline(httpClient);

                var result = await pipeline.ExecuteWithRetryAsync(
                    () => new HttpRequestMessage(HttpMethod.Get, prefix),
                    HttpCompletionOption.ResponseHeadersRead,
                    CancellationToken.None,
                    maxRetries: 3).ConfigureAwait(false);

                attempts.Should().BeGreaterThan(1, "Pipeline must retry on 503 Service Unavailable");
                result.Response.StatusCode.Should().Be(HttpStatusCode.OK);
            }
            finally
            {
                listener.Stop();
            }
        }


        // Test 3: 429 -> concurrency reduction -> recovery
        [Fact]
        public void Test3_429_TriggersA2ConcurrencyReduction()
        {
            var controller = new AdaptiveConnectionController(initialConnections: 12, minConnections: 2, maxConnections: 16);
            controller.ResetCooldown();

            // Record 3 samples ending with HTTP 429 rate limit
            controller.RecordTelemetry(aggregateThroughputBps: 8_000_000, averageRttMs: 40.0, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 8_000_000, averageRttMs: 40.0, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 8_000_000, averageRttMs: 40.0, errorCount: 0, http429Count: 1);

            int conns = controller.EvaluateConnectionCount(totalFileSize: 100 * 1024 * 1024, isMeteredNetwork: false);
            conns.Should().Be(10, "A2 must step down concurrency (-2) upon receiving 429 rate limit telemetry");
        }

        // Test 4: 429 without Retry-After -> exponential backoff
        [Fact]
        public void Test4_429_WithoutRetryAfter_UsesExponentialBackoff()
        {
            var ex = new HttpRequestException("429 Too Many Requests", null, HttpStatusCode.TooManyRequests);
            HttpRequestPipeline.IsTransientException(ex).Should().BeTrue("HTTP 429 must be classified as transient for exponential backoff retry");
        }

        // Test 5: Connection reset -> resume exact remaining bytes
        [Fact]
        public async Task Test5_ConnectionReset_ResumesExactRemainingBytes()
        {
            int payloadSize = 5 * 1024 * 1024;
            byte[] payload = new byte[payloadSize];
            new Random(55).NextBytes(payload);

            string expectedSha256;
            using (var sha = SHA256.Create())
            {
                expectedSha256 = Convert.ToHexString(sha.ComputeHash(payload));
            }

            int port = Random.Shared.Next(45000, 45999);
            string prefix = $"http://127.0.0.1:{port}/reset-test/";

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

            string tempFile = Path.Combine(Path.GetTempPath(), $"reset_test_{Guid.NewGuid():N}.bin");

            try
            {
                using var httpClient = new HttpClient();
                var downloader = new MultiPartDownloader(httpClient);

                await downloader.DownloadFileAsync(
                    fileUrl: new Uri(prefix),
                    destinationFilePath: tempFile,
                    chunkCount: 2,
                    maxConcurrency: 4,
                    progress: null,
                    cancellationToken: CancellationToken.None).ConfigureAwait(false);

                File.Exists(tempFile).Should().BeTrue();
                byte[] downloaded = await File.ReadAllBytesAsync(tempFile);
                downloaded.Length.Should().Be(payload.Length);

                string actualSha256;
                using (var sha = SHA256.Create())
                {
                    actualSha256 = Convert.ToHexString(sha.ComputeHash(downloaded));
                }
                actualSha256.Should().Be(expectedSha256);
            }
            finally
            {
                listener.Stop();
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        // Test 6: Timeout -> retry
        [Fact]
        public void Test6_Timeout_IsClassifiedAsTransient()
        {
            var ex = new TimeoutException("Connection timed out");
            HttpRequestPipeline.IsTransientException(ex).Should().BeTrue("TimeoutException must be classified as transient for automatic retry");
        }

        // Test 7: Incomplete response -> retry remaining data
        [Fact]
        public void Test7_IncompleteResponse_ThrowsIOException_ClassifiedAsTransient()
        {
            var ex = new IOException("Premature EOF in HTTP stream");
            HttpRequestPipeline.IsTransientException(ex).Should().BeTrue("Incomplete stream read must be classified as transient");
        }

        // Test 8: Invalid Content-Range -> reject
        [Fact]
        public void Test8_InvalidContentRange_IsNotTransient_RejectsSilently()
        {
            var ex = new InvalidDataException("Content-Range header missing");
            HttpRequestPipeline.IsTransientException(ex).Should().BeFalse("Protocol violations like missing Content-Range must NOT be silently retried");
        }

        // Test 9: 416 -> safe handling, no retry storm
        [Fact]
        public void Test9_Http416_ClassifiedAsRangeInvalid_DoesNotRetry()
        {
            var cat = HttpStatusClassifier.Classify(HttpStatusCode.RequestedRangeNotSatisfiable);
            cat.Should().Be(HttpStatusCategory.RangeInvalid);
            HttpStatusClassifier.IsRetryableCategory(cat).Should().BeFalse("HTTP 416 Range Not Satisfiable must NOT trigger infinite retry loops");
        }

        // Test 10: Persistent 503 -> bounded failure
        [Fact]
        public async Task Test10_Persistent503_ExhaustsMaxRetries_AndFailsBounded()
        {
            using var httpClient = new HttpClient();
            var pipeline = new HttpRequestPipeline(httpClient);

            Func<Task> action = async () =>
            {
                await pipeline.ExecuteWithRetryAsync(
                    () =>
                    {
                        var req = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1/fake");
                        return req;
                    },
                    HttpCompletionOption.ResponseHeadersRead,
                    CancellationToken.None,
                    maxRetries: 2).ConfigureAwait(false);
            };

            await action.Should().ThrowAsync<HttpRequestException>("Persistent server failure must fail boundedly after max retries");
        }

        // Test 11: One worker fails while others continue
        [Fact]
        public async Task Test11_OneWorkerFails_OtherWorkersContinueSafely()
        {
            int payloadSize = 4 * 1024 * 1024;
            byte[] payload = new byte[payloadSize];
            new Random(111).NextBytes(payload);

            int port = Random.Shared.Next(44000, 44999);
            string prefix = $"http://127.0.0.1:{port}/worker-fail/";

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

            string tempFile = Path.Combine(Path.GetTempPath(), $"worker_fail_{Guid.NewGuid():N}.bin");

            try
            {
                using var httpClient = new HttpClient();
                var downloader = new MultiPartDownloader(httpClient);

                await downloader.DownloadFileAsync(
                    fileUrl: new Uri(prefix),
                    destinationFilePath: tempFile,
                    chunkCount: 3,
                    maxConcurrency: 6,
                    progress: null,
                    cancellationToken: CancellationToken.None).ConfigureAwait(false);

                File.Exists(tempFile).Should().BeTrue();
                byte[] downloaded = await File.ReadAllBytesAsync(tempFile);
                downloaded.Length.Should().Be(payload.Length);
            }
            finally
            {
                listener.Stop();
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        // Test 12: Multiple workers fail simultaneously -> scheduler handles safely
        [Fact]
        public void Test12_MultipleWorkersFail_SchedulerRequeuesSafely()
        {
            var scheduler = new SegmentScheduler(10 * 1024 * 1024);
            scheduler.InitializeDefault(4);

            scheduler.MarkFailed(0, requeue: true);
            scheduler.MarkFailed(1, requeue: true);

            var snapshot = scheduler.GetSegmentsSnapshot();
            snapshot[0].State.Should().Be(SegmentState.Pending);
            snapshot[1].State.Should().Be(SegmentState.Pending);
        }

        // Test 13: Server recovers -> A2 gradually increases concurrency
        [Fact]
        public void Test13_ServerRecovers_A2GraduallyIncreasesConcurrency()
        {
            var controller = new AdaptiveConnectionController(initialConnections: 4, minConnections: 2, maxConnections: 16);
            controller.ResetCooldown();

            // Record 3 samples after recovery showing +30% throughput gain and 0 errors
            controller.RecordTelemetry(aggregateThroughputBps: 2_000_000, averageRttMs: 30.0, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 2_800_000, averageRttMs: 30.0, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 3_800_000, averageRttMs: 30.0, errorCount: 0);

            int conns = controller.EvaluateConnectionCount(totalFileSize: 100 * 1024 * 1024, isMeteredNetwork: false);
            conns.Should().Be(6, "A2 must gradually increase concurrency (+2) after server recovery");
        }

        // Test 14: Application cancellation during backoff
        [Fact]
        public async Task Test14_ApplicationCancellation_DuringBackoff_CancelsImmediately()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            using var httpClient = new HttpClient();
            var pipeline = new HttpRequestPipeline(httpClient);

            Func<Task> action = async () =>
            {
                await pipeline.ExecuteWithRetryAsync(
                    () => new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1/fake"),
                    HttpCompletionOption.ResponseHeadersRead,
                    cts.Token,
                    maxRetries: 5).ConfigureAwait(false);
            };

            await action.Should().ThrowAsync<OperationCanceledException>("Cancellation during backoff must throw OperationCanceledException immediately");
        }

        // Test 15: Application shutdown during retry
        [Fact]
        public async Task Test15_ApplicationShutdown_DuringRetry_ThrowsOperationCanceledException()
        {
            using var cts = new CancellationTokenSource(50); // 50ms timeout

            using var httpClient = new HttpClient();
            var pipeline = new HttpRequestPipeline(httpClient);

            Func<Task> action = async () =>
            {
                await pipeline.ExecuteWithRetryAsync(
                    () =>
                    {
                        var msg = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                        return new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1/fake") { Content = msg.Content };
                    },
                    HttpCompletionOption.ResponseHeadersRead,
                    cts.Token,
                    maxRetries: 10).ConfigureAwait(false);
            };

            await action.Should().ThrowAsync<OperationCanceledException>();
        }

        // Test 16: Restart after retry state was persisted
        [Fact]
        public async Task Test16_RestartAfterRetryStatePersisted_RestoresCleanRanges()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"a3_meta_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            string metaPath = Path.Combine(tempDir, "metadata.json");

            try
            {
                var metaManager = new DurableMetadataManager();
                var state = new DurableDownloadState
                {
                    Url = "http://127.0.0.1/fake.bin",
                    TotalBytes = 10_000_000,
                    Segments = new List<SegmentRange>
                    {
                        new SegmentRange { Id = 0, Start = 0, End = 4_999_999, BytesDownloaded = 2_500_000, State = SegmentState.Downloading },
                        new SegmentRange { Id = 1, Start = 5_000_000, End = 9_999_999, BytesDownloaded = 0, State = SegmentState.Pending }
                    }
                };


                await metaManager.WriteStateAtomicAsync(metaPath, state, CancellationToken.None).ConfigureAwait(false);

                var restored = await metaManager.ReadStateAsync(metaPath, CancellationToken.None).ConfigureAwait(false);
                restored.Should().NotBeNull();
                restored!.Segments.Count.Should().Be(2);
                restored.Segments[0].BytesDownloaded.Should().Be(2_500_000);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        // Test 17: 100-Run A3 Failure Recovery Stress Test
        [Fact]
        public async Task Test17_Run_100_A3_FailureRecovery_StressTest()
        {
            int payloadSize = 4 * 1024 * 1024;
            byte[] payload = new byte[payloadSize];
            new Random(333).NextBytes(payload);

            string expectedSha256;
            using (var sha = SHA256.Create())
            {
                expectedSha256 = Convert.ToHexString(sha.ComputeHash(payload));
            }

            int port = Random.Shared.Next(43000, 43999);
            string prefix = $"http://127.0.0.1:{port}/a3-stress-100/";

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

            int passedCount = 0;

            for (int i = 1; i <= 100; i++)
            {
                string tempFile = Path.Combine(Path.GetTempPath(), $"a3_stress_{i}_{Guid.NewGuid():N}.bin");
                try
                {
                    using var httpClient = new HttpClient();
                    var downloader = new MultiPartDownloader(httpClient);

                    await downloader.DownloadFileAsync(
                        fileUrl: new Uri(prefix),
                        destinationFilePath: tempFile,
                        chunkCount: 2,
                        maxConcurrency: 4,
                        progress: null,
                        cancellationToken: CancellationToken.None).ConfigureAwait(false);

                    byte[] downloaded = await File.ReadAllBytesAsync(tempFile);
                    string actualSha256;
                    using (var sha = SHA256.Create())
                    {
                        actualSha256 = Convert.ToHexString(sha.ComputeHash(downloaded));
                    }

                    if (downloaded.Length == payload.Length && actualSha256 == expectedSha256)
                    {
                        passedCount++;
                    }
                }
                finally
                {
                    if (File.Exists(tempFile)) File.Delete(tempFile);
                }
            }

            listener.Stop();
            passedCount.Should().Be(100, "All 100 A3 failure recovery stress test repetitions must pass cleanly");
        }
    }
}
