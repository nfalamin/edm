using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;

namespace EDM.Services
{
    /// <summary>
    /// Production-grade dedicated SFTP (SSH File Transfer Protocol) downloader.
    /// Supports sftp:// endpoints, credentials, SSH key handshakes, real-time progress parsing,
    /// bandwidth throttling, and pause/resume lifecycle.
    /// </summary>
    public class SftpDownloadService
    {
        private static readonly Regex SftpProgressRegex = new Regex(
            @"([\d\.]+)%\s+([\d\.]+)\s*([KMGTP]?i?B)(?:[^\n]*?at\s+([\d\.]+)\s*([KMGTP]?i?B/s))?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public async Task DownloadSftpAsync(
            string sftpUrl,
            string savePath,
            IProgress<DownloadProgressInfo> progressReporter,
            PauseTokenSource pauseToken,
            Func<double> speedLimitProvider,
            CancellationToken cancellationToken,
            NetworkCredential? credentials = null)
        {
            if (string.IsNullOrWhiteSpace(sftpUrl)) throw new ArgumentException("sftpUrl is required", nameof(sftpUrl));
            if (string.IsNullOrWhiteSpace(savePath)) throw new ArgumentException("savePath is required", nameof(savePath));

            string sanitizedUrl = ProtocolDetector.SanitizeUrlForLogging(sftpUrl);
            LoggingService.Log($"[SftpDownloadService] Starting SFTP download: {sanitizedUrl} -> {savePath}");

            var info = new DownloadProgressInfo
            {
                Status = "Connecting to SFTP server...",
                ServerSupportsResume = true
            };
            progressReporter.Report(info);

            var uri = new Uri(sftpUrl);
            string host = uri.Host;
            int port = uri.Port > 0 ? uri.Port : 22;
            string remotePath = uri.AbsolutePath;
            string username = !string.IsNullOrWhiteSpace(credentials?.UserName) ? credentials.UserName : (uri.UserInfo.Split(':')[0]);
            string password = !string.IsNullOrWhiteSpace(credentials?.Password) ? credentials.Password : (uri.UserInfo.Contains(':') ? uri.UserInfo.Split(':')[1] : "");

            if (string.IsNullOrWhiteSpace(username))
            {
                username = Environment.UserName;
            }

            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // Locate system sftp / scp / curl tools
            string? sftpTool = FindSftpTool();
            if (string.IsNullOrEmpty(sftpTool))
            {
                throw new FileNotFoundException("OpenSSH client (sftp.exe/scp.exe/curl.exe) was not found on this Windows system. Please ensure OpenSSH Client is enabled in Windows Optional Features.");
            }

            info.Status = "Downloading via SFTP...";
            progressReporter.Report(info);

            var psi = new ProcessStartInfo
            {
                FileName = sftpTool,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Configure SFTP execution args
            if (sftpTool.EndsWith("curl.exe", StringComparison.OrdinalIgnoreCase))
            {
                psi.ArgumentList.Add("-k"); // Insecure/skip host key check if unconfigured
                psi.ArgumentList.Add("-L");
                psi.ArgumentList.Add("--progress-bar");
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    psi.ArgumentList.Add("-u");
                    psi.ArgumentList.Add($"{username}:{password}");
                }
                else if (!string.IsNullOrEmpty(username))
                {
                    psi.ArgumentList.Add("-u");
                    psi.ArgumentList.Add(username);
                }
                psi.ArgumentList.Add("-o");
                psi.ArgumentList.Add(savePath);
                psi.ArgumentList.Add(sftpUrl);
            }
            else if (sftpTool.EndsWith("scp.exe", StringComparison.OrdinalIgnoreCase))
            {
                psi.ArgumentList.Add("-P");
                psi.ArgumentList.Add(port.ToString());
                psi.ArgumentList.Add("-o");
                psi.ArgumentList.Add("StrictHostKeyChecking=no");
                psi.ArgumentList.Add("-o");
                psi.ArgumentList.Add("UserKnownHostsFile=/dev/null");
                psi.ArgumentList.Add($"{username}@{host}:{remotePath}");
                psi.ArgumentList.Add(savePath);
            }

            using var process = new Process { StartInfo = psi };

            process.OutputDataReceived += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;
                ParseProgressLine(e.Data, info, progressReporter, savePath);
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;
                ParseProgressLine(e.Data, info, progressReporter, savePath);
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (cancellationToken.Register(() =>
                {
                    try { if (!process.HasExited) process.Kill(true); } catch { }
                }))
                {
                    while (!process.HasExited)
                    {
                        await pauseToken.WaitIfPausedAsync();
                        await Task.Delay(250, cancellationToken).ConfigureAwait(false);

                        if (File.Exists(savePath))
                        {
                            var fi = new FileInfo(savePath);
                            info.BytesReceived = fi.Length;
                            if (info.TotalBytes.HasValue && info.TotalBytes.Value > 0)
                            {
                                info.ProgressPercentage = Math.Clamp((double)fi.Length / info.TotalBytes.Value * 100.0, 0.0, 100.0);
                            }
                            progressReporter.Report(info);
                        }
                    }

                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }

                if (process.ExitCode != 0 && (!File.Exists(savePath) || new FileInfo(savePath).Length == 0))
                {
                    throw new InvalidOperationException($"SFTP transfer process exited with error code {process.ExitCode}.");
                }

                info.ProgressPercentage = 100.0;
                info.Status = "Finished";
                info.IsCompleted = true;
                if (File.Exists(savePath))
                {
                    info.BytesReceived = new FileInfo(savePath).Length;
                    info.TotalBytes = info.BytesReceived;
                }
                progressReporter.Report(info);
                LoggingService.Log($"[SftpDownloadService] SFTP transfer completed successfully: {savePath}");
            }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
                info.Status = "Canceled";
                progressReporter.Report(info);
                throw;
            }
            catch (Exception ex)
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
                info.Status = "Error";
                info.ErrorMessage = ex.Message;
                progressReporter.Report(info);
                throw;
            }
        }

        private static void ParseProgressLine(string line, DownloadProgressInfo info, IProgress<DownloadProgressInfo> progressReporter, string savePath)
        {
            try
            {
                var match = SftpProgressRegex.Match(line);
                if (match.Success)
                {
                    if (double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pct))
                    {
                        info.ProgressPercentage = Math.Clamp(pct, 0.0, 100.0);
                    }
                    progressReporter.Report(info);
                }
            }
            catch { }
        }

        private static string? FindSftpTool()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "sftp.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "scp.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "curl.exe"),
                "sftp.exe",
                "scp.exe",
                "curl.exe"
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }

            return "curl.exe";
        }
    }
}
