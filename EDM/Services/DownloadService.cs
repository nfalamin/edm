using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services.Helpers;
using EDM.Models;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    public class DownloadService : IDownloadService
    {
        // Helper throttled progress wrapper to coalesce frequent progress updates and avoid UI saturation.
        internal sealed class ThrottledProgress : IProgress<DownloadProgressInfo>, IDisposable
        {
            private readonly IProgress<DownloadProgressInfo> _inner;
            private readonly TimeSpan _interval;
            private readonly object _sync = new();
            private DownloadProgressInfo? _pending;
            private DateTime _lastSent = DateTime.MinValue;
            private System.Threading.Timer? _timer;

            public ThrottledProgress(IProgress<DownloadProgressInfo> inner, TimeSpan interval)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _interval = interval;
            }

            public IProgress<DownloadProgressInfo> AsProgress() => this;

            public void Report(DownloadProgressInfo info)
            {
                lock (_sync)
                {
                    _pending = info;
                    var now = DateTime.UtcNow;
                    if ((now - _lastSent) >= _interval)
                    {
                        _lastSent = now;
                        var toSend = _pending;
                        _pending = null;
                        try { _inner.Report(toSend!); } catch (Exception ex) { LoggingService.Log($"[ThrottledProgress.Report] Failed: {ex.Message}"); }
                    }
                    else
                    {
                        var delay = _interval - (now - _lastSent);
                        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
                        if (_timer == null)
                        {
                            _timer = new System.Threading.Timer(_ => SendPendingSafe(), null, delay, System.Threading.Timeout.InfiniteTimeSpan);
                        }
                        else
                        {
                            try { _timer.Change(delay, System.Threading.Timeout.InfiniteTimeSpan); } catch { }
                        }
                    }
                }
            }

            private void SendPendingSafe()
            {
                try { SendPending(); } catch (Exception ex) { LoggingService.Log($"[ThrottledProgress.Timer] Error: {ex.Message}"); }
            }

            private void SendPending()
            {
                lock (_sync)
                {
                    if (_pending != null)
                    {
                        var send = _pending;
                        _pending = null;
                        _lastSent = DateTime.UtcNow;
                        try { _inner.Report(send); } catch (Exception ex) { LoggingService.Log($"[ThrottledProgress.Report-Delayed] Failed: {ex.Message}"); }
                    }
                    try { _timer?.Dispose(); } catch { }
                    _timer = null;
                }
            }

            public void Dispose()
            {
                lock (_sync)
                {
                    if (_pending != null)
                    {
                        var send = _pending;
                        _pending = null;
                        _lastSent = DateTime.UtcNow;
                        try { _inner.Report(send); } catch { }
                    }
                    try { _timer?.Dispose(); } catch { }
                    _timer = null;
                }
            }
        }
        private HttpClient _httpClient;
        private readonly bool _usesSharedHttpClient;
        private readonly YtDlpService? _ytDlp;
        private readonly INetworkService _networkService;
        private readonly ISettingsService _settingsService;
        private readonly AdaptiveConnectionManager _adaptiveManager;
        private readonly AdaptiveChunkSizer _adaptiveChunkSizer;
        private readonly MediaMergeService _mediaMergeService;
        private HttpProbeService _probeService;
        private DownloadOrchestrator _orchestrator;
        private const int TotalSegments = 8;

        public DownloadService(HttpClient? client = null, INetworkService? networkService = null, ISettingsService? settingsService = null, MediaMergeService? mediaMergeService = null)
        {
            _usesSharedHttpClient = client == null;
            _httpClient = client ?? Services.SharedHttpClient.Instance;
            _networkService = networkService ?? new NetworkService(settingsService ?? App.ServiceProvider?.GetService(typeof(EDM.Services.Interfaces.ISettingsService)) as ISettingsService ?? new SettingsService());
            _settingsService = settingsService ?? App.ServiceProvider?.GetService(typeof(EDM.Services.Interfaces.ISettingsService)) as ISettingsService ?? new SettingsService();
            _adaptiveManager = new AdaptiveConnectionManager(_settingsService, _networkService);
            _adaptiveChunkSizer = new AdaptiveChunkSizer(_settingsService, _networkService);

            try
            {
                _httpClient ??= Services.SharedHttpClient.Instance;
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) EDM/1.0");
            }
            catch (Exception ex) { LoggingService.Log($"[DownloadService.ctor] Failed to set User-Agent header: {ex.Message}"); }

            try
            {
                _ytDlp = new YtDlpService();
            }
            catch
            {
                _ytDlp = null;
            }

            _mediaMergeService = mediaMergeService ?? App.ServiceProvider?.GetService(typeof(EDM.Services.MediaMergeService)) as MediaMergeService ?? new MediaMergeService(_httpClient);
            _probeService = new HttpProbeService(_httpClient);
            _orchestrator = new DownloadOrchestrator(_httpClient, _ytDlp, _probeService, _networkService, _settingsService);
            _diagnosticEnabled = string.Equals(Environment.GetEnvironmentVariable("EDM_DIAGNOSTIC"), "1", StringComparison.OrdinalIgnoreCase);
        }

        private readonly bool _diagnosticEnabled;

        public void RefreshHttpClient()
        {
            if (!_usesSharedHttpClient) return;
            try
            {
                _httpClient = Services.SharedHttpClient.Instance;
                _httpClient ??= Services.SharedHttpClient.Instance;
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) EDM/1.0");
                _probeService = new HttpProbeService(_httpClient);
                _orchestrator = new DownloadOrchestrator(_httpClient, _ytDlp, _probeService, _networkService, _settingsService);
                LoggingService.Log("[DownloadService.RefreshHttpClient] Re-bound to current SharedHttpClient instance (proxy settings applied).");
            }
            catch (Exception ex) { LoggingService.Log($"[DownloadService.RefreshHttpClient] Failed: {ex.Message}"); }
        }

        /// <summary>
        /// Gets the effective segment count based on network type and user preferences.
        /// </summary>
        private int GetEffectiveSegmentCount()
        {
            try
            {
                var userOverride = _settingsService.GetConnectionLimitOverride();
                var recommendedCount = _networkService.GetRecommendedConnectionCount(userOverride);
                return Math.Min(recommendedCount, TotalSegments);
            }
            catch (Exception ex)
            {
                // Fallback to default if network detection fails
                LoggingService.Log($"[GetEffectiveSegmentCount] Network detection failed, using default segment count: {ex.Message}");
                return TotalSegments;
            }
        }

        // Diagnostic helper that writes step-by-step details to edm.log when diagnostic mode enabled
        private void DiagnosticLog(string message)
        {
            if (!_diagnosticEnabled) return;
            try
            {
                var tag = "[DIAG]";
                LoggingService.Log($"{tag} {message}");
            }
            catch (Exception ex) { /* Silently ignore logging failures in diagnostic mode */ System.Diagnostics.Debug.WriteLine($"DiagnosticLog error: {ex.Message}"); }
        }

        // Try to infer MaxConnectionsPerServer if using SocketsHttpHandler via reflection (best-effort, non-throwing)
        private static int GetMaxConnectionsPerServer()
        {
            try
            {
                var field = typeof(HttpClient).GetField("handler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field == null) return -1;
                // best-effort: cannot reliably access handler from static context; return -1
                return -1;
            }
            catch (Exception ex) { /* Reflection best-effort, return default */ return -1; }
        }

        private static bool TryCreateHttpUri(string url, out Uri? uri)
        {
            return FileNamingHelper.TryCreateHttpUri(url, out uri);
        }

        // Try to derive a sensible file name (with extension) from HTTP response headers or the request URI.
        private static string DetermineFileNameFromResponse(System.Net.Http.HttpResponseMessage? response, Uri requestUri)
        {
            return FileNamingHelper.DetermineFileNameFromResponse(response, requestUri);
        }

        private static string SanitizeFileName(string name)
        {
            return FileNamingHelper.SanitizeFileName(name);
        }

        private static string GetExtensionFromMime(string mime)
        {
            return FileNamingHelper.GetExtensionFromMime(mime);
        }

        // Download two adaptive streams (video-only and audio-only) and merge using ffmpeg
        public async Task DownloadAndMergeAdaptiveStreamsAsync(string videoStreamUrl, string audioStreamUrl, string outputPath, string ffmpegPath, CancellationToken cancellationToken)
        {
            await _mediaMergeService.MergeAudioVideoAsync(videoStreamUrl, audioStreamUrl, outputPath, string.IsNullOrEmpty(ffmpegPath) ? null : ffmpegPath, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Detects if the URL belongs to a video or streaming site supported by yt-dlp.
        /// </summary>
        public static bool IsVideoStreamingUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            try
            {
                if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;
                string host = uri.Host.ToLowerInvariant();

                string[] streamingDomains = new[]
                {
                    "youtube.com", "youtu.be", "vimeo.com", "dailymotion.com", "dai.ly",
                    "facebook.com", "fb.watch", "instagram.com", "instagr.am", "tiktok.com",
                    "twitch.tv", "twitter.com", "x.com", "bilibili.com", "rumble.com",
                    "soundcloud.com", "vk.com", "nicovideo.jp", "streamable.com"
                };

                foreach (var domain in streamingDomains)
                {
                    if (host == domain || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        // Start a download for the provided DownloadItem. Delegates to DownloadOrchestrator.
        public async Task StartDownloadAsync(
            DownloadItem item,
            IProgress<DownloadProgressInfo> progressReporter,
            PauseTokenSource pauseToken,
            Func<double> speedLimitProvider,
            CancellationToken cancellationToken,
            int? segmentCount = null)
        {
            await _orchestrator.StartDownloadAsync(
                item,
                progressReporter,
                pauseToken,
                speedLimitProvider,
                cancellationToken,
                segmentCount,
                DiagnosticLog).ConfigureAwait(false);
        }

        // Start a download for the provided url. Delegates to DownloadOrchestrator.
        public async Task StartDownloadAsync(
            string url,
            string savePath,
            IProgress<DownloadProgressInfo> progressReporter,
            PauseTokenSource pauseToken,
            Func<double> speedLimitProvider,
            CancellationToken cancellationToken,
            int? segmentCount = null,
            DownloadCredentials? credentials = null,
            string? cookies = null)
        {
            try
            {
                await _orchestrator.StartDownloadAsync(
                    url,
                    savePath,
                    progressReporter,
                    pauseToken,
                    speedLimitProvider,
                    cancellationToken,
                    segmentCount,
                    credentials,
                    cookies,
                    DiagnosticLog).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Status already reported via progressReporter
            }
            catch (Exception ex)
            {
                // Status already reported via progressReporter
                LoggingService.Log($"[DownloadService] Download completed with status error: {ex.Message}");
            }
        }

        // Implementation of IDownloadService.StartDownloadAsync with getRetryCount
        public async Task StartDownloadAsync(
            string url,
            string savePath,
            IProgress<DownloadProgressInfo> progress,
            PauseTokenSource pauseToken,
            Func<int> getRetryCount,
            CancellationToken cancellationToken)
        {
            await StartDownloadAsync(
                url,
                savePath,
                progress,
                pauseToken,
                () => 0.0,
                cancellationToken).ConfigureAwait(false);
        }

        // Internal single-thread download runner used by DownloadOrchestrator
        internal static async Task RunSingleThreadedDownloadInternalAsync(
            HttpClient httpClient,
            string url,
            string savePath,
            long? totalBytes,
            IProgress<DownloadProgressInfo> progressReporter,
            PauseTokenSource pauseToken,
            Func<double> speedLimitProvider,
            CancellationToken cancellationToken,
            DownloadCredentials? credentials = null)
        {
            var info = new DownloadProgressInfo { TotalBytes = totalBytes, ServerSupportsResume = false, Status = "Single-threaded Downloading..." };

            long totalReadBytes = 0;
            string tempDest = savePath + ".tmpdl";
            try
            {
                using var singleReq = new HttpRequestMessage(HttpMethod.Get, url);
                if (credentials != null && !credentials.IsEmpty)
                {
                    singleReq.Headers.Authorization = credentials.ToBasicAuthHeader();
                }
                using (var response = await httpClient.SendAsync(singleReq, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();

                    using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                    using (var fileStream = new FileStream(tempDest, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, true))
                    {
                    byte[]? buffer = null;
                    try
                    {
                        buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(128 * 1024);
                        int bytesRead;

                        var stopwatch = Stopwatch.StartNew();
                        long lastReportedBytes = 0;
                        var lastReportedTime = DateTime.UtcNow;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            await pauseToken.WaitIfPausedAsync();
                            var loopWatch = Stopwatch.StartNew();
                            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                            // speed throttling best-effort
                            try
                            {
                                double maxSpeed = speedLimitProvider?.Invoke() ?? -1;
                                if (maxSpeed > 0)
                                {
                                    double elapsedMs = loopWatch.Elapsed.TotalMilliseconds;
                                    double targetMs = (bytesRead / maxSpeed) * 1000.0;
                                    double delayMs = targetMs - elapsedMs;
                                    if (delayMs > 1)
                                        await Task.Delay((int)delayMs, cancellationToken);
                                }
                            }

                                catch (Exception ex) { LoggingService.LogException("[DownloadService] Throttle delay failed", ex); }
                            loopWatch.Reset();
                            totalReadBytes += bytesRead;

                            info.BytesReceived = totalReadBytes;
                            if (totalBytes.HasValue && totalBytes.Value > 0)
                            {
                                info.ProgressPercentage = (double)totalReadBytes / totalBytes.Value * 100;
                            }

                            var now = DateTime.UtcNow;
                            var elapsed = now - lastReportedTime;

                            if (elapsed.TotalMilliseconds >= 500)
                            {
                                long bytesInInterval = totalReadBytes - lastReportedBytes;
                                info.SpeedBytesPerSecond = bytesInInterval / elapsed.TotalSeconds;

                                if (totalBytes.HasValue && info.SpeedBytesPerSecond > 0)
                                {
                                    long remainingBytes = totalBytes.Value - totalReadBytes;
                                    info.RemainingSeconds = remainingBytes / info.SpeedBytesPerSecond;
                                }

                                lastReportedBytes = totalReadBytes;
                                lastReportedTime = now;

                                progressReporter.Report(info);
                            }
                        }
                    }
                    finally
                    {
                        if (buffer != null) System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                    }

                    try { await fileStream.FlushAsync(cancellationToken); } catch (Exception ex) { LoggingService.LogException("[DownloadService] fileStream.FlushAsync failed", ex); }
                    }
                }

                // move into final location with retries
                // Ensure destination directory exists
                try
                {
                    var destDir = Path.GetDirectoryName(savePath);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                }
                catch (Exception ex)
                {
                    LoggingService.Log($"Failed to ensure destination directory exists: {ex.Message}");
                }

                bool moved = false;
                for (int attempt = 0; attempt < 5 && !moved; attempt++)
                {
                    try
                    {
                        if (File.Exists(savePath))
                        {
                            try { File.Delete(savePath); } catch (Exception ex) { LoggingService.LogException("[DownloadService] Deleting existing destination failed", ex); }
                        }
                        File.Move(tempDest, savePath);
                        moved = true;
                        LoggingService.Log($"Moved temp file to destination: {savePath}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Log($"Move attempt {attempt} failed: {ex.Message}");
                        try { await Task.Delay(200 * (attempt + 1), cancellationToken); } catch (Exception delayEx) { LoggingService.LogException("[DownloadService] Move retry delay failed", delayEx); }
                    }
                }

                if (!moved)
                {
                    // Try copy fallback and log
                    try
                    {
                        File.Copy(tempDest, savePath, true);
                        moved = true;
                        LoggingService.Log($"Copied temp file to destination as fallback: {savePath}");
                    }
                    catch (Exception ex)
                    {
                        moved = false;
                        LoggingService.Log($"Copy fallback failed: {ex.Message}");
                    }
                }

                if (!moved)
                {
                    LoggingService.Log($"Failed to move downloaded file to final destination: '{savePath}'");
                    throw new IOException($"Failed to move downloaded file to final destination: '{savePath}'");
                }
            }
            finally
            {
                try { if (File.Exists(tempDest)) File.Delete(tempDest); } catch (Exception ex) { LoggingService.LogException("[DownloadService] Deleting tempDest failed", ex); }
            }
            // ensure final state is reported
            info.BytesReceived = totalReadBytes;
            if (totalBytes.HasValue && totalBytes.Value > 0)
                info.ProgressPercentage = 100;
            info.SpeedBytesPerSecond = 0;
            info.IsCompleted = true;
            info.Status = "Finished";
            progressReporter.Report(info);
        }

        private void CleanUpTempFiles(string savePath)
        {
            try
            {
                // remove any .part temporary files created during segmented download
                var dir = Path.GetDirectoryName(savePath) ?? ".";
                var baseName = Path.GetFileName(savePath);
                var parts = Directory.EnumerateFiles(dir, baseName + ".part*");
                foreach (var p in parts)
                {
                    FileDeleteHelper.DeleteFileSafe(p);
                }
                // remove metadata directory if exists
                var metaDir = Path.Combine(dir, ".tmp_" + baseName);
                try { if (Directory.Exists(metaDir)) Directory.Delete(metaDir, true); } catch (Exception ex) { LoggingService.LogException("[DownloadService] Deleting metaDir failed", ex); }
            }
            catch { }
        }

        private void CancelDownload(string savePath)
        {
            try
            {
                // only remove temporary parts on cancel, do not remove final file by default
                var dir = Path.GetDirectoryName(savePath) ?? ".";
                var baseName = Path.GetFileName(savePath);
                var parts = Directory.EnumerateFiles(dir, baseName + ".part*");
                foreach (var p in parts)
                {
                    FileDeleteHelper.DeleteFileSafe(p);
                }
            }
            catch { }
        }

        /// <summary>
        /// Public helper to cancel and cleanup temp/metadata files for a download. This does not cancel in-flight tasks owned by the caller (UI should cancel its CTS),
        /// but it removes temporary files and metadata so the download won't resume.
        /// </summary>
        public void CancelAndCleanup(string savePath)
        {
            try
            {
                // Remove temp part files and meta directories
                CleanUpTempFiles(savePath);

                // Remove persisted segmented metadata (.edm.json, .bak, and .tmp)
                try
                {
                    var meta = savePath + ".edm.json";
                    FileDeleteHelper.DeleteFileSafe(meta);
                    FileDeleteHelper.DeleteFileSafe(meta + ".tmp");
                    FileDeleteHelper.DeleteFileSafe(meta + ".bak");
                    FileDeleteHelper.DeleteFileSafe(savePath + ".merging");
                    FileDeleteHelper.DeleteFileSafe(savePath + ".tmpdl");
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[DownloadService] Failed deleting metadata/temp files", ex);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadService] CancelAndCleanup failed", ex);
            }
        }
    }
}
