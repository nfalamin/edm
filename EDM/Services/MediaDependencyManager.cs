using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    /// <summary>
    /// Authoritative dependency manager for external media tooling (yt-dlp and ffmpeg).
    /// Discovers, validates, provisions, and returns absolute validated executable paths.
    /// </summary>
    public sealed class MediaDependencyManager
    {
        private static readonly Lazy<MediaDependencyManager> _lazyInstance = new(() => new MediaDependencyManager());
        public static MediaDependencyManager Instance => _lazyInstance.Value;

        private readonly string _toolsDir;
        private string? _cachedYtDlpPath;
        private string? _cachedFfmpegPath;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public MediaDependencyManager()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _toolsDir = Path.Combine(localAppData, "EDM", "tools");
            try
            {
                if (!Directory.Exists(_toolsDir)) Directory.CreateDirectory(_toolsDir);
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"[MediaDependencyManager] Failed to create tools directory '{_toolsDir}': {ex.Message}");
            }
        }

        public string ToolsDirectory => _toolsDir;

        /// <summary>
        /// Resolves and validates the absolute path to yt-dlp.exe. If not found locally, automatically provisions it.
        /// </summary>
        public async Task<string?> GetValidatedYtDlpPathAsync(string? customPath = null, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!string.IsNullOrWhiteSpace(customPath) && await ValidateExecutableAsync(customPath, "--version", cancellationToken).ConfigureAwait(false))
                {
                    _cachedYtDlpPath = Path.GetFullPath(customPath);
                    return _cachedYtDlpPath;
                }

                if (!string.IsNullOrWhiteSpace(_cachedYtDlpPath) && File.Exists(_cachedYtDlpPath))
                {
                    return _cachedYtDlpPath;
                }

                var candidatePaths = GetCandidatePaths("yt-dlp.exe", "yt-dlp");
                foreach (var candidate in candidatePaths)
                {
                    if (await ValidateExecutableAsync(candidate, "--version", cancellationToken).ConfigureAwait(false))
                    {
                        _cachedYtDlpPath = Path.GetFullPath(candidate);
                        LoggingService.Log($"[MediaDependencyManager] Found validated yt-dlp at: {_cachedYtDlpPath}");
                        return _cachedYtDlpPath;
                    }
                }

                // If not found anywhere, automatically provision/download yt-dlp
                LoggingService.Log("[MediaDependencyManager] yt-dlp not found locally. Auto-provisioning latest release...");
                bool provisioned = await ProvisionYtDlpInternalAsync(null, cancellationToken).ConfigureAwait(false);
                if (provisioned && !string.IsNullOrWhiteSpace(_cachedYtDlpPath))
                {
                    return _cachedYtDlpPath;
                }

                return null;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Resolves and validates the absolute path to ffmpeg.exe.
        /// </summary>
        public async Task<string?> GetValidatedFfmpegPathAsync(string? customPath = null, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!string.IsNullOrWhiteSpace(customPath) && await ValidateExecutableAsync(customPath, "-version", cancellationToken).ConfigureAwait(false))
                {
                    _cachedFfmpegPath = Path.GetFullPath(customPath);
                    return _cachedFfmpegPath;
                }

                if (!string.IsNullOrWhiteSpace(_cachedFfmpegPath) && File.Exists(_cachedFfmpegPath))
                {
                    return _cachedFfmpegPath;
                }

                var candidatePaths = GetCandidatePaths("ffmpeg.exe", "ffmpeg");
                foreach (var candidate in candidatePaths)
                {
                    if (await ValidateExecutableAsync(candidate, "-version", cancellationToken).ConfigureAwait(false))
                    {
                        _cachedFfmpegPath = Path.GetFullPath(candidate);
                        LoggingService.Log($"[MediaDependencyManager] Found validated FFmpeg at: {_cachedFfmpegPath}");
                        return _cachedFfmpegPath;
                    }
                }

                return null;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Provisions/downloads the latest yt-dlp.exe release directly into %LOCALAPPDATA%\EDM\tools.
        /// </summary>
        public async Task<bool> ProvisionYtDlpAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await ProvisionYtDlpInternalAsync(progress, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<bool> ProvisionYtDlpInternalAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            string targetPath = Path.Combine(_toolsDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "yt-dlp.exe" : "yt-dlp");
            string downloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

            try
            {
                LoggingService.Log($"[MediaDependencyManager] Provisioning yt-dlp from '{downloadUrl}' to '{targetPath}'...");
                string tempFile = targetPath + ".tmp";

                // Use shared HttpClient to avoid socket exhaustion from repeated new HttpClient() calls
                var client = SharedHttpClient.Instance;
                {
                    client.DefaultRequestHeaders.UserAgent.TryParseAdd("EDM-Downloader/2.0");
                    using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    long totalBytes = response.Content.Headers.ContentLength ?? -1;
                    long totalRead = 0;

                    using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    using var dest = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                    var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(81920);
                    try
                    {
                        int read;
                        while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            await dest.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                            totalRead += read;
                            if (totalBytes > 0) progress?.Report((double)totalRead / totalBytes * 100.0);
                        }
                    }
                    finally
                    {
                        System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                    }
                }

                if (File.Exists(targetPath)) File.Delete(targetPath);
                File.Move(tempFile, targetPath);

                if (await ValidateExecutableAsync(targetPath, "--version", cancellationToken).ConfigureAwait(false))
                {
                    _cachedYtDlpPath = targetPath;
                    LoggingService.Log($"[MediaDependencyManager] Successfully provisioned yt-dlp at: {targetPath}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[MediaDependencyManager] Failed to provision yt-dlp", ex);
                return false;
            }
        }

        private IEnumerable<string> GetCandidatePaths(string exeName, string commandName)
        {
            var results = new List<string>();

            // 1. Tools Directory (%LOCALAPPDATA%\EDM\tools\exeName)
            string localTools = Path.Combine(_toolsDir, exeName);
            if (File.Exists(localTools)) results.Add(localTools);

            // 2. Application Base Directory
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string appTools = Path.Combine(baseDir, "tools", exeName);
            if (File.Exists(appTools)) results.Add(appTools);

            string appRoot = Path.Combine(baseDir, exeName);
            if (File.Exists(appRoot)) results.Add(appRoot);

            // 3. System PATH directories
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(pathEnv))
            {
                var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
                foreach (var dir in paths)
                {
                    try
                    {
                        string candidate = Path.Combine(dir.Trim('"', ' '), exeName);
                        if (File.Exists(candidate)) results.Add(candidate);
                    }
                    catch { }
                }
            }

            // Fallback bare command name for PATH resolution via process start
            results.Add(commandName);

            return results;
        }

        public async Task<ProcessExecutionResult> ExecuteSupervisedProcessAsync(
            string fileName,
            IEnumerable<string> arguments,
            Action<string>? onOutputLine = null,
            Action<string>? onErrorLine = null,
            CancellationToken cancellationToken = default,
            TimeSpan? timeout = null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (var arg in arguments)
            {
                psi.ArgumentList.Add(arg);
            }

            var stdoutBuilder = new System.Text.StringBuilder();
            var stderrBuilder = new System.Text.StringBuilder();

            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    lock (stdoutBuilder) stdoutBuilder.AppendLine(e.Data);
                    onOutputLine?.Invoke(e.Data);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    lock (stderrBuilder) stderrBuilder.AppendLine(e.Data);
                    onErrorLine?.Invoke(e.Data);
                }
            };

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeout.HasValue)
            {
                linkedCts.CancelAfter(timeout.Value);
            }

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (linkedCts.Token.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                        }
                    }
                    catch { }
                }))
                {
                    await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
                }

                return new ProcessExecutionResult
                {
                    ExitCode = process.ExitCode,
                    StandardOutput = stdoutBuilder.ToString(),
                    StandardError = stderrBuilder.ToString(),
                    IsSuccess = process.ExitCode == 0
                };
            }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
                return new ProcessExecutionResult
                {
                    ExitCode = -1,
                    StandardOutput = stdoutBuilder.ToString(),
                    StandardError = stderrBuilder.ToString(),
                    IsCancelled = cancellationToken.IsCancellationRequested,
                    IsTimedOut = !cancellationToken.IsCancellationRequested && linkedCts.IsCancellationRequested,
                    IsSuccess = false
                };
            }
        }

        private async Task<bool> ValidateExecutableAsync(string pathOrCommand, string argument, CancellationToken cancellationToken)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = pathOrCommand,
                    Arguments = argument,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public class ProcessExecutionResult
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; } = string.Empty;
        public string StandardError { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public bool IsCancelled { get; set; }
        public bool IsTimedOut { get; set; }
    }
}
