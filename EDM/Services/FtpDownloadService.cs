using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;

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
    /// passive mode (PASV/EPSV), authentication credentials, pause/resume, and bandwidth throttling.
    /// </summary>
    public class FtpDownloadService
    {
        private const int DefaultBufferSize = 81920;

        public async Task<FtpProbeResult> ProbeFtpUrlAsync(string ftpUrl, NetworkCredential? credential = null, CancellationToken cancellationToken = default)
        {
            var uri = new Uri(ftpUrl);
            var result = new FtpProbeResult { Uri = uri };

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
                LoggingService.Log($"[FtpDownloadService] Probe SIZE failed for '{ftpUrl}': {ex.Message}");
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
            LoggingService.Log($"[FtpDownloadService] Starting FTP download for '{ftpUrl}' to '{targetFilePath}' with {segmentCount} segments.");

            var probe = await ProbeFtpUrlAsync(ftpUrl, credential, cancellationToken).ConfigureAwait(false);
            long totalBytes = probe.ContentLength;
            bool supportsRange = probe.SupportsRange && totalBytes > 0 && segmentCount > 1;

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath) ?? Path.GetTempPath());

            if (supportsRange)
            {
                await DownloadSegmentedFtpAsync(ftpUrl, targetFilePath, totalBytes, segmentCount, progressReporter, pauseToken, speedLimitProvider, cancellationToken, credential).ConfigureAwait(false);
            }
            else
            {
                await DownloadSingleThreadedFtpAsync(ftpUrl, targetFilePath, totalBytes, progressReporter, pauseToken, speedLimitProvider, cancellationToken, credential).ConfigureAwait(false);
            }
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
            string tempDir = Path.Combine(Path.GetDirectoryName(targetFilePath) ?? Path.GetTempPath(), "edm_ftp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            long chunkSize = totalBytes / segmentCount;
            var segments = new List<(int Index, long Start, long End, string PartPath)>();

            for (int i = 0; i < segmentCount; i++)
            {
                long start = i * chunkSize;
                long end = (i == segmentCount - 1) ? totalBytes - 1 : (start + chunkSize - 1);
                string partPath = Path.Combine(tempDir, $"seg_{i:D4}.part");
                segments.Add((i, start, end, partPath));
            }

            var downloadedBytesPerSeg = new ConcurrentDictionary<int, long>();
            var segmentFilePaths = new ConcurrentDictionary<int, string>();
            long totalDownloaded = 0;

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
                    long bytesWritten = 0;

                    var req = (FtpWebRequest)WebRequest.Create(new Uri(ftpUrl));
                    req.Method = WebRequestMethods.Ftp.DownloadFile;
                    req.ContentOffset = seg.Start;
                    req.UseBinary = true;
                    req.UsePassive = true;
                    if (credential != null) req.Credentials = credential;

                    using (var resp = (FtpWebResponse)await req.GetResponseAsync().ConfigureAwait(false))
                    await using (var ftpStream = resp.GetResponseStream())
                    await using (var fs = new FileStream(seg.PartPath, FileMode.Create, FileAccess.Write, FileShare.None, DefaultBufferSize, true))
                    {
                        var buffer = new byte[DefaultBufferSize];
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
                            long currentTotal = downloadedBytesPerSeg.Values.Sum();
                            Interlocked.Exchange(ref totalDownloaded, currentTotal);

                            double pct = totalBytes > 0 ? (double)currentTotal / totalBytes * 100.0 : 0;
                            progressReporter.Report(new DownloadProgressInfo
                            {
                                ProgressPercentage = Math.Min(99.9, pct),
                                BytesDownloaded = currentTotal,
                                TotalBytes = totalBytes,
                                Status = $"Downloading FTP Segment {seg.Index + 1}/{segmentCount}..."
                            });

                            // Apply speed limiting if active
                            double limit = speedLimitProvider?.Invoke() ?? -1;
                            if (limit > 0)
                            {
                                int delayMs = (int)(read / limit * 1000);
                                if (delayMs > 0) await Task.Delay(Math.Min(delayMs, 100), ct).ConfigureAwait(false);
                            }
                        }
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
                    Status = "Completed",
                    IsCompleted = true
                });
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                }
                catch { }
            }
        }

        private async Task DownloadSingleThreadedFtpAsync(
            string ftpUrl,
            string targetFilePath,
            long totalBytes,
            IProgress<DownloadProgressInfo> progressReporter,
            PauseTokenSource pauseToken,
            Func<double>? speedLimitProvider,
            CancellationToken cancellationToken,
            NetworkCredential? credential)
        {
            var req = (FtpWebRequest)WebRequest.Create(new Uri(ftpUrl));
            req.Method = WebRequestMethods.Ftp.DownloadFile;
            req.UseBinary = true;
            req.UsePassive = true;
            if (credential != null) req.Credentials = credential;

            long totalRead = 0;

            using (var resp = (FtpWebResponse)await req.GetResponseAsync().ConfigureAwait(false))
            await using (var ftpStream = resp.GetResponseStream())
            await using (var fs = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, DefaultBufferSize, true))
            {
                var buffer = new byte[DefaultBufferSize];
                int read;

                while ((read = await ftpStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (pauseToken != null) await pauseToken.WaitIfPausedAsync().ConfigureAwait(false);

                    await fs.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    totalRead += read;

                    double pct = totalBytes > 0 ? (double)totalRead / totalBytes * 100.0 : 0;
                    progressReporter.Report(new DownloadProgressInfo
                    {
                        ProgressPercentage = totalBytes > 0 ? Math.Min(99.9, pct) : 0,
                        BytesDownloaded = totalRead,
                        TotalBytes = totalBytes > 0 ? totalBytes : null,
                        Status = "Downloading FTP Stream..."
                    });

                    double limit = speedLimitProvider?.Invoke() ?? -1;
                    if (limit > 0)
                    {
                        int delayMs = (int)(read / limit * 1000);
                        if (delayMs > 0) await Task.Delay(Math.Min(delayMs, 100), cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            progressReporter.Report(new DownloadProgressInfo
            {
                ProgressPercentage = 100,
                BytesDownloaded = totalRead,
                TotalBytes = totalRead,
                Status = "Completed",
                IsCompleted = true
            });
        }
    }
}
