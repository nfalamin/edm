using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Tests.E2E
{
    /// <summary>
    /// In-process deterministic HTTP Test Server for real E2E network and download testing.
    /// Simulates 200 OK, 206 Partial Content (Range), 301/302 Redirects, 401 Auth, 403 Cookie checks,
    /// 503 Transient errors for retry validation, and throttled streaming.
    /// </summary>
    public sealed class LocalHttpTestServer : IAsyncDisposable, IDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private Task? _listenTask;
        private int _retryEndpointAttempts = 0;

        public int Port { get; }
        public string BaseUrl => $"http://127.0.0.1:{Port}/";

        // Deterministic binary test payloads
        public byte[] SmallData { get; } = GenerateDeterministicBytes(64 * 1024, 0x11);
        public byte[] OneMbData { get; } = GenerateDeterministicBytes(1024 * 1024, 0x22);
        public byte[] TenMbData { get; } = GenerateDeterministicBytes(10 * 1024 * 1024, 0x33);
        public byte[] RangeData { get; } = GenerateDeterministicBytes(2 * 1024 * 1024, 0x44);
        public byte[] NoRangeData { get; } = GenerateDeterministicBytes(256 * 1024, 0x55);

        public LocalHttpTestServer()
        {
            var rnd = new Random();
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Port = rnd.Next(25000, 35000);
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
                    _listener.Start();
                    break;
                }
                catch
                {
                    _listener?.Close();
                }
            }

            if (_listener == null || !_listener.IsListening)
            {
                throw new InvalidOperationException("Failed to bind LocalHttpTestServer to an open port.");
            }

            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        }

        public string GetExpectedSha256(byte[] data)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(data);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private async Task ListenLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync().ConfigureAwait(false);
                    _ = Task.Run(() => HandleRequestAsync(context, ct), ct);
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext ctx, CancellationToken ct)
        {
            var req = ctx.Request;
            var resp = ctx.Response;

            try
            {
                string path = req.Url?.AbsolutePath ?? "/";

                switch (path)
                {
                    case "/small.bin":
                        await ServeBinaryDataAsync(ctx, SmallData, supportsRange: true, ct).ConfigureAwait(false);
                        break;

                    case "/1mb.bin":
                        await ServeBinaryDataAsync(ctx, OneMbData, supportsRange: true, ct).ConfigureAwait(false);
                        break;

                    case "/10mb.bin":
                        await ServeBinaryDataAsync(ctx, TenMbData, supportsRange: true, ct).ConfigureAwait(false);
                        break;

                    case "/range.bin":
                        await ServeBinaryDataAsync(ctx, RangeData, supportsRange: true, ct).ConfigureAwait(false);
                        break;

                    case "/no-range.bin":
                        await ServeBinaryDataAsync(ctx, NoRangeData, supportsRange: false, ct).ConfigureAwait(false);
                        break;

                    case "/redirect.bin":
                        resp.StatusCode = 302;
                        resp.Headers.Add("Location", $"{BaseUrl}1mb.bin");
                        resp.Close();
                        break;

                    case "/slow.bin":
                        await ServeThrottledDataAsync(ctx, SmallData, ct).ConfigureAwait(false);
                        break;

                    case "/auth.bin":
                        string? authHeader = req.Headers["Authorization"];
                        if (string.Equals(authHeader, "Basic dXNlcjpwYXNz", StringComparison.Ordinal)) // user:pass
                        {
                            await ServeBinaryDataAsync(ctx, SmallData, supportsRange: true, ct).ConfigureAwait(false);
                        }
                        else
                        {
                            resp.StatusCode = 401;
                            resp.Headers.Add("WWW-Authenticate", "Basic realm=\"EDM_Test\"");
                            resp.Close();
                        }
                        break;

                    case "/cookie.bin":
                        string? cookieHeader = req.Headers["Cookie"];
                        if (cookieHeader != null && cookieHeader.Contains("session_token=edm_valid_token_123"))
                        {
                            await ServeBinaryDataAsync(ctx, SmallData, supportsRange: true, ct).ConfigureAwait(false);
                        }
                        else
                        {
                            resp.StatusCode = 403;
                            resp.Close();
                        }
                        break;

                    case "/retry.bin":
                        int attempt = Interlocked.Increment(ref _retryEndpointAttempts);
                        if (attempt <= 2)
                        {
                            resp.StatusCode = 503;
                            resp.Headers.Add("Retry-After", "1");
                            resp.Close();
                        }
                        else
                        {
                            await ServeBinaryDataAsync(ctx, SmallData, supportsRange: true, ct).ConfigureAwait(false);
                        }
                        break;

                    case "/media.m3u8":
                        string m3u8Content =
                            "#EXTM3U\n" +
                            "#EXT-X-VERSION:3\n" +
                            $"#EXT-X-STREAM-INF:BANDWIDTH=2500000,RESOLUTION=1920x1080\n{BaseUrl}stream_1080p.m3u8\n" +
                            $"#EXT-X-STREAM-INF:BANDWIDTH=1200000,RESOLUTION=1280x720\n{BaseUrl}stream_720p.m3u8\n" +
                            $"#EXT-X-STREAM-INF:BANDWIDTH=600000,RESOLUTION=854x480\n{BaseUrl}stream_480p.m3u8\n";
                        byte[] m3u8Bytes = Encoding.UTF8.GetBytes(m3u8Content);
                        resp.ContentType = "application/vnd.apple.mpegurl";
                        resp.ContentLength64 = m3u8Bytes.Length;
                        await resp.OutputStream.WriteAsync(m3u8Bytes, 0, m3u8Bytes.Length, ct).ConfigureAwait(false);
                        resp.Close();
                        break;

                    default:
                        resp.StatusCode = 404;
                        resp.Close();
                        break;
                }
            }
            catch (Exception)
            {
                try { resp.Close(); } catch { }
            }
        }

        private async Task ServeBinaryDataAsync(HttpListenerContext ctx, byte[] data, bool supportsRange, CancellationToken ct)
        {
            var req = ctx.Request;
            var resp = ctx.Response;

            resp.ContentType = "application/octet-stream";
            resp.SendChunked = false;

            if (supportsRange)
            {
                resp.Headers.Add("Accept-Ranges", "bytes");
            }

            string? rangeHeader = req.Headers["Range"];
            if (supportsRange && !string.IsNullOrWhiteSpace(rangeHeader) && rangeHeader.StartsWith("bytes="))
            {
                string rangeVal = rangeHeader["bytes=".Length..].Trim();
                string[] parts = rangeVal.Split('-');

                long start = 0;
                long end = data.Length - 1;

                if (parts.Length == 2)
                {
                    if (!string.IsNullOrEmpty(parts[0])) start = long.Parse(parts[0]);
                    if (!string.IsNullOrEmpty(parts[1])) end = long.Parse(parts[1]);
                }

                start = Math.Max(0, Math.Min(start, data.Length - 1));
                end = Math.Max(start, Math.Min(end, data.Length - 1));
                long rangeLength = end - start + 1;

                resp.StatusCode = 206; // Partial Content
                resp.Headers.Add("Content-Range", $"bytes {start}-{end}/{data.Length}");
                resp.ContentLength64 = rangeLength;

                await resp.OutputStream.WriteAsync(data.AsMemory((int)start, (int)rangeLength), ct).ConfigureAwait(false);
                resp.Close();
                return;
            }

            resp.StatusCode = 200;
            resp.ContentLength64 = data.Length;
            await resp.OutputStream.WriteAsync(data, 0, data.Length, ct).ConfigureAwait(false);
            resp.Close();
        }

        private async Task ServeThrottledDataAsync(HttpListenerContext ctx, byte[] data, CancellationToken ct)
        {
            var resp = ctx.Response;
            resp.StatusCode = 200;
            resp.ContentType = "application/octet-stream";
            resp.ContentLength64 = data.Length;

            int chunkSize = 8 * 1024;
            int offset = 0;

            while (offset < data.Length && !ct.IsCancellationRequested)
            {
                int toWrite = Math.Min(chunkSize, data.Length - offset);
                await resp.OutputStream.WriteAsync(data.AsMemory(offset, toWrite), ct).ConfigureAwait(false);
                await resp.OutputStream.FlushAsync(ct).ConfigureAwait(false);
                offset += toWrite;
                await Task.Delay(40, ct).ConfigureAwait(false);
            }

            resp.Close();
        }

        private static byte[] GenerateDeterministicBytes(int size, byte seed)
        {
            byte[] buffer = new byte[size];
            for (int i = 0; i < size; i++)
            {
                buffer[i] = (byte)((seed + (i * 31)) % 256);
            }
            return buffer;
        }

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { }
            try { _listener.Stop(); } catch { }
            try { _listener.Close(); } catch { }
            try { _cts.Dispose(); } catch { }
        }

        public async ValueTask DisposeAsync()
        {
            Dispose();
            if (_listenTask != null)
            {
                try { await _listenTask.ConfigureAwait(false); } catch { }
            }
        }
    }
}
