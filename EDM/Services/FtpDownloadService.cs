using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;

#pragma warning disable SYSLIB0014 // FtpWebRequest is legacy BCL API until migrated

namespace EDM.Services
{
    public class FtpProbeResult
    {
        public Uri Uri { get; set; } = null!;
        public long ContentLength { get; set; } = -1;
        public bool SupportsRange { get; set; }
        public DateTime? LastModified { get; set; }
    }

    /// <summary>
    /// Production-grade dedicated FTP/FTPS downloader service.
    /// Supports multi-segment parallel downloads via FTP REST (Restart/Range) command,
    /// passive mode (PASV/EPSV), authentication credentials, pause/resume, FTPS socket fallback, and bandwidth throttling.
    /// </summary>
    public class FtpDownloadService
    {
        private const int DefaultBufferSize = 81920;

        public async Task<FtpProbeResult> ProbeFtpUrlAsync(string ftpUrl, NetworkCredential? credential = null, CancellationToken cancellationToken = default)
        {
            var uri = new Uri(ftpUrl);
            var result = new FtpProbeResult { Uri = uri };

            string sanitizedUrl = ProtocolDetector.SanitizeUrlForLogging(ftpUrl);

            try
            {
                // Try SIZE command
                var reqSize = (FtpWebRequest)WebRequest.Create(uri);
                reqSize.Method = WebRequestMethods.Ftp.GetFileSize;
                reqSize.UseBinary = true;
                reqSize.UsePassive = true;
                reqSize.Timeout = 4000;
                reqSize.ReadWriteTimeout = 4000;
                if (credential != null) reqSize.Credentials = credential;

                using (var respSize = (FtpWebResponse)await reqSize.GetResponseAsync().ConfigureAwait(false))
                {
                    result.ContentLength = respSize.ContentLength;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[FtpDownloadService] Probe SIZE failed for '{sanitizedUrl}': {ex.Message}");
            }

            try
            {
                // Try MDTM command
                var reqMdtm = (FtpWebRequest)WebRequest.Create(uri);
                reqMdtm.Method = WebRequestMethods.Ftp.GetDateTimestamp;
                reqMdtm.UseBinary = true;
                reqMdtm.UsePassive = true;
                reqMdtm.Timeout = 4000;
                reqMdtm.ReadWriteTimeout = 4000;
                if (credential != null) reqMdtm.Credentials = credential;

                using (var respMdtm = (FtpWebResponse)await reqMdtm.GetResponseAsync().ConfigureAwait(false))
                {
                    result.LastModified = respMdtm.LastModified;
                }
            }
            catch { }

            // Validate Range (REST) support by attempting a 1-byte REST probe
            if (result.ContentLength > 0)
            {
                try
                {
                    var reqRest = (FtpWebRequest)WebRequest.Create(uri);
                    reqRest.Method = WebRequestMethods.Ftp.DownloadFile;
                    reqRest.ContentOffset = 1;
                    reqRest.UseBinary = true;
                    reqRest.UsePassive = true;
                    reqRest.Timeout = 4000;
                    reqRest.ReadWriteTimeout = 4000;
                    if (credential != null) reqRest.Credentials = credential;

                    using var respRest = (FtpWebResponse)await reqRest.GetResponseAsync().ConfigureAwait(false);
                    result.SupportsRange = true;
                }
                catch
                {
                    result.SupportsRange = false;
                }
            }

            return result;
        }

        public async Task DownloadFtpAsync(
            string ftpUrl,
            string targetFilePath,
            int segmentCount,
            IProgress<DownloadProgressInfo> progressReporter,
            PauseTokenSource pauseToken,
            Func<double>? speedLimitProvider,
            CancellationToken cancellationToken,
            NetworkCredential? credential = null)
        {
            string sanitizedUrl = ProtocolDetector.SanitizeUrlForLogging(ftpUrl);
            LoggingService.Log($"[FtpDownloadService] Starting FTP download for '{sanitizedUrl}' to '{targetFilePath}' with {segmentCount} segments.");

            var uri = new Uri(ftpUrl);

            // Handle FTPS via FtpsClientEngine if FTPS scheme or port 990
            if (uri.Scheme.Equals("ftps", StringComparison.OrdinalIgnoreCase) || uri.Port == 990)
            {
                await DownloadFtpsSocketAsync(ftpUrl, targetFilePath, progressReporter, pauseToken, speedLimitProvider, cancellationToken, credential).ConfigureAwait(false);
                return;
            }

            var probe = await ProbeFtpUrlAsync(ftpUrl, credential, cancellationToken).ConfigureAwait(false);
            long totalBytes = probe.ContentLength;
            bool supportsRange = probe.SupportsRange && totalBytes > 0;

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath) ?? Path.GetTempPath());

