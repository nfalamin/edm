using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    /// <summary>
    /// DownloadOrchestrator — Central authoritative download coordinator.
    /// Manages media download routing: dual-stream adaptive merging via MediaMergeService,
    /// streaming video detection, HTTP probing with 206 range verification,
    /// SQLite history recording, and segmented vs single-threaded download engine execution.
    /// </summary>
    public class DownloadOrchestrator
    {
        private readonly HttpClient _httpClient;
        private readonly YtDlpService? _ytDlp;
        private readonly HttpProbeService _probeService;
        private readonly INetworkService _networkService;
        private readonly ISettingsService _settingsService;
        private readonly AdaptiveConnectionManager _adaptiveManager;
        private readonly MediaMergeService _mediaMergeService;
        private readonly MediaVariantResolver _mediaVariantResolver;
        private readonly ControlPlaneClient? _controlPlaneClient;
        private readonly ControlPlaneTelemetryService? _telemetryService;

        public DownloadOrchestrator(
            HttpClient? httpClient = null,
            YtDlpService? ytDlp = null,
            HttpProbeService? probeService = null,
            INetworkService? networkService = null,
            ISettingsService? settingsService = null,
            MediaMergeService? mediaMergeService = null,
            ControlPlaneClient? controlPlaneClient = null,
            ControlPlaneTelemetryService? telemetryService = null,
            MediaVariantResolver? mediaVariantResolver = null)
        {
            _httpClient = httpClient ?? SharedHttpClient.Instance;
            _ytDlp = ytDlp ?? new YtDlpService();
            _probeService = probeService ?? new HttpProbeService(_httpClient);
            _settingsService = settingsService ?? App.ServiceProvider?.GetService(typeof(ISettingsService)) as ISettingsService ?? new SettingsService();
            _networkService = networkService ?? new NetworkService(_settingsService);
            _adaptiveManager = new AdaptiveConnectionManager(_settingsService, _networkService);
            _mediaMergeService = mediaMergeService ?? new MediaMergeService(_httpClient);
            _mediaVariantResolver = mediaVariantResolver ?? new MediaVariantResolver(_ytDlp);
            _controlPlaneClient = controlPlaneClient ?? App.ServiceProvider?.GetService(typeof(ControlPlaneClient)) as ControlPlaneClient;
            _telemetryService = telemetryService ?? App.ServiceProvider?.GetService(typeof(ControlPlaneTelemetryService)) as ControlPlaneTelemetryService;
        }

        public async Task StartDownloadAsync(
            DownloadItem item,
            IProgress<DownloadProgressInfo> progressReporter,
            PauseTokenSource pauseToken,
            Func<double> speedLimitProvider,
            CancellationToken cancellationToken,
            int? segmentCount = null,
            Action<string>? diagnosticLogger = null)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            // Check if dual-stream adaptive merge is required
            if (item.RequiresFfmpegMerge && !string.IsNullOrWhiteSpace(item.VideoUrl) && !string.IsNullOrWhiteSpace(item.AudioUrl))
            {
                LoggingService.Log($"[DownloadOrchestrator] Routing adaptive media item '{item.FileName}' ({item.Quality}) to MediaMergeService.");
                
                string ffmpegPath = _settingsService.GetFfmpegPath();
                await _mediaMergeService.MergeAudioVideoAsync(
                    item.VideoUrl,
                    item.AudioUrl,
                    item.SavePath,
                    ffmpegPath,
                    cancellationToken,
                    progressReporter,
                    pauseToken,
                    speedLimitProvider,
                    item.EstimatedSizeBytes > 0 ? (long)(item.EstimatedSizeBytes * 0.85) : -1,
                    item.EstimatedSizeBytes > 0 ? (long)(item.EstimatedSizeBytes * 0.15) : -1
                ).ConfigureAwait(false);

                // Final file verification
                if (!File.Exists(item.SavePath) || new FileInfo(item.SavePath).Length == 0)
                {
                    throw new InvalidOperationException($"Final output file '{item.SavePath}' was not created or is 0 bytes.");
                }

                var finalInfo = new DownloadProgressInfo
                {
                    Status = "Finished",
                    ProgressPercentage = 100.0,
                    BytesReceived = new FileInfo(item.SavePath).Length,
                    TotalBytes = new FileInfo(item.SavePath).Length,
                    IsCompleted = true,
                    ServerSupportsResume = true
                };
                progressReporter.Report(finalInfo);
                return;
            }

            // Normal single-stream or direct URL download
            string effectiveUrl = !string.IsNullOrWhiteSpace(item.VideoUrl) ? item.VideoUrl : item.Url;
            await StartDownloadAsync(
                effectiveUrl,
                item.SavePath,
                progressReporter,
                pauseToken,
                speedLimitProvider,
                cancellationToken,
                segmentCount,
                item.BuildCredentials(),
                item.Cookies,
                diagnosticLogger
            ).ConfigureAwait(false);
        }

        // Start a download for the provided url. Supports automatic protocol classification,
        // adaptive multi-segment range downloads, YouTube/streaming download via YtDlpService,
        // BitTorrent/Magnet URIs, and FTP/FTPS endpoints.
        public async Task StartDownloadAsync(
            string url,
            string savePath,
            IProgress<DownloadProgressInfo> progressReporter,
            PauseTokenSource pauseToken,
            Func<double> speedLimitProvider,
            CancellationToken cancellationToken,
            int? segmentCount = null,
            DownloadCredentials? credentials = null,
            string? cookies = null,
            Action<string>? diagnosticLogger = null)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("url is required", nameof(url));
            if (string.IsNullOrWhiteSpace(savePath)) throw new ArgumentException("savePath is required", nameof(savePath));
            if (progressReporter == null) throw new ArgumentNullException(nameof(progressReporter));
            if (pauseToken == null) throw new ArgumentNullException(nameof(pauseToken));

            LoggingService.Log($"[DownloadOrchestrator] Starting download: url={url}, savePath={savePath}");

            // Pre-download Security & URL Validation Check
            if (!DownloadSecurityPipeline.Instance.ValidateUrl(url, out var urlValidationError))
            {
                var blockedInfo = new DownloadProgressInfo
                {
                    Status = "Security Blocked",
                    ErrorMessage = urlValidationError
                };
                progressReporter.Report(blockedInfo);
                return;
            }

            // Server-Authoritative Ban / Account Suspension Check
            if (_controlPlaneClient != null && _controlPlaneClient.CurrentSecurityState == AccountSecurityState.Suspended)
            {
                var suspendedInfo = new DownloadProgressInfo
                {
                    Status = "Account Suspended",
                    ErrorMessage = "Account has been suspended by an administrator. New downloads are blocked."
                };
                progressReporter.Report(suspendedInfo);
                return;
            }

            var info = new DownloadProgressInfo { Status = "Connecting..." };
            progressReporter.Report(info);

            // Composite speed provider combining app throttle and user custom speed limit
            Func<double> combinedSpeedProvider = () =>
            {
                var kbps = _settingsService?.GetActiveBandwidthLimitKbps() ?? 0;
                double appLimit = kbps > 0 ? kbps * 1024.0 : -1;
                double userLimit = speedLimitProvider != null ? speedLimitProvider() : -1;
                if (appLimit <= 0 && userLimit <= 0) return -1;
                if (appLimit > 0 && userLimit > 0) return Math.Min(appLimit, userLimit);
                return appLimit > 0 ? appLimit : userLimit;
            };

            // Network monitoring adapter
            DownloadNetworkMonitorAdapter? networkAdapter = null;
            try
            {
                if (_networkService is INetworkMonitor monitor)
                {
                    networkAdapter = new DownloadNetworkMonitorAdapter(monitor, pauseToken, diagnosticLogger);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DownloadOrchestrator] Network monitor adapter setup failed: {ex.Message}");
            }

            try
            {
                long historyId = -1;

                // 0a. Check BitTorrent & Magnet Link URIs
                if (BitTorrentService.IsBitTorrentUrl(url))
                {
                    LoggingService.Log($"[DownloadOrchestrator] BitTorrent / Magnet URL detected. Routing to BitTorrentService: {url}");
                    info.Status = "Initializing BitTorrent Service...";
                    progressReporter.Report(info);

                    var btService = new BitTorrentService();
                    await btService.DownloadTorrentOrMagnetAsync(url, savePath, progressReporter, pauseToken, combinedSpeedProvider, cancellationToken).ConfigureAwait(false);
                    return;
                }

                // 0b. Check FTP / FTPS Protocol URIs
                if (Uri.TryCreate(url, UriKind.Absolute, out var parsedUri) && (parsedUri.Scheme.Equals("ftp", StringComparison.OrdinalIgnoreCase) || parsedUri.Scheme.Equals("ftps", StringComparison.OrdinalIgnoreCase)))
                {
                    LoggingService.Log($"[DownloadOrchestrator] FTP/FTPS Protocol URL detected. Routing to FtpDownloadService: {url}");
                    info.Status = "Connecting to FTP Server...";
                    progressReporter.Report(info);

                    var ftpService = new FtpDownloadService();
                    System.Net.NetworkCredential? netCred = credentials != null ? new System.Net.NetworkCredential(credentials.Username, credentials.Password) : null;
                    await ftpService.DownloadFtpAsync(url, savePath, segmentCount ?? 4, progressReporter, pauseToken, combinedSpeedProvider, cancellationToken, netCred).ConfigureAwait(false);
                    return;
                }

                // 0c. Check HLS (.m3u8) / DASH (.mpd) manifest streams
                if (url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase) || url.Contains(".mpd", StringComparison.OrdinalIgnoreCase))
                {
                    bool isHls = url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase);
                    LoggingService.Log($"[DownloadOrchestrator] {(isHls ? "HLS" : "DASH")} manifest URL detected. Routing to HlsDashDownloadService: {url}");
                    info.Status = isHls ? "Downloading HLS Stream..." : "Downloading DASH Stream...";
                    progressReporter.Report(info);

                    var hlsDash = new HlsDashDownloadService();
                    var hlsProgress = new Progress<double>(p =>
                    {
                        progressReporter.Report(new DownloadProgressInfo
                        {
                            ProgressPercentage = Math.Clamp(p, 0.0, 100.0),
                            Status = isHls ? $"Downloading HLS Stream ({p:F1}%)..." : $"Downloading DASH Stream ({p:F1}%)...",
                            ActiveConnections = 8,
                            ServerSupportsResume = true,
                            IsCompleted = p >= 100
                        });
                    });

                    if (isHls)
                    {
                        await hlsDash.DownloadHlsStreamAsync(url, savePath, cookies, hlsProgress, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await hlsDash.DownloadDashStreamAsync(url, savePath, cookies, hlsProgress, cancellationToken).ConfigureAwait(false);
                    }
                    return;
                }

                // 1. Detect streaming / video site URL
                if (DownloadService.IsVideoStreamingUrl(url))
                {
                    LoggingService.Log($"[DownloadOrchestrator] Video streaming site detected: {url}");
                    info.Status = "Resolving video stream...";
                    progressReporter.Report(info);

                    try { historyId = Services.History.DownloadHistoryRecorder.CreateEntry(url, savePath, -1); } catch { }

                    Exception? streamError = null;

                    // A. Attempt Native Stream Resolution via MediaVariantResolver
                    try
                    {
                        var variantResult = await _mediaVariantResolver.ResolveVariantsAsync(url, cookies: cookies, cancellationToken: cancellationToken).ConfigureAwait(false);
                        if (variantResult.Success && variantResult.Variants.Any())
                        {
                            var best = variantResult.Variants.OrderByDescending(v => v.Height).ThenByDescending(v => v.Bitrate).First();

                            if (best.RequiresFfmpegMerge && !string.IsNullOrWhiteSpace(best.DirectUrl) && !string.IsNullOrWhiteSpace(best.AudioStreamUrl))
                            {
                                LoggingService.Log($"[DownloadOrchestrator] Resolved dual-stream adaptive media ({best.QualityLabel}). Routing to MediaMergeService.");
                                string ffmpegPath = _settingsService.GetFfmpegPath();
                                await _mediaMergeService.MergeAudioVideoAsync(
                                    best.DirectUrl,
                                    best.AudioStreamUrl,
                                    savePath,
                                    ffmpegPath,
                                    cancellationToken,
                                    progressReporter,
                                    pauseToken,
                                    combinedSpeedProvider,
                                    best.EstimatedSizeBytes > 0 ? (long)(best.EstimatedSizeBytes * 0.85) : -1,
                                    best.EstimatedSizeBytes > 0 ? (long)(best.EstimatedSizeBytes * 0.15) : -1
                                ).ConfigureAwait(false);

                                if (File.Exists(savePath) && new FileInfo(savePath).Length > 0)
                                {
                                    info.ProgressPercentage = 100.0;
                                    info.Status = "Finished";
                                    info.IsCompleted = true;
                                    progressReporter.Report(info);
                                    if (historyId > 0) Services.History.DownloadHistoryRecorder.MarkCompleted(historyId);
                                    return;
                                }
                            }
                            else if (!string.IsNullOrWhiteSpace(best.DirectUrl))
                            {
                                LoggingService.Log($"[DownloadOrchestrator] Resolved direct stream URL ({best.QualityLabel}). Routing to progressive download.");
                                url = best.DirectUrl;
                            }
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        streamError = ex;
                        LoggingService.LogWarning($"[DownloadOrchestrator] Native stream resolution failed: {ex.Message}");
                    }

                    // B. Fallback to YtDlpService if native stream did not provide direct stream URL
                    if (_ytDlp != null && DownloadService.IsVideoStreamingUrl(url))
                    {
                        using var throttledYt = new DownloadService.ThrottledProgress(progressReporter, TimeSpan.FromMilliseconds(200));
                        IProgress<DownloadProgressInfo> effectiveProgressYt = throttledYt.AsProgress();

                        try
                        {
                            await _ytDlp.DownloadAsync(
                                url,
                                savePath,
                                formatArg: "",
                                progress: (percent, statusLine) =>
                                {
                                    var dlInfo = new DownloadProgressInfo
                                    {
                                        ProgressPercentage = Math.Clamp(percent, 0.0, 100.0),
                                        Status = string.IsNullOrWhiteSpace(statusLine) ? $"Downloading ({percent}%)..." : statusLine,
                                        IsCompleted = percent >= 100,
                                        ServerSupportsResume = true
                                    };

                                    if (YtDlpOutputParser.TryParseProgress(statusLine, out var parsedPct, out var totalBytes, out var bytesReceived, out var speedBps, out var etaSec))
                                    {
                                        if (parsedPct > 0) dlInfo.ProgressPercentage = Math.Clamp(parsedPct, 0.0, 100.0);
                                        if (totalBytes > 0) dlInfo.TotalBytes = totalBytes;
                                        if (bytesReceived > 0) dlInfo.BytesReceived = bytesReceived;
                                        if (speedBps > 0) dlInfo.SpeedBytesPerSecond = speedBps;
                                        if (etaSec > 0) dlInfo.RemainingSeconds = etaSec;
                                    }

                                    effectiveProgressYt.Report(dlInfo);

                                    if (historyId > 0)
                                    {
                                        BackgroundTaskManager.FireAndForget("HistoryUpdateYtDlp", async () =>
                                        {
                                            try
                                            {
                                                Services.History.DownloadHistoryRecorder.UpdateProgress(historyId, dlInfo.BytesReceived, dlInfo.TotalBytes ?? -1, dlInfo.SpeedBytesPerSecond);
                                                if (percent >= 100) Services.History.DownloadHistoryRecorder.MarkCompleted(historyId);
                                                await Task.CompletedTask;
                                            }
                                            catch { }
                                        });
                                    }
                                },
                                cancellationToken
                            ).ConfigureAwait(false);

                            if (File.Exists(savePath) && new FileInfo(savePath).Length > 0)
                            {
                                info.ProgressPercentage = 100;
                                info.Status = "Finished";
                                info.IsCompleted = true;
                                effectiveProgressYt.Report(info);
                                if (historyId > 0) Services.History.DownloadHistoryRecorder.MarkCompleted(historyId);
                                return;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            info.Status = "Canceled";
                            progressReporter.Report(info);
                            throw;
                        }
                        catch (Exception ex)
                        {
                            streamError = ex;
                            LoggingService.LogException($"[DownloadOrchestrator] yt-dlp download failed", ex);
                        }
                    }

                    // If still streaming URL (e.g. YouTube watch URL) and both native and yt-dlp failed, fail cleanly
                    if (DownloadService.IsVideoStreamingUrl(url))
                    {
                        string msg = streamError != null ? streamError.Message : "Could not resolve playable media stream from the provided URL.";
                        throw new InvalidOperationException($"Streaming media download failed: {msg}. Please check external tools configuration (yt-dlp/ffmpeg).");
                    }
                }

                // 2. Perform HTTP probe with 206 Partial Content range verification
                var probeResult = await _probeService.ProbeUrlAsync(url, savePath, credentials, cookies, cancellationToken).ConfigureAwait(false);
                url = probeResult.RequestUri.ToString();
                savePath = probeResult.SavePath;
                info.TotalBytes = probeResult.TotalBytes;
                info.ServerSupportsResume = probeResult.ServerSupportsResume;

                try { historyId = Services.History.DownloadHistoryRecorder.CreateEntry(url, savePath, probeResult.TotalBytes ?? -1); } catch { }

                using var throttled = new DownloadService.ThrottledProgress(progressReporter, TimeSpan.FromMilliseconds(200));
                IProgress<DownloadProgressInfo> effectiveProgress = throttled.AsProgress();

                // 3. Route to Segmented Download vs Single-Threaded Download based on 206 confirmation
                if (probeResult.TotalBytes.HasValue && probeResult.TotalBytes.Value > 0 && probeResult.ServerSupportsResume)
                {
                    info.Status = "Segmented Downloading...";
                    progressReporter.Report(info);

                    int segmentsToUse;
                    if (segmentCount.HasValue)
                    {
                        segmentsToUse = segmentCount.Value;
                    }
                    else
                    {
                        try
                        {
                            segmentsToUse = await _adaptiveManager.DetermineConnectionCountAsync(url, probeResult.TotalBytes, probeResult.ServerSupportsResume, cancellationToken).ConfigureAwait(false);
                        }
                        catch
                        {
                            segmentsToUse = 8;
                        }
                    }

                    string downloadId = Guid.NewGuid().ToString("N");
                    string host = Uri.TryCreate(url, UriKind.Absolute, out var parsedHost) ? parsedHost.Host : "localhost";
                    int allocatedBudget = GlobalConnectionGovernor.Instance.AcquireConnectionBudget(downloadId, host, segmentsToUse);
                    segmentsToUse = Math.Max(1, Math.Min(segmentsToUse, allocatedBudget));

                    LoggingService.Log($"[DownloadOrchestrator] Launching segmented download: segments={segmentsToUse} (GlobalBudget={allocatedBudget}), 206Confirmed=True");
                    _telemetryService?.TrackDownloadStarted(url, probeResult.TotalBytes, segmentsToUse);
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        await MultiPartAdapter.DownloadWithMultiPartAsync(url, savePath, segmentsToUse, effectiveProgress, pauseToken, combinedSpeedProvider, cancellationToken, credentials, cookies).ConfigureAwait(false);
                    }
                    finally
                    {
                        GlobalConnectionGovernor.Instance.ReleaseConnectionBudget(downloadId);
                    }

                    double duration = Math.Max(0.01, stopwatch.Elapsed.TotalSeconds);
                    double measuredSpeed = (probeResult.TotalBytes ?? 0) / duration;
                    _telemetryService?.TrackDownloadCompleted(url, probeResult.TotalBytes ?? 0, duration, measuredSpeed);

                    // Final validation
                    if (!File.Exists(savePath) || new FileInfo(savePath).Length == 0)
                    {
                        throw new InvalidOperationException($"Segmented download failed to produce non-empty output file at '{savePath}'.");
                    }
                    
                    // Trigger Next-Gen Post Download Pipeline (Subtitles, Smart Organizer, Cloud Handoff, Analytics)
                    await TriggerPostDownloadNextGenPipelineAsync(savePath, url, measuredSpeed).ConfigureAwait(false);
                }
                else
                {
                    info.Status = "Single-threaded Downloading...";
                    progressReporter.Report(info);

                    LoggingService.Log($"[DownloadOrchestrator] Launching single-threaded download (206Confirmed={probeResult.ServerSupportsResume})");
                    _telemetryService?.TrackDownloadStarted(url, probeResult.TotalBytes, 1);
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    await DownloadService.RunSingleThreadedDownloadInternalAsync(_httpClient, url, savePath, probeResult.TotalBytes, effectiveProgress, pauseToken, combinedSpeedProvider, cancellationToken, credentials).ConfigureAwait(false);
                    stopwatch.Stop();

                    double duration = Math.Max(0.01, stopwatch.Elapsed.TotalSeconds);
                    double measuredSpeed = (probeResult.TotalBytes ?? 0) / duration;
                    _telemetryService?.TrackDownloadCompleted(url, probeResult.TotalBytes ?? 0, duration, measuredSpeed);

                    // Final validation
                    if (!File.Exists(savePath) || new FileInfo(savePath).Length == 0)
                    {
                        throw new InvalidOperationException($"Single-threaded download failed to produce non-empty output file at '{savePath}'.");
                    }
                    
                    // Trigger Next-Gen Post Download Pipeline (Subtitles, Smart Organizer, Cloud Handoff, Analytics)
                    await TriggerPostDownloadNextGenPipelineAsync(savePath, url, measuredSpeed).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                info.Status = "Canceled";
                _telemetryService?.TrackDownloadPaused(url);
                progressReporter.Report(info);
                throw;
            }
            catch (Exception ex)
            {
                info.Status = "Error";
                info.ErrorMessage = ex.Message;
                _telemetryService?.TrackDownloadFailed(url, ex.Message, isRetriable: true);
                progressReporter.Report(info);
                throw;
            }
            finally
            {
                try { networkAdapter?.Dispose(); } catch { }
            }
        }

        private async Task TriggerPostDownloadNextGenPipelineAsync(string savePath, string url, double measuredSpeedBytesPerSec = 0)
        {
            try
            {
                if (!File.Exists(savePath)) return;

                // 0. Deterministic Security & Integrity Pipeline (Hash, Authenticode, Defender Scan, Quarantine)
                var secContext = new DownloadSecurityContext
                {
                    Url = url,
                    FilePath = savePath
                };
                var secResult = await DownloadSecurityPipeline.Instance.ProcessPostDownloadSecurityAsync(secContext, CancellationToken.None).ConfigureAwait(false);
                LoggingService.Log($"[SecurityPipeline] Security evaluation for '{Path.GetFileName(savePath)}': {secResult.Decision} ({secResult.Message})");

                if (secResult.Decision == SecurityDecision.SecurityQuarantined || !File.Exists(savePath))
                {
                    LoggingService.LogWarning($"[SecurityPipeline] File '{Path.GetFileName(savePath)}' was quarantined. Aborting downstream post-processing.");
                    return;
                }

                // 1. Smart File Organizer
                var organizer = new SmartFileOrganizerService();
                var orgResult = organizer.AnalyzeAndClassify(Path.GetFileName(savePath), url);
                LoggingService.Log($"[SmartOrganizer] File '{Path.GetFileName(savePath)}' categorized as '{orgResult.PrimaryCategory}' (Score: {orgResult.ConfidenceScore:P0})");

                // 2. Subtitle Auto Downloader for videos
                var subService = new SubtitleAutoDownloaderService(_httpClient);
                if (subService.IsVideoFile(savePath))
                {
                    var subtitleEnabled = _settingsService.GetSetting("EnableSubtitleAutoDownloader");
                    if (string.Equals(subtitleEnabled, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        var langs = new[] { "en", "bn" };
                        var tracks = await subService.FetchAndSaveSubtitlesAsync(savePath, langs).ConfigureAwait(false);
                        LoggingService.Log($"[SubtitleEngine] Downloaded {tracks.Count} companion subtitles for '{Path.GetFileName(savePath)}'");
                    }
                }

                // 3. Cloud Auto-Upload Handoff
                var cloudProviderSetting = _settingsService.GetSetting("DefaultCloudUploadProvider");
                if (!string.IsNullOrWhiteSpace(cloudProviderSetting) && 
                    Enum.TryParse<CloudStorageProvider>(cloudProviderSetting, true, out var provider) && 
                    provider != CloudStorageProvider.None)
                {
                    var cloudUploader = new CloudHandoffUploadService(_httpClient);
                    var job = cloudUploader.EnqueueUpload(savePath, provider);
                    var token = _settingsService.GetSetting("EncryptedCloudApiToken");
                    await cloudUploader.ProcessUploadJobAsync(job.JobId, token).ConfigureAwait(false);
                    LoggingService.Log($"[CloudHandoff] File '{Path.GetFileName(savePath)}' uploaded to {provider} -> {job.CloudFileUrl}");
                }

                // 4. Permissioned Auto-Archive Extraction
                var autoExtractPermitted = string.Equals(_settingsService.GetSetting("EnableAutoArchiveExtraction"), "true", StringComparison.OrdinalIgnoreCase);
                var deleteAfter = string.Equals(_settingsService.GetSetting("DeleteArchiveAfterExtraction"), "true", StringComparison.OrdinalIgnoreCase);
                var extractor = new AutoExtractorAndStreamService(autoExtractPermitted) { DeleteArchiveAfterExtraction = deleteAfter };
                if (extractor.IsCompressedArchive(savePath) && autoExtractPermitted)
                {
                    var extractResult = await extractor.TryExtractArchiveAsync(savePath, ct: CancellationToken.None).ConfigureAwait(false);
                    if (extractResult.IsSuccess)
                    {
                        LoggingService.Log($"[AutoExtractor] Successfully extracted {extractResult.ExtractedFileCount} files to '{extractResult.ExtractedFolderPath}'");
                    }
                }

                // 5. Download Analytics Recording
                var analyticsEnabled = !string.Equals(_settingsService.GetSetting("EnableDownloadAnalytics"), "false", StringComparison.OrdinalIgnoreCase);
                if (analyticsEnabled && File.Exists(savePath))
                {
                    var fi = new FileInfo(savePath);
                    var analyticsEngine = new DownloadAnalyticsEngine();
                    analyticsEngine.RecordDownloadSample(url, fi.Length, measuredSpeedBytesPerSec);
                    LoggingService.Log($"[Analytics] Recorded {fi.Length} bytes at {measuredSpeedBytesPerSec:F0} B/s for {url}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[NextGenPipeline] Failed for {savePath}", ex);
            }
        }
    }
}
