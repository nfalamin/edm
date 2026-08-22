using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EDM.Services;

namespace EDM.NativeMessaging
{
    /// <summary>
    /// Production-hardened Native Messaging host listener.
    /// Manages stdio length-prefixed JSON communication with browser extensions (Chrome, Edge, Firefox).
    /// Features robust IOException retry recovery with exponential backoff, safe ObjectDisposedException
    /// shutdown handling, payload credential redaction, and deduplication.
    /// </summary>
    public sealed class NativeMessageListener : IAsyncDisposable
    {
        private readonly Stream _stdin;
        private readonly Stream _stdout;
        private readonly CancellationTokenSource _cts = new();
        private readonly Channel<JsonElement> _messageChannel;
        private Task? _readerTask;
        private Task? _processorTask;
        private int _isRecovering;
        private int _consecutiveErrors;

        public const int MaxTransientRetries = 3;

        // Diagnostic mode toggle for browser integration debugging
        public static bool DiagnosticModeEnabled { get; set; } = false;

        // Async message handler events
        public event Func<JsonElement, Task<object?>>? MessageReceivedWithResult;
        public event Func<JsonElement, Task>? MessageReceived;

        public bool IsRunning => _readerTask != null && !_cts.IsCancellationRequested;

        public NativeMessageListener(Stream? stdin = null, Stream? stdout = null)
        {
            _stdin = stdin ?? Console.OpenStandardInput();
            _stdout = stdout ?? Console.OpenStandardOutput();
            _messageChannel = Channel.CreateUnbounded<JsonElement>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });
            _isRecovering = 0;
            _consecutiveErrors = 0;
        }

        public void Start()
        {
            if (_readerTask != null) return;
            _readerTask = Task.Run(() => ReadLoopAsync(_cts.Token));
            _processorTask = Task.Run(() => ProcessLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            try { _cts.Cancel(); } catch { }
            _messageChannel.Writer.TryComplete();
        }

        public async ValueTask DisposeAsync()
        {
            Stop();

            if (_readerTask != null)
            {
                try
                {
                    var delay = Task.Delay(TimeSpan.FromSeconds(2));
                    var completed = await Task.WhenAny(_readerTask, delay).ConfigureAwait(false);
                    if (completed == _readerTask)
                    {
                        try { await _readerTask.ConfigureAwait(false); } catch { }
                    }
                }
                catch { }
            }

            if (_processorTask != null)
            {
                try
                {
                    var delay = Task.Delay(TimeSpan.FromSeconds(2));
                    var completed = await Task.WhenAny(_processorTask, delay).ConfigureAwait(false);
                    if (completed == _processorTask)
                    {
                        try { await _processorTask.ConfigureAwait(false); } catch { }
                    }
                }
                catch { }
            }

            try { _stdin.Dispose(); } catch { }
            try { _stdout.Dispose(); } catch { }
            try { _cts.Dispose(); } catch { }
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            var lenBuf = new byte[4];

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    int read = await ReadExactAsync(_stdin, lenBuf, 0, 4, ct).ConfigureAwait(false);
                    if (read < 4)
                    {
                        // Native messaging host host stream closed or EOF detected
                        LoggingService.Log("[NativeMessageListener] Stdin EOF or host disconnected. Stopping read loop cleanly.");
                        break;
                    }

                    int messageLength = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
                    if (messageLength <= 0 || messageLength > 10 * 1024 * 1024)
                    {
                        LoggingService.Log($"[NativeMessageListener] Invalid message length received ({messageLength} bytes). Ignoring.");
                        continue;
                    }

                    var payload = new byte[messageLength];
                    int payloadRead = await ReadExactAsync(_stdin, payload, 0, messageLength, ct).ConfigureAwait(false);
                    if (payloadRead < messageLength)
                    {
                        LoggingService.Log("[NativeMessageListener] Partial payload read failure. Host disconnected.");
                        break;
                    }

                    // Reset consecutive error count on successful read
                    Interlocked.Exchange(ref _consecutiveErrors, 0);

                    var doc = JsonDocument.Parse(payload);

                    if (IsDuplicateMessage(doc.RootElement))
                    {
                        LoggingService.Log("[NativeMessageListener] Suppressed duplicate download event within deduplication window.");
                        await WriteResponseAsync(new { success = true, result = "duplicate_ignored" }, ct).ConfigureAwait(false);
                        continue;
                    }

                    // Enqueue into channel for non-blocking processing
                    _messageChannel.Writer.TryWrite(doc.RootElement.Clone());
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // Clean application shutdown / stream disposal
                    LoggingService.Log("[NativeMessageListener] Stream disposed during shutdown.");
                    break;
                }
                catch (IOException ioEx)
                {
                    if (ct.IsCancellationRequested) break;

                    int errors = Interlocked.Increment(ref _consecutiveErrors);
                    LoggingService.Log($"[NativeMessageListener] Transient IOException in read loop (Attempt {errors}/{MaxTransientRetries}): {ioEx.Message}");

                    if (errors > MaxTransientRetries)
                    {
                        LoggingService.Log("[NativeMessageListener] Bounded retry limit reached for IOException. Exiting read loop to prevent CPU spin.");
                        break;
                    }

                    // Controlled exponential backoff without CPU spinning or single recovery race
                    if (Interlocked.CompareExchange(ref _isRecovering, 1, 0) == 0)
                    {
                        try
                        {
                            int backoffMs = 100 * (1 << (errors - 1)); // 100ms, 200ms, 400ms
                            await Task.Delay(backoffMs, ct).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) { break; }
                        finally
                        {
                            Interlocked.Exchange(ref _isRecovering, 0);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ct.IsCancellationRequested) break;
                    LoggingService.LogException("[NativeMessageListener] Unexpected error in read loop", ex);
                    break;
                }
            }

            _messageChannel.Writer.TryComplete();
        }

        private async Task ProcessLoopAsync(CancellationToken ct)
        {
            try
            {
                while (await _messageChannel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    while (_messageChannel.Reader.TryRead(out var element))
                    {
                        await ProcessSingleMessageAsync(element, ct).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LoggingService.LogException("[NativeMessageListener] ProcessLoop error", ex);
            }
        }

        private async Task ProcessSingleMessageAsync(JsonElement element, CancellationToken ct)
        {
            string action = GetActionType(element);
            LoggingService.Log($"[NativeMessageListener] Processing action: {action}");

            if (string.Equals(action, "ping", StringComparison.OrdinalIgnoreCase))
            {
                await WriteResponseAsync(new
                {
                    success = true,
                    action = "pong",
                    version = "1.0",
                    timestamp = DateTime.UtcNow
                }, ct).ConfigureAwait(false);
                return;
            }

            if (string.Equals(action, "resolve_media_variants", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(action, "GET_MEDIA_VARIANTS", StringComparison.OrdinalIgnoreCase))
            {
                string url = element.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                string cookies = element.TryGetProperty("cookies", out var c) ? c.GetString() ?? "" : "";
                var resolver = new MediaVariantResolver();
                var variantsResult = await resolver.ResolveVariantsAsync(url, cookies, ct).ConfigureAwait(false);
                await WriteResponseAsync(new
                {
                    success = true,
                    action = "media_variants_resolved",
                    result = variantsResult
                }, ct).ConfigureAwait(false);
                return;
            }

            try
            {
                object? resultData = null;

                if (MessageReceivedWithResult != null)
                {
                    resultData = await MessageReceivedWithResult.Invoke(element).ConfigureAwait(false);
                }

                if (MessageReceived != null)
                {
                    var invocationList = MessageReceived.GetInvocationList();
                    foreach (Func<JsonElement, Task> d in invocationList)
                    {
                        await d.Invoke(element).ConfigureAwait(false);
                    }
                }

                await WriteResponseAsync(new
                {
                    success = true,
                    action,
                    data = resultData ?? "ok"
                }, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[NativeMessageListener] Error executing action '{action}'", ex);
                await WriteResponseAsync(new
                {
                    success = false,
                    action,
                    error = ex.Message
                }, ct).ConfigureAwait(false);
            }
        }

        public async Task WriteResponseAsync(object responseObj, CancellationToken ct = default)
        {
            try
            {
                byte[] utf8Json = JsonSerializer.SerializeToUtf8Bytes(responseObj, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                byte[] lenBuf = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(lenBuf, utf8Json.Length);

                lock (_stdout)
                {
                    _stdout.Write(lenBuf, 0, 4);
                    _stdout.Write(utf8Json, 0, utf8Json.Length);
                    _stdout.Flush();
                }
            }
            catch (ObjectDisposedException) { }
            catch (IOException ioEx)
            {
                LoggingService.Log($"[NativeMessageListener] Failed to write response (IOException): {ioEx.Message}");
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[NativeMessageListener] WriteResponseAsync error", ex);
            }

            await Task.CompletedTask;
        }

        private static string GetActionType(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("action", out var a) && a.ValueKind == JsonValueKind.String)
                    return a.GetString() ?? "unknown";
                if (element.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String)
                    return t.GetString() ?? "unknown";
                if (element.TryGetProperty("url", out _))
                    return "add_download";
            }
            return "unknown";
        }

        private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken ct)
        {
            int readTotal = 0;
            while (readTotal < count)
            {
                int r = await stream.ReadAsync(buffer.AsMemory(offset + readTotal, count - readTotal), ct).ConfigureAwait(false);
                if (r == 0) return readTotal;
                readTotal += r;
            }
            return readTotal;
        }

        /// <summary>
        /// Scrubs sensitive authentication tokens, cookies, and passwords from payloads before logging.
        /// </summary>
        public static string ScrubPayloadForLogs(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(
                jsonString,
                @"""(cookies?|authorization|token|password|auth|secret)""\s*:\s*""[^""]*""",
                @"""$1"": ""[REDACTED]""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private static readonly ConcurrentDictionary<string, DateTime> _recentMessageHashes = new();

        public static void ResetDeduplicationCacheForTesting()
        {
            _recentMessageHashes.Clear();
        }

        public static bool IsDuplicateMessage(JsonElement element)
        {
            try
            {
                if (element.ValueKind != JsonValueKind.Object) return false;

                string url = element.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                string correlationId = element.TryGetProperty("correlationId", out var c) ? c.GetString() ?? "" : "";
                string browserDownloadId = element.TryGetProperty("browserDownloadId", out var b) ? b.GetString() ?? "" : "";

                if (string.IsNullOrEmpty(url)) return false;

                // Composite deduplication identity key
                string dedupKey = !string.IsNullOrEmpty(correlationId)
                    ? correlationId
                    : (!string.IsNullOrEmpty(browserDownloadId) ? $"bid_{browserDownloadId}_{url}" : url);

                DateTime now = DateTime.UtcNow;

                foreach (var kvp in _recentMessageHashes)
                {
                    if ((now - kvp.Value).TotalSeconds > 3)
                    {
                        _recentMessageHashes.TryRemove(kvp.Key, out _);
                    }
                }

                if (_recentMessageHashes.TryGetValue(dedupKey, out var lastSeen) && (now - lastSeen).TotalSeconds <= 2)
                {
                    return true;
                }

                _recentMessageHashes[dedupKey] = now;
            }
            catch { }
            return false;
        }
    }
}