using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;

namespace EDM.Services
{
    /// <summary>
    /// Responsible for downloading separate video/audio streams and merging them using FFmpeg.
    /// Provides authoritative, real-time byte progress, speed tracking, and output validation.
    /// </summary>
    public sealed class MediaMergeService
    {
        private readonly HttpClient _httpClient;

        public MediaMergeService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Downloads separate video and audio streams concurrently with unified byte progress,
        /// then merges them into outputPath using FFmpeg.
        /// Temporary files are isolated per job and cleaned up reliably on completion or failure.
        /// </summary>
        public async Task MergeAudioVideoAsync(
            string videoUrl,
            string audioUrl,
            string outputPath,
            string? ffmpegPath,
            CancellationToken cancellationToken,
            IProgress<DownloadProgressInfo>? progress = null,
            PauseTokenSource? pauseToken = null,
            Func<double>? speedLimitProvider = null,
            long expectedVideoBytes = -1,
            long expectedAudioBytes = -1)
        {
            if (string.IsNullOrWhiteSpace(videoUrl)) throw new ArgumentException("videoUrl is required", nameof(videoUrl));
            if (string.IsNullOrWhiteSpace(audioUrl)) throw new ArgumentException("audioUrl is required", nameof(audioUrl));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("outputPath is required", nameof(outputPath));

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

            // Isolate temporary file names using unique execution GUIDs to prevent collisions
            string jobGuid = Guid.NewGuid().ToString("N");
            string tempVideo = $"{outputPath}.{jobGuid}.video.tmp";
            string tempAudio = $"{outputPath}.{jobGuid}.audio.tmp";

            var stopwatch = Stopwatch.StartNew();
            long totalVideoDownloaded = 0;
            long totalAudioDownloaded = 0;
            long totalKnownBytes = (expectedVideoBytes > 0 && expectedAudioBytes > 0) ? (expectedVideoBytes + expectedAudioBytes) : -1;

            var speedTracker = new SpeedTracker();

            try
            {
                LoggingService.Log($"[MediaMergeService] Starting dual-stream adaptive download: video={videoUrl}, audio={audioUrl}");

                progress?.Report(new DownloadProgressInfo
                {
                    Status = "Connecting to Video & Audio streams...",
                    ProgressPercentage = 0.0,
                    BytesReceived = 0,
                    TotalBytes = totalKnownBytes > 0 ? totalKnownBytes : null,
                    ServerSupportsResume = true,
                    ActiveConnections = 2
                });

                // Download Video Stream task
                var videoTask = Task.Run(async () =>
                {
                    using var respV = await _httpClient.GetAsync(videoUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    respV.EnsureSuccessStatusCode();

                    long vidLength = respV.Content.Headers.ContentLength ?? expectedVideoBytes;
                    await using var vs = await respV.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    await using var vfs = new FileStream(tempVideo, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);

                    byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(81920);
                    try
                    {
                        int bytesRead;
                        while ((bytesRead = await vs.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            if (pauseToken != null && pauseToken.IsPaused)
                            {
                                await pauseToken.WaitIfPausedAsync().ConfigureAwait(false);
                            }

                            await vfs.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                            Interlocked.Add(ref totalVideoDownloaded, bytesRead);

                            ReportCombinedProgress(progress, totalVideoDownloaded, totalAudioDownloaded, totalKnownBytes, speedTracker);
                        }
                        await vfs.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                    }
                }, cancellationToken);

                // Download Audio Stream task
                var audioTask = Task.Run(async () =>
                {
                    using var respA = await _httpClient.GetAsync(audioUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    respA.EnsureSuccessStatusCode();

                    long audLength = respA.Content.Headers.ContentLength ?? expectedAudioBytes;
                    await using var asr = await respA.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    await using var afs = new FileStream(tempAudio, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);

                    byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(81920);
                    try
                    {
                        int bytesRead;
                        while ((bytesRead = await asr.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            if (pauseToken != null && pauseToken.IsPaused)
                            {
                                await pauseToken.WaitIfPausedAsync().ConfigureAwait(false);
                            }

                            await afs.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                            Interlocked.Add(ref totalAudioDownloaded, bytesRead);

                            ReportCombinedProgress(progress, totalVideoDownloaded, totalAudioDownloaded, totalKnownBytes, speedTracker);
                        }
                        await afs.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                    }
                }, cancellationToken);

                // Await both stream downloads
                await Task.WhenAll(videoTask, audioTask).ConfigureAwait(false);

                long combinedDownloaded = totalVideoDownloaded + totalAudioDownloaded;

                // State transition: PreparingMerge / Merging
                progress?.Report(new DownloadProgressInfo
                {
                    Status = "Merging Audio & Video (FFmpeg)...",
                    ProgressPercentage = totalKnownBytes > 0 ? 99.0 : 0.0,
                    BytesReceived = combinedDownloaded,
                    TotalBytes = totalKnownBytes > 0 ? totalKnownBytes : combinedDownloaded,
                    SpeedBytesPerSecond = 0,
                    RemainingSeconds = 0,
                    ServerSupportsResume = true,
                    ActiveConnections = 1,
                    IsCompleted = false
                });

                // Merge using FFmpeg
                string? exe = await MediaDependencyManager.Instance.GetValidatedFfmpegPathAsync(ffmpegPath, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(exe))
                {
                    throw new FileNotFoundException("FFmpeg is required to merge separate video and audio streams into the final output file, but was not found on your system. Please install FFmpeg or place ffmpeg.exe in the EDM tools folder (%LOCALAPPDATA%\\EDM\\tools).");
                }

                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                psi.ArgumentList.Add("-y");
                psi.ArgumentList.Add("-i");
                psi.ArgumentList.Add(tempVideo);
                psi.ArgumentList.Add("-i");
                psi.ArgumentList.Add(tempAudio);
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add("copy");
                psi.ArgumentList.Add(outputPath);

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                    if (proc.ExitCode != 0)
                    {
                        string err = await proc.StandardError.ReadToEndAsync().ConfigureAwait(false);
                        LoggingService.Log($"[MediaMergeService] FFmpeg exit code {proc.ExitCode}: {err}");
                        throw new InvalidOperationException($"FFmpeg failed with exit code {proc.ExitCode}: {err}");
                    }
                }
                else
                {
                    throw new InvalidOperationException("Failed to launch FFmpeg process. Ensure FFmpeg is installed and accessible.");
                }

                // Final file verification on disk
                if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
                {
                    throw new InvalidOperationException("Final merged media file is missing or 0 bytes.");
                }

                LoggingService.Log($"[MediaMergeService] Merge successful: '{outputPath}' ({new FileInfo(outputPath).Length} bytes)");
            }
            catch (OperationCanceledException)
            {
                LoggingService.Log("[MediaMergeService] Download and merge cancelled by user.");
                throw;
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[MediaMergeService] Adaptive merge failed for '{outputPath}'", ex);
                throw;
            }
            finally
            {
                // Safely clean up temporary stream chunk files
                try { if (File.Exists(tempVideo)) File.Delete(tempVideo); } catch { }
                try { if (File.Exists(tempAudio)) File.Delete(tempAudio); } catch { }
            }
        }

        private static void ReportCombinedProgress(
            IProgress<DownloadProgressInfo>? progress,
            long vidDownloaded,
            long audDownloaded,
            long totalKnownBytes,
            SpeedTracker speedTracker)
        {
            if (progress == null) return;

            long combinedDownloaded = vidDownloaded + audDownloaded;
            if (!speedTracker.ShouldReport(combinedDownloaded, out double speed))
            {
                return;
            }

            double percentage = totalKnownBytes > 0 ? Math.Clamp(((double)combinedDownloaded / totalKnownBytes) * 99.0, 0.0, 99.0) : 0.0;
            double remainingSeconds = (totalKnownBytes > combinedDownloaded && speed > 0) ? (totalKnownBytes - combinedDownloaded) / speed : -1;

            progress.Report(new DownloadProgressInfo
            {
                Status = $"Downloading Video & Audio ({combinedDownloaded / (1024.0 * 1024.0):F1} MB)...",
                BytesReceived = combinedDownloaded,
                TotalBytes = totalKnownBytes > 0 ? totalKnownBytes : null,
                ProgressPercentage = percentage,
                SpeedBytesPerSecond = speed,
                RemainingSeconds = remainingSeconds,
                ActiveConnections = 2,
                ServerSupportsResume = true,
                IsCompleted = false
            });
        }
    }

    internal class SpeedTracker
    {
        private long _lastBytes = 0;
        private long _lastTicks = Environment.TickCount64;
        private long _lastReportTicks = 0;
        private double _smoothedSpeed = 0;
        private readonly object _lock = new();

        public bool ShouldReport(long currentBytes, out double speed)
        {
            lock (_lock)
            {
                long now = Environment.TickCount64;
                long elapsedMs = now - _lastTicks;
                if (elapsedMs >= 100)
                {
                    long bytesDelta = Math.Max(0, currentBytes - _lastBytes);
                    double currentInstSpeed = (bytesDelta / (double)elapsedMs) * 1000.0;

                    if (_smoothedSpeed <= 0)
                    {
                        _smoothedSpeed = currentInstSpeed;
                    }
                    else
                    {
                        // Exponential smoothing (alpha = 0.3)
                        _smoothedSpeed = (_smoothedSpeed * 0.7) + (currentInstSpeed * 0.3);
                    }

                    _lastBytes = currentBytes;
                    _lastTicks = now;
                }

                speed = _smoothedSpeed;
                if (now - _lastReportTicks >= 100)
                {
                    _lastReportTicks = now;
                    return true;
                }
                return false;
            }
        }

        public double UpdateAndGetSpeed(long currentBytes)
        {
            lock (_lock)
            {
                long now = Environment.TickCount64;
                long elapsedMs = now - _lastTicks;
                if (elapsedMs < 100) return _smoothedSpeed;

                long bytesDelta = Math.Max(0, currentBytes - _lastBytes);
                double currentInstSpeed = (bytesDelta / (double)elapsedMs) * 1000.0;

                if (_smoothedSpeed <= 0)
                {
                    _smoothedSpeed = currentInstSpeed;
                }
                else
                {
                    // Exponential smoothing (alpha = 0.3)
                    _smoothedSpeed = (_smoothedSpeed * 0.7) + (currentInstSpeed * 0.3);
                }

                _lastBytes = currentBytes;
                _lastTicks = now;
                return _smoothedSpeed;
            }
        }
    }
}
