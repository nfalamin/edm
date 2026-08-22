using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using EDM.Models;

namespace EDM.Services.Interfaces
{
    /// <summary>
    /// IHistoryProvider - Unified interface for download history management.
    /// Supports both batch loading/saving (for app startup) and real-time recording operations.
    /// 
    /// Implementations:
    /// - HistoryService (SQLite) - Primary, comprehensive
    /// - DownloadHistoryService (JSON) - Legacy, for data migration
    /// </summary>
    public interface IHistoryProvider
    {
        /// <summary>
        /// Load all download history (used at app startup).
        /// Implementations should return a collection of DownloadItem from persistent storage.
        /// </summary>
        Task<ObservableCollection<DownloadItem>> LoadHistoryAsync();

        /// <summary>
        /// Save download history (batch operation).
        /// Implementations should persist the entire collection to storage.
        /// </summary>
        Task SaveHistoryAsync(ObservableCollection<DownloadItem> downloads);

        /// <summary>
        /// Create a new download entry in history (optional, for real-time recording).
        /// Default implementation throws NotSupportedException for backward compatibility.
        /// </summary>
        /// <param name="url">Download URL</param>
        /// <param name="destination">Destination file path</param>
        /// <param name="totalBytes">Total download size in bytes</param>
        /// <returns>The ID of the created entry, or -1 on failure</returns>
        long CreateEntry(string url, string destination, long totalBytes)
        {
            throw new NotSupportedException("This implementation does not support real-time recording");
        }

        /// <summary>
        /// Update download progress (optional, for real-time recording).
        /// Default implementation throws NotSupportedException for backward compatibility.
        /// </summary>
        /// <param name="id">Entry ID returned from CreateEntry</param>
        /// <param name="bytesDownloaded">Current bytes downloaded</param>
        /// <param name="lastSpeed">Current download speed (bytes/sec)</param>
        /// <param name="avgSpeed">Average download speed (bytes/sec)</param>
        void UpdateProgress(long id, long bytesDownloaded, double lastSpeed, double avgSpeed)
        {
            throw new NotSupportedException("This implementation does not support real-time recording");
        }

        /// <summary>
        /// Mark a download as completed (optional, for real-time recording).
        /// Default implementation throws NotSupportedException for backward compatibility.
        /// </summary>
        /// <param name="id">Entry ID returned from CreateEntry</param>
        void MarkCompleted(long id)
        {
            throw new NotSupportedException("This implementation does not support real-time recording");
        }

        /// <summary>
        /// List recent download entries (optional).
        /// Default implementation throws NotSupportedException for backward compatibility.
        /// </summary>
        /// <param name="limit">Maximum number of entries to return (default: 100)</param>
        /// <returns>Enumerable of recent download entries</returns>
        IEnumerable<dynamic>? ListRecent(int limit = 100)
        {
            throw new NotSupportedException("This implementation does not support listing entries");
        }
    }
}
