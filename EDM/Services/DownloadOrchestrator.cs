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
    /// DownloadOrchestrator — Decoupled download orchestrator pattern.
    /// Handles flow coordination: streaming video detection, HTTP probing with 206 range verification,
    /// SQLite history recording, progress throttling, and routing to segmented vs single-threaded download engines.
    /// </summary>
    public class DownloadOrchestrator
    {
        private readonly HttpClient _httpClient;
        private readonly YtDlpService? _ytDlp;
        private readonly HttpProbeService _probeService;
        private readonly INetworkService _networkService;
        private readonly ISettingsService _settingsService;
        private readonly AdaptiveConnectionManager _adaptiveManager;
        private readonly ControlPlaneClient? _controlPlaneClient;
        private readonly ControlPlaneTelemetryService? _telemetryService;

        public DownloadOrchestrator(
            HttpClient? httpClient = null,
            YtDlpService? ytDlp = null,
            HttpProbeService? probeService = null,
            INetworkService? networkService = null,
            ISettingsService? settingsService = null,
            ControlPlaneClient? controlPlaneClient = null,
            ControlPlaneTelemetryService? telemetryService = null)
        {
            _httpClient = httpClient ?? SharedHttpClient.Instance;
            _ytDlp = ytDlp ?? new YtDlpService();
            _probeService = probeService ?? new HttpProbeService(_httpClient);
            _settingsService = settingsService ?? App.ServiceProvider?.GetService(typeof(ISettingsService)) as ISettingsService ?? new SettingsService();
            _networkService = networkService ?? new NetworkService(_settingsService);
            _adaptiveManager = new AdaptiveConnectionManager(_settingsService, _networkService);
            _controlPlaneClient = controlPlaneClient ?? App.ServiceProvider?.GetService(typeof(ControlPlaneClient)) as ControlPlaneClient;
            _telemetryService = telemetryService ?? App.ServiceProvider?.GetService(typeof(ControlPlaneTelemetryService)) as ControlPlaneTelemetryService;
        }

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
            LoggingService.Log($"[DownloadOrchestrator] Starting download: url={url}, savePath={savePath}");

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

            // Speed provider
            Func<double> combinedSpeedProvider = () =>
            {
                try
                {
                    var user = speedLimitProvider?.Invoke() ?? -1;
                    if (user > 0) return user;
                    var kbps = _settingsService?.GetActiveBandwidthLimitKbps() ?? 0;
                    if (kbps > 0) return kbps * 1024.0;
                    if (_networkService != null && _networkService.IsMeteredNetwork() && _settingsService != null && _settingsService.GetReduceQualityOnMeteredNetworks())
                    {
                        return 512 * 1024.0;
                    }
                    return -1;
                }
                catch (Exception ex)
                {
                    LoggingService.Log($"[DownloadOrchestrator] Speed limit calculation failed: {ex.Message}");
                    return -1;
                }
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

                // 1. Detect streaming / video site URL (yt-dlp)
                if (_ytDlp != null && DownloadService.IsVideoStreamingUrl(url))
                {
                    LoggingService.Log($"[DownloadOrchestrator] Video streaming site detected. Routing to YtDlpService.");
                    info.Status = "Connecting to streaming video service...";
                    progressReporter.Report(info);

                    try { historyId = Services.History.DownloadHistoryRecorder.CreateEntry(url, savePath, -1); } catch { }

                    using var throttledYt = new DownloadService.ThrottledProgress(progressReporter, TimeSpan.FromMilliseconds(400));
                    using var smoothYt = new SmoothProgressReporter(throttledYt.AsProgress());
                    IProgress<DownloadProgressInfo> effectiveProgressYt = smoothYt;

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
                                    ProgressPercentage = percent,
                                    Status = string.IsNullOrWhiteSpace(statusLine) ? $"Downloading ({percent}%)..." : statusLine,
                                    IsCompleted = percent >= 100
                                };
                                effectiveProgressYt.Report(dlInfo);

                                if (historyId > 0)
                                {
                                    BackgroundTaskManager.FireAndForget("HistoryUpdateYtDlp", async () =>
                                    {
                                        try
                                        {
                                            Services.History.DownloadHistoryRecorder.UpdateProgress(historyId, 0, 0, 0);
                                            if (percent >= 100) Services.History.DownloadHistoryRecorder.MarkCompleted(historyId);
                                            await Task.CompletedTask;
                                        }
                                        catch { }
                                    });
                                }
                            },
                            cancellationToken
                        ).ConfigureAwait(false);

                        info.ProgressPercentage = 100;
                        info.Status = "Finished";
                        info.IsCompleted = true;
                        effectiveProgressYt.Report(info);
                        if (historyId > 0) Services.History.DownloadHistoryRecorder.MarkCompleted(historyId);
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        info.Status = "Canceled";
                        progressReporter.Report(info);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogException($"[DownloadOrchestrator] yt-dlp download failed, falling back to HTTP probe", ex);
                    }
                }

                // 2. Perform HTTP probe with 206 Partial Content range verification
                var probeResult = await _probeService.ProbeUrlAsync(url, savePath, credentials, cookies, cancellationToken).ConfigureAwait(false);
                url = probeResult.RequestUri.ToString();
                savePath = probeResult.SavePath;
                info.TotalBytes = probeResult.TotalBytes;
                info.ServerSupportsResume = probeResult.ServerSupportsResume;

                try { historyId = Services.History.DownloadHistoryRecorder.CreateEntry(url, savePath, probeResult.TotalBytes ?? -1); } catch { }

                using var throttled = new DownloadService.ThrottledProgress(progressReporter, TimeSpan.FromMilliseconds(400));
                using var smooth = new SmoothProgressReporter(throttled.AsProgress());
                IProgress<DownloadProgressInfo> effectiveProgress = smooth;

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

                    LoggingService.Log($"[DownloadOrchestrator] Launching segmented download: segments={segmentsToUse}, 206Confirmed=True");
                    _telemetryService?.TrackDownloadStarted(url, probeResult.TotalBytes, segmentsToUse);
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    await MultiPartAdapter.DownloadWithMultiPartAsync(url, savePath, segmentsToUse, effectiveProgress, pauseToken, combinedSpeedProvider, cancellationToken, credentials, cookies).ConfigureAwait(false);
                    stopwatch.Stop();
                    _telemetryService?.TrackDownloadCompleted(url, probeResult.TotalBytes ?? 0, stopwatch.Elapsed.TotalSeconds, (probeResult.TotalBytes ?? 0) / Math.Max(0.01, stopwatch.Elapsed.TotalSeconds));
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
                    _telemetryService?.TrackDownloadCompleted(url, probeResult.TotalBytes ?? 0, stopwatch.Elapsed.TotalSeconds, (probeResult.TotalBytes ?? 0) / Math.Max(0.01, stopwatch.Elapsed.TotalSeconds));
                }

                // Trigger Next-Gen Post Download Pipeline (Subtitles, Smart Organizer, Cloud Handoff)
                await TriggerPostDownloadNextGenPipelineAsync(savePath, url).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                info.Status = "Canceled";
                _telemetryService?.TrackDownloadPaused(url);
                progressReporter.Report(info);
            }
            catch (Exception ex)
            {
                info.Status = "Error";
                info.ErrorMessage = ex.Message;
                _telemetryService?.TrackDownloadFailed(url, ex.Message, isRetriable: true);
                progressReporter.Report(info);
            }
            finally
            {
                try { networkAdapter?.Dispose(); } catch { }
            }
        }

        private async Task TriggerPostDownloadNextGenPipelineAsync(string savePath, string url)
        {
            try
            {
                if (!File.Exists(savePath)) return;

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
                    analyticsEngine.RecordDownloadSample(url, fi.Length, 15_000_000); // 15 MB/s sample
                    LoggingService.Log($"[Analytics] Recorded {fi.Length} bytes for {url}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[NextGenPipeline] Failed for {savePath}", ex);
            }
        }
    }
}