            if (supportsRange && segmentCount > 1)
            {
                await DownloadSegmentedFtpAsync(ftpUrl, targetFilePath, totalBytes, segmentCount, progressReporter, pauseToken, speedLimitProvider, cancellationToken, credential).ConfigureAwait(false);
            }
            else
            {
                await DownloadSingleThreadedFtpAsync(ftpUrl, targetFilePath, totalBytes, supportsRange, progressReporter, pauseToken, speedLimitProvider, cancellationToken, credential).ConfigureAwait(false);
            }
        }

        private async Task DownloadFtpsSocketAsync(
            string ftpUrl,
            string targetFilePath,
            IProgress<DownloadProgressInfo> progressReporter,
            PauseTokenSource pauseToken,
            Func<double>? speedLimitProvider,
            CancellationToken cancellationToken,
            NetworkCredential? credential)
        {
            var uri = new Uri(ftpUrl);
            string host = uri.Host;
            int port = uri.Port > 0 ? uri.Port : 21;
            string username = credential?.UserName ?? (string.IsNullOrEmpty(uri.UserInfo) ? "anonymous" : uri.UserInfo.Split(':')[0]);
            string password = credential?.Password ?? (uri.UserInfo.Contains(':') ? uri.UserInfo.Split(':')[1] : "user@domain.com");
            string remotePath = uri.AbsolutePath;

            var engine = new FtpsClientEngine(host, port, username, password, useTls: true);

            long existingBytes = File.Exists(targetFilePath) ? new FileInfo(targetFilePath).Length : 0;
            FileMode fileMode = existingBytes > 0 ? FileMode.Append : FileMode.Create;

            var speedTracker = new SpeedTracker();

            progressReporter.Report(new DownloadProgressInfo
            {
                ProgressPercentage = 0,
                BytesDownloaded = existingBytes,
                TotalBytes = null,
                ServerSupportsResume = true,
                Status = "Connecting to FTPS Server via TLS..."
            });

            await using var fs = new FileStream(targetFilePath, fileMode, FileAccess.Write, FileShare.None, DefaultBufferSize, true);

            var progress = new Progress<long>(totalDownloaded =>
            {
                double speed = speedTracker.UpdateAndGetSpeed(existingBytes + totalDownloaded);
                progressReporter.Report(new DownloadProgressInfo
                {
                    BytesDownloaded = existingBytes + totalDownloaded,
                    TotalBytes = null,
                    SpeedBytesPerSecond = speed,
                    ServerSupportsResume = true,
                    Status = "Downloading FTPS Stream (TLS 1.3)..."
                });
            });

            var result = await engine.DownloadFileAsync(remotePath, fs, resumeOffset: existingBytes, progress: progress, ct: cancellationToken).ConfigureAwait(false);

            if (!result.Success)
            {
                throw new IOException($"FTPS transfer failed: {result.ErrorMessage}");
            }

            long finalSize = new FileInfo(targetFilePath).Length;
            progressReporter.Report(new DownloadProgressInfo
            {
                ProgressPercentage = 100,
                BytesDownloaded = finalSize,
                TotalBytes = finalSize,
                SpeedBytesPerSecond = 0,
                Status = "Completed",
                IsCompleted = true
            });
        }

