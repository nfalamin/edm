using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services.Interfaces;

namespace EDM.Services.Storage
{
    public enum LocalSyncState
    {
        Synced,
        Uploading,
        Downloading,
        Syncing,
        ModifiedLocally,
        ModifiedRemotely,
        Conflict,
        Offline,
        Error
    }

    public record LocalFileMetadata(
        Guid FileId,
        string FileName,
        string RelativePath,
        string FullPath,
        long FileSizeBytes,
        string Sha256Hash,
        int Version,
        DateTime LastModifiedUtc,
        LocalSyncState SyncState,
        string? ErrorMessage = null);

    public record OfflineSyncOperation(
        Guid OperationId,
        string OperationType, // "REGISTER", "UPDATE", "DELETE", "RESOLVE_CONFLICT"
        Guid FileId,
        string RelativePath,
        string Sha256Hash,
        long FileSizeBytes,
        int Version,
        DateTime QueuedAtUtc,
        string? PayloadJson = null);

    public class InsufficientDiskSpaceException : Exception
    {
        public long RequiredBytes { get; }
        public long AvailableBytes { get; }

        public InsufficientDiskSpaceException(long requiredBytes, long availableBytes)
            : base($"Insufficient disk space. Required: {requiredBytes / (1024 * 1024)} MB, Available: {availableBytes / (1024 * 1024)} MB.")
        {
            RequiredBytes = requiredBytes;
            AvailableBytes = availableBytes;
        }
    }

    /// <summary>
    /// Master Local HDD Storage & Streaming Sync Engine for EDM.
    /// Manages configurable storage roots, disk space validation, chunked stream I/O,
    /// atomic file writes, SHA-256 hashing, versioning, and offline queueing.
    /// </summary>
    public class LocalHddStorageEngine
    {
        private static readonly Lazy<LocalHddStorageEngine> _instance = new(() => new LocalHddStorageEngine());
        public static LocalHddStorageEngine Instance => _instance.Value;

        private readonly ISettingsService _settingsService;
        private readonly object _lock = new();
        private const long SafetyMarginBytes = 100L * 1024 * 1024; // 100 MB safety buffer
        private const int StreamBufferSize = 4 * 1024 * 1024; // 4 MB chunk buffer for large files

        private string _storageRootPath;
        private readonly string _offlineQueuePath;
        private readonly ConcurrentDictionary<string, LocalFileMetadata> _localIndex = new(StringComparer.OrdinalIgnoreCase);

        public string StorageRootPath => _storageRootPath;

        public LocalHddStorageEngine(ISettingsService? settingsService = null)
        {
            _settingsService = settingsService ?? new SettingsService();
            
            // 1. Resolve configurable storage root (Default: %UserProfile%\EDM)
            string? configuredRoot = _settingsService.GetSetting("CustomStorageRootPath");
            if (!string.IsNullOrWhiteSpace(configuredRoot) && Directory.Exists(configuredRoot))
            {
                _storageRootPath = configuredRoot;
            }
            else
            {
                _storageRootPath = DownloadPathCategoryService.GetWorkspaceRootPath();
            }

            // 2. Ensure folder layout
            DownloadPathCategoryService.EnsureWorkspaceStructure(_storageRootPath);

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string edmAppData = Path.Combine(appData, "EDM");
            Directory.CreateDirectory(edmAppData);
            _offlineQueuePath = Path.Combine(edmAppData, "offline_storage_sync_queue.json");

            ScanAndIndexLocalFiles();
        }

        public void SetStorageRootPath(string newPath)
        {
            if (string.IsNullOrWhiteSpace(newPath)) throw new ArgumentNullException(nameof(newPath));

            string normalized = Path.GetFullPath(newPath);
            Directory.CreateDirectory(normalized);
            DownloadPathCategoryService.EnsureWorkspaceStructure(normalized);

            lock (_lock)
            {
                _storageRootPath = normalized;
                _settingsService.SetSetting("CustomStorageRootPath", normalized);
                _localIndex.Clear();
            }

            ScanAndIndexLocalFiles();
            LoggingService.Log($"[LocalHddStorageEngine] Storage root updated to: {normalized}");
        }

