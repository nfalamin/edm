using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using EDM.Models;
using EDM.Services;
using EDM.Services.History;

namespace EDM.ViewModels
{
    /// <summary>
    /// DownloadManagerViewModel - Centralized state management for downloads
    /// Provides filtering, action handling, and event notifications for UI synchronization
    /// Upgraded to use CommunityToolkit.Mvvm for property change notification
    /// </summary>
    public partial class DownloadManagerViewModel : ViewModelBase
    {
        /// <summary>
        /// All downloads in the manager
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private ObservableCollection<DownloadItem> allDownloads = new ObservableCollection<DownloadItem>();

        /// <summary>
        /// Filtered downloads based on current category/status
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private ObservableCollection<DownloadItem> filteredDownloads = new ObservableCollection<DownloadItem>();

        /// <summary>
        /// Current filter selection (All, Downloading, Paused, Queued, Completed)
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string currentFilter = "All";

        /// <summary>
        /// Reference to parent ViewModel if managed by parent control
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private DownloadManagerViewModel? parentViewModel;

        // ==================== DASHBOARD METRICS PROPERTIES ====================

        /// <summary>
        /// Total number of all downloads (all-time)
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private int totalDownloadsCount = 0;

        /// <summary>
        /// Number of currently downloading/active items
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private int activeDownloadsCount = 0;

        /// <summary>
        /// Number of completed downloads
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private int completedDownloadsCount = 0;

        /// <summary>
        /// Total size of all downloaded data in formatted string (GB/MB/KB)
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string totalSizeDownloaded = "0 MB";

        /// <summary>
        /// Current download speed in MB/s format (real-time)
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string currentDownloadSpeed = "0 MB/s";

        /// <summary>
        /// Total amount of data uploaded (this session or all-time)
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string totalUploadedData = "0 MB";

        /// <summary>
        /// Network connection status description
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string connectionStatus = "High Speed Connection";

        /// <summary>
        /// Bandwidth availability status
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string bandwidthStatus = "Unlimited Bandwidth";

        /// <summary>
        /// Maximum speed limit for downloads
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string maxSpeedLimit = "Unlimited";

        public DownloadManagerViewModel()
        {
            AllDownloads = new ObservableCollection<DownloadItem>();
            FilteredDownloads = new ObservableCollection<DownloadItem>();
            // Load real history from SQLite on startup (fire-and-forget, UI-safe)
            _ = LoadHistoryFromDatabaseAsync();
        }

        /// <summary>
        /// Partial method called when CurrentFilter property changes.
        /// Automatically called by the MVVM Toolkit source generator.
        /// </summary>
        partial void OnCurrentFilterChanged(string value)
        {
            ApplyFilter();
        }

        /// <summary>
        /// Partial method called when AllDownloads property changes.
        /// Automatically called by the MVVM Toolkit source generator.
        /// </summary>
        partial void OnAllDownloadsChanged(ObservableCollection<DownloadItem> value)
        {
            ApplyFilter();
        }

        // ===== File extension helper sets =====
        private static readonly string[] VideoExts     = { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v" };
        private static readonly string[] MusicExts     = { ".mp3", ".flac", ".wav", ".aac", ".ogg", ".m4a", ".wma" };
        private static readonly string[] DocumentExts  = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".epub" };
        private static readonly string[] ProgramExts   = { ".exe", ".msi", ".apk", ".dmg", ".deb", ".rpm", ".appimage" };
        private static readonly string[] CompressedExts = { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".tar.gz" };

        private static bool HasExtension(string fileName, string[] extensions)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            string lower = fileName.ToLowerInvariant();
            foreach (var ext in extensions)
                if (lower.EndsWith(ext)) return true;
            return false;
        }

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string searchQuery = string.Empty;

        partial void OnSearchQueryChanged(string value)
        {
            ApplyFilter();
        }

