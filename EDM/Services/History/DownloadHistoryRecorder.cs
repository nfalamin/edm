using System;
using System.Threading.Tasks;
using EDM.Services;
using EDM.Services.Data;

namespace EDM.Services.History
{
    internal static class DownloadHistoryRecorder
    {
        // Static instance for efficient resource reuse across calls
        private static HistoryService? _historyService;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets or initializes the shared HistoryService instance.
        /// </summary>
        private static HistoryService GetHistoryService()
        {
            if (_historyService == null)
            {
                lock (_lock)
                {
                    if (_historyService == null)
                    {
                        _historyService = new HistoryService();
                    }
                }
            }
            return _historyService;
        }

        /// <summary>
        /// Creates a new download entry in the history database.
        /// </summary>
        public static long CreateEntry(string url, string destination, long totalBytes)
        {
            try
            {
                var service = GetHistoryService();
                var id = service.CreateEntry(url ?? string.Empty, destination ?? string.Empty, totalBytes);
                LoggingService.Log($"[DownloadHistoryRecorder.CreateEntry] Created entry {id} for {url}");
                return id;
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DownloadHistoryRecorder.CreateEntry] Failed: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Updates progress for a download entry. Thread-safe for concurrent calls.
        /// </summary>
        public static void UpdateProgress(long id, long bytesDownloaded, double lastSpeed, double avgSpeed)
        {
            try
            {
                var service = GetHistoryService();
                service.UpdateProgress(id, bytesDownloaded, lastSpeed, avgSpeed);
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DownloadHistoryRecorder.UpdateProgress] Failed for id {id}: {ex.Message}");
            }
        }

        /// <summary>
        /// Marks a download as completed in the history database.
        /// </summary>
        public static void MarkCompleted(long id)
        {
            try
            {
                var service = GetHistoryService();
                service.MarkCompleted(id);
                LoggingService.Log($"[DownloadHistoryRecorder.MarkCompleted] Entry {id} marked as completed");
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DownloadHistoryRecorder.MarkCompleted] Failed for id {id}: {ex.Message}");
            }
        }

        /// <summary>
        /// Records verification metadata for a history entry.
        /// </summary>
        public static void RecordVerification(long id, Models.VerificationState state, string? algorithm, string? trustedHash, string? computedHash, string? message)
        {
            try
            {
                var service = GetHistoryService();
                service.UpdateVerification(id, state, algorithm, trustedHash, computedHash, message, DateTime.UtcNow);
                LoggingService.Log($"[DownloadHistoryRecorder.RecordVerification] Recorded verification for entry {id}: state={state}");
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DownloadHistoryRecorder.RecordVerification] Failed for id {id}: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a point-in-time backup of the database asynchronously.
        /// </summary>
        public static async Task<string?> CreateBackupAsync()
        {
            try
            {
                var service = GetHistoryService();
                var backupPath = await service.CreateBackupAsync();
                LoggingService.Log($"[DownloadHistoryRecorder.CreateBackupAsync] Backup created: {backupPath}");
                return backupPath;
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DownloadHistoryRecorder.CreateBackupAsync] Failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets diagnostic statistics about database operations.
        /// </summary>
        public static string GetDiagnostics()
        {
            try
            {
                var service = GetHistoryService();
                var stats = service.GetAuditStatistics();
                var recentEntries = service.GetRecentAuditEntries(10);

                var diagnostics = new System.Text.StringBuilder();
                diagnostics.AppendLine("=== Database Audit Statistics ===");
                diagnostics.AppendLine(stats.ToString());
                diagnostics.AppendLine("\n=== Recent Operations (last 10) ===");
                foreach (var entry in recentEntries)
                {
                    diagnostics.AppendLine(entry.ToString());
                }
                return diagnostics.ToString();
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DownloadHistoryRecorder.GetDiagnostics] Failed: {ex.Message}");
                return $"Failed to get diagnostics: {ex.Message}";
            }
        }

        /// <summary>
        /// Gracefully shuts down the history service.
        /// </summary>
        public static void Shutdown()
        {
            try
            {
                if (_historyService != null)
                {
                    lock (_lock)
                    {
                        _historyService?.Dispose();
                        _historyService = null;
                    }
                    LoggingService.Log("[DownloadHistoryRecorder.Shutdown] History service shut down successfully");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DownloadHistoryRecorder.Shutdown] Error: {ex.Message}");
            }
        }
    }
}