        public void ValidateAvailableDiskSpace(long requiredBytes, string? targetDirectory = null)
        {
            if (requiredBytes <= 0) return;

            string path = targetDirectory ?? _storageRootPath;
            string root = Path.GetPathRoot(path) ?? "C:\\";

            try
            {
                var driveInfo = new DriveInfo(root);
                if (driveInfo.AvailableFreeSpace < (requiredBytes + SafetyMarginBytes))
                {
                    throw new InsufficientDiskSpaceException(requiredBytes, driveInfo.AvailableFreeSpace);
                }
            }
            catch (Exception ex) when (ex is not InsufficientDiskSpaceException)
            {
                LoggingService.Log($"[LocalHddStorageEngine] Could not check drive info for '{root}': {ex.Message}");
            }
        }

        public async Task<string> CalculateFileSha256Async(string filePath, CancellationToken ct = default)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("File not found for hash computation.", filePath);

            using var sha256 = SHA256.Create();
            using var fileStream = new FileStream(
                filePath, 
                FileMode.Open, 
                FileAccess.Read, 
                FileShare.Read, 
                StreamBufferSize, 
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            byte[] buffer = new byte[StreamBufferSize];
            int bytesRead;

            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
            {
                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            }

            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
        }

        public async Task<LocalFileMetadata> StreamWriteAtomicAsync(
            Stream sourceStream, 
            string relativePath, 
            long expectedBytes, 
            IProgress<double>? progress = null, 
            CancellationToken ct = default)
        {
            if (sourceStream == null) throw new ArgumentNullException(nameof(sourceStream));
            if (string.IsNullOrWhiteSpace(relativePath)) throw new ArgumentNullException(nameof(relativePath));

            string fullDestinationPath = Path.Combine(_storageRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string? destinationDir = Path.GetDirectoryName(fullDestinationPath);
            if (!string.IsNullOrEmpty(destinationDir)) Directory.CreateDirectory(destinationDir);

            // 1. Validate free disk space
            ValidateAvailableDiskSpace(expectedBytes, destinationDir);

            // 2. Stream to temporary file
            string tempPath = $"{fullDestinationPath}.{Guid.NewGuid():N}.part";
            string calculatedHash;
            long totalWritten = 0;

            try
            {
                using (var sha256 = SHA256.Create())
                using (var targetStream = new FileStream(
                    tempPath, 
                    FileMode.Create, 
                    FileAccess.Write, 
                    FileShare.None, 
                    StreamBufferSize, 
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    byte[] buffer = new byte[StreamBufferSize];
                    int bytesRead;

                    while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                    {
                        await targetStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
                        sha256.TransformBlock(buffer, 0, bytesRead, null, 0);

                        totalWritten += bytesRead;
                        if (expectedBytes > 0 && progress != null)
                        {
                            progress.Report(Math.Min(100.0, (double)totalWritten / expectedBytes * 100.0));
                        }
                    }

                    sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    calculatedHash = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
                }

                // 3. Handle versioning / backup if overwriting existing file
                if (File.Exists(fullDestinationPath))
                {
                    CreateVersionedBackup(fullDestinationPath);
                    File.Delete(fullDestinationPath);
                }

                // 4. Atomic rename
                File.Move(tempPath, fullDestinationPath);
            }
            catch
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
                throw;
            }

            // 5. Update index
            var fileInfo = new FileInfo(fullDestinationPath);
            var metadata = new LocalFileMetadata(
                FileId: Guid.NewGuid(),
                FileName: fileInfo.Name,
                RelativePath: relativePath.Replace('\\', '/'),
                FullPath: fullDestinationPath,
                FileSizeBytes: fileInfo.Length,
                Sha256Hash: calculatedHash,
                Version: 1,
                LastModifiedUtc: fileInfo.LastWriteTimeUtc,
                SyncState: LocalSyncState.Synced);

            _localIndex[metadata.RelativePath] = metadata;
            return metadata;
        }

        public string? CreateVersionedBackup(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            try
            {
                string dir = Path.GetDirectoryName(filePath) ?? _storageRootPath;
                string versionsDir = Path.Combine(dir, ".edm_versions");
                Directory.CreateDirectory(versionsDir);

                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string ext = Path.GetExtension(filePath);
                string backupPath = Path.Combine(versionsDir, $"{fileName}_v{DateTime.UtcNow:yyyyMMdd_HHmmss}{ext}");

                File.Copy(filePath, backupPath, overwrite: true);
                LoggingService.Log($"[LocalHddStorageEngine] Created versioned backup at: {backupPath}");
                return backupPath;
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[LocalHddStorageEngine] Versioned backup failed", ex);
                return null;
            }
        }

        public void ScanAndIndexLocalFiles()
        {
            try
            {
                if (!Directory.Exists(_storageRootPath)) return;

                var files = Directory.GetFiles(_storageRootPath, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    // Ignore temp / version files
                    if (file.EndsWith(".part", StringComparison.OrdinalIgnoreCase) ||
                        file.Contains(".edm_versions", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string relPath = Path.GetRelativePath(_storageRootPath, file).Replace('\\', '/');
                    var fi = new FileInfo(file);

                    if (!_localIndex.TryGetValue(relPath, out var existing) || existing.LastModifiedUtc != fi.LastWriteTimeUtc)
                    {
                        _localIndex[relPath] = new LocalFileMetadata(
                            FileId: Guid.NewGuid(),
                            FileName: fi.Name,
                            RelativePath: relPath,
                            FullPath: file,
                            FileSizeBytes: fi.Length,
                            Sha256Hash: string.Empty, // Computed lazily on demand
                            Version: 1,
                            LastModifiedUtc: fi.LastWriteTimeUtc,
                            SyncState: LocalSyncState.Synced);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[LocalHddStorageEngine] File scan failed", ex);
            }
        }

        public IReadOnlyList<LocalFileMetadata> GetIndexedFiles()
        {
            return _localIndex.Values.ToList();
        }

        public void EnqueueOfflineSyncOperation(OfflineSyncOperation op)
        {
            lock (_lock)
            {
                var queue = LoadOfflineQueue();
                queue.Add(op);
                SaveOfflineQueue(queue);
            }
            LoggingService.Log($"[LocalHddStorageEngine] Enqueued offline sync operation: {op.OperationType} for '{op.RelativePath}'");
        }

        public List<OfflineSyncOperation> GetPendingOfflineOperations()
        {
            lock (_lock)
            {
                return LoadOfflineQueue();
            }
        }

        public void ClearProcessedOfflineOperation(Guid operationId)
        {
            lock (_lock)
            {
                var queue = LoadOfflineQueue();
                queue.RemoveAll(x => x.OperationId == operationId);
                SaveOfflineQueue(queue);
            }
        }

        private List<OfflineSyncOperation> LoadOfflineQueue()
        {
            try
            {
                if (File.Exists(_offlineQueuePath))
                {
                    string json = File.ReadAllText(_offlineQueuePath);
                    return JsonSerializer.Deserialize<List<OfflineSyncOperation>>(json) ?? new();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[LocalHddStorageEngine] Failed to read offline queue", ex);
            }
            return new();
        }

        private void SaveOfflineQueue(List<OfflineSyncOperation> queue)
        {
            try
            {
                string json = JsonSerializer.Serialize(queue, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_offlineQueuePath, json);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[LocalHddStorageEngine] Failed to save offline queue", ex);
            }
        }
    }
}
