using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    /// <summary>
    /// Phase A4 — HTTP Range, Content-Length &amp; Download Integrity Hardening
    ///
    /// All tests use a deterministic local HttpListener to simulate exact server behaviours.
    /// No real network required.
    ///
    /// Design: Each test method creates its OWN server instance and disposes it within
    /// the test body. This prevents cross-test port collisions and listener lifecycle issues
    /// when [Theory] generates multiple invocations.
    /// </summary>
    public class HttpRangeIntegrityTests
    {
        // -----------------------------------------------------------------------
        // Deterministic 64 KB fixture: bytes[i] = (byte)(i % 251)
        // SHA-256 is computed once and reused for hash assertions.
        // -----------------------------------------------------------------------
        private static readonly byte[] Fixture = BuildFixture(64 * 1024);
        private static readonly string FixtureSha256 = ComputeSha256(Fixture);

        private static byte[] BuildFixture(int size)
        {
            var b = new byte[size];
            for (int i = 0; i < size; i++) b[i] = (byte)(i % 251);
            return b;
        }

        private static string ComputeSha256(byte[] data)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(data);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        // -----------------------------------------------------------------------
        // Self-contained server: each call creates, runs, and returns a disposable server.
        // Use `await using` to guarantee cleanup even on test failure.
        // -----------------------------------------------------------------------
        private sealed class TestServer : IAsyncDisposable
        {
            private readonly HttpListener _listener;
            public string Url { get; }

            private TestServer(HttpListener listener, string url)
            {
                _listener = listener;
                Url = url;
            }

            public static TestServer Start(Func<HttpListenerContext, Task> handler)
            {
                int port = FindFreePort();
                string prefix = $"http://localhost:{port}/";
                var listener = new HttpListener();
                listener.Prefixes.Add(prefix);
                listener.Start();

                // Background loop — stops when listener is stopped
                _ = Task.Run(async () =>
                {
                    while (listener.IsListening)
                    {
                        HttpListenerContext ctx;
                        try { ctx = await listener.GetContextAsync(); }
                        catch { break; }
                        _ = Task.Run(async () =>
                        {
                            try { await handler(ctx); }
                            catch { try { ctx.Response.Abort(); } catch { } }
                        });
                    }
                });

                return new TestServer(listener, prefix + "file");
            }

            private static int FindFreePort()
            {
                var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                return port;
            }

            public async ValueTask DisposeAsync()
            {
                try { _listener.Stop(); } catch { }
                await Task.CompletedTask;
            }
        }

        private static HttpRequestPipeline BuildPipeline()
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            return new HttpRequestPipeline(client);
        }

        // Full 206 server that handles HEAD + range GET correctly
        private static async Task FullRangeServer(HttpListenerContext ctx, byte[] data)
        {
            try
            {
                // Connection: close prevents HttpClient from attempting keep-alive reuse
                // on a connection that the HttpListener may have already recycled.
                ctx.Response.Headers["Connection"] = "close";

                if (ctx.Request.HttpMethod == "HEAD")
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.Headers["Accept-Ranges"] = "bytes";
                    ctx.Response.ContentLength64 = data.Length;
                    ctx.Response.Close();
                    return;
                }

                string? rangeHeader = ctx.Request.Headers["Range"];
                if (rangeHeader != null && rangeHeader.StartsWith("bytes="))
                {
                    var parts = rangeHeader.Substring(6).Split('-');
                    if (parts.Length == 2 &&
                        long.TryParse(parts[0], out long start) &&
                        long.TryParse(parts[1], out long end))
                    {
                        end = Math.Min(end, data.Length - 1);
                        int len = (int)(end - start + 1);
                        ctx.Response.StatusCode = 206;
                        ctx.Response.Headers["Content-Range"] = $"bytes {start}-{end}/{data.Length}";
                        ctx.Response.Headers["Accept-Ranges"] = "bytes";
                        ctx.Response.ContentLength64 = len;
                        await ctx.Response.OutputStream.WriteAsync(data, (int)start, len);
                        ctx.Response.Close();
                        return;
                    }
                }

                ctx.Response.StatusCode = 200;
                ctx.Response.ContentLength64 = data.Length;
                await ctx.Response.OutputStream.WriteAsync(data, 0, data.Length);
                ctx.Response.Close();
            }
            catch { try { ctx.Response.Abort(); } catch { } }
        }

        // -----------------------------------------------------------------------
        // TEST 1: Valid 206 — happy path
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Valid206_AcceptsCorrectPartialResponse()
        {
            long start = 0, end = 999, total = Fixture.Length;
            byte[] body = Fixture[0..1000];
            await using var server = TestServer.Start(async ctx =>
            {
                ctx.Response.StatusCode = 206;
                ctx.Response.Headers["Content-Range"] = $"bytes {start}-{end}/{total}";
                ctx.Response.ContentLength64 = body.Length;
                await ctx.Response.OutputStream.WriteAsync(body);
                ctx.Response.Close();
            });

            var pipeline = BuildPipeline();
            var result = await pipeline.ExecuteWithRetryAsync(
                () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri(server.Url), start, end),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None,
                requirePartialContent: true,
                expectedRangeStart: start,
                expectedRangeEnd: end,
                knownTotalBytes: total);

            result.IsPartialContent.Should().BeTrue();
            result.ContentRangeStart.Should().Be(start);
            result.ContentRangeEnd.Should().Be(end);
            result.ContentRangeTotal.Should().Be(total);
        }

        // -----------------------------------------------------------------------
        // TEST 2: Wrong Content-Range start byte
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Wrong_ContentRange_Start_ThrowsInvalidDataException()
        {
            await using var server = TestServer.Start(async ctx =>
            {
                ctx.Response.StatusCode = 206;
                ctx.Response.Headers["Content-Range"] = "bytes 500-1999/65536";
                ctx.Response.ContentLength64 = 1500;
                await ctx.Response.OutputStream.WriteAsync(new byte[1500]);
                ctx.Response.Close();
            });

            var pipeline = BuildPipeline();
            Func<Task> act = () => pipeline.ExecuteWithRetryAsync(
                () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri(server.Url), 1000, 1999),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None,
                requirePartialContent: true,
                expectedRangeStart: 1000,
                expectedRangeEnd: 1999,
                knownTotalBytes: 65536);

            await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*start*mismatch*");
        }

        // -----------------------------------------------------------------------
        // TEST 3: Wrong Content-Range end byte
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Wrong_ContentRange_End_ThrowsInvalidDataException()
        {
            await using var server = TestServer.Start(async ctx =>
            {
                ctx.Response.StatusCode = 206;
                ctx.Response.Headers["Content-Range"] = "bytes 1000-1500/65536";
                ctx.Response.ContentLength64 = 501;
                await ctx.Response.OutputStream.WriteAsync(new byte[501]);
                ctx.Response.Close();
            });

            var pipeline = BuildPipeline();
            Func<Task> act = () => pipeline.ExecuteWithRetryAsync(
                () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri(server.Url), 1000, 1999),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None,
                requirePartialContent: true,
                expectedRangeStart: 1000,
                expectedRangeEnd: 1999,
                knownTotalBytes: 65536);

            await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*end*mismatch*");
        }

        // -----------------------------------------------------------------------
        // TEST 4: Wrong Content-Range total
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Wrong_ContentRange_Total_ThrowsInvalidDataException()
        {
            await using var server = TestServer.Start(async ctx =>
            {
                ctx.Response.StatusCode = 206;
                ctx.Response.Headers["Content-Range"] = "bytes 0-999/99999";
                ctx.Response.ContentLength64 = 1000;
                await ctx.Response.OutputStream.WriteAsync(new byte[1000]);
                ctx.Response.Close();
            });

            var pipeline = BuildPipeline();
            Func<Task> act = () => pipeline.ExecuteWithRetryAsync(
                () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri(server.Url), 0, 999),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None,
                requirePartialContent: true,
                expectedRangeStart: 0,
                expectedRangeEnd: 999,
                knownTotalBytes: 65536);

            await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*total*mismatch*");
        }

        // -----------------------------------------------------------------------
        // TEST 5: Missing Content-Range header on 206
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Missing_ContentRange_Header_ThrowsInvalidDataException()
        {
            await using var server = TestServer.Start(async ctx =>
            {
                ctx.Response.StatusCode = 206;
                // Intentionally omit Content-Range header
                ctx.Response.ContentLength64 = 1000;
                await ctx.Response.OutputStream.WriteAsync(new byte[1000]);
                ctx.Response.Close();
            });

            var pipeline = BuildPipeline();
            Func<Task> act = () => pipeline.ExecuteWithRetryAsync(
                () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri(server.Url), 0, 999),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None,
                requirePartialContent: true,
                expectedRangeStart: 0,
                expectedRangeEnd: 999,
                knownTotalBytes: 65536);

            await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*Content-Range*Protocol violation*");
        }

        // -----------------------------------------------------------------------
        // TEST 6: Wrong Content-Length
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Wrong_ContentLength_ThrowsInvalidDataException()
        {
            await using var server = TestServer.Start(async ctx =>
            {
                ctx.Response.StatusCode = 206;
                ctx.Response.Headers["Content-Range"] = "bytes 0-999/65536";
                ctx.Response.ContentLength64 = 500; // Claims 500 but segment expects 1000
                await ctx.Response.OutputStream.WriteAsync(new byte[500]);
                ctx.Response.Close();
            });

            var pipeline = BuildPipeline();
            Func<Task> act = () => pipeline.ExecuteWithRetryAsync(
                () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri(server.Url), 0, 999),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None,
                requirePartialContent: true,
                expectedRangeStart: 0,
                expectedRangeEnd: 999,
                knownTotalBytes: 65536);

            await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*Content-Length*mismatch*");
        }

        // -----------------------------------------------------------------------
        // TEST 7: Short body — stream closes after 999 of 1000 expected bytes
        // -----------------------------------------------------------------------
        [Fact]
        public async Task ShortBody_SegmentWorker_DetectsAndThrows()
        {
            // Server: sends correct headers but truncated body
            await using var server = TestServer.Start(async ctx =>
            {
                if (ctx.Request.HttpMethod == "HEAD")
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.Headers["Accept-Ranges"] = "bytes";
                    ctx.Response.ContentLength64 = 65536;
                    ctx.Response.Close();
                    return;
                }
                ctx.Response.StatusCode = 206;
                ctx.Response.Headers["Content-Range"] = "bytes 0-65535/65536";
                ctx.Response.ContentLength64 = 65536;
                // Send only 60000 bytes — short read
                await ctx.Response.OutputStream.WriteAsync(new byte[60000]);
                ctx.Response.Close();
            });

            string destDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(destDir);
            string destFile = Path.Combine(destDir, "short.bin");
            try
            {
                using var handler = new HttpClientHandler();
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
                var downloader = new MultiPartDownloader(client);

                Func<Task> act = () => downloader.DownloadFileAsync(
                    new Uri(server.Url), destFile, chunkCount: 1, maxConcurrency: 1,
                    cancellationToken: new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token);

                // Must throw — short read must NOT produce a "complete" file
                await act.Should().ThrowAsync<Exception>(
                    "a short read must not silently complete the download");

                // If file was written, it must be absent or partial
                if (File.Exists(destFile))
                    new FileInfo(destFile).Length.Should().BeLessThan(65536,
                        "partial file must not be accepted as complete");
            }
            finally { try { Directory.Delete(destDir, true); } catch { } }
        }

        // -----------------------------------------------------------------------
        // TEST 8: Server returns 200 OK on range request → RangeFallbackRequiredException
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Server200_OnRangeRequest_ThrowsRangeFallbackRequiredException()
        {
            await using var server = TestServer.Start(async ctx =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentLength64 = Fixture.Length;
                await ctx.Response.OutputStream.WriteAsync(Fixture);
                ctx.Response.Close();
            });

            var pipeline = BuildPipeline();
            Func<Task> act = () => pipeline.ExecuteWithRetryAsync(
                () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri(server.Url), 1000, 1999),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None,
                requirePartialContent: true,
                expectedRangeStart: 1000,
                expectedRangeEnd: 1999,
                knownTotalBytes: Fixture.Length);

            await act.Should().ThrowAsync<RangeFallbackRequiredException>()
                .WithMessage("*200 OK*instead of 206*");
        }

        // -----------------------------------------------------------------------
        // TEST 9: 416 Range Not Satisfiable — not retried
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Status416_IsNotRetried_ThrowsHttpRequestException()
        {
            int requestCount = 0;
            await using var server = TestServer.Start(async ctx =>
            {
                Interlocked.Increment(ref requestCount);
                ctx.Response.StatusCode = 416;
                ctx.Response.Headers["Content-Range"] = "bytes */65536";
                ctx.Response.Close();
                await Task.CompletedTask;
            });

            var pipeline = BuildPipeline();
            Func<Task> act = () => pipeline.ExecuteWithRetryAsync(
                () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri(server.Url), 99999, 100000),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None,
                maxRetries: 3);

            await act.Should().ThrowAsync<HttpRequestException>();
            requestCount.Should().Be(1, "416 must not be retried");
        }

        // -----------------------------------------------------------------------
        // TEST 10: 429 Too Many Requests — retried with Retry-After
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Status429_IsRetried_WithRetryAfter()
        {
            int requestCount = 0;
            await using var server = TestServer.Start(async ctx =>
            {
                int attempt = Interlocked.Increment(ref requestCount);
                if (attempt <= 2)
                {
                    ctx.Response.StatusCode = 429;
                    ctx.Response.Headers["Retry-After"] = "1";
                    ctx.Response.Close();
                }
                else
                {
                    ctx.Response.StatusCode = 206;
                    ctx.Response.Headers["Content-Range"] = $"bytes 0-999/{Fixture.Length}";
                    ctx.Response.ContentLength64 = 1000;
                    await ctx.Response.OutputStream.WriteAsync(Fixture, 0, 1000);
                    ctx.Response.Close();
                }
            });

            var pipeline = BuildPipeline();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await pipeline.ExecuteWithRetryAsync(
                () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri(server.Url), 0, 999),
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token,
                maxRetries: 5,
                requirePartialContent: true,
                expectedRangeStart: 0,
                expectedRangeEnd: 999,
                knownTotalBytes: Fixture.Length);

            result.IsPartialContent.Should().BeTrue();
            requestCount.Should().BeGreaterThanOrEqualTo(3);
        }

        // -----------------------------------------------------------------------
        // TEST 11: 500 — retried up to maxRetries, then throws
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Status500_IsRetried_ThenFails()
        {
            int requestCount = 0;
            await using var server = TestServer.Start(async ctx =>
            {
                Interlocked.Increment(ref requestCount);
                ctx.Response.StatusCode = 500;
                ctx.Response.Close();
                await Task.CompletedTask;
            });

            var pipeline = BuildPipeline();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            Func<Task> act = () => pipeline.ExecuteWithRetryAsync(
                () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri(server.Url), 0, 999),
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token,
                maxRetries: 2);

            await act.Should().ThrowAsync<Exception>();
            requestCount.Should().BeGreaterThanOrEqualTo(2);
        }

        // -----------------------------------------------------------------------
        // TEST 12: 503 — retried with Retry-After
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Status503_IsRetried_WithBackoff()
        {
            int requestCount = 0;
            await using var server = TestServer.Start(async ctx =>
            {
                int attempt = Interlocked.Increment(ref requestCount);
                if (attempt <= 1)
                {
                    ctx.Response.StatusCode = 503;
                    ctx.Response.Headers["Retry-After"] = "1";
                    ctx.Response.Close();
                }
                else
                {
                    ctx.Response.StatusCode = 206;
                    ctx.Response.Headers["Content-Range"] = $"bytes 0-999/{Fixture.Length}";
                    ctx.Response.ContentLength64 = 1000;
                    await ctx.Response.OutputStream.WriteAsync(Fixture, 0, 1000);
                    ctx.Response.Close();
                }
            });

            var pipeline = BuildPipeline();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await pipeline.ExecuteWithRetryAsync(
                () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri(server.Url), 0, 999),
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token,
                maxRetries: 3,
                requirePartialContent: true,
                expectedRangeStart: 0,
                expectedRangeEnd: 999,
                knownTotalBytes: Fixture.Length);

            result.IsPartialContent.Should().BeTrue();
        }

        // -----------------------------------------------------------------------
        // TEST 13: Connection reset mid-stream
        // -----------------------------------------------------------------------
        [Fact]
        public async Task ConnectionReset_MidStream_ThrowsException()
        {
            await using var server = TestServer.Start(async ctx =>
            {
                if (ctx.Request.HttpMethod == "HEAD")
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.Headers["Accept-Ranges"] = "bytes";
                    ctx.Response.ContentLength64 = 65536;
                    ctx.Response.Close();
                    return;
                }
                ctx.Response.StatusCode = 206;
                ctx.Response.Headers["Content-Range"] = "bytes 0-65535/65536";
                ctx.Response.ContentLength64 = 65536;
                await ctx.Response.OutputStream.WriteAsync(new byte[400]);
                ctx.Response.Abort(); // Abrupt connection close
            });

            string destDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(destDir);
            string destFile = Path.Combine(destDir, "reset.bin");
            try
            {
                using var handler = new HttpClientHandler();
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
                var downloader = new MultiPartDownloader(client);

                Func<Task> act = () => downloader.DownloadFileAsync(
                    new Uri(server.Url), destFile, chunkCount: 1, maxConcurrency: 1,
                    cancellationToken: new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);

                await act.Should().ThrowAsync<Exception>(
                    "connection reset must not silently produce a complete file");

                if (File.Exists(destFile))
                    new FileInfo(destFile).Length.Should().BeLessThan(65536);
            }
            finally { try { Directory.Delete(destDir, true); } catch { } }
        }

        // -----------------------------------------------------------------------
        // TEST 14: Cancellation — CancellationToken respected
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Cancellation_IsRespected_DoesNotHang()
        {
            await using var server = TestServer.Start(async ctx =>
            {
                await Task.Delay(Timeout.Infinite); // Hang forever
                ctx.Response.Close();
            });

            var pipeline = BuildPipeline();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            Func<Task> act = () => pipeline.ExecuteWithRetryAsync(
                () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri(server.Url), 0, 999),
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token,
                maxRetries: 0);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        // -----------------------------------------------------------------------
        // TEST 15/16/17: End-to-End SHA-256 with 1, 4, 8 connections
        // Each invocation gets its own server instance (no IDisposable sharing).
        // -----------------------------------------------------------------------
        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(8)]
        public async Task EndToEnd_Sha256_MatchesFixture(int connections)
        {
            // Use a self-contained server per invocation (each [Theory] runs independently)
            await using var server = TestServer.Start(ctx => FullRangeServer(ctx, Fixture));

            string destDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(destDir);
            string destFile = Path.Combine(destDir, $"e2e_{connections}.bin");

            try
            {
                // Use SocketsHttpHandler with PooledConnectionLifetime=0 to prevent
                // HttpClient from trying to reuse connections across worker threads
                // when the listener has already served a request on that socket.
                using var handler = new System.Net.Http.SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.Zero,
                    PooledConnectionIdleTimeout = TimeSpan.Zero,
                    MaxConnectionsPerServer = connections + 2
                };
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
                var downloader = new MultiPartDownloader(client);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                await downloader.DownloadFileAsync(
                    new Uri(server.Url), destFile, chunkCount: connections, maxConcurrency: connections,
                    cancellationToken: cts.Token);

                File.Exists(destFile).Should().BeTrue();
                new FileInfo(destFile).Length.Should().Be(Fixture.Length,
                    $"file size must match fixture for {connections} connections");

                var integrity = new FileIntegrityService();
                string actualHash = await integrity.ComputeSha256Async(destFile, CancellationToken.None);
                actualHash.Should().Be(FixtureSha256,
                    $"SHA-256 must match fixture for {connections} connection(s)");
            }
            finally { try { Directory.Delete(destDir, true); } catch { } }
        }

        // -----------------------------------------------------------------------
        // TEST 18: ETag changed on resume — rejected
        // -----------------------------------------------------------------------
        [Fact]
        public void ETagChanged_OnResume_RejectedByMetadataManager()
        {
            var manager = new DurableMetadataManager();
            var state = new DurableDownloadState
            {
                TotalBytes = 65536,
                ETag = "\"original-etag\"",
                LastModified = "Mon, 10 Aug 2026 00:00:00 GMT",
                Segments = new System.Collections.Generic.List<SegmentRange>()
            };

            bool valid = manager.ReconcileAndValidate(state, "\"changed-etag\"", "Mon, 10 Aug 2026 00:00:00 GMT");
            valid.Should().BeFalse("ETag mismatch must invalidate resume state");
        }

        // -----------------------------------------------------------------------
        // TEST 19: Last-Modified changed on resume — rejected
        // -----------------------------------------------------------------------
        [Fact]
        public void LastModifiedChanged_OnResume_RejectedByMetadataManager()
        {
            var manager = new DurableMetadataManager();
            var state = new DurableDownloadState
            {
                TotalBytes = 65536,
                ETag = null,
                LastModified = "Mon, 10 Aug 2026 00:00:00 GMT",
                Segments = new System.Collections.Generic.List<SegmentRange>()
            };

            bool valid = manager.ReconcileAndValidate(state, "", "Tue, 11 Aug 2026 12:00:00 GMT");
            valid.Should().BeFalse("Last-Modified change must invalidate resume state");
        }

        // -----------------------------------------------------------------------
        // TEST 20: ETag unchanged — resume accepted
        // -----------------------------------------------------------------------
        [Fact]
        public void ETagUnchanged_OnResume_AcceptedByMetadataManager()
        {
            var manager = new DurableMetadataManager();
            var state = new DurableDownloadState
            {
                TotalBytes = 65536,
                ETag = "\"stable-etag\"",
                LastModified = "Mon, 10 Aug 2026 00:00:00 GMT",
                Segments = new System.Collections.Generic.List<SegmentRange>()
            };

            bool valid = manager.ReconcileAndValidate(state, "\"stable-etag\"", "Mon, 10 Aug 2026 00:00:00 GMT");
            valid.Should().BeTrue("matching ETag must allow resume");
        }
    }
}
