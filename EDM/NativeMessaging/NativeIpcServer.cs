using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;

namespace EDM.NativeMessaging
{
    /// <summary>
    /// Named Pipe IPC Server in the primary EDM GUI application.
    /// Receives download handoff requests from EDM.NativeHost (or external browser CLI triggers)
    /// and safely dispatches them to the download engine and UI.
    /// </summary>
    public sealed class NativeIpcServer : IAsyncDisposable
    {
        public const string PipeName = "EDM_NativeMessaging_Pipe";
        public string TargetPipeName { get; }
        private readonly CancellationTokenSource _cts = new();
        private Task? _listenerTask;
        private readonly Func<IpcHandoffPayload, Task<bool>> _handoffHandler;

        public NativeIpcServer(Func<IpcHandoffPayload, Task<bool>> handoffHandler, string? pipeName = null)
        {
            _handoffHandler = handoffHandler ?? throw new ArgumentNullException(nameof(handoffHandler));
            TargetPipeName = !string.IsNullOrWhiteSpace(pipeName) ? pipeName : PipeName;
        }

        private NamedPipeServerStream? _currentServerStream;

        public void Start()
        {
            if (_listenerTask != null) return;
            _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            LoggingService.Log($"[NativeIpcServer] IPC server listening on pipe '{TargetPipeName}'");
        }

        public void Stop()
        {
            try { _cts.Cancel(); } catch { }
            try { _currentServerStream?.Dispose(); } catch { }
        }

        public async ValueTask DisposeAsync()
        {
            Stop();
            if (_listenerTask != null)
            {
                try
                {
                    await Task.WhenAny(_listenerTask, Task.Delay(500)).ConfigureAwait(false);
                }
                catch { }
            }
        }

        private async Task ListenLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                NamedPipeServerStream? serverStream = null;
                try
                {
                    serverStream = new NamedPipeServerStream(
                        TargetPipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);
                    _currentServerStream = serverStream;

                    await serverStream.WaitForConnectionAsync(ct).ConfigureAwait(false);
                    _currentServerStream = null;

                    // Process incoming handoff connection in background task so server immediately listens for next
                    _ = ProcessConnectionAsync(serverStream, ct);
                }
                catch (OperationCanceledException)
                {
                    serverStream?.Dispose();
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    serverStream?.Dispose();
                    if (ct.IsCancellationRequested) break;
                    LoggingService.LogException("[NativeIpcServer] Error in pipe listener loop", ex);
                    try { await Task.Delay(200, ct).ConfigureAwait(false); } catch { break; }
                }
            }
        }

        internal async Task ProcessConnectionAsync(Stream stream, CancellationToken ct = default)
        {
            using (stream)
            {
                try
                {
                    using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                    using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                    string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        await writer.WriteLineAsync(JsonSerializer.Serialize(new { success = false, error = "Empty request" })).ConfigureAwait(false);
                        return;
                    }

                    LoggingService.Log($"[NativeIpcServer] Received IPC handoff message (length {line.Length})");
                    var payload = JsonSerializer.Deserialize<IpcHandoffPayload>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (payload == null || string.IsNullOrWhiteSpace(payload.Url))
                    {
                        await writer.WriteLineAsync(JsonSerializer.Serialize(new { success = false, error = "Invalid URL payload" })).ConfigureAwait(false);
                        return;
                    }

                    bool accepted = await _handoffHandler.Invoke(payload).ConfigureAwait(false);

                    var responseJson = JsonSerializer.Serialize(new
                    {
                        success = accepted,
                        status = accepted ? "accepted" : "rejected",
                        timestamp = DateTime.UtcNow
                    });

                    await writer.WriteLineAsync(responseJson).ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[NativeIpcServer] Failed to process connection", ex);
                }
            }
        }
    }
}
