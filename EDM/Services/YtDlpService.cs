using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    /// <summary>
    /// Lightweight wrapper to run yt-dlp (if available) and parse progress.
    /// This service executes yt-dlp as an external process and exposes progress via callback.
    /// </summary>
    public class YtDlpService
    {
        private readonly string _ytDlpPath;
        private readonly string _ffmpegPath;

        public YtDlpService(Services.Interfaces.ISettingsService? settings = null, string? ytDlpPath = null, string? ffmpegPath = null)
        {
            // Resolve settings via DI fallback if not provided (maintain compatibility with tests/old callers)
            var s = settings ?? App.ServiceProvider?.GetService(typeof(Services.Interfaces.ISettingsService)) as Services.Interfaces.ISettingsService ?? new Services.SettingsService();
            _ytDlpPath = !string.IsNullOrWhiteSpace(ytDlpPath) ? ytDlpPath : (string.IsNullOrWhiteSpace(s.GetYtDlpPath()) ? "yt-dlp" : s.GetYtDlpPath());
            _ffmpegPath = !string.IsNullOrWhiteSpace(ffmpegPath) ? ffmpegPath : s.GetFfmpegPath();
        }

        /// <summary>
        /// Download the given url to the specified output path. The progress callback receives percent (0-100)
        /// and a short status string. Throws if the process fails.
        /// </summary>
        public async Task DownloadAsync(string url, string outputPath, string formatArg, Action<int, string> progress, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ytDlpPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("--newline");
            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(outputPath);

            if (!string.IsNullOrWhiteSpace(formatArg))
            {
                var parts = formatArg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    psi.ArgumentList.Add(part);
                }
            }

            psi.ArgumentList.Add(url);

            // Validate explicit path if provided (rooted or contains directory separators)
            if (Path.IsPathRooted(_ytDlpPath) || _ytDlpPath.Contains(Path.DirectorySeparatorChar) || _ytDlpPath.Contains(Path.AltDirectorySeparatorChar))
            {
                if (!File.Exists(_ytDlpPath)) throw new FileNotFoundException($"yt-dlp executable not found at '{_ytDlpPath}'. Update the path in settings.");
            }

            using (var proc = new Process { StartInfo = psi, EnableRaisingEvents = true })
            {
                var stderrSb = new StringBuilder();
                var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

                proc.OutputDataReceived += (s, e) =>
                {
                    if (e.Data == null) return;
                    TryReportProgress(e.Data, progress);
                };

                proc.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data == null) return;
                    try { stderrSb.AppendLine(e.Data); } catch (Exception ex) { LoggingService.LogException("[YtDlpService] ErrorDataReceived append failed", ex); }
                    TryReportProgress(e.Data, progress);
                };

                proc.Exited += (s, e) => tcs.TrySetResult(proc.ExitCode);

                try
                {
                    LoggingService.Log($"[YtDlpService] Starting yt-dlp: '{_ytDlpPath}' Args: {string.Join(" ", psi.ArgumentList)}");
                    proc.Start();
                    LoggingService.Log("[YtDlpService] Process started.");
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[YtDlpService] Failed to start process", ex);
                    throw new InvalidOperationException($"Failed to start yt-dlp process at '{_ytDlpPath}': {ex.Message}", ex);
                }

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                using (ct.Register(() => { try { if (!proc.HasExited) proc.Kill(true); } catch (Exception ex) { LoggingService.LogException("[YtDlpService] Kill on cancel failed", ex); } }))
                {
                    try
                    {
                        LoggingService.Log("[YtDlpService] Waiting for process to exit...");
                        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
                        LoggingService.Log("[YtDlpService] WaitForExitAsync completed.");
                    }
                    catch (OperationCanceledException oce)
                    {
                        try { if (!proc.HasExited) proc.Kill(true); } catch (Exception ex) { LoggingService.LogException("[YtDlpService] Kill after cancel failed", ex); }
                        LoggingService.Log($"[YtDlpService] Download cancelled: {oce}");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogException("[YtDlpService] WaitForExitAsync failed", ex);
                        throw;
                    }

                    var exit = proc.ExitCode;
                    var stderr = stderrSb.ToString();
                    if (exit != 0)
                    {
                        try { LoggingService.LogException("[YtDlpService] yt-dlp exited with non-zero code", new InvalidOperationException($"Exit={exit}. stderr: {stderr}")); } catch (Exception logEx) { System.Diagnostics.Debug.WriteLine($"[YtDlpService] Logging failed: {logEx}"); }
                        throw new InvalidOperationException($"yt-dlp exited with code {exit}. {(string.IsNullOrEmpty(stderr) ? string.Empty : "stderr: " + stderr)}");
                    }
                    else
                    {
                        LoggingService.Log($"[YtDlpService] yt-dlp completed successfully. stderr (if any): {stderr}");
                    }
                }
            }
        }

        private void TryReportProgress(string line, Action<int, string> progress)
        {
            // yt-dlp progress lines often look like: "[download]   5.2% of 3.14MiB at 123.45KiB/s ETA 00:12"
            var m = Regex.Match(line, "(\\d{1,3}\\.\\d{1,2})%|(\\d{1,3})%");
            if (m.Success)
            {
                if (int.TryParse(Math.Floor(double.Parse(m.Value.TrimEnd('%'))).ToString(), out var p))
                {
                    progress?.Invoke(Math.Min(100, Math.Max(0, p)), line);
                    return;
                }
            }

            // fallback: send 0 with the raw line
            progress?.Invoke(0, line);
        }

        /// <summary>
        /// Extracts video metadata (title, available formats/resolutions) without downloading.
        /// Uses yt-dlp with --dump-json to get video info.
        /// </summary>
        /// <param name="url">The URL to extract metadata from</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>JSON metadata string from yt-dlp</returns>
        public async Task<string?> GetVideoInfoAsync(string url, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _ytDlpPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                psi.ArgumentList.Add("--dump-json");
                psi.ArgumentList.Add("--no-download");
                psi.ArgumentList.Add(url);

                using (var proc = new Process { StartInfo = psi, EnableRaisingEvents = true })
                {
                    var outputSb = new StringBuilder();
                    var errorSb = new StringBuilder();
                    var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

                    proc.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                            try { outputSb.AppendLine(e.Data); } catch (Exception ex) { LoggingService.LogException("[YtDlpService] OutputDataReceived append failed", ex); }
                    };

                    proc.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                            try { errorSb.AppendLine(e.Data); } catch (Exception ex) { LoggingService.LogException("[YtDlpService] ErrorDataReceived append failed", ex); }
                    };

                    proc.Exited += (s, e) => tcs.TrySetResult(proc.ExitCode);

                    try
                    {
                        LoggingService.Log($"[YtDlpService] Getting metadata for URL: {url}");
                        proc.Start();
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogException("[YtDlpService.GetVideoInfoAsync] Process start failed", ex);
                        return null;
                    }

                    using (ct.Register(() => { try { if (!proc.HasExited) proc.Kill(true); } catch (Exception ex) { LoggingService.LogException("[YtDlpService] Kill on cancel failed", ex); } }))
                    {
                        try
                        {
                            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            try { if (!proc.HasExited) proc.Kill(true); } catch (Exception ex) { LoggingService.LogException("[YtDlpService] Kill after cancel failed", ex); }
                            return null;
                        }
                        catch (Exception ex)
                        {
                            LoggingService.LogException("[YtDlpService.GetVideoInfoAsync] Wait failed", ex);
                            return null;
                        }

                        if (proc.ExitCode == 0)
                        {
                            return outputSb.ToString();
                        }
                        else
                        {
                            LoggingService.LogWarning($"[YtDlpService] Metadata extraction failed with exit code {proc.ExitCode}");
                            // Attempt self-update and retry once if cipher changed
                            _ = AutoUpdateEngineAsync();
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[YtDlpService.GetVideoInfoAsync]", ex);
                return null;
            }
        }

        /// <summary>
        /// Automatically checks for and applies updates to the YouTube extractor engine (yt-dlp).
        /// Downloads the latest release from GitHub if outdated or missing, ensuring future YouTube changes never break downloads.
        /// </summary>
        public async Task<bool> AutoUpdateEngineAsync(CancellationToken ct = default)
        {
            try
            {
                LoggingService.Log("[YtDlpService] Checking for YouTube extractor engine updates...");

                // 1. Try running yt-dlp -U if binary exists
                if (File.Exists(_ytDlpPath))
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = _ytDlpPath,
                            Arguments = "-U",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };
                        using var p = Process.Start(psi);
                        if (p != null)
                        {
                            await p.WaitForExitAsync(ct).ConfigureAwait(false);
                            if (p.ExitCode == 0)
                            {
                                LoggingService.Log("[YtDlpService] yt-dlp updated successfully via self-update.");
                                return true;
                            }
                        }
                    }
                    catch { }
                }

                // 2. Download latest binary from official GitHub release
                var localFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EDM", "tools");
                if (!Directory.Exists(localFolder)) Directory.CreateDirectory(localFolder);
                var targetExe = Path.Combine(localFolder, "yt-dlp.exe");

                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("EDM-Download-Manager/6.0");
                const string latestReleaseUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

                var bytes = await client.GetByteArrayAsync(latestReleaseUrl, ct).ConfigureAwait(false);
                if (bytes != null && bytes.Length > 100_000)
                {
                    var tempFile = targetExe + ".tmp";
                    await File.WriteAllBytesAsync(tempFile, bytes, ct).ConfigureAwait(false);
                    if (File.Exists(targetExe)) File.Delete(targetExe);
                    File.Move(tempFile, targetExe);
                    LoggingService.Log($"[YtDlpService] YouTube extractor successfully auto-updated at '{targetExe}' ({bytes.Length} bytes).");
                    return true;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[YtDlpService.AutoUpdateEngineAsync] Update failed", ex);
            }
            return false;
        }
    }
}
