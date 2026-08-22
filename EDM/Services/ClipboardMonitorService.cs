using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using EDM.Models;
using EDM.Services.Interfaces;
using EDM.ViewModels;
using EDM.Views;

namespace EDM.Services
{
    /// <summary>
    /// Advanced Event-Driven Windows Clipboard Monitor Service.
    /// Uses native Win32 AddClipboardFormatListener / WM_CLIPBOARDUPDATE for 0% idle CPU overhead,
    /// safe COM retry on locked clipboard, robust URL validation, multi-layer deduplication,
    /// and seamless integration with the existing EDM download pipeline.
    /// </summary>
    public sealed class ClipboardMonitorService : IClipboardMonitorService
    {
        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private const int MaxClipboardScanLength = 65536; // Cap scanning on large clipboard text (e.g. huge copied code/docs)

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private readonly ISettingsService _settingsService;
        private readonly Action<string>? _legacyCallback;
        private readonly object _lock = new();

        private IntPtr _hwnd = IntPtr.Zero;
        private HwndSource? _hwndSource;
        private bool _isMessageSourceCreatedLocally;
        private bool _isRunning;
        private bool _isDisposed;

        // Sliding deduplication cache: URL -> Last detected timestamp (UTC)
        private readonly ConcurrentDictionary<string, DateTime> _recentUrls = new(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan DuplicateSuppressionWindow = TimeSpan.FromSeconds(60);

        // Precompiled regex for extracting downloadable URLs
        private static readonly Regex UrlExtractorRegex = new(
            @"\b(?<url>(?:https?|ftp|ftps):\/\/[^\s<>""]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Schemes explicitly rejected as dangerous or non-downloadable
        private static readonly string[] RejectedSchemes = new[]
        {
            "javascript:", "data:", "file:", "chrome:", "edge:", "about:", "blob:", "res:", "resource:"
        };

        public bool IsRunning => _isRunning;

        public event EventHandler<ClipboardUrlDetectedEventArgs>? UrlDetected;

        public ClipboardMonitorService(ISettingsService? settingsService = null, Action<string>? onUrlDetected = null)
        {
            _settingsService = settingsService ?? 
                (App.ServiceProvider?.GetService(typeof(ISettingsService)) as ISettingsService) ?? 
                new SettingsService();
            _legacyCallback = onUrlDetected;
        }

        /// <summary>
        /// Starts event-driven clipboard monitoring on the given window handle or a message-only HWND.
        /// </summary>
        public void Start(IntPtr windowHandle = default)
        {
            lock (_lock)
            {
                if (_isRunning || _isDisposed) return;

                try
                {
                    var app = System.Windows.Application.Current;
                    if (app != null && app.Dispatcher != null && !app.Dispatcher.HasShutdownStarted)
                    {
                        if (app.Dispatcher.CheckAccess())
                        {
                            InitializeNativeListener(windowHandle);
                        }
                        else
                        {
                            app.Dispatcher.Invoke(() => InitializeNativeListener(windowHandle));
                        }
                    }
                    else
                    {
                        InitializeNativeListener(windowHandle);
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[ClipboardMonitorService] Failed to start native listener", ex);
                }

                _isRunning = true;
                LoggingService.Log("[ClipboardMonitorService] Windows event-driven clipboard monitor started successfully.");
            }
        }

        private void InitializeNativeListener(IntPtr windowHandle)
        {
            try
            {
                if (windowHandle != IntPtr.Zero)
                {
                    _hwnd = windowHandle;
                    _hwndSource = HwndSource.FromHwnd(_hwnd);
                    _hwndSource?.AddHook(WndProc);
                    _isMessageSourceCreatedLocally = false;
                }
                else if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
                {
                    // Create a lightweight message-only window source on STA thread
                    var parameters = new HwndSourceParameters("EDM_ClipboardMonitorMessageWindow")
                    {
                        Width = 0,
                        Height = 0,
                        PositionX = 0,
                        PositionY = 0,
                        WindowStyle = 0x800000 // WS_BORDER (invisible hidden window)
                    };
                    _hwndSource = new HwndSource(parameters);
                    _hwnd = _hwndSource.Handle;
                    _hwndSource.AddHook(WndProc);
                    _isMessageSourceCreatedLocally = true;
                }

                if (_hwnd != IntPtr.Zero)
                {
                    bool registered = AddClipboardFormatListener(_hwnd);
                    if (!registered)
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        LoggingService.LogWarning($"[ClipboardMonitorService] AddClipboardFormatListener returned false (Win32 Error: {errorCode}).");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[ClipboardMonitorService] InitializeNativeListener exception", ex);
            }
        }

        /// <summary>
        /// Stops clipboard monitoring and detaches native hooks.
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRunning) return;

                try
                {
                    if (_hwnd != IntPtr.Zero)
                    {
                        try { RemoveClipboardFormatListener(_hwnd); } catch { }
                    }

                    if (_hwndSource != null)
                    {
                        try { _hwndSource.RemoveHook(WndProc); } catch { }
                        if (_isMessageSourceCreatedLocally)
                        {
                            try { _hwndSource.Dispose(); } catch { }
                            _hwndSource = null;
                        }
                    }

                    _hwnd = IntPtr.Zero;
                    _isRunning = false;
                    LoggingService.Log("[ClipboardMonitorService] Windows clipboard monitor stopped cleanly.");
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[ClipboardMonitorService] Error stopping clipboard monitor", ex);
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_isDisposed) return;
                _isDisposed = true;
                Stop();
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE && _isRunning)
            {
                // Handle notification asynchronously to avoid blocking the Win32 message loop
                var dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
                dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    try
                    {
                        ProcessClipboardUpdate();
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogException("[ClipboardMonitorService] Error handling WM_CLIPBOARDUPDATE", ex);
                    }
                }));

