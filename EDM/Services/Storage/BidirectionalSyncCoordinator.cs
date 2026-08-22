using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services.Interfaces;

namespace EDM.Services.Storage
{
    public enum SyncCoordinatorStatus
    {
        Idle,
        Syncing,
        Paused,
        Offline,
        Error
    }

    public record SyncProgressEvent(
        string Operation,
        string RelativePath,
        double ProgressPercentage,
        SyncCoordinatorStatus Status,
        string? Message = null);

    /// <summary>
    /// Master Bidirectional Synchronization Coordinator for EDM.
    /// Orchestrates two-way synchronization between:
    /// Windows File Explorer <-> Local File Watcher <-> EDM Desktop Agent <-> Control Plane API / Cloud <-> Dashboard.
    /// 
    /// Features:
    /// - Continuous real-time local file watching with debouncing.
    /// - Periodic and on-demand cloud delta polling.
    /// - Conflict detection and automatic non-destructive conflict forking.
    /// - Sync loop prevention and event suppression.
    /// - Offline queueing and auto-replay upon reconnection.
    /// - Disk space validation before cloud downloads.
    /// - File-locking safety and retry mechanism.
    /// </summary>
    public class BidirectionalSyncCoordinator : IDisposable
    {
        private static readonly Lazy<BidirectionalSyncCoordinator> _instance = new(() => new BidirectionalSyncCoordinator());
        public static BidirectionalSyncCoordinator Instance => _instance.Value;

        private readonly LocalHddStorageEngine _storageEngine;
        private readonly CloudFileSyncAgent _cloudAgent;
        private readonly LocalHddFileSystemWatcher _fileWatcher;
        private readonly ISettingsService _settingsService;

        private readonly object _lock = new();
        private CancellationTokenSource? _coordinatorCts;
        private Task? _backgroundPollingTask;
        private bool _isDisposed;

        private DateTime? _lastSyncUtc;
        private SyncCoordinatorStatus _currentStatus = SyncCoordinatorStatus.Idle;

        public event Action<SyncProgressEvent>? ProgressReported;
        public event Action<SyncCoordinatorStatus>? StatusChanged;

        public bool IsRunning => _coordinatorCts != null && !_coordinatorCts.IsCancellationRequested;
        public SyncCoordinatorStatus Status => _currentStatus;
        public DateTime? LastSyncUtc => _lastSyncUtc;

        public BidirectionalSyncCoordinator(
            LocalHddStorageEngine? storageEngine = null,
            CloudFileSyncAgent? cloudAgent = null,
            LocalHddFileSystemWatcher? fileWatcher = null,
            ISettingsService? settingsService = null)
        {
            _settingsService = settingsService ?? new SettingsService();
            _storageEngine = storageEngine ?? LocalHddStorageEngine.Instance;
            _cloudAgent = cloudAgent ?? CloudFileSyncAgent.Instance;
            _fileWatcher = fileWatcher ?? LocalHddFileSystemWatcher.Instance;

            string? lastSync = _settingsService.GetSetting("LastCloudSyncTimestampUtc");
            if (!string.IsNullOrEmpty(lastSync) && DateTime.TryParse(lastSync, out var parsed))
            {
                _lastSyncUtc = parsed;
            }
        }

        public void Start(int pollingIntervalSeconds = 10)
        {
            lock (_lock)
            {
                if (IsRunning) return;

                _coordinatorCts = new CancellationTokenSource();
                var token = _coordinatorCts.Token;

                // 1. Hook and start local filesystem watcher
                _fileWatcher.FileChanged += OnLocalFileChanged;
                _fileWatcher.Start();

                // 2. Start background cloud polling loop
                _backgroundPollingTask = Task.Run(() => RunBackgroundPollingLoopAsync(pollingIntervalSeconds, token), token);

                SetStatus(SyncCoordinatorStatus.Idle);
                LoggingService.Log("[BidirectionalSyncCoordinator] Sync engine started successfully.");
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!IsRunning) return;

                _fileWatcher.FileChanged -= OnLocalFileChanged;
                _fileWatcher.Stop();

                _coordinatorCts?.Cancel();
                _coordinatorCts?.Dispose();
                _coordinatorCts = null;

                SetStatus(SyncCoordinatorStatus.Paused);
                LoggingService.Log("[BidirectionalSyncCoordinator] Sync engine stopped.");
            }
        }

