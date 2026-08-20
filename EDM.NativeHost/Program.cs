using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.NativeMessaging;
using EDM.Services;

namespace EDM.NativeHost
{
    /// <summary>
    /// Dedicated, ultra-reliable Native Messaging Host process for Exclusive Download Manager (EDM).
    /// Conforms strictly to Chromium and Mozilla Native Messaging Stdio framing specifications.
    /// Intercepts downloads and media variant inquiries from Chrome, Edge, Firefox, Brave, Opera, and Vivaldi,
    /// communicating directly with the running EDM GUI or launching it on-demand.
    /// </summary>
    public static class Program
    {
        private static readonly Stream Stdin = Console.OpenStandardInput();
        private static readonly Stream Stdout = Console.OpenStandardOutput();
        private static readonly CancellationTokenSource Cts = new();

        public static async Task<int> Main(string[] args)
        {
            // Set process title and avoid console window prompts
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            LoggingService.Log("[EDM.NativeHost] Process started via browser Native Messaging stdio.");

            try
            {
                var lenBuf = new byte[4];

                while (!Cts.IsCancellationRequested)
                {
                    int read = await ReadExactAsync(Stdin, lenBuf, 0, 4, Cts.Token).ConfigureAwait(false);
                    if (read < 4)
                    {
                        // Browser closed stdin / disconnected port -> clean exit
                        LoggingService.Log("[EDM.NativeHost] Stdin EOF detected. Browser disconnected.");
                        break;
                    }

                    // Gracefully handle any leading UTF-8 BOM (0xEF, 0xBB, 0xBF)
                    if (lenBuf[0] == 0xEF && lenBuf[1] == 0xBB && lenBuf[2] == 0xBF)
                    {
                        lenBuf[0] = lenBuf[3];
                        int extraRead = await ReadExactAsync(Stdin, lenBuf, 1, 3, Cts.Token).ConfigureAwait(false);
                        if (extraRead < 3) break;
                    }

                    int messageLength = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
                    if (messageLength <= 0 || messageLength > 10 * 1024 * 1024)
                    {
                        LoggingService.Log($"[EDM.NativeHost] Invalid message length received ({messageLength} bytes). Exiting.");
                        break;
                    }

                    var payloadBytes = new byte[messageLength];
                    int payloadRead = await ReadExactAsync(Stdin, payloadBytes, 0, messageLength, Cts.Token).ConfigureAwait(false);
                    if (payloadRead < messageLength)
                    {
                        LoggingService.Log("[EDM.NativeHost] Incomplete payload read. Exiting.");
                        break;
                    }

                    string rawJson = Encoding.UTF8.GetString(payloadBytes);
                    LoggingService.Log($"[EDM.NativeHost] Received native message: {NativeMessageListener.ScrubPayloadForLogs(rawJson)}");

                    var req = JsonSerializer.Deserialize<NativeMessageRequest>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new NativeMessageRequest();

                    string action = req.GetEffectiveAction();

                    if (string.Equals(action, "ping", StringComparison.OrdinalIgnoreCase))
                    {
                        await SendResponseAsync(new NativeMessageResponse
                        {
                            Success = true,
                            Action = "pong",
                            RequestId = req.RequestId,
                            Version = "2.0.0",
                            Status = "ready",
                            Timestamp = DateTime.UtcNow
                        }).ConfigureAwait(false);
                        continue;
                    }

                    if (string.Equals(action, "GET_MEDIA_VARIANTS", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(action, "resolve_media_variants", StringComparison.OrdinalIgnoreCase))
                    {
                        string mediaUrl = !string.IsNullOrWhiteSpace(req.Url) ? req.Url : (req.PageUrl ?? string.Empty);
                        var resolver = new MediaVariantResolver();
                        var variants = await resolver.ResolveVariantsAsync(mediaUrl, req.Cookies, Cts.Token).ConfigureAwait(false);

                        await SendResponseAsync(new NativeMessageResponse
                        {
                            Success = variants.Success,
                            Action = "media_variants_resolved",
                            RequestId = req.RequestId,
                            Result = variants,
                            Data = variants.Variants,
                            Variants = variants.Variants,
                            Error = variants.ErrorMessage
                        }).ConfigureAwait(false);
                        continue;
                    }

                    if (string.Equals(action, "download_url", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(action, "START_DOWNLOAD", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(action, "START_EDM_DOWNLOAD", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(action, "DOWNLOAD_REQUEST", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(action, "intercept", StringComparison.OrdinalIgnoreCase))
                    {
                        string downloadUrl = req.Url ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(downloadUrl))
                        {
                            await SendResponseAsync(new NativeMessageResponse
                            {
                                Success = false,
                                Action = action,
                                RequestId = req.RequestId,
                                Error = "Missing download URL."
                            }).ConfigureAwait(false);
                            continue;
                        }

                        // Strict URL validation to reject dangerous schemes & malformed URIs
                        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var parsedDlUri) ||
                            (parsedDlUri.Scheme != "http" && parsedDlUri.Scheme != "https" && parsedDlUri.Scheme != "ftp" && parsedDlUri.Scheme != "ftps"))
                        {
                            await SendResponseAsync(new NativeMessageResponse
                            {
                                Success = false,
                                Action = action,
                                RequestId = req.RequestId,
                                Error = "Invalid or unsupported download URL scheme."
                            }).ConfigureAwait(false);
                            continue;
                        }

                        // Size guard rails to prevent buffer overflow & header exhaustion
                        if (downloadUrl.Length > 8192 || (req.Cookies != null && req.Cookies.Length > 32768))
                        {
                            await SendResponseAsync(new NativeMessageResponse
                            {
                                Success = false,
                                Action = action,
                                RequestId = req.RequestId,
                                Error = "Payload exceeds maximum safe size limits."
                            }).ConfigureAwait(false);
                            continue;
                        }

                        // Sanitize filename
                        req.Filename = SecuritySanitizer.SanitizeFileName(req.GetEffectiveFileName());

                        // Send handoff to primary EDM application
                        bool handoffSuccess = await ForwardToEdmAppAsync(req).ConfigureAwait(false);

                        await SendResponseAsync(new NativeMessageResponse
                        {
                            Success = handoffSuccess,
                            Action = action,
                            RequestId = req.RequestId,
                            Status = handoffSuccess ? "handed_off" : "failed",
                            Error = handoffSuccess ? null : "Failed to hand off download to EDM application."
                        }).ConfigureAwait(false);
                        continue;
                    }

                    // Default response for unhandled actions
                    await SendResponseAsync(new NativeMessageResponse
                    {
                        Success = true,
                        Action = action,
                        RequestId = req.RequestId,
                        Status = "acknowledged"
                    }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[EDM.NativeHost] Unhandled exception in message loop", ex);
            }

            LoggingService.Log("[EDM.NativeHost] Process exiting cleanly.");
            return 0;
        }

        private static async Task<bool> ForwardToEdmAppAsync(NativeMessageRequest req)
        {
            var payload = new IpcHandoffPayload
            {
                Url = req.Url ?? string.Empty,
                Filename = req.GetEffectiveFileName(),
                Cookies = req.Cookies,
                PageUrl = req.PageUrl,
                Referer = !string.IsNullOrWhiteSpace(req.Referer) ? req.Referer : req.PageUrl,
                UserAgent = req.UserAgent,
                AuthHeader = req.AuthHeader,
                PostData = req.PostData,
                TabId = req.TabId,
                FrameId = req.FrameId,
                Quality = req.Quality,
                Format = req.Format,
                Browser = req.Browser,
                CorrelationId = req.CorrelationId,
                DownloadIdentity = req.DownloadIdentity,
                Source = req.Browser ?? "BrowserExtension",
                AudioUrl = req.AudioUrl,
                VideoUrl = req.VideoUrl,
                FormatArg = req.FormatArg,
                RequiresFfmpegMerge = req.RequiresFfmpegMerge ?? false,
                Title = req.Title,
                ManifestUrl = req.ManifestUrl,
                AudioCodec = req.AudioCodec,
                Codec = req.Codec,
                Container = req.Container,
                EstimatedSizeBytes = req.EstimatedSizeBytes,
                IsAudioOnly = req.IsAudioOnly
            };

            string jsonPayload = JsonSerializer.Serialize(payload);

            // 1. Attempt to connect to running EDM Named Pipe server
            try
            {
                using var pipeClient = new NamedPipeClientStream(".", NativeIpcServer.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipeClient.ConnectAsync(1000).ConfigureAwait(false);

                using var writer = new StreamWriter(pipeClient, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                using var reader = new StreamReader(pipeClient, Encoding.UTF8, leaveOpen: true);

                await writer.WriteLineAsync(jsonPayload).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
                string? responseLine = await reader.ReadLineAsync().ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(responseLine))
                {
                    LoggingService.Log($"[EDM.NativeHost] Received IPC response from EDM GUI: {responseLine}");
                    return true;
                }
            }
            catch (Exception)
            {
                LoggingService.Log("[EDM.NativeHost] Running EDM GUI instance not found on named pipe. Launching EDM.exe...");
            }

            // 2. Fallback: Launch EDM.exe with --handoff argument
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidatePaths = new[]
                {
                    Path.Combine(baseDir, "EDM.exe"),
                    Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\EDM\bin\Debug\net10.0-windows\EDM.exe")),
                    Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\EDM\bin\Release\net10.0-windows\EDM.exe")),
                    Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\EDM\bin\Debug\net10.0-windows\EDM.exe")),
                    Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\EDM\bin\Release\net10.0-windows\EDM.exe")),
                    Path.GetFullPath(Path.Combine(baseDir, @"..\EDM.exe")),
                    Path.GetFullPath(Path.Combine(baseDir, @"..\EDM\EDM.exe")),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\EDM\EDM.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Exclusive Download Manager\EDM.exe")
                };

                string? edmExe = candidatePaths.FirstOrDefault(File.Exists);

                if (!string.IsNullOrEmpty(edmExe) && File.Exists(edmExe))
                {
                    string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonPayload));
                    var psi = new ProcessStartInfo
                    {
                        FileName = edmExe,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    psi.ArgumentList.Add("--handoff");
                    psi.ArgumentList.Add(b64);
                    Process.Start(psi);
                    LoggingService.Log($"[EDM.NativeHost] Successfully launched EDM.exe with handoff payload from '{edmExe}'.");
                    return true;
                }
                else
                {
                    LoggingService.Log($"[EDM.NativeHost] EDM.exe not found in candidate paths: {string.Join(", ", candidatePaths)}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[EDM.NativeHost] Failed to launch EDM.exe fallback", ex);
            }

            return false;
        }

        private static async Task SendResponseAsync(NativeMessageResponse response)
        {
            try
            {
                byte[] utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(response, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                byte[] lenBuf = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(lenBuf, utf8Bytes.Length);

                lock (Stdout)
                {
                    Stdout.Write(lenBuf, 0, 4);
                    Stdout.Write(utf8Bytes, 0, utf8Bytes.Length);
                    Stdout.Flush();
                }

                LoggingService.Log($"[EDM.NativeHost] Sent response ({utf8Bytes.Length} bytes): Action={response.Action}, Success={response.Success}");
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[EDM.NativeHost] Failed to write response to stdout", ex);
            }

            await Task.CompletedTask;
        }

        private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken ct)
        {
            int total = 0;
            while (total < count)
            {
                int r = await stream.ReadAsync(buffer.AsMemory(offset + total, count - total), ct).ConfigureAwait(false);
                if (r == 0) return total;
                total += r;
            }
            return total;
        }
    }
}
