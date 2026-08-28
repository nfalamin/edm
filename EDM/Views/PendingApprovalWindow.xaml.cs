using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EDM.Models;
using EDM.Services;
using EDM.Services.Interfaces;

namespace EDM.Views
{
    /// <summary>
    /// Interaction logic for PendingApprovalWindow.xaml.
    /// Provides an interactive user confirmation gate for reviewing and approving/rejecting
    /// incoming external download requests before any network traffic is initiated.
    /// </summary>
    public partial class PendingApprovalWindow : Window
    {
        private static PendingApprovalWindow? _activeInstance;
        private static readonly object _instanceLock = new();

        private readonly IPendingConfirmationQueueService _queueService;
        private readonly ISettingsService _settingsService;
        private readonly ObservableCollection<PendingDownloadRequest> _pendingItems = new();

        public PendingApprovalWindow(IPendingConfirmationQueueService? queueService = null, ISettingsService? settingsService = null)
        {
            InitializeComponent();

            _queueService = queueService 
                ?? (App.ServiceProvider?.GetService(typeof(IPendingConfirmationQueueService)) as IPendingConfirmationQueueService)
                ?? PendingConfirmationQueueService.Instance;

            _settingsService = settingsService
                ?? (App.ServiceProvider?.GetService(typeof(ISettingsService)) as ISettingsService)
                ?? new SettingsService();

            PendingItemsControl.ItemsSource = _pendingItems;

            // Wire real-time queue events
            _queueService.RequestEnqueued += OnQueueRequestEnqueued;
            _queueService.RequestStateChanged += OnQueueRequestStateChanged;

            this.Closed += OnWindowClosed;

            RefreshList();
            SyncTheme();
        }

        public static void ShowOrUpdate(IPendingConfirmationQueueService? queueService = null)
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;

            if (!app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.BeginInvoke(() => ShowOrUpdate(queueService));
                return;
            }

            lock (_instanceLock)
            {
                if (_activeInstance != null && _activeInstance.IsLoaded)
                {
                    _activeInstance.RefreshList();
                    if (_activeInstance.WindowState == WindowState.Minimized)
                    {
                        _activeInstance.WindowState = WindowState.Normal;
                    }
                    _activeInstance.Activate();
                    _activeInstance.Focus();
                    return;
                }

                _activeInstance = new PendingApprovalWindow(queueService);
                if (app.MainWindow != null && app.MainWindow.IsLoaded)
                {
                    _activeInstance.Owner = app.MainWindow;
                }
                _activeInstance.Show();
                _activeInstance.Activate();
                _activeInstance.Focus();
            }
        }

