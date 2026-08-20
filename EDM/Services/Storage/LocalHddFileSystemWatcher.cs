using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services.Interfaces;

namespace EDM.Services.Storage
{
    public enum LocalFileChangeType
    {
        Created,
        Modified,
        Renamed,
        Deleted
    }

    public record LocalFileChangeEvent(
        LocalFileChangeType ChangeType,
        string RelativePath,
        string? OldRelativePath,
        string FullPath,
        DateTime TimestampUtc);

    /// <summary>
    /// Robust Windows FileSystemWatcher for EDM Local HDD Workspace.
    /// Features:
    /// - Debouncing and coalescing of rapid file system events.
    /// - Echo/loop prevention (suppression of self-generated writes).
    /// - Safe handling of locked/busy files with exponential backoff.
    /// - Recursive subdirectory monitoring and path normalization.
    /// </summary>
    public class LocalHddFileSystemWatcher : IDisposable
    {
        private static readonly Lazy<LocalHddFileSystemWatcher> _instance = new(() => new LocalHddFileSystemWatcher());
        public static LocalHddFileSystemWatcher Instance => _instance.Value;

        private readonly LocalHddStorageEngine _storageEngine;
        private FileSystemWatcher? _watcher;
        private bool _isDisposed;
        private readonly object _lock = new();

        private readonly ConcurrentDictionary<string, (CancellationTokenSource Cts, LocalFileChangeEvent Event)> _pendingDebounceEvents = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTime> _suppressedPaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> _appliedCloudHashes = new(StringComparer.OrdinalIgnoreCase);

        private const int DebounceDelayMs = 600; // 600ms debounce buffer
        private const int DefaultSuppressionSeconds = 10;

        public event Action<LocalFileChangeEvent>? FileChanged;

        public bool IsRunning => _watcher != null && _watcher.EnableRaisingEvents;
        public string WatchedPath => _storageEngine.StorageRootPath;

        public LocalHddFileSystemWatcher(LocalHddStorageEngine? storageEngine = null)
        {
            _storageEngine = storageEngine ?? LocalHddStorageEngine.Instance;
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_watcher != null) return;

                string root = _storageEngine.StorageRootPath;
                if (!Directory.Exists(root))
                {
                    Directory.CreateDirectory(root);
                }

                _watcher = new FileSystemWatcher(root)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName 
                                 | NotifyFilters.DirectoryName 
                                 | NotifyFilters.LastWrite 
                                 | NotifyFilters.Size 
                                 | NotifyFilters.CreationTime,
                    InternalBufferSize = 64 * 1024 // 64 KB buffer to avoid buffer overflow under heavy disk I/O
                };

                _watcher.Created += OnFileSystemEvent;
                _watcher.Changed += OnFileSystemEvent;
                _watcher.Deleted += OnFileSystemEvent;
                _watcher.Renamed += OnRenamedEvent;
                _watcher.Error += OnWatcherError;

                _watcher.EnableRaisingEvents = true;
                LoggingService.Log($"[LocalHddFileSystemWatcher] Started watching '{root}'");
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (_watcher == null) return;

                _watcher.EnableRaisingEvents = false;
                _watcher.Created -= OnFileSystemEvent;
                _watcher.Changed -= OnFileSystemEvent;
                _watcher.Deleted -= OnFileSystemEvent;
                _watcher.Renamed -= OnRenamedEvent;
                _watcher.Error -= OnWatcherError;
                _watcher.Dispose();
                _watcher = null;

                // Cancel all pending debounce tasks
                foreach (var kvp in _pendingDebounceEvents)
                {
                    kvp.Value.Cts.Cancel();
                    kvp.Value.Cts.Dispose();
                }
                _pendingDebounceEvents.Clear();