        /// <summary>
        /// Apply current filter to populate FilteredDownloads.
        /// Supports: AllDownloads, All, Downloading, Paused, Queued, Completed,
        ///           Video, Music, Documents, Programs, Compressed, Queues, and real-time SearchQuery
        /// </summary>
        public void ApplyFilter()
        {
            var matchedItems = new System.Collections.Generic.List<DownloadItem>();

            foreach (var download in AllDownloads)
            {
                bool matches = CurrentFilter switch
                {
                    "Downloading"  => download.Status != null && download.Status.Contains("Downloading"),
                    "Paused"       => download.Status != null && download.Status.Contains("Paused"),
                    "Queued"       => download.Status != null && download.Status.Contains("Queued"),
                    "Queues"       => download.Status != null && download.Status.Contains("Queued"),
                    "Completed"    => download.Status != null && download.Status.Contains("Completed"),
                    "Video"        => HasExtension(download.FileName, VideoExts) || (download.Category != null && download.Category.Equals("Video", StringComparison.OrdinalIgnoreCase)),
                    "Music"        => HasExtension(download.FileName, MusicExts) || (download.Category != null && download.Category.Equals("Music", StringComparison.OrdinalIgnoreCase)),
                    "Documents"    => HasExtension(download.FileName, DocumentExts) || (download.Category != null && download.Category.Equals("Documents", StringComparison.OrdinalIgnoreCase)),
                    "Programs"     => HasExtension(download.FileName, ProgramExts) || (download.Category != null && download.Category.Equals("Programs", StringComparison.OrdinalIgnoreCase)),
                    "Compressed"   => HasExtension(download.FileName, CompressedExts) || (download.Category != null && download.Category.Equals("Compressed", StringComparison.OrdinalIgnoreCase)),
                    "AllDownloads" => true,
                    _              => true  // "All", "Dashboard" or any unknown
                };

                if (matches && !string.IsNullOrWhiteSpace(SearchQuery))
                {
                    string q = SearchQuery.Trim();
                    matches = (download.FileName != null && download.FileName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                              (download.Url != null && download.Url.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                              (download.Category != null && download.Category.Contains(q, StringComparison.OrdinalIgnoreCase));
                }

                if (matches)
                    matchedItems.Add(download);
            }

            if (FilteredDownloads == null)
            {
                FilteredDownloads = new ObservableCollection<DownloadItem>(matchedItems);
            }
            else
            {
                FilteredDownloads.Clear();
                foreach (var it in matchedItems)
                {
                    FilteredDownloads.Add(it);
                }
            }
            OnPropertyChanged(nameof(FilteredDownloads));
        }

        /// <summary>
        /// Toggle download pause/resume state — wires real PauseTokenSource.
        /// </summary>
        [RelayCommand]
        public void TogglePauseResume(DownloadItem? download)
        {
            if (download == null) return;

            if (download.Status.Contains("Downloading"))
            {
                // Actually pause the running task
                download.PauseSource.Pause();
                download.Status = "Paused";
                download.TransferRate = "0 B/s";
            }
            else if (download.Status.Contains("Paused"))
            {
                // Actually resume the running task
                download.PauseSource.Resume();
                download.Status = "Downloading";
                _ = StartDownloadProcessAsync(download);
            }
            else
            {
                download.Status = "Downloading";
                _ = StartDownloadProcessAsync(download);
            }

            ApplyFilter();
            RecalculateMetrics();
        }

        /// <summary>
        /// Resume all paused/queued/stopped downloads — actually calls PauseSource.Resume().
        /// </summary>
        [RelayCommand]
        public void ResumeAll()
        {
            foreach (var download in AllDownloads
                .Where(d => d.Status.Contains("Paused") || d.Status.Contains("Queued") || d.Status.Contains("Stopped"))
                .ToList())
            {
                download.PauseSource.Resume(); // unblock any waiting task
                download.Status = "Downloading";
                _ = StartDownloadProcessAsync(download);
            }
            ApplyFilter();
            RecalculateMetrics();
        }

        /// <summary>
        /// Pause all active downloads — actually calls PauseTokenSource.Pause() to suspend network I/O.
        /// </summary>
        [RelayCommand]
        public void PauseAll()
        {
            foreach (var download in AllDownloads.Where(d => d.Status.Contains("Downloading")).ToList())
            {
                download.PauseSource.Pause(); // suspends SegmentWorker I/O at next WaitIfPausedAsync()
                download.Status = "Paused";
                download.TransferRate = "0 B/s";
            }
            ApplyFilter();
            RecalculateMetrics();
        }

        /// <summary>
        /// Stop all active downloads — calls CancelAndReset() to hard-cancel and replace CTS.
        /// </summary>
        [RelayCommand]
        public void StopAll()
        {
            foreach (var download in AllDownloads.Where(d => d.Status.Contains("Downloading")).ToList())
            {
                download.CancelAndReset(); // fires CancellationToken, creates fresh one for next start
                download.PauseSource.Resume(); // unblock any paused wait so cancel propagates
                download.Status = "Stopped";
                download.TransferRate = "0 B/s";
            }
            ApplyFilter();
            RecalculateMetrics();
        }

        /// <summary>
        /// Delete selected or completed download items
        /// </summary>
        [RelayCommand]
        public void DeleteSelected()
        {
            var itemsToDelete = AllDownloads.Where(d => d.IsSelected).ToList();
            if (!itemsToDelete.Any() && AllDownloads.Any())
            {
                // Delete first completed or last item if none explicitly selected
                var target = AllDownloads.FirstOrDefault(d => d.Status == "Completed") ?? AllDownloads.LastOrDefault();
                if (target != null) itemsToDelete.Add(target);
            }

            foreach (var item in itemsToDelete)
            {
                AllDownloads.Remove(item);
            }
            ApplyFilter();
            RecalculateMetrics();
        }

        /// <summary>
        /// Delete a download item
        /// </summary>
        [RelayCommand]
        public void DeleteDownload(DownloadItem? download)
        {
            if (download == null) return;

            AllDownloads.Remove(download);
            ApplyFilter();
            RecalculateMetrics();
        }

        /// <summary>
        /// Add a new download and start progress loop if marked active
        /// </summary>
        [RelayCommand]
        public void AddDownload(DownloadItem? download)
        {
            if (download == null) return;

            AllDownloads.Insert(0, download);
            ApplyFilter();
            RecalculateMetrics();

            // Persist immediately to history database
            BackgroundTaskManager.FireAndForget("AddDownloadHistory", async () =>
            {
                try
                {
                    var historyService = (App.ServiceProvider?.GetService(typeof(HistoryService)) as HistoryService) ?? new HistoryService();
                    await historyService.SaveDownloadAsync(download).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[DownloadManagerViewModel.AddDownload] Failed to save history entry", ex);
                }
            });

            if (download.Status == "Downloading")
            {
                _ = StartDownloadProcessAsync(download);
            }
        }

        /// <summary>
        /// Runs real download via DownloadService, using item's own PauseSource and CancellationToken.
        /// Falls back to smooth simulated progress loop if DownloadService is unavailable.
        /// </summary>
        public async Task StartDownloadProcessAsync(DownloadItem item)
        {
            if (item == null) return;

            var downloadService = (App.ServiceProvider?.GetService(typeof(DownloadService)) as DownloadService) ?? new DownloadService();
            if (!string.IsNullOrWhiteSpace(item.Url) && !string.IsNullOrWhiteSpace(item.SavePath))
            {
                var progress = new Progress<DownloadProgressInfo>(info =>
                {
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        item.Progress = info.ProgressPercentage;
                        if (info.TotalBytes.HasValue && info.TotalBytes.Value > 0)
                        {
                            item.Size = FormatBytes(info.TotalBytes.Value);
                        }
                        item.TransferRate = info.SpeedBytesPerSecond > 0 ? $"{info.SpeedBytesPerSecond / (1024.0 * 1024.0):F2} MB/s" : "0 B/s";
                        item.TimeLeft = info.RemainingSeconds > 0 ? TimeSpan.FromSeconds(Math.Min(info.RemainingSeconds, 86400 * 30)).ToString(@"hh\:mm\:ss") : "Calculating...";
                        if (info.ProgressPercentage >= 100 || info.IsCompleted)
                        {
                            item.Status = "Completed";
                            item.TransferRate = "--";
                            item.TimeLeft = "Completed";
                        }
                        RecalculateMetrics();
                    });
                });

                try
                {
                    // Execute real multi-segment download pipeline
                    await downloadService.StartDownloadAsync(
                        item.Url,
                        item.SavePath,
                        progress,
                        item.PauseSource,
                        () => -1,
                        item.CancellationToken
                    ).ConfigureAwait(false);

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        item.Status = "Completed";
                        item.Progress = 100.0;
                        item.TransferRate = "--";
                        item.TimeLeft = "Completed";
                        RecalculateMetrics();
                    });
                }
                catch (OperationCanceledException)
                {
                    LoggingService.Log($"[DownloadManagerViewModel] Download cancelled: {item.FileName}");
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (item.Status == "Downloading") item.Status = "Stopped";
                        item.TransferRate = "0 B/s";
                        RecalculateMetrics();
                    });
                }
                catch (Exception ex)
                {
                    LoggingService.LogException($"[DownloadManagerViewModel] Download failed for {item.FileName}", ex);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        item.Status = "Error";
                        item.TransferRate = "0 B/s";
                        item.TimeLeft = "Failed";
                        RecalculateMetrics();
                    });
                }
            }
        }

        // ==================== HISTORY LOADING ====================

        /// <summary>
        /// Loads real download history from the SQLite database via HistoryService.
        /// Called once from the constructor (fire-and-forget). Falls back to an empty
        /// list if the DB is missing or empty — never shows fake sample data.
        /// </summary>
        public async Task LoadHistoryFromDatabaseAsync()
        {
            try
            {
                var historyService = App.ServiceProvider?.GetService(typeof(HistoryService)) as HistoryService;
                if (historyService == null)
                {
                    // HistoryService not registered yet; try constructing directly
                    historyService = new HistoryService();
                }

                var items = await historyService.LoadHistoryAsync().ConfigureAwait(false);

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AllDownloads.Clear();
                    foreach (var item in items)
                        AllDownloads.Add(item);
                    ApplyFilter();
                    RecalculateMetrics();
                });

                LoggingService.Log($"[DownloadManagerViewModel] Loaded {items.Count} downloads from history DB.");
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadManagerViewModel] LoadHistoryFromDatabaseAsync failed", ex);
            }
        }



        /// <summary>
        /// Delete all downloads from list and history database
        /// </summary>
        [RelayCommand]
        public void DeleteAll()
        {
            AllDownloads.Clear();
            ApplyFilter();
            RecalculateMetrics();

            Task.Run(async () =>
            {
                try
                {
                    var historyService = App.ServiceProvider?.GetService(typeof(HistoryService)) as HistoryService ?? new HistoryService();
                    await historyService.ClearHistoryAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[DownloadManagerViewModel] Failed to clear DB history", ex);
                }
            });
        }

        // ==================== METRICS UPDATE METHODS ====================

        /// <summary>
        /// Recalculate all KPI metrics from the AllDownloads collection
        /// Updates: TotalDownloadsCount, ActiveDownloadsCount, CompletedDownloadsCount, TotalSizeDownloaded
        /// Uses Dispatcher to ensure UI thread safety for WPF binding updates
        /// </summary>
        public void RecalculateMetrics()
        {
            // Dispatch to UI thread for thread-safe property updates
            Dispatcher.CurrentDispatcher?.BeginInvoke(() =>
            {
                try
                {
                    // Count totals directly from live collection
                    int total = AllDownloads?.Count ?? 0;
                    int active = AllDownloads?.Count(d => d.Status != null && d.Status.Contains("Downloading")) ?? 0;
                    int completed = AllDownloads?.Count(d => d.Status != null && d.Status.Contains("Completed")) ?? 0;

                    // Calculate real total downloaded bytes from live items
                    double totalBytes = 0;
                    if (AllDownloads != null)
                    {
                        foreach (var item in AllDownloads)
                        {
                            if (item == null) continue;
                            double bytes = ParseSizeToBytes(item.Size);
                            if (item.Status != null && item.Status.Contains("Completed"))
                            {
                                totalBytes += bytes;
                            }
                            else if (bytes > 0 && item.Progress > 0)
                            {
                                totalBytes += bytes * (item.Progress / 100.0);
                            }
                        }
                    }

                    string sizeFmt;
                    if (totalBytes >= 1024L * 1024 * 1024)
                    {
                        sizeFmt = $"{totalBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
                    }
                    else if (totalBytes >= 1024 * 1024)
                    {
                        sizeFmt = $"{totalBytes / (1024.0 * 1024.0):F1} MB";
                    }
                    else if (totalBytes >= 1024)
                    {
                        sizeFmt = $"{totalBytes / 1024.0:F0} KB";
                    }
                    else
                    {
                        sizeFmt = $"{totalBytes:F0} B";
                    }

                    // Update properties
                    TotalDownloadsCount = total;
                    ActiveDownloadsCount = active;
                    CompletedDownloadsCount = completed;
                    TotalSizeDownloaded = sizeFmt;
                }
                catch (Exception ex)
                {
                    // Log error, don't crash UI
                    System.Diagnostics.Debug.WriteLine($"Error in RecalculateMetrics: {ex.Message}");
                }
            });
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:F0} KB";
            return $"{bytes} B";
        }

        private static double ParseSizeToBytes(string? sizeStr)
        {
            if (string.IsNullOrWhiteSpace(sizeStr)) return 0;
            string s = sizeStr.Trim();
            if (s.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(s[..^2].Trim(), out double gb)) return gb * 1024.0 * 1024.0 * 1024.0;
            }
            else if (s.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(s[..^2].Trim(), out double mb)) return mb * 1024.0 * 1024.0;
            }
            else if (s.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(s[..^2].Trim(), out double kb)) return kb * 1024.0;
            }
            else if (s.EndsWith("B", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(s[..^1].Trim(), out double b)) return b;
            }
            else if (double.TryParse(s, out double val))
            {
                return val;
            }
            return 0;
        }

        /// <summary>
        /// Calculates and updates current aggregated download speed from live active downloads.
        /// Zero mock/random values — accurately sums active transfer rates.
        /// </summary>
        public void UpdateDownloadSpeed()
        {
            Dispatcher.CurrentDispatcher?.BeginInvoke(() =>
            {
                try
                {
                    if (AllDownloads != null && AllDownloads.Any(d => d.Status != null && d.Status.Contains("Downloading")))
                    {
                        double totalBytesPerSec = 0;
                        foreach (var item in AllDownloads.Where(d => d.Status != null && d.Status.Contains("Downloading")))
                        {
                            if (!string.IsNullOrWhiteSpace(item.TransferRate) && item.TransferRate != "0 B/s")
                            {
                                totalBytesPerSec += ParseSpeedToBytesPerSecond(item.TransferRate);
                            }
                        }

                        if (totalBytesPerSec >= 1024L * 1024 * 1024)
                            CurrentDownloadSpeed = $"{totalBytesPerSec / (1024.0 * 1024.0 * 1024.0):F2} GB/s";
                        else if (totalBytesPerSec >= 1024 * 1024)
                            CurrentDownloadSpeed = $"{totalBytesPerSec / (1024.0 * 1024.0):F2} MB/s";
                        else if (totalBytesPerSec >= 1024)
                            CurrentDownloadSpeed = $"{totalBytesPerSec / 1024.0:F1} KB/s";
                        else if (totalBytesPerSec > 0)
                            CurrentDownloadSpeed = $"{totalBytesPerSec:F0} B/s";
                        else
                            CurrentDownloadSpeed = "0 B/s";
                    }
                    else
                    {
                        CurrentDownloadSpeed = "0 B/s";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in UpdateDownloadSpeed: {ex.Message}");
                }
            });
        }

        private static double ParseSpeedToBytesPerSecond(string? speedStr)
        {
            if (string.IsNullOrWhiteSpace(speedStr)) return 0;
            string s = speedStr.Trim();
            if (s.EndsWith("/s", StringComparison.OrdinalIgnoreCase)) s = s[..^2].Trim();

            if (s.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(s[..^2].Trim(), out double gb)) return gb * 1024.0 * 1024.0 * 1024.0;
            }
            else if (s.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(s[..^2].Trim(), out double mb)) return mb * 1024.0 * 1024.0;
            }
            else if (s.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(s[..^2].Trim(), out double kb)) return kb * 1024.0;
            }
            else if (s.EndsWith("B", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(s[..^1].Trim(), out double b)) return b;
            }
            else if (double.TryParse(s, out double val))
            {
                return val;
            }
            return 0;
        }

        /// <summary>
        /// Update network status and bandwidth information
        /// In production, would check actual network connectivity and available bandwidth
        /// </summary>
        public void UpdateNetworkStatus()
        {
            Dispatcher.CurrentDispatcher?.BeginInvoke(() =>
            {
                try
                {
                    // Check if any downloads are active
                    bool hasActive = ActiveDownloadsCount > 0;

                    // Simulate connection status (in real app, would use actual network APIs)
                    ConnectionStatus = hasActive ? "High Speed Connection" : "Connected";
                    BandwidthStatus = hasActive ? "Unlimited Bandwidth" : "Idle";
                    MaxSpeedLimit = "Unlimited"; // Could be loaded from settings
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in UpdateNetworkStatus: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Start periodic metrics updates (for real-time dashboard)
        /// Call this from Dashboard.xaml.cs when the view loads
        /// </summary>
        public async Task StartMetricsUpdates(int updateIntervalMs = 500, CancellationToken cancellationToken = default)
        {
            try
            {
                // Initial calculation
                RecalculateMetrics();
                UpdateNetworkStatus();

                // Periodic updates
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(updateIntervalMs, cancellationToken);
                    RecalculateMetrics();
                    UpdateNetworkStatus();
                }
            }
            catch (OperationCanceledException)
            {
                // Normal task cancellation
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadManagerViewModel] Error in StartMetricsUpdates", ex);
            }
        }
    }
}
