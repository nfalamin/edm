using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using EDM.Models;
using EDM.NativeMessaging;
using EDM.Services;
using EDM.Views;
using Microsoft.Extensions.DependencyInjection;
using MessageBox = System.Windows.MessageBox;

namespace EDM
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private NativeMessageListener? _nativeListener;
        private NativeIpcServer? _ipcServer;
        private EdmWebSocketServer? _webSocketServer;
        private System.IServiceProvider? _serviceProvider;
        private System.Windows.Threading.DispatcherUnhandledExceptionEventHandler? _dispatcherUnhandledExceptionHandler;
        private System.UnhandledExceptionEventHandler? _appDomainUnhandledExceptionHandler;
        private EventHandler<UnobservedTaskExceptionEventArgs>? _taskSchedulerUnobservedExceptionHandler;

        // Ergonomic accessors for resolving services
        public IServiceProvider? Services => _serviceProvider;
        public static IServiceProvider? ServiceProvider => (App.Current as App)?.Services;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Register global exception handlers FIRST
                RegisterExceptionHandlers();

                // IDM-style Auto Extension Registration for Chrome, Edge, Firefox, Brave, Opera, Vivaldi
                EDM.Services.BrowserExtensionInstaller.InstallAllBrowsersIntegration();

                // Check for headless native-host mode early
                bool isNativeHostMode = Array.Exists(e.Args, arg => arg == "--native-host");

                // Configure dependency injection BEFORE creating MainWindow so services are available in constructors
                var services = new ServiceCollection();
                services.AddSingleton<EDM.Services.Interfaces.IFileDialogService, EDM.Services.FileDialogService>();
                services.AddSingleton<EDM.Services.Interfaces.IDialogService, EDM.Services.DialogService>();
                services.AddTransient<EDM.ViewModels.AddUrlViewModel>();

                // Register main application services and download engines as singletons
                services.AddSingleton<EDM.Services.Interfaces.IDownloadService, EDM.Services.DownloadService>();
                services.AddSingleton<EDM.Services.DownloadService>(sp => (EDM.Services.DownloadService)sp.GetRequiredService<EDM.Services.Interfaces.IDownloadService>());
                services.AddSingleton<EDM.Services.DownloadOrchestrator>();
                services.AddSingleton<EDM.Domain.Protocols.IDownloadEngine, EDM.Domain.Protocols.HttpMultiPartEngine>();
                services.AddSingleton<EDM.Domain.Protocols.HttpMultiPartEngine>();
                services.AddSingleton<EDM.Services.MediaMergeService>(sp => new EDM.Services.MediaMergeService(sp.GetService<System.Net.Http.HttpClient>() ?? EDM.Services.SharedHttpClient.Instance));
                services.AddSingleton<EDM.Services.FtpDownloadService>();
                services.AddSingleton<EDM.Services.BitTorrentService>();
                services.AddSingleton<EDM.Services.MediaVariantResolver>();

                // Register settings service FIRST (needed by ThemeService)
                services.AddSingleton<EDM.Services.Interfaces.ISettingsService, EDM.Services.SettingsService>();

                // Register theme service for dynamic theme switching
                services.AddSingleton<EDM.Services.Interfaces.IThemeService, EDM.Services.ThemeService>();

                // Register history services: SQLite (primary) + JSON (legacy fallback) + Facade for unified access
                services.AddSingleton<EDM.Services.History.HistoryService>();
                services.AddSingleton<EDM.Services.DownloadHistoryService>();
                services.AddSingleton<EDM.Services.HistoryServiceFacade>();
                services.AddSingleton<EDM.Services.Interfaces.IHistoryProvider>(sp => sp.GetRequiredService<EDM.Services.HistoryServiceFacade>());

                services.AddSingleton<EDM.Services.SchedulerService>();
                services.AddSingleton<EDM.Services.ExternalBackendService>();

                // Register WindowsNetworkMonitor as singleton INetworkMonitor for network interface switching
                services.AddSingleton<EDM.Services.INetworkMonitor, EDM.Services.WindowsNetworkMonitor>();

                // Resume scanner service to detect incomplete downloads on startup (SettingsService injected)
                services.AddSingleton<EDM.Services.ResumeScannerService>();

                // Register HlsDashDownloadService so it's available via DI if needed
                services.AddSingleton<EDM.Services.HlsDashDownloadService>();

                // Register Control Plane Client & Background Telemetry Service
                services.AddSingleton<EDM.Services.ControlPlaneClient>();
                services.AddSingleton<EDM.Services.ControlPlaneTelemetryService>();

                // Register Clipboard Monitoring Service
                services.AddSingleton<EDM.Services.Interfaces.IClipboardMonitorService, EDM.Services.ClipboardMonitorService>();
                services.AddSingleton<EDM.Services.ClipboardMonitorService>(sp => (EDM.Services.ClipboardMonitorService)sp.GetRequiredService<EDM.Services.Interfaces.IClipboardMonitorService>());

                // Register Pending Download Confirmation Queue Service
                services.AddSingleton<EDM.Services.Interfaces.IPendingConfirmationQueueService, EDM.Services.PendingConfirmationQueueService>();
                services.AddSingleton<EDM.Services.PendingConfirmationQueueService>(sp => (EDM.Services.PendingConfirmationQueueService)sp.GetRequiredService<EDM.Services.Interfaces.IPendingConfirmationQueueService>());

                // Register Unified Download Request Gateway
                services.AddSingleton<EDM.Services.Interfaces.IDownloadRequestGateway, EDM.Services.DownloadRequestGateway>();

                // Register Subscription Entitlement Client
                services.AddSingleton<ISubscriptionEntitlementClient, SubscriptionEntitlementClient>();

                _serviceProvider = services.BuildServiceProvider();

                // Initialize Telemetry & Background Status Check asynchronously
                try
                {
                    var cpClient = _serviceProvider.GetRequiredService<EDM.Services.ControlPlaneClient>();
                    var entitlementClient = _serviceProvider.GetRequiredService<ISubscriptionEntitlementClient>();
                    entitlementClient.StartBackgroundSync(TimeSpan.FromMinutes(15));
                    _ = Task.Run(async () => {
                        try { await entitlementClient.SyncPolicyAsync(); } catch { }
                    });
                    var telemetry = _serviceProvider.GetRequiredService<EDM.Services.ControlPlaneTelemetryService>();
                    telemetry.TrackAppStarted("2.0.0", Environment.OSVersion.ToString());

                    Task.Run(async () =>
                    {
                        try { await cpClient.CheckAccountStatusAsync().ConfigureAwait(false); } catch { }
                    });
                }
                catch (Exception ex)
                {
                    EDM.Services.LoggingService.LogException("[App.OnStartup] ControlPlane init", ex);
                }

                try
                {
                    this.Resources["ServiceProvider"] = _serviceProvider;
                }
                catch (Exception ex)
                {
                    EDM.Services.LoggingService.LogException("[App.OnStartup] ServiceProvider resource", ex);
                }

                // Native IPC Server & WebSocket Bridge startup for browser extension handoff
                try
                {
                    _ipcServer = new NativeIpcServer(HandleIpcHandoffAsync);
                    _ipcServer.Start();

                    _webSocketServer = new EdmWebSocketServer(HandleIpcHandoffAsync);
                    _webSocketServer.Start();
                }
                catch (Exception ex)
                {
                    EDM.Services.LoggingService.LogStartupFailure("NativeIpcServer/WebSocketServer", ex);
                }

                if (isNativeHostMode)
                {
                    EDM.Services.LoggingService.Log("=== EDM Native Host Mode (Headless) ===");
                    this.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
                }
                else
                {
                    EDM.Services.LoggingService.Log("=== EDM Application Startup (UI Mode) ===");
                    var mainWindow = new MainWindow();
                    try
                    {
                        try { _serviceProvider?.GetService(typeof(EDM.Services.HlsDashDownloadService)); } catch { }
                        try { var _ = EDM.Services.MultiNicManager.Instance; } catch { }

                        try
                        {
                            var tray = new SystemTrayManager(mainWindow);
                            try { this.Resources["SystemTrayManager"] = tray; } catch { }
                            tray.OnPauseAllRequested += () => mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try { if (mainWindow.MainDashboard.DataContext is ViewModels.DownloadManagerViewModel vm) vm.PauseAll(); } catch (Exception ex) { EDM.Services.LoggingService.LogException("[App] SystemTrayManager PauseAll handler", ex); }
                            }));
                            tray.OnResumeAllRequested += () => mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try { if (mainWindow.MainDashboard.DataContext is ViewModels.DownloadManagerViewModel vm) vm.ResumeAll(); } catch (Exception ex) { EDM.Services.LoggingService.LogException("[App] SystemTrayManager ResumeAll handler", ex); }
                            }));
                        }
                        catch (Exception ex)
                        {
                            EDM.Services.LoggingService.LogException("[App.OnStartup] SystemTrayManager init", ex);
                        }
                    }
                    catch { }

                    mainWindow.Show();

                    // Check for --handoff CLI payload argument on launch
                    for (int i = 0; i < e.Args.Length - 1; i++)
                    {
                        if (e.Args[i] == "--handoff" && !string.IsNullOrWhiteSpace(e.Args[i + 1]))
                        {
                            try
                            {
                                string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(e.Args[i + 1]));
                                var payload = System.Text.Json.JsonSerializer.Deserialize<IpcHandoffPayload>(json);
                                if (payload != null)
                                {
                                    _ = HandleIpcHandoffAsync(payload);
                                }
                            }
                            catch (Exception ex)
                            {
                                EDM.Services.LoggingService.LogException("[App.OnStartup] Error processing CLI handoff", ex);
                            }
                        }
                    }
                }

                EDM.Services.LoggingService.Log("[App] Startup completed successfully");
            }
            catch (Exception ex)
            {
                EDM.Services.LoggingService.LogStartupFailure("OnStartup", ex);
                MessageBox.Show(
                    $"Failed to start application: {ex.Message}",
                    "EDM Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Registers global exception handlers for unhandled exceptions and task failures.
        /// Handlers are stored as fields so they can be unsubscribed during shutdown.
        /// </summary>
        private void RegisterExceptionHandlers()
        {
            // Handle unhandled exceptions in UI thread
            _dispatcherUnhandledExceptionHandler = (sender, e) =>
            {
                try
                {
                    EDM.Services.LoggingService.LogException("UnhandledException (UI Thread)", e.Exception);
                    SaveCrashReport(e.Exception, "UnhandledException (UI Thread)");
                }
                catch (Exception ex2)
                {
                    EDM.Services.LoggingService.LogException("RegisterExceptionHandlers.DispatcherHandler", ex2);
                }
                // Don't suppress - let WPF handle it to avoid hiding critical errors
                e.Handled = false;
            };
            this.DispatcherUnhandledException += _dispatcherUnhandledExceptionHandler;

            // Handle unhandled exceptions in background threads
            _appDomainUnhandledExceptionHandler = (sender, e) =>
            {
                try
                {
                    var ex = e.ExceptionObject as Exception;
                    if (ex != null)
                    {
                        EDM.Services.LoggingService.LogException("UnhandledException (Background Thread)", ex);
                        SaveCrashReport(ex, "UnhandledException (Background Thread)");
                    }
                }
                catch (Exception ex2)
                {
                    EDM.Services.LoggingService.LogException("RegisterExceptionHandlers.AppDomainHandler", ex2);
                }
                // IsTerminating typically true for background thread exceptions
            };
            AppDomain.CurrentDomain.UnhandledException += _appDomainUnhandledExceptionHandler;

            // Handle unhandled Task exceptions
            _taskSchedulerUnobservedExceptionHandler = (sender, e) =>
            {
                try
                {
                    EDM.Services.LoggingService.LogException("UnobservedTaskException", e.Exception);
                    SaveCrashReport(e.Exception, "UnobservedTaskException");
                }
                catch (Exception ex2)
                {
                    EDM.Services.LoggingService.LogException("RegisterExceptionHandlers.TaskSchedulerHandler", ex2);
                }
                e.SetObserved(); // Prevent process termination
            };
            TaskScheduler.UnobservedTaskException += _taskSchedulerUnobservedExceptionHandler;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            EDM.Services.LoggingService.Log("[App.OnExit] Application exit started");

            // small helper to remove crash handlers on exit is already present below


            try
            {
                // 0. Unsubscribe from global exception handlers to prevent leaks during shutdown
                try
                {
                    if (_dispatcherUnhandledExceptionHandler != null)
                        this.DispatcherUnhandledException -= _dispatcherUnhandledExceptionHandler;
                }
                catch (Exception ex)
                {
                    EDM.Services.LoggingService.LogException("[App.OnExit] Failed to unsubscribe DispatcherUnhandledException", ex);
                }

                try
                {
                    if (_appDomainUnhandledExceptionHandler != null)
                        AppDomain.CurrentDomain.UnhandledException -= _appDomainUnhandledExceptionHandler;
                }
                catch (Exception ex)
                {
                    EDM.Services.LoggingService.LogException("[App.OnExit] Failed to unsubscribe AppDomain.UnhandledException", ex);
                }

                try
                {
                    if (_taskSchedulerUnobservedExceptionHandler != null)
                        TaskScheduler.UnobservedTaskException -= _taskSchedulerUnobservedExceptionHandler;
                }
                catch (Exception ex)
                {
                    EDM.Services.LoggingService.LogException("[App.OnExit] Failed to unsubscribe TaskScheduler.UnobservedTaskException", ex);
                }

                // 1. Dispose of native IPC server, WebSocket server, and message listener
                try
                {
                    if (_webSocketServer != null)
                    {
                        EDM.Services.LoggingService.Log("[App.OnExit] Disposing WebSocket server");
                        _webSocketServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    }
                    if (_ipcServer != null)
                    {
                        EDM.Services.LoggingService.Log("[App.OnExit] Disposing IPC server");
                        _ipcServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    }
                    if (_nativeListener != null)
                    {
                        try { _nativeListener.MessageReceived -= OnNativeMessage; } catch (Exception ex) { EDM.Services.LoggingService.LogException("[App.OnExit] Unsubscribe native listener failed", ex); }
                        EDM.Services.LoggingService.Log("[App.OnExit] Disposing native listener");
                        _nativeListener.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    }
                }
                catch (Exception ex)
                {
                    EDM.Services.LoggingService.LogException("[App.OnExit] Error disposing native listener/IPC", ex);
                }

                // 2. Dispose the DI service provider so singletons implementing IDisposable are cleaned up
                try
                {
                    EDM.Services.LoggingService.Log("[App.OnExit] Disposing service provider");
                    if (_serviceProvider is IDisposable disp)
                    {
                        try
                        {
                            disp.Dispose();
                        }
                        catch (Exception ex)
                        {
                            EDM.Services.LoggingService.LogException("[App.OnExit] ServiceProvider.Dispose", ex);
                        }
                    }
                    else if (_serviceProvider is IAsyncDisposable asyncDisp)
                    {
                        try
                        {
                            asyncDisp.DisposeAsync().AsTask().GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            EDM.Services.LoggingService.LogException("[App.OnExit] ServiceProvider.DisposeAsync", ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    EDM.Services.LoggingService.LogException("[App.OnExit] ServiceProvider disposal check", ex);
                }

                // 3. Graceful window shutdown
                try
                {
                    EDM.Services.LoggingService.Log("[App.OnExit] Closing application windows");
                    foreach (Window w in this.Windows)
                    {
                        if (w is EDM.Views.DownloadProgressWindow dpw)
                        {
                            try
                            {
                                dpw.CancelDownload();
                            }
                            catch (Exception ex)
                            {
                                EDM.Services.LoggingService.LogException("DownloadProgressWindow.CancelDownload", ex);
                            }
                        }
                    }
                    base.OnExit(e);
                }
                catch (Exception ex)
                {
                    EDM.Services.LoggingService.LogException("[App.OnExit] Window shutdown", ex);
                }

                EDM.Services.LoggingService.LogShutdown("Normal");
            }
            catch (Exception ex)
            {
                EDM.Services.LoggingService.LogException("[App.OnExit] Critical error", ex);
            }
            finally
            {
                // 4. Force exit to ensure all cleanup is complete
                try
                {
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    EDM.Services.LoggingService.LogException("[App.OnExit] Environment.Exit", ex);
                }
            }
        }

        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DownloadProgressWindow> _activeIpcWindows = new(StringComparer.OrdinalIgnoreCase);

        public async Task<bool> HandleIpcHandoffAsync(IpcHandoffPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Url)) return false;

            var settingsService = _serviceProvider?.GetService(typeof(EDM.Services.Interfaces.ISettingsService)) as EDM.Services.Interfaces.ISettingsService
                ?? new EDM.Services.SettingsService();

            // 1. Security Gate: Validate URL Scheme
            if (!SecuritySanitizer.IsAllowedUrlScheme(payload.Url))
            {
                LoggingService.LogWarning($"[App.HandleIpcHandoffAsync] Security rejection: Disallowed or unsafe scheme for '{ProtocolDetector.SanitizeUrlForLogging(payload.Url)}'");
                return false;
            }

            // 2. Setting Guard: Verify browser integration is enabled
            if (!settingsService.GetEnableBrowserIntegration() || !settingsService.GetBrowserCaptureDownloads())
            {
                LoggingService.Log("[App.HandleIpcHandoffAsync] Browser integration is disabled in settings; rejecting handoff.");
                return false;
            }

            // 3. Resolve metadata and safe filename
            string effectiveFileName = payload.Filename ?? string.Empty;
            if ((string.IsNullOrWhiteSpace(effectiveFileName) || effectiveFileName.StartsWith("YouTube_Video_", StringComparison.OrdinalIgnoreCase) || effectiveFileName == "download" || effectiveFileName == "download.mp4") && !string.IsNullOrWhiteSpace(payload.Title))
            {
                string safe = EDM.Services.Helpers.FileNamingHelper.SanitizeFileName(payload.Title);
                if (!string.IsNullOrWhiteSpace(safe))
                {
                    effectiveFileName = safe.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ? safe : $"{safe}.mp4";
                }
            }

            if ((string.IsNullOrWhiteSpace(effectiveFileName) || effectiveFileName.StartsWith("YouTube_Video_", StringComparison.OrdinalIgnoreCase)) && MediaVariantResolver.IsYouTubeUrl(payload.Url))
            {
                try
                {
                    string? fastTitle = await MediaVariantResolver.FetchYouTubeTitleFastAsync(payload.Url, CancellationToken.None).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(fastTitle))
                    {
                        string safe = EDM.Services.Helpers.FileNamingHelper.SanitizeFileName(fastTitle);
                        if (!string.IsNullOrWhiteSpace(safe))
                        {
                            effectiveFileName = safe.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ? safe : $"{safe}.mp4";
                        }
                    }
                }
                catch { }
            }

            // 4. Enqueue into Pending Confirmation Queue
            var pendingQueue = _serviceProvider?.GetService(typeof(EDM.Services.Interfaces.IPendingConfirmationQueueService)) as EDM.Services.Interfaces.IPendingConfirmationQueueService
                ?? EDM.Services.PendingConfirmationQueueService.Instance;

            var pendingReq = pendingQueue.EnqueueRequest(
                url: payload.Url,
                source: IngestionSource.BrowserExtension,
                suggestedFileName: !string.IsNullOrWhiteSpace(effectiveFileName) ? effectiveFileName : payload.Filename,
                title: payload.Title,
                referrer: payload.Referer ?? payload.PageUrl,
                cookies: payload.Cookies,
                userAgent: payload.UserAgent,
                authHeader: payload.AuthHeader,
                quality: payload.Quality,
                format: payload.Format,
                videoUrl: payload.VideoUrl,
                audioUrl: payload.AudioUrl,
                estimatedSizeBytes: payload.EstimatedSizeBytes,
                requiresFfmpegMerge: payload.RequiresFfmpegMerge);

            // 5. Zero-Trust Confirmation Policy Check
            bool requireConfirmation = settingsService.GetBrowserShowConfirmation();

            if (requireConfirmation)
            {
                // Dispatch confirmation UI on the Dispatcher
                await Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        PendingApprovalWindow.ShowOrUpdate(pendingQueue);

                        if (settingsService.GetBrowserShowNotification())
                        {
                            NotificationService.Instance.Notify(
                                "Download Request Captured",
                                $"Reviewing: {(string.IsNullOrWhiteSpace(effectiveFileName) ? payload.Url : effectiveFileName)}",
                                NotificationSeverity.Info,
                                NotificationCategory.System);
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogException("[App.HandleIpcHandoffAsync] Failed to display PendingApprovalWindow", ex);
                    }
                });

                return true;
            }

            // 6. Direct / Silent Mode Fallback (Only if confirmation explicitly disabled by user)
            if (pendingQueue.TryApprove(pendingReq.PendingRequestId, out var approvedReq) && approvedReq != null)
            {
                var gateway = _serviceProvider?.GetService(typeof(EDM.Services.Interfaces.IDownloadRequestGateway)) as EDM.Services.Interfaces.IDownloadRequestGateway
                    ?? new EDM.Services.DownloadRequestGateway(settingsService);

                var req = new EDM.Services.DownloadRequest
                {
                    Source = EDM.Services.IngestionSource.BrowserExtension,
                    Url = payload.Url,
                    SuggestedFileName = approvedReq.SuggestedFileName,
                    Referrer = approvedReq.Referrer,
                    Cookies = approvedReq.Cookies,
                    SilentMode = false
                };

                if (!string.IsNullOrWhiteSpace(payload.AuthHeader)) req.CustomHeaders["Authorization"] = payload.AuthHeader;
                if (!string.IsNullOrWhiteSpace(payload.UserAgent)) req.CustomHeaders["User-Agent"] = payload.UserAgent;

                var result = await gateway.SubmitRequestAsync(req).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    LoggingService.Log($"[App.HandleIpcHandoffAsync] Gateway rejected direct request: {result.Status} - {result.Message}");
                    return false;
                }

                if (result.Item != null)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            var item = result.Item;
                            string safeFileName = item.FileName;
                            string downloadIdentity = !string.IsNullOrWhiteSpace(payload.DownloadIdentity)
                                ? payload.DownloadIdentity
                                : $"{item.Url}|{payload.Quality}|{payload.VideoUrl}|{safeFileName}";

                            item.DownloadIdentity = downloadIdentity;

                            if (_activeIpcWindows.TryGetValue(downloadIdentity, out var existingWin) && existingWin != null && existingWin.IsLoaded)
                            {
                                if (existingWin.WindowState == WindowState.Minimized) existingWin.WindowState = WindowState.Normal;
                                existingWin.Activate();
                                existingWin.Focus();
                                return;
                            }

                            if (!string.IsNullOrWhiteSpace(payload.VideoUrl)) item.VideoUrl = payload.VideoUrl;
                            if (!string.IsNullOrWhiteSpace(payload.AudioUrl)) item.AudioUrl = payload.AudioUrl;
                            if (!string.IsNullOrWhiteSpace(payload.Quality)) item.Quality = payload.Quality;
                            if (!string.IsNullOrWhiteSpace(payload.Format)) item.DesiredFormat = payload.Format;
                            if (!string.IsNullOrWhiteSpace(payload.Title)) item.Title = payload.Title;
                            if (payload.RequiresFfmpegMerge) item.RequiresFfmpegMerge = true;
                            if (payload.EstimatedSizeBytes.HasValue && payload.EstimatedSizeBytes.Value > 0)
                            {
                                item.EstimatedSizeBytes = payload.EstimatedSizeBytes.Value;
                                item.Size = $"≈ {payload.EstimatedSizeBytes.Value / (1024.0 * 1024.0):F1} MB";
                            }

                            var progressWin = new DownloadProgressWindow(item);
                            _activeIpcWindows[downloadIdentity] = progressWin;
                            progressWin.Closed += (s, e) => _activeIpcWindows.TryRemove(downloadIdentity, out _);

                            progressWin.Show();
                            progressWin.Activate();
                            progressWin.Focus();

                            BackgroundTaskManager.FireAndForget("IpcDownloadTask", async () =>
                            {
                                try
                                {
                                    await progressWin.StartDownloadForItemAsync(item).ConfigureAwait(false);
                                }
                                catch (Exception ex)
                                {
                                    LoggingService.LogBackgroundTaskFailure("IpcDownloadTask", ex);
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            LoggingService.LogException("[App.HandleIpcHandoffAsync] Direct UI dispatch failed", ex);
                        }
                    });
                }
            }

            return true;
        }

        private async Task OnNativeMessage(System.Text.Json.JsonElement element)
        {
            try
            {
                if (element.TryGetProperty("action", out var actionProp))
                {
                    string action = actionProp.GetString() ?? "";
                    if (action.Equals("DOWNLOAD_ALL_LINKS", StringComparison.OrdinalIgnoreCase))
                    {
                        var urls = new List<string>();
                        if (element.TryGetProperty("links", out var linksProp) && linksProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var link in linksProp.EnumerateArray())
                            {
                                string? l = link.GetString();
                                if (!string.IsNullOrEmpty(l)) urls.Add(l);
                            }
                        }
                        else if (element.TryGetProperty("url", out var pageUrlProp))
                        {
                            string pageUrl = pageUrlProp.GetString() ?? "";
                            if (!string.IsNullOrEmpty(pageUrl))
                            {
                                var grabber = new SiteGrabberService();
                                urls = await grabber.ScanPageAsync(pageUrl).ConfigureAwait(false);
                            }
                        }

                        await Dispatcher.InvokeAsync(() =>
                        {
                            var win = new Views.DownloadAllLinksWindow(urls);
                            win.Show();
                        });
                        return;
                    }
                    else if (action.Equals("SITE_GRABBER", StringComparison.OrdinalIgnoreCase))
                    {
                        string startUrl = element.TryGetProperty("url", out var u) ? (u.GetString() ?? "") : "";
                        await Dispatcher.InvokeAsync(() =>
                        {
                            var win = new Views.SiteGrabberWizardWindow();
                            if (!string.IsNullOrEmpty(startUrl)) win.StartUrlBox.Text = startUrl;
                            win.Show();
                        });
                        return;
                    }
                }

                if (element.TryGetProperty("url", out var urlProp))
                {
                    string? url = urlProp.GetString();
                    if (string.IsNullOrWhiteSpace(url)) return;

                    string? filename = element.TryGetProperty("filename", out var fn) ? fn.GetString() : null;
                    string? cookies = element.TryGetProperty("cookies", out var ck) ? ck.GetString() : null;

                    var payload = new IpcHandoffPayload
                    {
                        Url = url,
                        Filename = filename,
                        Cookies = cookies
                    };

                    await HandleIpcHandoffAsync(payload).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                EDM.Services.LoggingService.LogException("[App.OnNativeMessage] Critical error", ex);
            }
        }

