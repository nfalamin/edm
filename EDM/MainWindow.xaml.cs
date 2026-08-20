using System.Collections.ObjectModel;
using System;
using System.Windows;
using System.Windows.Input;
using EDM.Services;     
using EDM.Services.Interfaces;
using EDM.Views;
using EDM.Models;
using System.IO;
using System.Linq;

namespace EDM
{
    /// <summary>
    /// MainWindow - Primary application window for EDM 1.0 (Exclusive Download Manager)
    /// Manages modular components: CustomTitleBar, Sidebar, and Dashboard
    /// </summary>
    public partial class MainWindow : Window
    {
        // Downloads data collection for binding
        public ObservableCollection<DownloadItem> Downloads { get; set; } = new System.Collections.ObjectModel.ObservableCollection<DownloadItem>();

        private readonly IHistoryProvider? _historyService;
        private ClipboardMonitorService? _clipboardMonitor;
        private SchedulerService? _schedulerService;
        private Action? _onScheduleTriggeredHandler;
        private DownloadQueueManager _downloadQueueManager = null!;
        private const int MaxConcurrentDownloads = 3;
        private CancellationTokenSource? _historyLoadCts;

        public MainWindow()
        {
            InitializeComponent();

            // Share the ViewModel created by Dashboard with the Sidebar
            if (MainDashboard.DataContext is ViewModels.DownloadManagerViewModel vm)
            {
                MainSidebar.ViewModel = vm;
            }

            // Initialize history service from dependency injection
            _historyService = App.ServiceProvider?.GetService(typeof(EDM.Services.Interfaces.IHistoryProvider)) as IHistoryProvider;
            if (_historyService == null)
            {
                _historyService = App.ServiceProvider?.GetService(typeof(EDM.Services.History.HistoryService)) as IHistoryProvider
                    ?? App.ServiceProvider?.GetService(typeof(EDM.Services.DownloadHistoryService)) as IHistoryProvider;
            }

            // Initialize download queue manager
            _downloadQueueManager = new DownloadQueueManager(MaxConcurrentDownloads);
            Downloads = new System.Collections.ObjectModel.ObservableCollection<DownloadItem>();
            this.DataContext = this;
            _historyLoadCts = new CancellationTokenSource();

            // Load download history asynchronously
            if (_historyService != null)
            {
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        var loaded = await _historyService.LoadHistoryAsync().ConfigureAwait(false);
                        if (!_historyLoadCts.Token.IsCancellationRequested)
                        {
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                Downloads.Clear();
                                foreach (var d in loaded) Downloads.Add(d);
                            });
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { EDM.Services.LoggingService.Log($"[MainWindow] Load history failed: {ex.Message}"); }
                }, _historyLoadCts.Token);
            }

            // Initialize clipboard monitor for URL detection
            _clipboardMonitor = new ClipboardMonitorService(OnClipboardUrlDetected);
            _clipboardMonitor.Start();

            // Initialize scheduler service
            try
            {
                _schedulerService = App.ServiceProvider?.GetService(typeof(EDM.Services.SchedulerService)) as SchedulerService;
                if (_schedulerService == null) throw new InvalidOperationException("SchedulerService is not registered.");
                _onScheduleTriggeredHandler = () =>
                {
                    this.Dispatcher.BeginInvoke(() => StartQueuedDownloadsFromScheduler());
                };
                _schedulerService.OnScheduleTriggered += _onScheduleTriggeredHandler;
            }
            catch (Exception ex) { EDM.Services.LoggingService.LogException("[MainWindow] Scheduler init failed", ex); }

            this.Closed += MainWindow_Closed;
        }

        /// <summary>
        /// Detect URLs from clipboard and trigger download dialog
        /// </summary>
        /// <summary>
        /// Detect URLs from clipboard and trigger download dialog (single instance guarded)
        /// </summary>
        private void OnClipboardUrlDetected(string url)
        {
            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var _) && !url.Contains("://"))
                {
                    url = "https://" + url;
                }

                this.Dispatcher.BeginInvoke(() =>
                {
                    // Guard against duplicate dialogs
                    var existing = System.Windows.Application.Current.Windows.OfType<AddUrlWindow>().FirstOrDefault();
                    if (existing != null)
                    {
                        existing.UrlTextBox.Text = url;
                        existing.Activate();
                        return;
                    }

                    var addUrlWindow = new AddUrlWindow();
                    var vm = MainDashboard.DataContext as ViewModels.DownloadManagerViewModel;
                    addUrlWindow.Initialize(vm, url);
                    addUrlWindow.Owner = this;
                    addUrlWindow.ShowDialog();
                });
            }
            catch (Exception ex)
            {
                EDM.Services.LoggingService.LogException("[MainWindow] Clipboard URL detection failed", ex);
            }
        }

        /// <summary>
        /// Start queued downloads triggered by scheduler
        /// </summary>
        private void StartQueuedDownloadsFromScheduler()
        {
            try
            {
                var vm = MainDashboard.DataContext as ViewModels.DownloadManagerViewModel;
                if (vm != null)
                {
                    var queuedItems = vm.AllDownloads.Where(d => d.Status != null && d.Status.Contains("Queued", StringComparison.OrdinalIgnoreCase)).ToList();
                    foreach (var item in queuedItems)
                    {
                        item.Status = "Downloading";
                        _ = vm.StartDownloadProcessAsync(item);
                        System.Diagnostics.Debug.WriteLine($"Started scheduled download: {item.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                EDM.Services.LoggingService.LogException("[MainWindow] Scheduler download trigger failed", ex);
            }
        }

        /// <summary>
        /// Clean up resources when window closes
        /// </summary>
        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            try
            {
                // Cancel any pending history load
                _historyLoadCts?.Cancel();
                _historyLoadCts?.Dispose();

                // Stop clipboard monitor
                if (_clipboardMonitor != null)
                {
                    _clipboardMonitor.Stop();
                    // Only dispose if the service implements IDisposable
                    if (_clipboardMonitor is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

                // Unsubscribe from scheduler
                if (_schedulerService != null && _onScheduleTriggeredHandler != null)
                {
                    _schedulerService.OnScheduleTriggered -= _onScheduleTriggeredHandler;
                }

                // Dispose download queue manager if it implements IDisposable
                if (_downloadQueueManager is IDisposable disposableQueueManager)
                {
                    disposableQueueManager.Dispose();
                }
            }
            catch (Exception ex)
            {
                EDM.Services.LoggingService.LogException("[MainWindow] Cleanup failed", ex);
            }
        }
    }
}
