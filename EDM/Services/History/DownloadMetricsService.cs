using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using EDM.Helpers;

namespace EDM.Services.History
{
    public class DownloadMetricsSnapshot
    {
        public int TotalDownloadsCount { get; set; }
        public int ActiveDownloadsCount { get; set; }
        public int CompletedDownloadsCount { get; set; }
        public long TotalDownloadedBytes { get; set; }
        public string TotalSizeDownloadedFormatted { get; set; } = "0 B";
    }

    /// <summary>
    /// Authoritative Single Metrics Service for EDM Dashboard.
    /// Uses the persistent SQLite database as the single source of truth for historical statistics
    /// while aggregating live active downloads and debouncing high-frequency UI updates.
    /// </summary>
    public class DownloadMetricsService : INotifyPropertyChanged
    {
        private static readonly Lazy<DownloadMetricsService> _instance = new(() => new DownloadMetricsService());
        public static DownloadMetricsService Instance => _instance.Value;

        private readonly HistoryService _historyService;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        private int _totalDownloadsCount = 0;
        private int _activeDownloadsCount = 0;
        private int _completedDownloadsCount = 0;
        private long _totalDownloadedBytes = 0;
        private string _totalSizeDownloadedFormatted = "0 B";

        public int TotalDownloadsCount { get => _totalDownloadsCount; private set { _totalDownloadsCount = value; OnPropertyChanged(); } }
        public int ActiveDownloadsCount { get => _activeDownloadsCount; private set { _activeDownloadsCount = value; OnPropertyChanged(); } }
        public int CompletedDownloadsCount { get => _completedDownloadsCount; private set { _completedDownloadsCount = value; OnPropertyChanged(); } }
        public long TotalDownloadedBytes { get => _totalDownloadedBytes; private set { _totalDownloadedBytes = value; OnPropertyChanged(); } }
        public string TotalSizeDownloadedFormatted { get => _totalSizeDownloadedFormatted; private set { _totalSizeDownloadedFormatted = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action<DownloadMetricsSnapshot>? MetricsChanged;

        public DownloadMetricsService(HistoryService? historyService = null)
        {
            _historyService = historyService ?? new HistoryService();
        }

        /// <summary>
        /// Asynchronously queries SQLite aggregate metrics and updates observable properties safely on Dispatcher.
        /// </summary>
        public async Task<DownloadMetricsSnapshot> RefreshMetricsAsync(int activeDownloads = -1)
        {
            await _refreshLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var (total, completed, downloadedBytes) = await _historyService.GetMetricsSnapshotAsync().ConfigureAwait(false);

                // Format byte string (guaranteed never negative)
                string formatted = downloadedBytes > 0 
                    ? SizeFormatter.FormatBytes(downloadedBytes, "0 B") 
                    : "0 B";

                var snapshot = new DownloadMetricsSnapshot
                {
                    TotalDownloadsCount = total,
                    ActiveDownloadsCount = activeDownloads >= 0 ? activeDownloads : _activeDownloadsCount,
                    CompletedDownloadsCount = completed,
                    TotalDownloadedBytes = downloadedBytes,
                    TotalSizeDownloadedFormatted = formatted
                };

                // Dispatch to UI thread if Application is running
                var app = System.Windows.Application.Current;
                if (app != null && app.Dispatcher != null && !app.Dispatcher.CheckAccess())
                {
                    await app.Dispatcher.InvokeAsync(() => ApplySnapshot(snapshot));
                }
                else
                {
                    ApplySnapshot(snapshot);
                }

                MetricsChanged?.Invoke(snapshot);
                return snapshot;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        public void SetActiveDownloadsCount(int count)
        {
            ActiveDownloadsCount = Math.Max(0, count);
        }

        private void ApplySnapshot(DownloadMetricsSnapshot snapshot)
        {
            TotalDownloadsCount = snapshot.TotalDownloadsCount;
            ActiveDownloadsCount = snapshot.ActiveDownloadsCount;
            CompletedDownloadsCount = snapshot.CompletedDownloadsCount;
            TotalDownloadedBytes = snapshot.TotalDownloadedBytes;
            TotalSizeDownloadedFormatted = snapshot.TotalSizeDownloadedFormatted;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
