using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using EDM.Models;
using EDM.Services.Interfaces;
using EDM.ViewModels;

namespace EDM.Services
{
    /// <summary>
    /// Authoritative Unified Download Request Gateway and Deduplication Engine.
    /// Centralizes all download requests (Manual, Clipboard, Browser, NativeHost, Dashboard, CLI),
    /// executing zero-trust validation, filename sanitization, atomic thread-safe deduplication,
    /// and routing to the existing DownloadManagerViewModel and DownloadQueueScheduler.
    /// </summary>
    public sealed class DownloadRequestGateway : IDownloadRequestGateway
    {
        private const int MaxUrlLength = 8192;
        private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(60);

        private readonly ISettingsService _settingsService;
        private readonly ConcurrentDictionary<string, DateTime> _recentIdentities = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _syncLock = new();

        public DownloadRequestGateway(ISettingsService? settingsService = null)
        {
            _settingsService = settingsService ??
                (App.ServiceProvider?.GetService(typeof(ISettingsService)) as ISettingsService) ??
                new SettingsService();
        }

        public async Task<DownloadSubmissionResult> SubmitRequestAsync(DownloadRequest request, CancellationToken ct = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Url))
            {
                return DownloadSubmissionResult.Invalid("Download request or URL cannot be empty.");
            }

            string rawUrl = request.Url.Trim();

            // 0. Subscription Entitlement Policy Guard
            var entitlementClient = App.ServiceProvider?.GetService(typeof(ISubscriptionEntitlementClient)) as ISubscriptionEntitlementClient;
            if (entitlementClient != null && entitlementClient.IsBlocked)
            {
                LoggingService.LogWarning("[DownloadRequestGateway] Security rejection: Client is suspended by administrative subscription policy.");
                return DownloadSubmissionResult.SecurityRejected("Downloads are temporarily disabled because this installation is suspended by administrative policy. Please check your Subscription status or contact support.");
            }

            // 1. URL Length Guard
            if (rawUrl.Length > MaxUrlLength)
            {
                LoggingService.LogWarning($"[DownloadRequestGateway] Request rejected: URL exceeds maximum safe length ({rawUrl.Length} > {MaxUrlLength}).");
                return DownloadSubmissionResult.SecurityRejected("URL exceeds maximum safe length limit of 8192 characters.");
            }

            // 2. Settings Permission Check
            if (request.Source == IngestionSource.BrowserExtension)
            {
                if (!_settingsService.GetEnableBrowserIntegration() || !_settingsService.GetBrowserCaptureDownloads())
                {
                    LoggingService.Log("[DownloadRequestGateway] Browser extension request rejected: Browser integration is disabled in settings.");
                    return DownloadSubmissionResult.Disabled("Browser integration is disabled in settings.");
                }
            }
            else if (request.Source == IngestionSource.ClipboardMonitor)
            {
                if (!_settingsService.GetEnableClipboardMonitoring())
                {
                    LoggingService.Log("[DownloadRequestGateway] Clipboard request rejected: Clipboard monitoring is disabled in settings.");
                    return DownloadSubmissionResult.Disabled("Clipboard monitoring is disabled in settings.");
                }
            }

            // 3. Protocol & Scheme Validation
            if (!SecuritySanitizer.IsAllowedUrlScheme(rawUrl))
            {
                LoggingService.LogWarning($"[DownloadRequestGateway] Security rejection: Disallowed or unsafe URL scheme for '{ProtocolDetector.SanitizeUrlForLogging(rawUrl)}'.");
                return DownloadSubmissionResult.SecurityRejected($"The URL scheme is not permitted or is potentially unsafe: {ProtocolDetector.SanitizeUrlForLogging(rawUrl)}");
            }

            // 4. Filename & Path Resolution
            string rawFileName = !string.IsNullOrWhiteSpace(request.SuggestedFileName)
                ? request.SuggestedFileName
                : ExtractDefaultFileName(rawUrl);