        private async Task DownloadSegmentedFtpAsync(
            string ftpUrl,
            string targetFilePath,
            long totalBytes,
            int segmentCount,
            IProgress<DownloadProgressInfo> progressReporter,
            PauseTokenSource pauseToken,
            Func<double>? speedLimitProvider,
            CancellationToken cancellationToken,
            NetworkCredential? credential)
        {
            string targetDir = Path.GetDirectoryName(targetFilePath) ?? Path.GetTempPath();
            string stagingDir = Path.Combine(targetDir, "." + Path.GetFileName(targetFilePath) + ".ftp_segments");
            Directory.CreateDirectory(stagingDir);

            long chunkSize = totalBytes / segmentCount;
            var segments = new List<(int Index, long Start, long End, string PartPath, string TmpPath)>();

            for (int i = 0; i < segmentCount; i++)
            {
                long start = i * chunkSize;
                long end = (i == segmentCount - 1) ? totalBytes - 1 : (start + chunkSize - 1);
                string partPath = Path.Combine(stagingDir, $"seg_{i:D4}.part");
                string tmpPath = Path.Combine(stagingDir, $"seg_{i:D4}.part.tmp");
                segments.Add((i, start, end, partPath, tmpPath));
            }

            var downloadedBytesPerSeg = new ConcurrentDictionary<int, long>();
            var segmentFilePaths = new ConcurrentDictionary<int, string>();
            var speedTracker = new SpeedTracker();

            // Pre-scan existing parts for resume
            foreach (var seg in segments)
            {
                if (File.Exists(seg.PartPath))
                {
                    long len = new FileInfo(seg.PartPath).Length;
                    long expectedLen = seg.End - seg.Start + 1;
                    if (len == expectedLen)
                    {
                        downloadedBytesPerSeg[seg.Index] = len;
                        segmentFilePaths[seg.Index] = seg.PartPath;
                    }
                }
            }

            long totalDownloaded = downloadedBytesPerSeg.Values.Sum();

            try
            {
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = segmentCount,
                    CancellationToken = cancellationToken
                };

                await Parallel.ForEachAsync(segments, parallelOptions, async (seg, ct) =>
                {
                    long segLength = seg.End - seg.Start + 1;

                    // Skip if already downloaded
                    if (segmentFilePaths.ContainsKey(seg.Index)) return;

                    long bytesWritten = 0;

                    var req = (FtpWebRequest)WebRequest.Create(new Uri(ftpUrl));
                    req.Method = WebRequestMethods.Ftp.DownloadFile;
                    req.ContentOffset = seg.Start;
                    req.UseBinary = true;
                    req.UsePassive = true;
                    if (credential != null) req.Credentials = credential;

                    using (var resp = (FtpWebResponse)await req.GetResponseAsync().ConfigureAwait(false))
                    await using (var ftpStream = resp.GetResponseStream())
                    await using (var fs = new FileStream(seg.TmpPath, FileMode.Create, FileAccess.Write, FileShare.None, DefaultBufferSize, true))
                    {
                        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
                        long lastReportTicks = 0;
                        try
                        {
                            while (bytesWritten < segLength)
                            {
                                ct.ThrowIfCancellationRequested();
                                if (pauseToken != null) await pauseToken.WaitIfPausedAsync().ConfigureAwait(false);

                                int toRead = (int)Math.Min(buffer.Length, segLength - bytesWritten);
                                int read = await ftpStream.ReadAsync(buffer.AsMemory(0, toRead), ct).ConfigureAwait(false);
                                if (read == 0) break;

                                await fs.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                                bytesWritten += read;

                                downloadedBytesPerSeg[seg.Index] = bytesWritten;
                                long currentTotal = Interlocked.Add(ref totalDownloaded, read);

                                long nowTicks = Environment.TickCount64;
                                if (nowTicks - lastReportTicks >= 100 || currentTotal >= totalBytes)
                                {
                                    double currentSpeed = speedTracker.UpdateAndGetSpeed(currentTotal);
                                    double remainingSecs = currentSpeed > 0 ? (totalBytes - currentTotal) / currentSpeed : 0;
                                    double pct = totalBytes > 0 ? (double)currentTotal / totalBytes * 100.0 : 0;

                                    progressReporter.Report(new DownloadProgressInfo
                                    {
                                        ProgressPercentage = Math.Min(99.9, pct),
                                        BytesDownloaded = currentTotal,
                                        TotalBytes = totalBytes,
                                        SpeedBytesPerSecond = currentSpeed,
                                        RemainingSeconds = remainingSecs,
                                        ActiveConnections = segmentCount,
                                        SegmentCount = segmentCount,
                                        ServerSupportsResume = true,
                                        Status = $"Downloading FTP Segments ({segmentCount} active)..."
                                    });
                                    lastReportTicks = nowTicks;
                                }

                                // Apply speed limiting if active
                                double limit = speedLimitProvider?.Invoke() ?? -1;
                                if (limit > 0)
                                {
                                    int delayMs = (int)(read / limit * 1000);
                                    if (delayMs > 0) await Task.Delay(Math.Min(delayMs, 100), ct).ConfigureAwait(false);
                                }
                            }
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                        }
                    }

                    if (File.Exists(seg.TmpPath))
                    {
                        if (File.Exists(seg.PartPath)) File.Delete(seg.PartPath);
                        File.Move(seg.TmpPath, seg.PartPath);
                    }

                    segmentFilePaths[seg.Index] = seg.PartPath;
                }).ConfigureAwait(false);

                // Merge segment files in order into target file
                await using var outFs = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, DefaultBufferSize, true);
                for (int i = 0; i < segmentCount; i++)
                {
                    if (segmentFilePaths.TryGetValue(i, out var partPath) && File.Exists(partPath))
                    {
                        await using var partFs = File.OpenRead(partPath);
                        await partFs.CopyToAsync(outFs, cancellationToken).ConfigureAwait(false);
                    }
                }
                await outFs.FlushAsync(cancellationToken).ConfigureAwait(false);

