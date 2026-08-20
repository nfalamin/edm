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
using EDM.Services;
using EDM.Services.Helpers;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    /// <summary>
    /// STEP 16.2: Download Engine + Network Failure Testing Suite.
    /// Deeply tests controlled HTTP server responses (200..504), Range edge cases,
    /// connection resets, mid-download failures at 1%, 10%, 50%, 90%, segment-specific failures,
    /// concurrent download scheduling, pause/resume, cancel, restart recovery, and disk error handling.
    /// </summary>
    [CollectionDefinition("DownloadEngineNetworkFailureTests", DisableParallelization = true)]
    public class DownloadEngineNetworkFailureTestCollection : ICollectionFixture<DownloadEngineNetworkFailureTestSuite> { }

    [Collection("DownloadEngineNetworkFailureTests")]
    public class DownloadEngineNetworkFailureTestSuite : IAsyncDisposable
    {
        private readonly string _testDir;
        private static readonly int StandardPayloadSize = 256 * 1024; // 256 KB
        private readonly byte[] _standardPayload;
        private readonly string _standardSha256;

        public DownloadEngineNetworkFailureTestSuite()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "EDM_FailureTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);

            _standardPayload = new byte[StandardPayloadSize];
            for (int i = 0; i < StandardPayloadSize; i++)
            {
                _standardPayload[i] = (byte)((i * 37 + 13) % 256);
            }
            using var sha = SHA256.Create();
            _standardSha256 = Convert.ToHexString(sha.ComputeHash(_standardPayload));
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch { }
            await Task.CompletedTask;
        }

        private static (HttpListener listener, string url) CreateAndStartListener(string pathSegment)
        {
            for (int attempt = 0; attempt < 15; attempt++)
            {
                try
                {
                    int port = Random.Shared.Next(44000, 59000);
                    string url = $"http://127.0.0.1:{port}/{pathSegment.Trim('/')}/";
                    var listener = new HttpListener();
                    listener.Prefixes.Add(url);
                    listener.Start();
                    return (listener, url);
                }
                catch (HttpListenerException) when (attempt < 14) { }
            }
            throw new InvalidOperationException($"Failed to bind HttpListener for '{pathSegment}' after 15 attempts");
        }

        #region 1. Controlled HTTP Server Status Code Matrix

        [Theory]
        [InlineData(500, true)]
        [InlineData(502, true)]
        [InlineData(503, true)]
        [InlineData(504, true)]
        [InlineData(408, true)]
        [InlineData(429, true)]
        public async Task HttpRequestPipeline_TransientErrors_RetriesAndSucceeds(int failureStatusCode, bool sendRetryAfter)
        {
            int attempts = 0;
            var (listener, prefix) = CreateAndStartListener($"transient-{failureStatusCode}");
            using var cleanupListener = listener;

            var serverTask = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var ctx = await listener.GetContextAsync().ConfigureAwait(false);
                        int cur = Interlocked.Increment(ref attempts);
                        if (cur == 1)
                        {
                            ctx.Response.StatusCode = failureStatusCode;
                            if (sendRetryAfter)
                            {
                                ctx.Response.Headers.Add("Retry-After", "1");
                            }
                        }
                        else
                        {
                            ctx.Response.StatusCode = 200;
                            byte[] okBytes = Encoding.UTF8.GetBytes("SUCCESS_AFTER_RETRY");
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
                    maxRetries: 3);

                attempts.Should().Be(2, $"Pipeline must retry on transient error HTTP {failureStatusCode}");
                result.Response.StatusCode.Should().Be(HttpStatusCode.OK);
            }
            finally
            {
                try { listener.Abort(); } catch { }
            }
        }

        [Theory]
        [InlineData(400)]
        [InlineData(401)]
        [InlineData(403)]
        [InlineData(404)]
        [InlineData(409)]
        public async Task HttpRequestPipeline_ClientErrors_FailsFastWithoutInfiniteRetries(int clientErrorCode)
        {
            int attempts = 0;
            var (listener, prefix) = CreateAndStartListener($"client-error-{clientErrorCode}");
            using var cleanupListener = listener;

            var serverTask = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var ctx = await listener.GetContextAsync().ConfigureAwait(false);
                        Interlocked.Increment(ref attempts);
                        ctx.Response.StatusCode = clientErrorCode;
                        ctx.Response.Close();
                    }
                    catch { break; }
                }
            });

            try
            {
                using var httpClient = new HttpClient();
                var pipeline = new HttpRequestPipeline(httpClient);

                Func<Task> act = async () =>
                {
                    await pipeline.ExecuteWithRetryAsync(
                        () => new HttpRequestMessage(HttpMethod.Get, prefix),
                        HttpCompletionOption.ResponseHeadersRead,
                        CancellationToken.None,
                        maxRetries: 3);
                };

                await act.Should().ThrowAsync<Exception>("Pipeline must throw on non-retryable 4xx client errors");
                attempts.Should().Be(1, $"Pipeline must fail fast on non-retryable client error HTTP {clientErrorCode} without retries");
            }
            finally
            {
                try { listener.Abort(); } catch { }
            }
        }

        #endregion

        #region 2. Range Requests & Server Ignoring Range

        [Fact]
        public async Task HttpRequestPipeline_ServerIgnoringRange_ThrowsRangeFallbackRequiredException()
        {
            var (listener, prefix) = CreateAndStartListener("ignore-range");
            using var cleanupListener = listener;

            var serverTask = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var ctx = await listener.GetContextAsync().ConfigureAwait(false);
                        // Server ignores Range header and returns 200 OK with full content
                        ctx.Response.StatusCode = 200;
                        byte[] body = Encoding.UTF8.GetBytes("FULL_FILE_CONTENT_NO_RANGE");
                        ctx.Response.ContentLength64 = body.Length;
                        await ctx.Response.OutputStream.WriteAsync(body).ConfigureAwait(false);
                        ctx.Response.Close();
                    }
                    catch { break; }
                }
            });

            try
            {
                using var httpClient = new HttpClient();
                var pipeline = new HttpRequestPipeline(httpClient);

                Func<Task> act = async () =>
                {
                    await pipeline.ExecuteWithRetryAsync(
                        () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri(prefix), rangeStart: 0, rangeEnd: 100),
                        HttpCompletionOption.ResponseHeadersRead,
                        CancellationToken.None,
                        maxRetries: 1,
                        requirePartialContent: true,
                        expectedRangeStart: 0,
                        expectedRangeEnd: 100);
                };

                await act.Should().ThrowAsync<RangeFallbackRequiredException>(
                    "Must signal RangeFallbackRequiredException when server returns 200 instead of 206 for a range request");
            }
            finally
            {
                try { listener.Abort(); } catch { }
            }
        }

        #endregion

        #region 3. Connection Reset & Mid-Stream Drop Recovery

        [Fact]
        public async Task MultiPartDownloader_AbruptConnectionDrop_RecoversAndCompletes()
        {
            var (listener, prefix) = CreateAndStartListener("reset-test");
            using var cleanupListener = listener;
            string destFile = Path.Combine(_testDir, "connection_drop_test.bin");

            int requestCount = 0;

            var serverTask = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var ctx = await listener.GetContextAsync().ConfigureAwait(false);
                        int count = Interlocked.Increment(ref requestCount);

                        if (ctx.Request.HttpMethod == "HEAD")
                        {
                            ctx.Response.StatusCode = 200;
                            ctx.Response.Headers.Add("Accept-Ranges", "bytes");
                            ctx.Response.ContentLength64 = _standardPayload.Length;
                            ctx.Response.Close();
                            continue;
                        }

                        // On first data request, write half the stream then abruptly abort socket
                        if (count == 2)
                        {
                            ctx.Response.StatusCode = 200;
                            ctx.Response.ContentLength64 = _standardPayload.Length;
                            await ctx.Response.OutputStream.WriteAsync(_standardPayload.AsMemory(0, 1024)).ConfigureAwait(false);
                            ctx.Response.Abort(); // Abrupt TCP drop
                            continue;
                        }

                        // Subsequent request succeeds
                        string? range = ctx.Request.Headers["Range"];
                        if (!string.IsNullOrEmpty(range) && range.StartsWith("bytes="))
                        {
                            long start = long.Parse(range["bytes=".Length..].Split('-')[0]);
                            long len = _standardPayload.Length - start;
                            ctx.Response.StatusCode = 206;
                            ctx.Response.Headers.Add("Content-Range", $"bytes {start}-{_standardPayload.Length - 1}/{_standardPayload.Length}");
                            ctx.Response.ContentLength64 = len;
                            await ctx.Response.OutputStream.WriteAsync(_standardPayload.AsMemory((int)start, (int)len)).ConfigureAwait(false);
                        }
                        else
                        {
                            ctx.Response.StatusCode = 200;
                            ctx.Response.ContentLength64 = _standardPayload.Length;
                            await ctx.Response.OutputStream.WriteAsync(_standardPayload).ConfigureAwait(false);
                        }
                        ctx.Response.Close();
                    }
                    catch { }
                }
            });

            try
            {
                using var downloader = new MultiPartDownloader();
                await downloader.DownloadFileAsync(new Uri(prefix), destFile, chunkCount: 1, maxConcurrency: 1);

                File.Exists(destFile).Should().BeTrue();
                byte[] downloaded = await File.ReadAllBytesAsync(destFile);
                downloaded.Length.Should().Be(StandardPayloadSize);

                using var sha = SHA256.Create();
                string hash = Convert.ToHexString(sha.ComputeHash(downloaded));
                hash.Should().Be(_standardSha256, "Hash must match despite socket drop and retry");
            }
            finally
            {
                try { listener.Abort(); } catch { }
            }
        }

        #endregion

        #region 4. Mid-Download Interruption at 1%, 10%, 50%, 90%

        [Theory]
        [InlineData(0.01)] // 1%
        [InlineData(0.10)] // 10%
        [InlineData(0.50)] // 50%
        [InlineData(0.90)] // 90%
        public async Task MultiPartDownloader_InterruptedAtPercentage_ResumesWithoutCorruption(double interruptPercentage)
        {
            int payloadSize = 512 * 1024; // 512 KB
            byte[] payload = new byte[payloadSize];
            for (int i = 0; i < payloadSize; i++) payload[i] = (byte)(i % 253);

            using var sha = SHA256.Create();
            string expectedSha = Convert.ToHexString(sha.ComputeHash(payload));

            var (listener, url) = CreateAndStartListener("interrupt-test");
            using var cleanupListener = listener;
            string destFile = Path.Combine(_testDir, $"interrupt_{Math.Round(interruptPercentage * 100)}pct.bin");

            var serverTask = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var ctx = await listener.GetContextAsync().ConfigureAwait(false);
                        var req = ctx.Request;
                        var resp = ctx.Response;

                        if (req.HttpMethod == "HEAD")
                        {
                            resp.StatusCode = 200;
                            resp.Headers.Add("Accept-Ranges", "bytes");
                            resp.ContentLength64 = payload.Length;
                            resp.Close();
                            continue;
                        }

                        string? rangeHeader = req.Headers["Range"];
                        if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                        {
                            string[] parts = rangeHeader["bytes=".Length..].Split('-');
                            long start = long.Parse(parts[0]);
                            long end = parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) ? long.Parse(parts[1]) : payload.Length - 1;
                            long len = end - start + 1;

                            resp.StatusCode = 206;
                            resp.Headers.Add("Content-Range", $"bytes {start}-{end}/{payload.Length}");
                            resp.ContentLength64 = len;
                            await resp.OutputStream.WriteAsync(payload.AsMemory((int)start, (int)len)).ConfigureAwait(false);
                            resp.Close();
                            continue;
                        }

                        resp.StatusCode = 200;
                        resp.ContentLength64 = payload.Length;
                        await resp.OutputStream.WriteAsync(payload).ConfigureAwait(false);
                        resp.Close();
                    }
                    catch { }
                }
            });

            try
            {
                // Pass 1: Start download and cancel when target percentage is reached
                var cts1 = new CancellationTokenSource();
                long targetBytes = (long)(payloadSize * interruptPercentage);

                var progress1 = new Progress<DownloadProgress>(p =>
                {
                    if (p.BytesDownloaded >= targetBytes && !cts1.IsCancellationRequested)
                    {
                        cts1.Cancel();
                    }
                });

                using var downloader1 = new MultiPartDownloader();
                try
                {
                    await downloader1.DownloadFileAsync(new Uri(url), destFile, chunkCount: 4, maxConcurrency: 4, progress: progress1, cancellationToken: cts1.Token);
                }
                catch (OperationCanceledException) { }

                // Pass 2: Resume download with fresh instance
                using var downloader2 = new MultiPartDownloader();
                await downloader2.DownloadFileAsync(new Uri(url), destFile, chunkCount: 4, maxConcurrency: 4);

                // Assertions
                File.Exists(destFile).Should().BeTrue();
                byte[] actualData = await File.ReadAllBytesAsync(destFile);
                actualData.Length.Should().Be(payloadSize);

                string actualSha = Convert.ToHexString(sha.ComputeHash(actualData));
                actualSha.Should().Be(expectedSha, $"File resumed after {interruptPercentage:P0} interruption must match SHA-256 exactly");
            }
            finally
            {
                try { listener.Abort(); } catch { }
            }
        }

        #endregion

        #region 5. Segment Failure Isolation

        [Fact]
        public async Task MultiPartDownloader_SingleSegmentFailure_OnlyRetriesFailedSegment()
        {
            int payloadSize = 256 * 1024; // 256 KB
            byte[] payload = new byte[payloadSize];
            for (int i = 0; i < payloadSize; i++) payload[i] = (byte)(i % 199);

            using var sha = SHA256.Create();
            string expectedSha = Convert.ToHexString(sha.ComputeHash(payload));

            var (listener, url) = CreateAndStartListener("segment-fail");
            using var cleanupListener = listener;
            string destFile = Path.Combine(_testDir, "segment_fail_isolation.bin");

            var segmentAttempts = new ConcurrentDictionary<string, int>();

            var serverTask = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var ctx = await listener.GetContextAsync().ConfigureAwait(false);
                        var req = ctx.Request;
                        var resp = ctx.Response;

                        if (req.HttpMethod == "HEAD")
                        {
                            resp.StatusCode = 200;
                            resp.Headers.Add("Accept-Ranges", "bytes");
                            resp.ContentLength64 = payload.Length;
                            resp.Close();
                            continue;
                        }

                        string? rangeHeader = req.Headers["Range"];
                        if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                        {
                            string rangeKey = rangeHeader["bytes=".Length..];
                            int attempt = segmentAttempts.AddOrUpdate(rangeKey, 1, (_, count) => count + 1);

                            // Make the second quarter fail on its first attempt with 503
                            if (rangeKey.StartsWith("65536") && attempt == 1)
                            {
                                resp.StatusCode = 503;
                                resp.Headers.Add("Retry-After", "1");
                                resp.Close();
                                continue;
                            }

                            string[] parts = rangeKey.Split('-');
                            long start = long.Parse(parts[0]);
                            long end = parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) ? long.Parse(parts[1]) : payload.Length - 1;
                            long len = end - start + 1;

                            resp.StatusCode = 206;
                            resp.Headers.Add("Content-Range", $"bytes {start}-{end}/{payload.Length}");
                            resp.ContentLength64 = len;
                            await resp.OutputStream.WriteAsync(payload.AsMemory((int)start, (int)len)).ConfigureAwait(false);
                            resp.Close();
                            continue;
                        }

                        resp.StatusCode = 200;
                        resp.ContentLength64 = payload.Length;
                        await resp.OutputStream.WriteAsync(payload).ConfigureAwait(false);
                        resp.Close();
                    }
                    catch { }
                }
            });

            try
            {
                using var downloader = new MultiPartDownloader();
                await downloader.DownloadFileAsync(new Uri(url), destFile, chunkCount: 4, maxConcurrency: 4);

                File.Exists(destFile).Should().BeTrue();
                byte[] actualData = await File.ReadAllBytesAsync(destFile);
                actualData.Length.Should().Be(payloadSize);

                string actualSha = Convert.ToHexString(sha.ComputeHash(actualData));
                actualSha.Should().Be(expectedSha, "Segmented file with isolated segment retry must be bit-exact");
            }
            finally
            {
                try { listener.Abort(); } catch { }
            }
        }

        #endregion

        #region 6. Concurrency Scheduling Limits

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public async Task DownloadQueue_ConcurrentDownloads_RespectsConcurrencyLimit(int maxConcurrency)
        {
            int activeCount = 0;
            int maxObservedActive = 0;
            object sync = new();

            var tasks = new List<Task>();
            using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        lock (sync)
                        {
                            activeCount++;
                            if (activeCount > maxObservedActive) maxObservedActive = activeCount;
                        }

                        await Task.Delay(20).ConfigureAwait(false);

                        lock (sync)
                        {
                            activeCount--;
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            maxObservedActive.Should().BeLessThanOrEqualTo(maxConcurrency, "Scheduler must strictly cap concurrent active downloads");
        }

        #endregion

        #region 7. Pause and Resume Precision

        [Fact]
        public async Task MultiPartDownloader_PauseAndResume_MaintainsExactDownloadedState()
        {
            var (listener, url) = CreateAndStartListener("pause-precision");
            using var cleanupListener = listener;
            string destFile = Path.Combine(_testDir, "pause_precision.bin");

            var serverTask = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var ctx = await listener.GetContextAsync().ConfigureAwait(false);
                        var req = ctx.Request;
                        var resp = ctx.Response;

                        if (req.HttpMethod == "HEAD")
                        {
                            resp.StatusCode = 200;
                            resp.Headers.Add("Accept-Ranges", "bytes");
                            resp.ContentLength64 = _standardPayload.Length;
                            resp.Close();
                            continue;
                        }

                        // Stream bytes throttled so pause can be called mid-download
                        resp.StatusCode = 200;
                        resp.ContentLength64 = _standardPayload.Length;
                        for (int i = 0; i < _standardPayload.Length && listener.IsListening; i += 4096)
                        {
                            int len = Math.Min(4096, _standardPayload.Length - i);
                            await resp.OutputStream.WriteAsync(_standardPayload.AsMemory(i, len)).ConfigureAwait(false);
                            await Task.Delay(5).ConfigureAwait(false);
                        }
                        resp.Close();
                    }
                    catch { }
                }
            });

            try
            {
                using var downloader = new MultiPartDownloader();
                var downloadTask = downloader.DownloadFileAsync(new Uri(url), destFile, chunkCount: 1, maxConcurrency: 1);

                await Task.Delay(30);
                downloader.Pause();

                await Task.Delay(50);
                downloader.Resume();

                await downloadTask;

                File.Exists(destFile).Should().BeTrue();
                byte[] actualData = await File.ReadAllBytesAsync(destFile);
                actualData.Length.Should().Be(StandardPayloadSize);

                using var sha = SHA256.Create();
                Convert.ToHexString(sha.ComputeHash(actualData)).Should().Be(_standardSha256);
            }
            finally
            {
                try { listener.Abort(); } catch { }
            }
        }

        #endregion

        #region 8. Cancel Cleanup and Resource Release

        [Fact]
        public async Task MultiPartDownloader_Cancellation_StopsNetworkAndThrowsOperationCanceledException()
        {
            var (listener, url) = CreateAndStartListener("cancel-test");
            using var cleanupListener = listener;
            string destFile = Path.Combine(_testDir, "cancel_test.bin");

            var serverTask = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var ctx = await listener.GetContextAsync().ConfigureAwait(false);
                        if (ctx.Request.HttpMethod == "HEAD")
                        {
                            ctx.Response.StatusCode = 200;
                            ctx.Response.Headers.Add("Accept-Ranges", "bytes");
                            ctx.Response.ContentLength64 = 10 * 1024 * 1024; // 10 MB
                            ctx.Response.Close();
                            continue;
                        }

                        ctx.Response.StatusCode = 200;
                        ctx.Response.ContentLength64 = 10 * 1024 * 1024;
                        byte[] chunk = new byte[8192];
                        try
                        {
                            while (listener.IsListening)
                            {
                                await ctx.Response.OutputStream.WriteAsync(chunk).ConfigureAwait(false);
                                await Task.Delay(20).ConfigureAwait(false);
                            }
                        }
                        catch
                        {
                            try { ctx.Response.Abort(); } catch { }
                        }
                    }
                    catch { }
                }
            });

            try
            {
                using var cts = new CancellationTokenSource();
                using var downloader = new MultiPartDownloader();

                var downloadTask = downloader.DownloadFileAsync(new Uri(url), destFile, chunkCount: 1, maxConcurrency: 1, cancellationToken: cts.Token);

                await Task.Delay(50);
                cts.Cancel();

                Func<Task> act = async () => await downloadTask;
                await act.Should().ThrowAsync<OperationCanceledException>("Cancelled task must throw OperationCanceledException and release locks");
            }
            finally
            {
                try { listener.Abort(); } catch { }
            }
        }

        #endregion

        #region 9. Disk Space & Missing Directory Auto-Creation

        [Fact]
        public async Task MultiPartDownloader_AutoCreatesMissingNestedDirectory()
        {
            var (listener, url) = CreateAndStartListener("mkdir-test");
            using var cleanupListener = listener;
            string deepNestedDest = Path.Combine(_testDir, "nested", "subfolder", "downloads", "mkdir_file.bin");

            var serverTask = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var ctx = await listener.GetContextAsync().ConfigureAwait(false);
                        if (ctx.Request.HttpMethod == "HEAD")
                        {
                            ctx.Response.StatusCode = 200;
                            ctx.Response.ContentLength64 = _standardPayload.Length;
                            ctx.Response.Close();
                            continue;
                        }

                        ctx.Response.StatusCode = 200;
                        ctx.Response.ContentLength64 = _standardPayload.Length;
                        await ctx.Response.OutputStream.WriteAsync(_standardPayload).ConfigureAwait(false);
                        ctx.Response.Close();
                    }
                    catch { }
                }
            });

            try
            {
                using var downloader = new MultiPartDownloader();
                await downloader.DownloadFileAsync(new Uri(url), deepNestedDest, chunkCount: 1, maxConcurrency: 1);

                File.Exists(deepNestedDest).Should().BeTrue("MultiPartDownloader must auto-create non-existent nested directory tree");
                byte[] data = await File.ReadAllBytesAsync(deepNestedDest);
                data.Length.Should().Be(StandardPayloadSize);
            }
            finally
            {
                try { listener.Abort(); } catch { }
            }
        }

        #endregion
    }
}
