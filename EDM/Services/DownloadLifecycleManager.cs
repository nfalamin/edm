using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services.Helpers;
using EDM.Services.History;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    /// <summary>
    /// DownloadLifecycleManager — Authoritative manager for download state transitions,
    /// safe cancellation, graceful shutdown waiting, temporary file purging, SQLite history cleanup,
    /// and final file deletion semantics.
    /// </summary>
    public sealed class DownloadLifecycleManager
    {
        private static readonly Lazy<DownloadLifecycleManager> _lazyInstance = new(() => new DownloadLifecycleManager());
        public static DownloadLifecycleManager Instance => _lazyInstance.Value;

        private readonly HistoryService? _historyService;

        public DownloadLifecycleManager(HistoryService? historyService = null)
        {
            _historyService = historyService ?? App.ServiceProvider?.GetService(typeof(HistoryService)) as HistoryService;
        }

        /// <summary>
        /// Gracefully stops/cancels an active download.
        /// </summary>
        public async Task CancelDownloadAsync(DownloadItem item, TimeSpan? timeout = null)
        {
            if (item == null) return;

            LoggingService.Log($"[DownloadLifecycleManager] Cancelling download: '{item.FileName}' ({item.Id})");
            try
            {
                // Signal pause release and cancellation
                item.PauseSource.Resume();
                item.CancelAndReset();

                // Await engine worker shutdown
                await item.WaitForCompletionAsync(timeout ?? TimeSpan.FromSeconds(3)).ConfigureAwait(false);

                item.Status = "Stopped";
                item.TransferRate = "0 B/s";
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"[DownloadLifecycleManager] Error during cancel: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes the complete authoritative delete lifecycle for a single download item:
        /// 1. Detect Active state
        /// 2. Cancel Download & Signal tokens
        /// 3. Wait for Workers & Process Shutdown
        /// 4. Dispose streams & handles
        /// 5. Purge all .part, .tmpdl, .tmp_* folders, and .edm.json files
        /// 6. Remove persisted SQLite history
        /// 7. Delete final output file IF AND ONLY IF deleteFileFromDisk is true
        /// </summary>
        public async Task DeleteDownloadAsync(DownloadItem item, bool deleteFileFromDisk, CancellationToken cancellationToken = default)
        {
            if (item == null) return;

            LoggingService.Log($"[DownloadLifecycleManager] Initiating full delete lifecycle for '{item.FileName}' (DeleteFileFromDisk={deleteFileFromDisk})");

            // 1 & 2. Signal cancellation
            try
            {
                item.PauseSource.Resume();
                item.CancelAndReset();
            }
            catch { }

            // 3. Await worker shutdown with bounded timeout
            try
            {
                await item.WaitForCompletionAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
            catch { }

            // 4 & 5. Purge temporary and segment files
            await CleanTemporaryFilesAsync(item.SavePath, cancellationToken).ConfigureAwait(false);

            // 6. Delete persisted history from SQLite database
            if (_historyService != null)
            {
                try
                {
                    await _historyService.DeleteHistoryItemAsync(item.Url, item.SavePath).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"[DownloadLifecycleManager] SQLite history removal failed for {item.Id}: {ex.Message}");
                }
            }

            // 7. Delete final destination file if user requested
            if (deleteFileFromDisk && !string.IsNullOrWhiteSpace(item.SavePath))
            {
                try
                {
                    await FileDeleteHelper.DeleteFileSafeAsync(item.SavePath, cancellationToken: cancellationToken).ConfigureAwait(false);
                    LoggingService.Log($"[DownloadLifecycleManager] Deleted final output file: '{item.SavePath}'");
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"[DownloadLifecycleManager] Could not delete final file '{item.SavePath}': {ex.Message}");
                }
            }

            LoggingService.Log($"[DownloadLifecycleManager] Completed delete lifecycle for '{item.FileName}'");
        }

        /// <summary>
        /// Executes the delete lifecycle for multiple items.
        /// </summary>
        public async Task DeleteDownloadsAsync(IEnumerable<DownloadItem> items, bool deleteFilesFromDisk, CancellationToken cancellationToken = default)
        {
            if (items == null) return;
            var list = items.ToList();
            foreach (var item in list)
            {
                await DeleteDownloadAsync(item, deleteFilesFromDisk, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Removes all temporary segment files, .tmpdl files, and .tmp_* metadata directories for a given target path.
        /// </summary>
        public static async Task CleanTemporaryFilesAsync(string? savePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(savePath)) return;

            string? dir = Path.GetDirectoryName(savePath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            string fileName = Path.GetFileName(savePath);

            // 1. Direct temporary file: savePath.tmpdl
            string singleTemp = savePath + ".tmpdl";
            await FileDeleteHelper.DeleteFileSafeAsync(singleTemp, cancellationToken: cancellationToken).ConfigureAwait(false);

            // 2. Metadata file: savePath.edm.json
            string metaFile = savePath + ".edm.json";
            await FileDeleteHelper.DeleteFileSafeAsync(metaFile, cancellationToken: cancellationToken).ConfigureAwait(false);

            // 3. Segment metadata folders matching .tmp_{fileName}_*
            try
            {
                var matchingDirs = Directory.GetDirectories(dir, $".tmp_{fileName}_*");
                foreach (var d in matchingDirs)
                {
                    try
                    {
                        Directory.Delete(d, true);
                    }
                    catch
                    {
                        // Retry file-by-file if directory delete fails
                        try
                        {
                            foreach (var f in Directory.GetFiles(d))
                            {
                                await FileDeleteHelper.DeleteFileSafeAsync(f, cancellationToken: cancellationToken).ConfigureAwait(false);
                            }
                            Directory.Delete(d, true);
                        }
                        catch { }
                    }
                }
            }
            catch { }

            // 4. Temporary audio/video split merge files
            string tempVideo = savePath + ".temp_video";
            string tempAudio = savePath + ".temp_audio";
            await FileDeleteHelper.DeleteFileSafeAsync(tempVideo, cancellationToken: cancellationToken).ConfigureAwait(false);
            await FileDeleteHelper.DeleteFileSafeAsync(tempAudio, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