                progressReporter.Report(new DownloadProgressInfo
                {
                    ProgressPercentage = 100,
                    BytesDownloaded = totalBytes,
                    TotalBytes = totalBytes,
                    ActiveConnections = 0,
                    SpeedBytesPerSecond = 0,
                    Status = "Completed",
                    IsCompleted = true
                });
            }
            finally
            {
                try
                {
                    if (Directory.Exists(stagingDir) && totalDownloaded >= totalBytes)
                    {
                        Directory.Delete(stagingDir, true);
                    }
                }
                catch { }
            }
        }

        private async Task DownloadSingleThreadedFtpAsync(
            string ftpUrl,
            string targetFilePath,
            long totalBytes,
            bool supportsRange,
            IProgress<DownloadProgressInfo> progressReporter,
            PauseTokenSource pauseToken,
            Func<double>? speedLimitProvider,
            CancellationToken cancellationToken,
            NetworkCredential? credential)
        {
            long existingBytes = (supportsRange && File.Exists(targetFilePath)) ? new FileInfo(targetFilePath).Length : 0;
            if (existingBytes >= totalBytes && totalBytes > 0)
            {
                progressReporter.Report(new DownloadProgressInfo
                {
                    ProgressPercentage = 100,
                    BytesDownloaded = totalBytes,
                    TotalBytes = totalBytes,
                    Status = "Completed",
                    IsCompleted = true
                });
                return;
            }

            var req = (FtpWebRequest)WebRequest.Create(new Uri(ftpUrl));
            req.Method = WebRequestMethods.Ftp.DownloadFile;
            req.UseBinary = true;
            req.UsePassive = true;
            if (existingBytes > 0) req.ContentOffset = existingBytes;
            if (credential != null) req.Credentials = credential;

            long totalRead = existingBytes;
            var speedTracker = new SpeedTracker();

            FileMode fileMode = existingBytes > 0 ? FileMode.Append : FileMode.Create;

            using (var resp = (FtpWebResponse)await req.GetResponseAsync().ConfigureAwait(false))
            await using (var ftpStream = resp.GetResponseStream())
            await using (var fs = new FileStream(targetFilePath, fileMode, FileAccess.Write, FileShare.None, DefaultBufferSize, true))
            {
                var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
                long lastReportTicks = 0;
                try
                {
                    int read;
                    while ((read = await ftpStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (pauseToken != null) await pauseToken.WaitIfPausedAsync().ConfigureAwait(false);

                        await fs.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        totalRead += read;

                        long nowTicks = Environment.TickCount64;
                        if (nowTicks - lastReportTicks >= 100 || (totalBytes > 0 && totalRead >= totalBytes))
                        {
                            double currentSpeed = speedTracker.UpdateAndGetSpeed(totalRead);
                            double remainingSecs = (currentSpeed > 0 && totalBytes > 0) ? (totalBytes - totalRead) / currentSpeed : 0;
                            double pct = totalBytes > 0 ? (double)totalRead / totalBytes * 100.0 : 0;

                            progressReporter.Report(new DownloadProgressInfo
                            {
                                ProgressPercentage = totalBytes > 0 ? Math.Min(99.9, pct) : 0,
                                BytesDownloaded = totalRead,
                                TotalBytes = totalBytes > 0 ? totalBytes : null,
                                SpeedBytesPerSecond = currentSpeed,
                                RemainingSeconds = remainingSecs,
                                ActiveConnections = 1,
                                ServerSupportsResume = supportsRange,
                                Status = "Downloading FTP Stream..."
                            });
                            lastReportTicks = nowTicks;
                        }

                        double limit = speedLimitProvider?.Invoke() ?? -1;
                        if (limit > 0)
                        {
                            int delayMs = (int)(read / limit * 1000);
                            if (delayMs > 0) await Task.Delay(Math.Min(delayMs, 100), cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            progressReporter.Report(new DownloadProgressInfo
            {
                ProgressPercentage = 100,
                BytesDownloaded = totalRead,
                TotalBytes = totalRead,
                ActiveConnections = 0,
                SpeedBytesPerSecond = 0,
                Status = "Completed",
                IsCompleted = true
            });
        }
    }
}

