using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;

namespace EDM.Services
{
    internal static class MultiPartAdapter
    {
        public static async Task DownloadWithMultiPartAsync(
            string url, 
            string destinationPath, 
            int chunkCount, 
            IProgress<DownloadProgressInfo> progress, 
            PauseTokenSource pauseToken, 
            Func<double> speedLimitProvider, 
            CancellationToken ct, 
            DownloadCredentials? credentials = null, 
            string? cookies = null)
        {
            var downloader = new MultiPartDownloader(Services.SharedHttpClient.Instance)
            {
                Credentials = credentials,
                Cookies = cookies
            };

            // Set initial speed limit: convert bytes/sec to KB/s
            double bytesPerSec = speedLimitProvider?.Invoke() ?? 0;
            if (bytesPerSec > 0)
            {
                downloader.ThrottleKbps = (int)(bytesPerSec / 1024.0);
            }

            // Subscribe to pause token changes
            Action<bool>? pauseHandler = null;
            pauseHandler = (isPaused) =>
            {
                if (isPaused)
                    downloader.Pause();
                else
                    downloader.Resume();
            };

            var overallStopwatch = Stopwatch.StartNew();
            long lastSampleBytes = 0;
            long lastSampleTime = Stopwatch.GetTimestamp();
            double smoothedSpeed = 0;

            try
            {
                pauseToken.OnPauseChanged += pauseHandler;

                var innerProgress = new Progress<DownloadProgress>(dp =>
                {
                    try
                    {
                        var info = new DownloadProgressInfo();
                        info.TotalBytes = dp.TotalBytes;
                        info.BytesReceived = dp.BytesDownloaded;
                        info.BytesDownloaded = dp.BytesDownloaded;
                        info.ProgressPercentage = dp.TotalBytes > 0 ? (double)dp.BytesDownloaded / dp.TotalBytes * 100.0 : 0.0;
                        info.ServerSupportsResume = true;
                        info.Status = "Segmented Downloading...";

                        // Compute real throughput speed
                        long now = Stopwatch.GetTimestamp();
                        double deltaSec = (double)(now - lastSampleTime) / Stopwatch.Frequency;
                        if (deltaSec >= 0.1)
                        {
                            long deltaBytes = dp.BytesDownloaded - lastSampleBytes;
                            double instSpeed = Math.Max(0, deltaBytes / deltaSec);
                            smoothedSpeed = (smoothedSpeed <= 0) ? instSpeed : (0.4 * instSpeed + 0.6 * smoothedSpeed);
                            lastSampleBytes = dp.BytesDownloaded;
                            lastSampleTime = now;
                        }

                        double totalElapsed = overallStopwatch.Elapsed.TotalSeconds;
                        double avgSpeed = totalElapsed > 0.05 ? (double)dp.BytesDownloaded / totalElapsed : smoothedSpeed;

                        info.SpeedBytesPerSecond = pauseToken.IsPaused ? 0 : smoothedSpeed;
                        info.AverageSpeedBytesPerSecond = avgSpeed;

                        if (smoothedSpeed > 0 && dp.TotalBytes > dp.BytesDownloaded)
                        {
                            info.RemainingSeconds = (dp.TotalBytes - dp.BytesDownloaded) / smoothedSpeed;
                        }
                        else if (dp.BytesDownloaded >= dp.TotalBytes && dp.TotalBytes > 0)
                        {
                            info.RemainingSeconds = 0;
                        }

                        if (dp.ChunkStats != null)
                        {
                            var ordered = dp.ChunkStats.OrderBy(k => k.Key).ToArray();
                            info.SegmentCount = dp.Telemetry?.ConfiguredMaximumConnections ?? ordered.Length;
                            info.ActiveConnections = dp.Telemetry != null ? dp.Telemetry.ActiveConnections : dp.ChunkStats.Values.Count(c => c.IsActive);
                            info.ChunkStats = dp.ChunkStats;
                            var arr = new long[ordered.Length];
                            for (int i = 0; i < ordered.Length; i++) arr[i] = ordered[i].Value.Downloaded;
                            info.SegmentBytes = arr;
                        }

                        // Apply dynamic speed limit changes in real-time
                        if (speedLimitProvider != null)
                        {
                            double curLimitBytes = speedLimitProvider();
                            downloader.ThrottleKbps = curLimitBytes > 0 ? (int)(curLimitBytes / 1024.0) : 0;
                        }

                        progress?.Report(info);
                    }
                    catch (Exception ex) 
                    { 
                        EDM.Services.LoggingService.LogException("[MultiPartAdapter] Progress mapping failed", ex); 
                    }
                });

                await downloader.DownloadFileAsync(new Uri(url), destinationPath, chunkCount, chunkCount, innerProgress, ct).ConfigureAwait(false);
            }
            finally
            {
                overallStopwatch.Stop();
                if (pauseHandler != null)
                {
                    pauseToken.OnPauseChanged -= pauseHandler;
                }
            }
        }
    }
}
