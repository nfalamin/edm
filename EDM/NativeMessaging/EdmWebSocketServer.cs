using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;

namespace EDM.NativeMessaging
{
    /// <summary>
    /// Rock-solid Zero-Permission Embedded Local TCP HTTP & WebSocket Server for EDM.
    /// Uses raw TcpListener on 127.0.0.1:48912 — works 100% without Windows Admin rights.
    /// </summary>
    public sealed class EdmWebSocketServer : IAsyncDisposable
    {
        public const int DefaultPort = 48912;
        private readonly int _port;
        private readonly Func<IpcHandoffPayload, Task<bool>> _handoffHandler;
        private readonly Func<string, string, Task<string>>? _variantsHandler;
        private readonly CancellationTokenSource _cts = new();
        private TcpListener? _tcpListener;
        private Task? _listenTask;

        public bool IsRunning => _tcpListener != null && !_cts.IsCancellationRequested;

        /// <param name="handoffHandler">Handles POST /handoff download dispatch.</param>
        /// <param name="variantsHandler">Optional. Handles POST /variants format resolution. Args: (url, cookies) → JSON string.</param>
        public EdmWebSocketServer(
            Func<IpcHandoffPayload, Task<bool>> handoffHandler,
            Func<string, string, Task<string>>? variantsHandler = null,
            int port = DefaultPort)
        {
            _handoffHandler = handoffHandler ?? throw new ArgumentNullException(nameof(handoffHandler));
            _variantsHandler = variantsHandler;
            _port = port;
        }


        public void Start()
        {
            if (_listenTask != null) return;

            try
            {
                _tcpListener = new TcpListener(IPAddress.Loopback, _port);
                _tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _tcpListener.Start(100);
                _listenTask = Task.Run(() => AcceptClientsLoopAsync(_cts.Token));
                LoggingService.Log($"[EdmWebSocketServer] Bulletproof Local Server listening on 127.0.0.1:{_port}");
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[EdmWebSocketServer] Failed to bind TcpListener on port {_port}", ex);
            }
        }

        public void Stop()
        {
            try { _cts.Cancel(); } catch { }
            try { _tcpListener?.Stop(); } catch { }
        }

        public async ValueTask DisposeAsync()
        {
            Stop();
            if (_listenTask != null)
            {
                try { await Task.WhenAny(_listenTask, Task.Delay(500)).ConfigureAwait(false); } catch { }
            }
            try { _cts.Dispose(); } catch { }
        }