                handled = false;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Safely reads the clipboard text with retry backoff for lock contention.
        /// </summary>
        private void ProcessClipboardUpdate()
        {
            if (!_settingsService.GetEnableClipboardMonitoring())
            {
                return;
            }

            // Fire async clipboard read on background — avoids Thread.Sleep blocking thread pool
            BackgroundTaskManager.FireAndForget("ClipboardRead", async () =>
            {
                string? clipboardText = await ReadClipboardTextWithRetryAsync(maxRetries: 3, delayMs: 50).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(clipboardText))
                {
                    ProcessText(clipboardText, source: "WindowsClipboard");
                }
            });
        }

        private static async Task<string?> ReadClipboardTextWithRetryAsync(int maxRetries, int delayMs)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        return System.Windows.Clipboard.GetText();
                    }
                    return null;
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // Clipboard is temporarily locked by another process, wait and retry
                    if (i < maxRetries - 1)
                    {
                        await Task.Delay(delayMs * (i + 1)).ConfigureAwait(false); // was Thread.Sleep — non-blocking now
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[ClipboardMonitorService] Clipboard read error", ex);
                    break;
                }
            }

            return null;
        }

        /// <summary>
        /// Processes a text payload, extracts candidate downloadable URLs, validates them,
        /// applies deduplication, and routes them to the EDM download pipeline.
        /// </summary>
        public bool ProcessText(string? text, string source = "Manual")
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            // Security & Performance: Ignore huge non-URL payloads
            if (text.Length > MaxClipboardScanLength)
            {
                text = text.Substring(0, MaxClipboardScanLength);
            }

            // Quick check for rejected schemes
            string trimmed = text.Trim();
            foreach (var rejected in RejectedSchemes)
            {
                if (trimmed.StartsWith(rejected, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Reject local Windows paths (e.g. C:\Downloads\file.zip, \\server\share)
            if (IsLocalWindowsPath(trimmed))
            {
                return false;
            }

            // Extract valid URLs
            var extractedUrls = ExtractDownloadableUrls(trimmed);
            if (extractedUrls.Count == 0)
            {
                return false;
            }

            bool anyAccepted = false;
            foreach (var url in extractedUrls)
            {
                if (ProcessSingleUrl(url, source))
                {
                    anyAccepted = true;
                }
            }

            return anyAccepted;
        }

        private bool ProcessSingleUrl(string url, string source)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            // Protocol & Scheme Validation
            if (!ValidateUrlProtocol(url, out string normalizedUrl, out string reason))
            {
                LoggingService.Log($"[ClipboardMonitorService] URL rejected ({reason}): {ProtocolDetector.SanitizeUrlForLogging(url)}");
                return false;
            }

            // Duplicate Protection
            if (_settingsService.GetClipboardIgnoreDuplicates() && IsDuplicate(normalizedUrl))
            {
                LoggingService.Log($"[ClipboardMonitorService] Duplicate URL ignored: {ProtocolDetector.SanitizeUrlForLogging(normalizedUrl)}");
                return false;
            }

            // Record in sliding deduplication cache
            RecordDetectedUrl(normalizedUrl);

            // Log sanitized event
            LoggingService.Log($"[ClipboardMonitorService] Valid URL detected from {source}: {ProtocolDetector.SanitizeUrlForLogging(normalizedUrl)}");

            // Raise UrlDetected event
            var eventArgs = new ClipboardUrlDetectedEventArgs(normalizedUrl, source);
            try
            {
                UrlDetected?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[ClipboardMonitorService] Event handler error", ex);
            }

            // Call legacy callback if attached
            try
            {
                _legacyCallback?.Invoke(normalizedUrl);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[ClipboardMonitorService] Legacy callback error", ex);
            }

            // If event was handled externally, return success
            if (eventArgs.Handled)
            {
                return true;
            }

            // Execute configured action
            var action = _settingsService.GetClipboardAction();
            switch (action)
            {
                case ClipboardAction.AskBeforeDownload:
                    DispatchAskBeforeDownload(normalizedUrl);
                    break;

                case ClipboardAction.AutoDownload:
                    DispatchAutoDownload(normalizedUrl);
                    break;

                case ClipboardAction.Ignore:
                    LoggingService.Log($"[ClipboardMonitorService] Action set to Ignore; skipping download for {ProtocolDetector.SanitizeUrlForLogging(normalizedUrl)}");
                    break;
            }

            return true;
        }

        /// <summary>
        /// Validates that the URL uses an allowed and enabled scheme.
        /// </summary>
        private bool ValidateUrlProtocol(string url, out string normalizedUrl, out string rejectionReason)
        {
            normalizedUrl = url.Trim();
            rejectionReason = string.Empty;

            if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
            {
                // Try prepending https:// if missing scheme
                if (!normalizedUrl.Contains("://") && Uri.TryCreate("https://" + normalizedUrl, UriKind.Absolute, out uri))
                {
                    normalizedUrl = "https://" + normalizedUrl;
                }
                else
                {
                    rejectionReason = "Malformed URI";
                    return false;
                }
            }

            string scheme = uri.Scheme.ToLowerInvariant();

            if (scheme == "http")
            {
                if (!_settingsService.GetClipboardMonitorHttp())
                {
                    rejectionReason = "HTTP monitoring disabled";
                    return false;
                }
                return true;
            }

            if (scheme == "https")
            {
                if (!_settingsService.GetClipboardMonitorHttps())
                {
                    rejectionReason = "HTTPS monitoring disabled";
                    return false;
                }
                return true;
            }

            if (scheme == "ftp" || scheme == "ftps")
            {
                if (!_settingsService.GetClipboardMonitorFtp())
                {
                    rejectionReason = "FTP monitoring disabled";
                    return false;
                }
                return true;
            }

            rejectionReason = $"Unsupported scheme '{scheme}'";
            return false;
        }

        private static bool IsLocalWindowsPath(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            // Drive letter check (e.g. C:\ or D:/)
            if (text.Length >= 3 && char.IsLetter(text[0]) && text[1] == ':' && (text[2] == '\\' || text[2] == '/'))
            {
                return true;
            }

            // UNC network path (e.g. \\server\share)
            if (text.StartsWith(@"\\") || text.StartsWith("//"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Extracts all downloadable URLs from a text payload.
        /// Preserves full query strings, signed signatures, and tokens without destructive normalization.
        /// </summary>
        public static List<string> ExtractDownloadableUrls(string text)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return results;

            // Direct URL check
            string trimmed = text.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var directUri) &&
                (directUri.Scheme == Uri.UriSchemeHttp || directUri.Scheme == Uri.UriSchemeHttps ||
                 directUri.Scheme == Uri.UriSchemeFtp || directUri.Scheme == "ftps"))
            {
                results.Add(trimmed);
                return results;
            }

            // Regex-based extraction for text containing links
            var matches = UrlExtractorRegex.Matches(text);
            foreach (Match match in matches)
            {
                string candidate = match.Groups["url"].Value.TrimEnd('.', ',', ';', ')', ']');
                if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps ||
                     uri.Scheme == Uri.UriSchemeFtp || uri.Scheme == "ftps"))
                {
                    if (!results.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                    {
                        results.Add(candidate);
                    }
                }
            }

            return results;
        }

        private bool IsDuplicate(string url)
        {
            DateTime now = DateTime.UtcNow;

            // 1. Check sliding deduplication cache
            if (_recentUrls.TryGetValue(url, out var timestamp))
            {
                if (now - timestamp < DuplicateSuppressionWindow)
                {
                    return true;
                }
            }

            // 2. Check active downloads in UI ViewModel
            var app = System.Windows.Application.Current;
            if (app?.MainWindow?.DataContext is DownloadManagerViewModel vm)
            {
                bool existsInActive = vm.AllDownloads.Any(d => 
                    string.Equals(d.Url, url, StringComparison.OrdinalIgnoreCase) &&
                    (d.Status == "Downloading" || d.Status == "Connecting..." || d.Status == "Queued" || d.Status == "Paused"));
                if (existsInActive) return true;
            }

            return false;
        }

        private void RecordDetectedUrl(string url)
        {
            DateTime now = DateTime.UtcNow;
            _recentUrls[url] = now;

            // Clean expired cache entries if dictionary grows large
            if (_recentUrls.Count > 100)
            {
                foreach (var kv in _recentUrls.Where(k => (now - k.Value) > DuplicateSuppressionWindow).ToList())
                {
                    _recentUrls.TryRemove(kv.Key, out _);
                }
            }
        }

        /// <summary>
        /// "Ask before downloading" workflow (Default):
        /// Opens the standard AddUrlWindow with the URL prefilled and stream auto-analyzed,
        /// guarding against multiple popup windows for the same URL.
        /// </summary>
        private void DispatchAskBeforeDownload(string url)
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;

            app.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                try
                {
                    // Guard against duplicate dialogs: check if AddUrlWindow is already open
                    var existing = app.Windows.OfType<AddUrlWindow>().FirstOrDefault();
                    if (existing != null)
                    {
                        existing.UrlTextBox.Text = url;
                        if (existing.WindowState == WindowState.Minimized)
                        {
                            existing.WindowState = WindowState.Normal;
                        }
                        existing.Activate();
                        existing.Focus();
                        return;
                    }

                    var addUrlWindow = new AddUrlWindow();
                    var vm = app.MainWindow?.DataContext as DownloadManagerViewModel;
                    addUrlWindow.Initialize(vm, url);
                    addUrlWindow.Owner = app.MainWindow;

                    // Show notification if enabled
                    if (_settingsService.GetClipboardShowNotification())
                    {
                        NotificationService.Instance.Notify(
                            "Download Link Detected",
                            $"Copied link: {Path.GetFileName(new Uri(url).LocalPath)}",
                            NotificationSeverity.Info,
                            NotificationCategory.System);
                    }

                    addUrlWindow.Show();
                    addUrlWindow.Activate();
                    addUrlWindow.Focus();
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[ClipboardMonitorService] DispatchAskBeforeDownload failed", ex);
                }
            }));
        }

        /// <summary>
        /// "Automatically download" workflow:
        /// Directly submits the DownloadRequest to the central IDownloadRequestGateway.
        /// </summary>
        private void DispatchAutoDownload(string url)
        {
            var gateway = (App.ServiceProvider?.GetService(typeof(Interfaces.IDownloadRequestGateway)) as Interfaces.IDownloadRequestGateway)
                ?? new DownloadRequestGateway(_settingsService);

            var req = new DownloadRequest
            {
                Source = IngestionSource.ClipboardMonitor,
                Url = url
            };

            BackgroundTaskManager.FireAndForget($"ClipboardAutoDownload_{Guid.NewGuid():N}", async () =>
            {
                try
                {
                    await gateway.SubmitRequestAsync(req).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[ClipboardMonitorService] DispatchAutoDownload via Gateway failed", ex);
                }
            });
        }
    }
}