                LoggingService.Log("[LocalHddFileSystemWatcher] Stopped watching");
            }
        }

        /// <summary>
        /// Temporarily suppresses watcher events for a given relative path.
        /// Prevents echo loops when Cloud -> Local sync writes to the disk.
        /// </summary>
        public void SuppressPath(string relativePath, TimeSpan? duration = null)
        {
            string normalized = NormalizeRelativePath(relativePath);
            var expiry = DateTime.UtcNow.Add(duration ?? TimeSpan.FromSeconds(DefaultSuppressionSeconds));
            _suppressedPaths[normalized] = expiry;
        }

        /// <summary>
        /// Records the known SHA-256 hash applied from the cloud to prevent echo uploads.
        /// </summary>
        public void RecordAppliedCloudHash(string relativePath, string sha256Hash)
        {
            string normalized = NormalizeRelativePath(relativePath);
            _appliedCloudHashes[normalized] = sha256Hash.ToLowerInvariant().Trim();
        }

        public bool IsPathSuppressed(string relativePath)
        {
            string normalized = NormalizeRelativePath(relativePath);
            if (_suppressedPaths.TryGetValue(normalized, out var expiry))
            {
                if (DateTime.UtcNow < expiry)
                {
                    return true;
                }
                _suppressedPaths.TryRemove(normalized, out _);
            }
            return false;
        }

        public string? GetAppliedCloudHash(string relativePath)
        {
            string normalized = NormalizeRelativePath(relativePath);
            return _appliedCloudHashes.TryGetValue(normalized, out var hash) ? hash : null;
        }

        public void ClearAppliedCloudHash(string relativePath)
        {
            string normalized = NormalizeRelativePath(relativePath);
            _appliedCloudHashes.TryRemove(normalized, out _);
        }

        private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        {
            if (ShouldIgnoreFile(e.FullPath)) return;

            string relPath = GetRelativePath(e.FullPath);
            if (IsPathSuppressed(relPath)) return;

            LocalFileChangeType type = e.ChangeType switch
            {
                WatcherChangeTypes.Created => LocalFileChangeType.Created,
                WatcherChangeTypes.Deleted => LocalFileChangeType.Deleted,
                _ => LocalFileChangeType.Modified
            };

            var changeEvent = new LocalFileChangeEvent(type, relPath, null, e.FullPath, DateTime.UtcNow);
            EnqueueDebouncedEvent(changeEvent);
        }

        private void OnRenamedEvent(object sender, RenamedEventArgs e)
        {
            if (ShouldIgnoreFile(e.FullPath) && ShouldIgnoreFile(e.OldFullPath)) return;

            string newRel = GetRelativePath(e.FullPath);
            string oldRel = GetRelativePath(e.OldFullPath);

            if (IsPathSuppressed(newRel) || IsPathSuppressed(oldRel)) return;

            var changeEvent = new LocalFileChangeEvent(LocalFileChangeType.Renamed, newRel, oldRel, e.FullPath, DateTime.UtcNow);
            EnqueueDebouncedEvent(changeEvent);
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            LoggingService.Log($"[LocalHddFileSystemWatcher] Watcher error: {e.GetException()?.Message}");
        }

        private void EnqueueDebouncedEvent(LocalFileChangeEvent changeEvent)
        {
            string key = changeEvent.RelativePath;

            // If a previous debounce is pending for this file, cancel it
            if (_pendingDebounceEvents.TryRemove(key, out var previous))
            {
                previous.Cts.Cancel();
                previous.Cts.Dispose();
            }

            // Deleted events are dispatched immediately without debouncing
            if (changeEvent.ChangeType == LocalFileChangeType.Deleted)
            {
                DispatchEvent(changeEvent);
                return;
            }

            var cts = new CancellationTokenSource();
            _pendingDebounceEvents[key] = (cts, changeEvent);

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DebounceDelayMs, cts.Token).ConfigureAwait(false);

                    if (_pendingDebounceEvents.TryRemove(key, out var current) && !cts.IsCancellationRequested)
                    {
                        DispatchEvent(current.Event);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Debounce reset by new incoming event
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[LocalHddFileSystemWatcher] Debounce dispatch error", ex);
                }
                finally
                {
                    cts.Dispose();
                }
            });
        }

        private void DispatchEvent(LocalFileChangeEvent changeEvent)
        {
            if (IsPathSuppressed(changeEvent.RelativePath)) return;

            try
            {
                FileChanged?.Invoke(changeEvent);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[LocalHddFileSystemWatcher] Handler exception", ex);
            }
        }

        /// <summary>
        /// Waits until a locked or busy file is released by another process (e.g. Word, Excel, or ongoing browser download).
        /// </summary>
        public static async Task<bool> WaitForFileReadyAsync(
            string fullPath, 
            int maxAttempts = 10, 
            int initialDelayMs = 150, 
            CancellationToken ct = default)
        {
            if (!File.Exists(fullPath)) return false;

            int delay = initialDelayMs;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (ct.IsCancellationRequested) return false;

                try
                {
                    using var stream = new FileStream(
                        fullPath, 
                        FileMode.Open, 
                        FileAccess.Read, 
                        FileShare.ReadWrite, 
                        4096, 
                        FileOptions.None);

                    if (stream.Length >= 0)
                    {
                        return true;
                    }
                }
                catch (IOException)
                {
                    // File is locked/busy; back off and retry
                }
                catch (UnauthorizedAccessException)
                {
                    // Permission issue or file being deleted
                }

                await Task.Delay(delay, ct).ConfigureAwait(false);
                delay = Math.Min(delay * 2, 2000); // Exponential backoff capped at 2s
            }

            return false;
        }

        private bool ShouldIgnoreFile(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return true;

            string name = Path.GetFileName(fullPath);
            if (name.StartsWith("~$", StringComparison.OrdinalIgnoreCase) || // Office temp files
                name.StartsWith(".part", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".part", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".crdownload", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase) ||
                fullPath.Contains(".edm_versions", StringComparison.OrdinalIgnoreCase) ||
                fullPath.Contains(".git", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private string GetRelativePath(string fullPath)
        {
            string root = _storageEngine.StorageRootPath;
            return NormalizeRelativePath(Path.GetRelativePath(root, fullPath));
        }

        private static string NormalizeRelativePath(string path)
        {
            return path.Replace('\\', '/').Trim('/');
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Stop();
        }
    }
}