        private async Task AcceptClientsLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _tcpListener != null)
            {
                try
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync().WaitAsync(ct).ConfigureAwait(false);
                    _ = Task.Run(() => ProcessClientAsync(tcpClient, ct), ct);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    if (ct.IsCancellationRequested) break;
                    LoggingService.LogException("[EdmWebSocketServer] Accept error", ex);
                    try { await Task.Delay(200, ct).ConfigureAwait(false); } catch { break; }
                }
            }
        }

        private async Task ProcessClientAsync(TcpClient client, CancellationToken ct)
        {
            using (client)
            {
                try
                {
                    using var stream = client.GetStream();
                    stream.ReadTimeout = 10000;
                    stream.WriteTimeout = 10000;

                    var headerBuffer = new byte[16384];
                    int bytesRead = await stream.ReadAsync(headerBuffer, 0, headerBuffer.Length, ct).ConfigureAwait(false);
                    if (bytesRead <= 0) return;

                    string requestString = Encoding.UTF8.GetString(headerBuffer, 0, bytesRead);
                    string[] lines = requestString.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    if (lines.Length == 0) return;

                    string requestLine = lines[0];
                    string[] reqParts = requestLine.Split(' ');
                    string method = reqParts.Length > 0 ? reqParts[0].ToUpperInvariant() : "GET";
                    string path = reqParts.Length > 1 ? reqParts[1] : "/";

                    // 1. CORS Preflight OPTIONS
                    if (method == "OPTIONS")
                    {
                        await SendCorsResponseAsync(stream).ConfigureAwait(false);
                        return;
                    }

                    // 2. GET /ping, /health, /status — Extension connectivity check
                    if (method == "GET" && (path.StartsWith("/ping", StringComparison.OrdinalIgnoreCase) ||
                                           path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
                                           path.StartsWith("/status", StringComparison.OrdinalIgnoreCase)))
                    {
                        string json = JsonSerializer.Serialize(new { status = "ok", app = "EDM", version = "1.0.0", connected = true });
                        await SendJsonResponseAsync(stream, 200, "OK", json).ConfigureAwait(false);
                        return;
                    }

                    // 3. WebSocket Upgrade
                    if (requestString.Contains("Upgrade: websocket", StringComparison.OrdinalIgnoreCase) ||
                        requestString.Contains("Upgrade: WebSocket", StringComparison.OrdinalIgnoreCase))
                    {
                        await HandleWebSocketHandshakeAndLoopAsync(stream, requestString, ct).ConfigureAwait(false);
                        return;
                    }

                    // 4. POST /variants — Browser extension requests media format list
                    if (method == "POST" && path.StartsWith("/variants", StringComparison.OrdinalIgnoreCase))
                    {
                        string body = await ReadPostBodyAsync(stream, requestString, lines, ct).ConfigureAwait(false);
                        LoggingService.Log($"[EdmWebSocketServer] Received /variants request (Length: {body.Length} bytes)");

                        if (_variantsHandler != null && !string.IsNullOrWhiteSpace(body))
                        {
                            try
                            {
                                using var doc = System.Text.Json.JsonDocument.Parse(body);
                                string url = doc.RootElement.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                                string cookies = doc.RootElement.TryGetProperty("cookies", out var c) ? c.GetString() ?? "" : "";

                                if (!string.IsNullOrWhiteSpace(url))
                                {
                                    string variantsJson = await _variantsHandler(url, cookies).ConfigureAwait(false);
                                    await SendJsonResponseAsync(stream, 200, "OK", variantsJson).ConfigureAwait(false);
                                    return;
                                }
                            }
                            catch (Exception ex)
                            {
                                LoggingService.LogException("[EdmWebSocketServer] /variants handler error", ex);
                            }
                        }

                        // Fallback: return empty variants (extension will use its own YouTube extractor)
                        string emptyJson = JsonSerializer.Serialize(new { success = false, variants = Array.Empty<object>(), error = "Variants resolver unavailable" });
                        await SendJsonResponseAsync(stream, 200, "OK", emptyJson).ConfigureAwait(false);
                        return;
                    }

                    if (method == "POST")
                    {
                        int headerEnd = requestString.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                        string body = string.Empty;

                        if (headerEnd >= 0)
                        {
                            int bodyStartIndex = headerEnd + 4;
                            int headerByteCount = Encoding.UTF8.GetByteCount(requestString.Substring(0, bodyStartIndex));
                            int bodyByteCount = bytesRead - headerByteCount;

                            if (bodyByteCount > 0)
                            {
                                body = Encoding.UTF8.GetString(headerBuffer, headerByteCount, bodyByteCount);
                            }
                        }

                        int contentLength = 0;
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                            {
                                int.TryParse(line.Substring(15).Trim(), out contentLength);
                                break;
                            }
                        }

                        if (contentLength > 0 && Encoding.UTF8.GetByteCount(body) < contentLength)
                        {
                            int remaining = contentLength - Encoding.UTF8.GetByteCount(body);
                            var bodyBuf = new byte[remaining];
                            int read = await stream.ReadAsync(bodyBuf, 0, remaining, ct).ConfigureAwait(false);
                            if (read > 0)
                            {
                                body += Encoding.UTF8.GetString(bodyBuf, 0, read);
                            }
                        }

                        LoggingService.Log($"[EdmWebSocketServer] Received HTTP POST Handoff (Length: {body.Length} bytes)");

                        var payload = JsonSerializer.Deserialize<IpcHandoffPayload>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (payload != null && !string.IsNullOrWhiteSpace(payload.Url))
                        {
                            // Scheme & Protocol Security Verification
                            if (!SecuritySanitizer.IsAllowedUrlScheme(payload.Url))
                            {
                                LoggingService.LogWarning($"[EdmWebSocketServer] Security rejection: Disallowed scheme in POST handoff '{ProtocolDetector.SanitizeUrlForLogging(payload.Url)}'");
                                string secErrJson = JsonSerializer.Serialize(new { success = false, status = "rejected", error = "Security rejection: Disallowed or unsafe URL scheme" });
                                await SendJsonResponseAsync(stream, 400, "Bad Request", secErrJson).ConfigureAwait(false);
                                return;
                            }

                            bool success = await _handoffHandler(payload).ConfigureAwait(false);
                            string respJson = JsonSerializer.Serialize(new 
                            { 
                                success = success, 
                                status = success ? "held_for_confirmation" : "rejected",
                                timestamp = DateTime.UtcNow
                            });
                            await SendJsonResponseAsync(stream, 200, "OK", respJson).ConfigureAwait(false);
                        }
                        else
                        {
                            string errJson = JsonSerializer.Serialize(new { success = false, status = "rejected", error = "Invalid URL payload" });
                            await SendJsonResponseAsync(stream, 400, "Bad Request", errJson).ConfigureAwait(false);
                        }
                        return;
                    }

                    // Default response
                    string defJson = JsonSerializer.Serialize(new { status = "ready", server = "EDM Native Bridge" });
                    await SendJsonResponseAsync(stream, 200, "OK", defJson).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[EdmWebSocketServer] ProcessClient error", ex);
                }
            }
        }

        /// <summary>
        /// Extracts the HTTP POST body from an already-read request string + remaining stream bytes.
        /// Shared by /handoff and /variants handlers.
        /// </summary>
        private static async Task<string> ReadPostBodyAsync(Stream stream, string requestString, string[] lines, CancellationToken ct)
        {
            int headerEnd = requestString.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            string body = string.Empty;

            // Count bytes already read
            byte[] rawBuf = Encoding.UTF8.GetBytes(requestString);

            if (headerEnd >= 0)
            {
                int bodyStartIndex = headerEnd + 4;
                int headerByteCount = Encoding.UTF8.GetByteCount(requestString.Substring(0, bodyStartIndex));
                int bodyByteCount = rawBuf.Length - headerByteCount;
                if (bodyByteCount > 0)
                    body = Encoding.UTF8.GetString(rawBuf, headerByteCount, bodyByteCount);
            }

            // Read remaining bytes if Content-Length says there's more
            int contentLength = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(line.Substring(15).Trim(), out contentLength);
                    break;
                }
            }

            if (contentLength > 0 && Encoding.UTF8.GetByteCount(body) < contentLength)
            {
                int remaining = contentLength - Encoding.UTF8.GetByteCount(body);
                var bodyBuf = new byte[Math.Min(remaining, 1024 * 1024)]; // cap at 1MB
                int read = await stream.ReadAsync(bodyBuf, 0, bodyBuf.Length, ct).ConfigureAwait(false);
                if (read > 0) body += Encoding.UTF8.GetString(bodyBuf, 0, read);
            }

            return body;
        }

        private static async Task SendCorsResponseAsync(Stream stream)
        {
            var sb = new StringBuilder();
            sb.Append("HTTP/1.1 204 No Content\r\n");
            sb.Append("Access-Control-Allow-Origin: *\r\n");
            sb.Append("Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n");
            sb.Append("Access-Control-Allow-Headers: Content-Type, Authorization, X-Requested-With\r\n");
            sb.Append("Access-Control-Max-Age: 86400\r\n");
            sb.Append("Connection: close\r\n\r\n");

            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        private static async Task SendJsonResponseAsync(Stream stream, int statusCode, string statusMsg, string json)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(json);
            var sb = new StringBuilder();
            sb.Append($"HTTP/1.1 {statusCode} {statusMsg}\r\n");
            sb.Append("Access-Control-Allow-Origin: *\r\n");
            sb.Append("Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n");
            sb.Append("Access-Control-Allow-Headers: Content-Type, Authorization, X-Requested-With\r\n");
            sb.Append("Content-Type: application/json; charset=utf-8\r\n");
            sb.Append($"Content-Length: {bodyBytes.Length}\r\n");
            sb.Append("Connection: close\r\n\r\n");

            byte[] headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length).ConfigureAwait(false);
            await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        private async Task HandleWebSocketHandshakeAndLoopAsync(NetworkStream stream, string requestString, CancellationToken ct)
        {
            string secWebSocketKey = string.Empty;
            string[] lines = requestString.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                {
                    secWebSocketKey = line.Substring(18).Trim();
                    break;
                }
            }

            if (string.IsNullOrEmpty(secWebSocketKey)) return;

            string magic = secWebSocketKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            byte[] sha1 = SHA1.HashData(Encoding.UTF8.GetBytes(magic));
            string acceptKey = Convert.ToBase64String(sha1);

            var sb = new StringBuilder();
            sb.Append("HTTP/1.1 101 Switching Protocols\r\n");
            sb.Append("Upgrade: websocket\r\n");
            sb.Append("Connection: Upgrade\r\n");
            sb.Append($"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n");

            byte[] responseBytes = Encoding.UTF8.GetBytes(sb.ToString());
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);

            LoggingService.Log("[EdmWebSocketServer] WebSocket connection established successfully.");

            var buffer = new byte[65536];
            while (!ct.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
                if (read <= 0) break;

                bool isMasked = (buffer[1] & 0x80) != 0;
                int payloadLen = buffer[1] & 0x7F;
                int maskOffset = 2;

                if (payloadLen == 126)
                {
                    payloadLen = (buffer[2] << 8) | buffer[3];
                    maskOffset = 4;
                }
                else if (payloadLen == 127)
                {
                    maskOffset = 10;
                }

                if (isMasked && payloadLen > 0 && read >= maskOffset + 4 + payloadLen)
                {
                    byte[] masks = new byte[4] { buffer[maskOffset], buffer[maskOffset + 1], buffer[maskOffset + 2], buffer[maskOffset + 3] };
                    int payloadOffset = maskOffset + 4;
                    var decoded = new byte[payloadLen];
                    for (int i = 0; i < payloadLen; i++)
                    {
                        decoded[i] = (byte)(buffer[payloadOffset + i] ^ masks[i % 4]);
                    }

                    string text = Encoding.UTF8.GetString(decoded);
                    try
                    {
                        var payload = JsonSerializer.Deserialize<IpcHandoffPayload>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (payload != null && !string.IsNullOrWhiteSpace(payload.Url))
                        {
                            _ = _handoffHandler(payload);
                        }
                    }
                    catch { }
                }
            }
        }
    }
}
