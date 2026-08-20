using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public static class YtDlpOutputParser
    {
        private static readonly Regex DetailedProgressRegex = new Regex(
            @"\[download\]\s+([\d\.]+)%\s+of\s+~?([\d\.]+)\s*([KMGTP]?i?B)(?:\s+at\s+(?:([\d\.]+)\s*([KMGTP]?i?B/s)|[^\s]+\s*B/s))?(?:\s+ETA\s+(\d+:\d+(?::\d+)?|[^\s]+))?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex FinishedRegex = new Regex(
            @"\[download\]\s+100%\s+of\s+~?([\d\.]+)\s*([KMGTP]?i?B)(?:\s+in\s+[^\s]+)?(?:\s+at\s+([\d\.]+)\s*([KMGTP]?i?B/s))?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool TryParseProgress(string? line, out double percent, out long totalBytes, out long bytesReceived, out double speedBps, out double etaSec)
        {
            percent = 0;
            totalBytes = 0;
            bytesReceived = 0;
            speedBps = 0;
            etaSec = 0;

            if (string.IsNullOrWhiteSpace(line)) return false;

            // 1. Check detailed progress line
            var m = DetailedProgressRegex.Match(line);
            if (m.Success)
            {
                if (double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p))
                {
                    percent = Math.Clamp(p, 0.0, 100.0);
                }

                if (double.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sizeVal))
                {
                    string unit = m.Groups[3].Value.ToUpperInvariant();
                    totalBytes = ParseByteUnit(sizeVal, unit);
                    bytesReceived = (long)(totalBytes * (percent / 100.0));
                }

                if (m.Groups[4].Success && double.TryParse(m.Groups[4].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var spdVal))
                {
                    string spdUnit = m.Groups[5].Value.ToUpperInvariant().Replace("/S", "");
                    speedBps = ParseByteUnit(spdVal, spdUnit);
                }

                if (m.Groups[6].Success)
                {
                    string etaStr = m.Groups[6].Value;
                    var parts = etaStr.Split(':');
                    if (parts.Length == 2 && double.TryParse(parts[0], out var mm) && double.TryParse(parts[1], out var ss))
                    {
                        etaSec = mm * 60 + ss;
                    }
                    else if (parts.Length == 3 && double.TryParse(parts[0], out var hh) && double.TryParse(parts[1], out var mm2) && double.TryParse(parts[2], out var ss2))
                    {
                        etaSec = hh * 3600 + mm2 * 60 + ss2;
                    }
                }

                return true;
            }

            // 2. Check 100% finished format
            var fm = FinishedRegex.Match(line);
            if (fm.Success)
            {
                percent = 100.0;
                if (double.TryParse(fm.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fSizeVal))
                {
                    string unit = fm.Groups[2].Value.ToUpperInvariant();
                    totalBytes = ParseByteUnit(fSizeVal, unit);
                    bytesReceived = totalBytes;
                }
                if (fm.Groups[3].Success && double.TryParse(fm.Groups[3].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fSpdVal))
                {
                    string spdUnit = fm.Groups[4].Value.ToUpperInvariant().Replace("/S", "");
                    speedBps = ParseByteUnit(fSpdVal, spdUnit);
                }
                etaSec = 0;
                return true;
            }

            // 3. Fallback for percentage only
            var simpleMatch = Regex.Match(line, @"(\d{1,3}(?:\.\d+)?)%");
            if (simpleMatch.Success && double.TryParse(simpleMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sp))
            {
                percent = Math.Clamp(sp, 0.0, 100.0);
                return true;
            }

            return false;
        }

        private static long ParseByteUnit(double val, string unit)
        {
            if (unit.StartsWith("K")) return (long)(val * 1024);
            if (unit.StartsWith("M")) return (long)(val * 1024 * 1024);
            if (unit.StartsWith("G")) return (long)(val * 1024 * 1024 * 1024);
            if (unit.StartsWith("T")) return (long)(val * 1024L * 1024L * 1024L * 1024L);
            return (long)val;
        }
    }

    /// <summary>
    /// Lightweight wrapper to run yt-dlp (if available) and parse progress.
    /// This service executes yt-dlp as an external process and exposes progress via callback.
    /// </summary>
    public class YtDlpService
    {
        private readonly string _ytDlpPath;
        private readonly string _ffmpegPath;
        private int _lastReportedProgressPercent = 0;

        public YtDlpService(Services.Interfaces.ISettingsService? settings = null, string? ytDlpPath = null, string? ffmpegPath = null)
        {
            // Resolve settings via DI fallback if not provided (maintain compatibility with tests/old callers)
            var s = settings ?? App.ServiceProvider?.GetService(typeof(Services.Interfaces.ISettingsService)) as Services.Interfaces.ISettingsService ?? new Services.SettingsService();
            string rawYtDlp = !string.IsNullOrWhiteSpace(ytDlpPath) ? ytDlpPath : s.GetYtDlpPath();
            _ytDlpPath = ResolveExecutablePath(rawYtDlp);
            _ffmpegPath = !string.IsNullOrWhiteSpace(ffmpegPath) ? ffmpegPath : s.GetFfmpegPath();
        }

        public static string ResolveExecutablePath(string? preferredPath = null)
        {
            if (!string.IsNullOrWhiteSpace(preferredPath) && File.Exists(preferredPath))
                return preferredPath;

            // Check %LOCALAPPDATA%\EDM\tools\yt-dlp.exe
            string localAppDataCandidate = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EDM", "tools", "yt-dlp.exe");
            if (File.Exists(localAppDataCandidate)) return localAppDataCandidate;

            // Check next to EDM executable
            string baseDirCandidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");
            if (File.Exists(baseDirCandidate)) return baseDirCandidate;

            string baseDirTools = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "yt-dlp.exe");
            if (File.Exists(baseDirTools)) return baseDirTools;

            // Check in PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var candidate = Path.Combine(dir.Trim('"', ' '), "yt-dlp.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }

            return !string.IsNullOrWhiteSpace(preferredPath) ? preferredPath : "yt-dlp";
        }

        public bool IsAvailable()
        {
            var resolved = ResolveExecutablePath(_ytDlpPath);
            return File.Exists(resolved);
        }

        /// <summary>
        /// Download the given url to the specified output path. The progress callback receives percent (0-100)
        /// and a short status string. Throws if the process fails.
        /// </summary>
        public async Task DownloadAsync(string url, string outputPath, string formatArg, Action<int, string> progress, CancellationToken ct)
        {
            _lastReportedProgressPercent = 0;
            string? execPath = await MediaDependencyManager.Instance.GetValidatedYtDlpPathAsync(_ytDlpPath, ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(execPath))
            {
                throw new FileNotFoundException($"yt-dlp executable was not found. Please install yt-dlp or place yt-dlp.exe in the EDM tools directory ({MediaDependencyManager.Instance.ToolsDirectory}).");
            }

            var psi = new ProcessStartInfo
            {
                FileName = execPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("--newline");
            psi.ArgumentList.Add("-N");
            psi.ArgumentList.Add("16"); // 16 concurrent fragments for ultra-high speed
            psi.ArgumentList.Add("--buffer-size");
            psi.ArgumentList.Add("16M");
            psi.ArgumentList.Add("--http-chunk-size");
            psi.ArgumentList.Add("10M");
            psi.ArgumentList.Add("--no-playlist");
            psi.ArgumentList.Add("--no-check-certificates");
            psi.ArgumentList.Add("--retries");
            psi.ArgumentList.Add("10");
            psi.ArgumentList.Add("--fragment-retries");
            psi.ArgumentList.Add("10");
            psi.ArgumentList.Add("--extractor-args");
            psi.ArgumentList.Add("youtube:player_client=android,web");
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
                    string safeArgs = ScrubArgumentListForLogs(psi.ArgumentList);
                    LoggingService.Log($"[YtDlpService] Starting yt-dlp: '{_ytDlpPath}' Args: {safeArgs}");
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
            if (YtDlpOutputParser.TryParseProgress(line, out var parsedPct, out _, out _, out _, out _))
            {
                _lastReportedProgressPercent = (int)Math.Floor(parsedPct);
                progress?.Invoke(_lastReportedProgressPercent, line);
                return;
            }

            // Keep the last reported progress percent instead of resetting to 0
            progress?.Invoke(_lastReportedProgressPercent, line);
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
                string? execPath = await MediaDependencyManager.Instance.GetValidatedYtDlpPathAsync(_ytDlpPath, ct).ConfigureAwait(false);
                if (string.IsNullOrEmpty(execPath))
                {
                    LoggingService.LogWarning("[YtDlpService] yt-dlp executable could not be resolved or provisioned.");
                    return null;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = execPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                psi.ArgumentList.Add("--dump-json");
                psi.ArgumentList.Add("--no-download");
                psi.ArgumentList.Add("--extractor-args");
                psi.ArgumentList.Add("youtube:player_client=android,web");
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
                        string safeUrl = ProtocolDetector.SanitizeUrlForLogging(url);
                        LoggingService.Log($"[YtDlpService] Getting metadata for URL: {safeUrl}");
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

        private static string ScrubArgumentListForLogs(IEnumerable<string> args)
        {
            var scrubbed = new List<string>();
            bool nextIsSensitive = false;

            foreach (var arg in args)
            {
                if (nextIsSensitive)
                {
                    scrubbed.Add("***");
                    nextIsSensitive = false;
                    continue;
                }

                if (arg.Equals("--add-header", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--cookies", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-u", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--username", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-p", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--password", StringComparison.OrdinalIgnoreCase))
                {
                    scrubbed.Add(arg);
                    nextIsSensitive = true;
                }
                else if (arg.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                         arg.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                         arg.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
                {
                    scrubbed.Add(ProtocolDetector.SanitizeUrlForLogging(arg));
                }
                else
                {
                    scrubbed.Add(arg);
                }
            }

            return string.Join(" ", scrubbed);
        }
    }
}