        private void SyncTheme()
        {
            try
            {
                var appRes = System.Windows.Application.Current?.Resources?.MergedDictionaries;
                var themeDict = appRes?.FirstOrDefault(d => d.Source != null &&
                    (d.Source.OriginalString.Contains("LightTheme") || d.Source.OriginalString.Contains("DarkTheme")));

                bool isLight = themeDict?.Source?.OriginalString?.Contains("LightTheme") == true;
                if (isLight)
                {
                    this.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF0, 0xF4, 0xFF));
                    this.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x29, 0x3B));
                    this.Resources["DlgBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF0, 0xF4, 0xFF));
                    this.Resources["DlgCardBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
                    this.Resources["DlgCardBorder"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC7, 0xD2, 0xF7));
                    this.Resources["DlgHeaderBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xEF, 0xFF));
                    this.Resources["DlgTextPrimary"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x29, 0x3B));
                    this.Resources["DlgTextSecondary"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4B, 0x55, 0x63));
                    this.Resources["DlgTextMuted"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6B, 0x72, 0x80));
                }
            }
            catch { }
        }

        private void RefreshList()
        {
            var pending = _queueService.GetPendingRequests();
            _pendingItems.Clear();
            foreach (var req in pending)
            {
                _pendingItems.Add(req);
                _queueService.MarkAsDisplayed(req.PendingRequestId);
            }

            UpdateBadgeCount();
        }

        private void UpdateBadgeCount()
        {
            int count = _pendingItems.Count;
            PendingCountBadge.Text = $"{count} Pending";
            EmptyStateCard.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
            PendingItemsControl.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnQueueRequestEnqueued(object? sender, PendingRequestEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (!_pendingItems.Any(r => r.PendingRequestId == e.Request.PendingRequestId))
                {
                    _pendingItems.Add(e.Request);
                    _queueService.MarkAsDisplayed(e.Request.PendingRequestId);
                    UpdateBadgeCount();
                }
            });
        }

        private void OnQueueRequestStateChanged(object? sender, PendingRequestEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (e.NewStatus is PendingConfirmationStatus.Approved 
                                  or PendingConfirmationStatus.Rejected 
                                  or PendingConfirmationStatus.Expired 
                                  or PendingConfirmationStatus.Cancelled 
                                  or PendingConfirmationStatus.Failed)
                {
                    var existing = _pendingItems.FirstOrDefault(r => r.PendingRequestId == e.Request.PendingRequestId);
                    if (existing != null)
                    {
                        _pendingItems.Remove(existing);
                        UpdateBadgeCount();
                    }
                }
            });
        }

        private async void ApproveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is Guid requestId)
            {
                elem.IsEnabled = false;
                await ProcessApprovalAsync(requestId);
            }
        }

        private async Task ProcessApprovalAsync(Guid requestId)
        {
            // Atomically approve through the queue service state machine
            if (_queueService.TryApprove(requestId, out var approvedReq) && approvedReq != null)
            {
                StatusMessageText.Text = $"Approved: {approvedReq.DisplayName}";

                // Submit to authoritative DownloadRequestGateway
                var gateway = (App.ServiceProvider?.GetService(typeof(IDownloadRequestGateway)) as IDownloadRequestGateway)
                    ?? new DownloadRequestGateway(_settingsService);

                var dlReq = new DownloadRequest
                {
                    Source = approvedReq.Source,
                    Url = approvedReq.Url,
                    SuggestedFileName = approvedReq.SuggestedFileName,
                    DestinationDirectory = approvedReq.DestinationDirectory ?? string.Empty,
                    TargetCategory = approvedReq.TargetCategory,
                    TargetQueueId = approvedReq.TargetQueueId ?? "default",
                    Referrer = approvedReq.Referrer,
                    Cookies = approvedReq.Cookies,
                    SilentMode = false // User approved: starts downloading as normal
                };

                if (!string.IsNullOrWhiteSpace(approvedReq.AuthHeader)) dlReq.CustomHeaders["Authorization"] = approvedReq.AuthHeader;
                if (!string.IsNullOrWhiteSpace(approvedReq.UserAgent)) dlReq.CustomHeaders["User-Agent"] = approvedReq.UserAgent;

                var result = await gateway.SubmitRequestAsync(dlReq).ConfigureAwait(true);
                if (result.IsSuccess)
                {
                    LoggingService.Log($"[PendingApprovalWindow] Download successfully enqueued and started for {approvedReq.DisplayName}");
                }
                else
                {
                    LoggingService.LogWarning($"[PendingApprovalWindow] Gateway rejected approved download: {result.Message}");
                }
            }
            else
            {
                StatusMessageText.Text = "Item has already been processed or expired.";
            }

            var item = _pendingItems.FirstOrDefault(r => r.PendingRequestId == requestId);
            if (item != null) _pendingItems.Remove(item);
            UpdateBadgeCount();
        }

        private void RejectItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is Guid requestId)
            {
                _queueService.TryReject(requestId, "User rejected in Confirmation Gate");
                var item = _pendingItems.FirstOrDefault(r => r.PendingRequestId == requestId);
                if (item != null) _pendingItems.Remove(item);
                UpdateBadgeCount();
                StatusMessageText.Text = "Download request rejected.";
            }
        }

        private async void ApproveAll_Click(object sender, RoutedEventArgs e)
        {
            var itemsToApprove = _pendingItems.ToList();
            foreach (var item in itemsToApprove)
            {
                await ProcessApprovalAsync(item.PendingRequestId);
            }
        }

        private void RejectAll_Click(object sender, RoutedEventArgs e)
        {
            var itemsToReject = _pendingItems.ToList();
            foreach (var item in itemsToReject)
            {
                _queueService.TryReject(item.PendingRequestId, "User rejected all requests");
                _pendingItems.Remove(item);
            }
            UpdateBadgeCount();
            StatusMessageText.Text = "All pending requests rejected.";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _queueService.RequestEnqueued -= OnQueueRequestEnqueued;
            _queueService.RequestStateChanged -= OnQueueRequestStateChanged;

            lock (_instanceLock)
            {
                if (_activeInstance == this)
                {
                    _activeInstance = null;
                }
            }
        }
    }
}