#if DEBUG
        /// <summary>
        /// Debug-only: validate that a small set of canonical resource keys exist in Application resources
        /// Logs missing keys as warnings instead of throwing to aid startup diagnostics.
        /// </summary>
        private void ValidateResources()
        {
            var keys = new[]
            {
                "PrimaryTextBrush",
                "SecondaryTextBrush",
                "BackgroundBrush",
                "SurfaceBrush",
                "CardBrush",
                "BorderBrush",
                "AccentBrush",
                "SuccessBrush",
                "WarningBrush",
                "ErrorBrush",
                "DisabledBrush",
                "AccentGradientBrush",
                "CardShadow",
                "PurpleGradient",
                "TextPrimaryBrush",
                "TextSecondaryBrush",
                "AppFontFamily",
                "FontSizeNormal"
            };

            var missing = new System.Collections.Generic.List<string>();
            foreach (var k in keys)
            {
                if (!ResourceExists(k)) missing.Add(k);
            }

            if (missing.Count > 0)
            {
                EDM.Services.LoggingService.LogWarning($"[App.ValidateResources] Missing resource keys: {string.Join(", ", missing)}");
            }
            else
            {
                EDM.Services.LoggingService.Log("[App.ValidateResources] All canonical resource keys present.");
            }
        }

        private bool ResourceExists(object key)
        {
            try
            {
                if (System.Windows.Application.Current == null) return false;
                var dict = System.Windows.Application.Current.Resources;
                if (dict.Contains(key)) return true;
                foreach (var md in dict.MergedDictionaries)
                {
                    if (md == null) continue;
                    if (md.Contains(key)) return true;
                }
            }
            catch (Exception ex)
            {
                EDM.Services.LoggingService.LogException("[App.ResourceExists] Error checking resources", ex);
            }
            return false;
        }
#endif

    }
}
