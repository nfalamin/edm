using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services.History;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    /// <summary>
    /// HistoryServiceFacade - Unified facade for all history operations.
    /// Consolidates access to both HistoryService (SQLite, real-time) and DownloadHistoryService (JSON, batch).
    /// 
    /// This facade simplifies usage by:
    /// 1. Routing batch operations (LoadHistoryAsync, SaveHistoryAsync) to the appropriate backend
    /// 2. Providing convenient methods for real-time recording
    /// 3. Handling fallback if a backend is unavailable
    /// 
    /// Usage:
    /// var facade = new HistoryServiceFacade(historyService, downloadHistoryService, settingsService);
    /// var history = await facade.LoadHistoryAsync();
    /// var entryId = facade.CreateEntry(url, path, totalBytes);
    /// facade.UpdateProgress(entryId, bytesDownloaded, speed, avgSpeed);
    /// facade.MarkCompleted(entryId);
    /// </summary>
    public class HistoryServiceFacade : IHistoryProvider
    {
        private readonly HistoryService _sqliteProvider;
        private readonly DownloadHistoryService? _jsonProvider;
        private readonly ISettingsService? _settingsService;

        public HistoryServiceFacade(
            HistoryService sqliteProvider,
            DownloadHistoryService? jsonProvider = null,
            ISettingsService? settingsService = null)
        {
            _sqliteProvider = sqliteProvider ?? throw new ArgumentNullException(nameof(sqliteProvider));
            _jsonProvider = jsonProvider;
            _settingsService = settingsService;
        }

        /// <summary>
        /// Load history - Attempts SQLite first, falls back to JSON if configured
        /// </summary>
        public async Task<ObservableCollection<DownloadItem>> LoadHistoryAsync()
        {
            try
            {
                // Try SQLite first (primary source)
                var items = await _sqliteProvider.LoadHistoryAsync().ConfigureAwait(false);
                if (items != null && items.Count > 0)
                {
                    LoggingService.Log($"[HistoryServiceFacade] Loaded {items.Count} items from SQLite");
                    return items;
                }

                // Fall back to JSON if configured and SQLite is empty
                if (_jsonProvider != null)
                {
                    LoggingService.Log("[HistoryServiceFacade] Attempting fallback to JSON history");
                    var jsonItems = await _jsonProvider.LoadHistoryAsync().ConfigureAwait(false);
                    if (jsonItems != null && jsonItems.Count > 0)
                    {
                        LoggingService.Log($"[HistoryServiceFacade] Loaded {jsonItems.Count} items from JSON (legacy)");
                        // Optionally: Import JSON items to SQLite for future use
                        return jsonItems;
                    }
                }

                return new ObservableCollection<DownloadItem>();
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[HistoryServiceFacade] LoadHistoryAsync failed", ex);
                return new ObservableCollection<DownloadItem>();
            }
        }

        /// <summary>
        /// Save history - Saves to SQLite (primary) and optionally to JSON for backup
        /// </summary>
        public async Task SaveHistoryAsync(ObservableCollection<DownloadItem> downloads)
        {
            try
            {
                // Save to SQLite
                await _sqliteProvider.SaveHistoryAsync(downloads).ConfigureAwait(false);
                LoggingService.Log($"[HistoryServiceFacade] Saved {downloads.Count} items to SQLite");

                // Optionally save to JSON for backup (controlled by setting)
                if (_jsonProvider != null && _settingsService?.GetSetting("EnableJsonBackup") == "true")
                {
                    await _jsonProvider.SaveHistoryAsync(downloads).ConfigureAwait(false);
                    LoggingService.Log($"[HistoryServiceFacade] Backed up {downloads.Count} items to JSON");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[HistoryServiceFacade] SaveHistoryAsync failed", ex);
            }
        }

        /// <summary>
        /// Create a real-time history entry
        /// </summary>
        public long CreateEntry(string url, string destination, long totalBytes)
        {
            try
            {
                return _sqliteProvider.CreateEntry(url, destination, totalBytes);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[HistoryServiceFacade] CreateEntry failed", ex);
                return -1;
            }
        }

        /// <summary>
        /// Update progress for an active download
        /// </summary>
        public void UpdateProgress(long id, long bytesDownloaded, double lastSpeed, double avgSpeed)
        {
            try
            {
                _sqliteProvider.UpdateProgress(id, bytesDownloaded, lastSpeed, avgSpeed);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[HistoryServiceFacade] UpdateProgress failed", ex);
            }
        }

        /// <summary>
        /// Mark a download as completed
        /// </summary>
        public void MarkCompleted(long id)
        {
            try
            {
                _sqliteProvider.MarkCompleted(id);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[HistoryServiceFacade] MarkCompleted failed", ex);
            }
        }

        /// <summary>
        /// List recent downloads
        /// </summary>
        public IEnumerable<dynamic>? ListRecent(int limit = 100)
        {
            try
            {
                return _sqliteProvider.ListRecent(limit);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[HistoryServiceFacade] ListRecent failed", ex);
                return null;
            }
        }
    }
}