            string safeFileName = SecuritySanitizer.SanitizeFileName(rawFileName);
            if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName == ".dat")
            {
                safeFileName = "EDM_Download_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".bin";
            }

            string destDir = !string.IsNullOrWhiteSpace(request.DestinationDirectory)
                ? request.DestinationDirectory
                : _settingsService.GetDefaultDownloadPath();

            if (string.IsNullOrWhiteSpace(destDir))
            {
                destDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            }

            string fullSavePath = Path.Combine(destDir, safeFileName);

            // 5. Deterministic Identity & Deduplication
            string identityKey = ResolveIdentityKey(rawUrl, safeFileName);

            lock (_syncLock)
            {
                if (IsDuplicateInternal(rawUrl, identityKey))
                {
                    LoggingService.Log($"[DownloadRequestGateway] Duplicate request suppressed: {ProtocolDetector.SanitizeUrlForLogging(rawUrl)} (Source: {request.Source})");
                    return DownloadSubmissionResult.Duplicate($"Download request is already active or queued: {safeFileName}");
                }

                _recentIdentities[identityKey] = DateTime.UtcNow;
                _recentIdentities[rawUrl] = DateTime.UtcNow;
            }

            // 6. Intelligent Rule & Profile Evaluation (Step 19.6)
            var ruleResult = DownloadRuleEngine.Instance.Resolve(request, destDir);
            string finalSavePath = !string.IsNullOrWhiteSpace(ruleResult.DestinationPath)
                ? ruleResult.DestinationPath
                : fullSavePath;
            string finalCategory = !string.IsNullOrWhiteSpace(ruleResult.Category)
                ? ruleResult.Category
                : FileCategorizationService.GetTargetSubfolder(safeFileName);
            string finalQueueId = !string.IsNullOrWhiteSpace(ruleResult.QueueId)
                ? ruleResult.QueueId
                : "default";
            var finalPriority = ruleResult.Priority;

            // 7. Build authoritative DownloadItem
            var item = new DownloadItem
            {
                Url = rawUrl,
                FileName = safeFileName,
                SavePath = finalSavePath,
                Status = request.SilentMode ? "Queued" : "Downloading",
                Progress = 0.0,
                LastTryDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Size = "Detecting...",
                TransferRate = "0 B/s",
                Referer = request.Referrer ?? string.Empty,
                Cookies = request.Cookies ?? string.Empty,
                Category = finalCategory,
                QueueId = finalQueueId,
                QueuePriority = (QueuePriority)(int)finalPriority,
                DownloadIdentity = identityKey
            };

            // Apply custom headers if present
            if (request.CustomHeaders != null)
            {
                if (request.CustomHeaders.TryGetValue("Authorization", out var auth)) item.AuthHeader = auth;
                if (request.CustomHeaders.TryGetValue("User-Agent", out var ua)) item.UserAgent = ua;
            }

            // 7. Dispatch to Existing ViewModel / Scheduler on UI Thread
            await DispatchToUIAsync(item, request).ConfigureAwait(false);

            LoggingService.Log($"[DownloadRequestGateway] Accepted and enqueued download '{safeFileName}' from {request.Source} ({ProtocolDetector.SanitizeUrlForLogging(rawUrl)})");

            return DownloadSubmissionResult.Succeeded(item, item.Id.ToString("N"));
        }

        public bool IsDuplicate(string url, string? downloadIdentity = null)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            string key = !string.IsNullOrWhiteSpace(downloadIdentity)
                ? downloadIdentity
                : ResolveIdentityKey(url, ExtractDefaultFileName(url));

            lock (_syncLock)
            {
                return IsDuplicateInternal(url, key);
            }
        }

        public void ResetDeduplicationCache()
        {
            lock (_syncLock)
            {
                _recentIdentities.Clear();
            }
        }

        private bool IsDuplicateInternal(string url, string identityKey)
        {
            DateTime now = DateTime.UtcNow;

            // 1. Check sliding window cache
            if (_recentIdentities.TryGetValue(identityKey, out var time1) && (now - time1) < DuplicateWindow)
            {
                return true;
            }

            if (_recentIdentities.TryGetValue(url, out var time2) && (now - time2) < DuplicateWindow)
            {
                return true;
            }

            // 2. Check active downloads in ViewModel
            var app = System.Windows.Application.Current;
            if (app?.MainWindow?.DataContext is DownloadManagerViewModel vm)
            {
                bool activeInVm = vm.AllDownloads.Any(d =>
                    (string.Equals(d.Url, url, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(d.DownloadIdentity, identityKey, StringComparison.OrdinalIgnoreCase)) &&
                    (d.Status == "Downloading" || d.Status == "Connecting..." || d.Status == "Queued" || d.Status == "Paused"));

                if (activeInVm) return true;
            }

            return false;
        }

        private static string ResolveIdentityKey(string url, string fileName)
        {
            try
            {
                var uri = new Uri(url);
                // Lowercase scheme and host, retain path and query verbatim (preserves S3/GCS tokens)
                string canonical = $"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}{uri.PathAndQuery}";
                return $"{canonical}|{fileName.ToLowerInvariant()}";
            }
            catch
            {
                return $"{url.Trim()}|{fileName.Trim()}";
            }
        }

        private static string ExtractDefaultFileName(string url)
        {
            try
            {
                var uri = new Uri(url);
                string pathName = Path.GetFileName(uri.LocalPath);
                if (!string.IsNullOrWhiteSpace(pathName)) return pathName;
            }
            catch { }

            return "download.dat";
        }

        private async Task DispatchToUIAsync(DownloadItem item, DownloadRequest request)
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;

            var dispatcher = app.Dispatcher ?? Dispatcher.CurrentDispatcher;

            if (dispatcher.CheckAccess())
            {
                EnqueueInViewModel(app, item, request);
            }
            else
            {
                await dispatcher.InvokeAsync(() => EnqueueInViewModel(app, item, request));
            }
        }

        private void EnqueueInViewModel(System.Windows.Application app, DownloadItem item, DownloadRequest request)
        {
            if (app.MainWindow?.DataContext is DownloadManagerViewModel vm)
            {
                vm.AddDownload(item);
            }

            // Trigger notification if enabled
            bool showNotification = request.Source switch
            {
                IngestionSource.BrowserExtension => _settingsService.GetBrowserShowNotification(),
                IngestionSource.ClipboardMonitor => _settingsService.GetClipboardShowNotification(),
                _ => true
            };

            if (showNotification && !request.SilentMode)
            {
                NotificationService.Instance.Notify(
                    "Download Added",
                    $"Added {item.FileName} from {request.Source}",
                    NotificationSeverity.Info,
                    NotificationCategory.System);
            }
        }
    }
}