        public async Task<bool> SyncNowAsync(CancellationToken ct = default)
        {
            if (_currentStatus == SyncCoordinatorStatus.Syncing) return false;

            SetStatus(SyncCoordinatorStatus.Syncing);
            try
            {
                // 1. Process pending offline queue
                await _cloudAgent.ProcessOfflineQueueAsync(ct).ConfigureAwait(false);

                // 2. Pull and apply cloud deltas
                await PollAndApplyCloudDeltasAsync(ct).ConfigureAwait(false);

                // 3. Scan local files and push any unindexed/modified local files
                await ScanAndPushLocalChangesAsync(ct).ConfigureAwait(false);

                SetStatus(SyncCoordinatorStatus.Idle);
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[BidirectionalSyncCoordinator] SyncNow failed", ex);
                SetStatus(SyncCoordinatorStatus.Error);
                return false;
            }
        }

        private async Task RunBackgroundPollingLoopAsync(int intervalSeconds, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(3, intervalSeconds)), ct).ConfigureAwait(false);

                    // Replay offline queue
                    await _cloudAgent.ProcessOfflineQueueAsync(ct).ConfigureAwait(false);

                    // Poll cloud changes
                    await PollAndApplyCloudDeltasAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LoggingService.Log($"[BidirectionalSyncCoordinator] Background polling iteration error: {ex.Message}");
                }
            }
        }

        #region Local -> Cloud Handling

        private void OnLocalFileChanged(LocalFileChangeEvent e)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleLocalChangeEventAsync(e).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LoggingService.LogException($"[BidirectionalSyncCoordinator] Failed to process local event for {e.RelativePath}", ex);
                }
            });
        }

        public async Task HandleLocalChangeEventAsync(LocalFileChangeEvent e, CancellationToken ct = default)
        {
            string relPath = e.RelativePath.Replace('\\', '/').Trim('/');

            // Check if suppressed
            if (_fileWatcher.IsPathSuppressed(relPath)) return;

            LoggingService.Log($"[BidirectionalSyncCoordinator] Processing local {e.ChangeType} event for '{relPath}'");

            switch (e.ChangeType)
            {
                case LocalFileChangeType.Deleted:
                    ReportProgress("DELETE", relPath, 50, SyncCoordinatorStatus.Syncing);
                    await _cloudAgent.DeleteRemoteFileByPathAsync(relPath, ct).ConfigureAwait(false);
                    _fileWatcher.ClearAppliedCloudHash(relPath);
                    ReportProgress("DELETE", relPath, 100, SyncCoordinatorStatus.Idle, "File deleted remotely.");
                    break;

                case LocalFileChangeType.Renamed:
                    if (!string.IsNullOrEmpty(e.OldRelativePath))
                    {
                        string oldRel = e.OldRelativePath.Replace('\\', '/').Trim('/');
                        await _cloudAgent.DeleteRemoteFileByPathAsync(oldRel, ct).ConfigureAwait(false);
                        _fileWatcher.ClearAppliedCloudHash(oldRel);
                    }
                    await PushLocalFileToCloudAsync(relPath, ct).ConfigureAwait(false);
                    break;

                case LocalFileChangeType.Created:
                case LocalFileChangeType.Modified:
                    await PushLocalFileToCloudAsync(relPath, ct).ConfigureAwait(false);
                    break;
            }
        }

        private async Task PushLocalFileToCloudAsync(string relativePath, CancellationToken ct = default)
        {
            string fullPath = Path.Combine(_storageEngine.StorageRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

            // 1. Wait for file locking release
            bool isReady = await LocalHddFileSystemWatcher.WaitForFileReadyAsync(fullPath, maxAttempts: 8, initialDelayMs: 200, ct: ct).ConfigureAwait(false);
            if (!isReady)
            {
                LoggingService.Log($"[BidirectionalSyncCoordinator] File '{relativePath}' is locked or inaccessible; skipping sync.");
                return;
            }

            // 2. Calculate local hash
            string currentHash = await _storageEngine.CalculateFileSha256Async(fullPath, ct).ConfigureAwait(false);

            // 3. Check if hash matches the last applied cloud hash to prevent loop
            string? appliedHash = _fileWatcher.GetAppliedCloudHash(relativePath);
            if (!string.IsNullOrEmpty(appliedHash) && string.Equals(appliedHash, currentHash, StringComparison.OrdinalIgnoreCase))
            {
                // Echo event from cloud download; ignore
                return;
            }

            ReportProgress("UPLOAD", relativePath, 10, SyncCoordinatorStatus.Syncing, "Uploading local changes...");

            // 4. Sync metadata / file with cloud
            var syncResult = await _cloudAgent.SyncLocalFileAsync(relativePath, ct: ct).ConfigureAwait(false);

            if (syncResult.Success)
            {
                _fileWatcher.RecordAppliedCloudHash(relativePath, currentHash);
                ReportProgress("UPLOAD", relativePath, 100, SyncCoordinatorStatus.Idle, "File synced with cloud.");
            }
            else if (syncResult.IsConflict)
            {
                LoggingService.Log($"[BidirectionalSyncCoordinator] Conflict detected for '{relativePath}'. Initiating safe conflict fork.");
                await HandleConflictForkAsync(relativePath, currentHash, ct).ConfigureAwait(false);
            }
            else
            {
                ReportProgress("UPLOAD", relativePath, 0, SyncCoordinatorStatus.Error, syncResult.Message);
            }
        }

        private async Task HandleConflictForkAsync(string relativePath, string localHash, CancellationToken ct)
        {
            string fullPath = Path.Combine(_storageEngine.StorageRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath)) return;

            string dir = Path.GetDirectoryName(fullPath) ?? _storageEngine.StorageRootPath;
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fullPath);
            string ext = Path.GetExtension(fullPath);

            string machineName = Environment.MachineName.Replace(" ", "_");
            string conflictFileName = $"{fileNameWithoutExt} (Conflict - {machineName} - {DateTime.UtcNow:yyyyMMdd_HHmmss}){ext}";
            string conflictFullPath = Path.Combine(dir, conflictFileName);

            try
            {
                // Copy current local modified file to conflict copy
                File.Copy(fullPath, conflictFullPath, overwrite: true);
                LoggingService.Log($"[BidirectionalSyncCoordinator] Created local conflict fork at: {conflictFullPath}");

                // Sync the conflict file as a new independent file
                string conflictRelPath = Path.GetRelativePath(_storageEngine.StorageRootPath, conflictFullPath).Replace('\\', '/');
                await _cloudAgent.SyncLocalFileAsync(conflictRelPath, ct: ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[BidirectionalSyncCoordinator] Conflict fork failed", ex);
            }
        }

        #endregion

        #region Cloud -> Local Handling

        public async Task PollAndApplyCloudDeltasAsync(CancellationToken ct = default)
        {
            var deltas = await _cloudAgent.FetchDeltasAsync(_lastSyncUtc, ct: ct).ConfigureAwait(false);
            if (deltas == null || deltas.Changes.Count == 0) return;

            LoggingService.Log($"[BidirectionalSyncCoordinator] Received {deltas.Changes.Count} cloud delta(s).");

            foreach (var remote in deltas.Changes)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    await ApplyRemoteChangeAsync(remote, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LoggingService.LogException($"[BidirectionalSyncCoordinator] Failed to apply remote change for '{remote.RelativePath}'", ex);
                }
            }

            _lastSyncUtc = deltas.ServerTimeUtc;
            _settingsService.SetSetting("LastCloudSyncTimestampUtc", _lastSyncUtc.Value.ToString("O"));
        }

        private async Task ApplyRemoteChangeAsync(CloudFileRecordDto remote, CancellationToken ct)
        {
            string relPath = remote.RelativePath.Replace('\\', '/').Trim('/');
            string fullPath = Path.Combine(_storageEngine.StorageRootPath, relPath.Replace('/', Path.DirectorySeparatorChar));

            // Case A: Remote file is deleted
            if (remote.IsDeleted)
            {
                if (File.Exists(fullPath))
                {
                    _fileWatcher.SuppressPath(relPath, TimeSpan.FromSeconds(5));
                    _storageEngine.CreateVersionedBackup(fullPath);
                    File.Delete(fullPath);
                    _fileWatcher.ClearAppliedCloudHash(relPath);
                    LoggingService.Log($"[BidirectionalSyncCoordinator] Applied remote delete for: '{relPath}'");
                }
                return;
            }

            // Case B: Remote file exists or is updated
            if (File.Exists(fullPath))
            {
                string localHash = await _storageEngine.CalculateFileSha256Async(fullPath, ct).ConfigureAwait(false);

                // If identical, already in sync
                if (string.Equals(localHash, remote.Sha256Hash, StringComparison.OrdinalIgnoreCase))
                {
                    _fileWatcher.RecordAppliedCloudHash(relPath, remote.Sha256Hash);
                    return;
                }

                // If local file was modified independently with a different hash -> Conflict!
                string? appliedHash = _fileWatcher.GetAppliedCloudHash(relPath);
                if (!string.IsNullOrEmpty(appliedHash) && !string.Equals(localHash, appliedHash, StringComparison.OrdinalIgnoreCase))
                {
                    // Local file has unsynced local edits -> Fork local copy first!
                    LoggingService.Log($"[BidirectionalSyncCoordinator] Remote update conflicts with local edits for '{relPath}'. Forking local copy.");
                    await HandleConflictForkAsync(relPath, localHash, ct).ConfigureAwait(false);
                }
            }

            // Pre-flight disk space check
            _storageEngine.ValidateAvailableDiskSpace(remote.FileSizeBytes, Path.GetDirectoryName(fullPath));

            // Suppress watcher before writing to prevent echo loop
            _fileWatcher.SuppressPath(relPath, TimeSpan.FromSeconds(10));
            _fileWatcher.RecordAppliedCloudHash(relPath, remote.Sha256Hash);

            ReportProgress("DOWNLOAD", relPath, 20, SyncCoordinatorStatus.Syncing, "Downloading remote update...");

            bool downloaded = await _cloudAgent.DownloadFileStreamAsync(remote.Id, relPath, null, ct).ConfigureAwait(false);
            if (downloaded)
            {
                ReportProgress("DOWNLOAD", relPath, 100, SyncCoordinatorStatus.Idle, "File updated from cloud.");
                LoggingService.Log($"[BidirectionalSyncCoordinator] Successfully synced remote file to local HDD: '{relPath}'");
            }
            else
            {
                _fileWatcher.ClearAppliedCloudHash(relPath);
                ReportProgress("DOWNLOAD", relPath, 0, SyncCoordinatorStatus.Error, "Download failed.");
            }
        }

        private async Task ScanAndPushLocalChangesAsync(CancellationToken ct)
        {
            var localFiles = _storageEngine.GetIndexedFiles();
            foreach (var local in localFiles)
            {
                if (ct.IsCancellationRequested) break;

                string fullPath = local.FullPath;
                if (!File.Exists(fullPath)) continue;

                string currentHash = await _storageEngine.CalculateFileSha256Async(fullPath, ct).ConfigureAwait(false);
                string? applied = _fileWatcher.GetAppliedCloudHash(local.RelativePath);

                if (string.IsNullOrEmpty(applied) || !string.Equals(applied, currentHash, StringComparison.OrdinalIgnoreCase))
                {
                    await PushLocalFileToCloudAsync(local.RelativePath, ct).ConfigureAwait(false);
                }
            }
        }

        #endregion

        private void SetStatus(SyncCoordinatorStatus status)
        {
            _currentStatus = status;
            StatusChanged?.Invoke(status);
        }

        private void ReportProgress(string op, string path, double pct, SyncCoordinatorStatus status, string? msg = null)
        {
            ProgressReported?.Invoke(new SyncProgressEvent(op, path, pct, status, msg));
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Stop();
        }
    }
}
