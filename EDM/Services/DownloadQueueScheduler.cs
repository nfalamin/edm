using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using EDM.Models;

namespace EDM.Services
{
    public enum QueueItemState
    {
        Queued,
        Starting,
        Downloading,
        Paused,
        Completed,
        Failed,
        Cancelled,
        Retrying
    }

    public class QueuedDownloadItem
    {
        public string DownloadId { get; set; } = Guid.NewGuid().ToString("N");
        public string QueueId { get; set; } = "default";
        public string Url { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public DownloadPriority Priority { get; set; } = DownloadPriority.Normal;
        public long TotalBytes { get; set; }
        public long RemainingBytes { get; set; }
        public DateTime EnqueuedTimeUtc { get; set; } = DateTime.UtcNow;
        public QueueItemState State { get; set; } = QueueItemState.Queued;
        public int RetryCount { get; set; } = 0;
        public int MaxRetries { get; set; } = 3;
        public DateTime? NextRetryTimeUtc { get; set; }
        public string LastFailureReason { get; set; } = string.Empty;
        public string FailureCategory { get; set; } = string.Empty;
        public int CustomOrderIndex { get; set; } = 0;

        public double CalculateQueueScore()
        {
            double basePriority = Priority switch
            {
                DownloadPriority.Urgent => 100.0,
                DownloadPriority.High => 50.0,
                DownloadPriority.Normal => 20.0,
                DownloadPriority.Low => 10.0,
                _ => 20.0
            };

            // Priority Aging: +2.0 score per minute waiting to prevent starvation
            double waitingMinutes = (DateTime.UtcNow - EnqueuedTimeUtc).TotalMinutes;
            double agingScore = waitingMinutes * 2.0;

            // Completion / Small-File bonus: Smaller/near-complete files get up to +25 score
            double sizeBonus = 0.0;
            if (RemainingBytes > 0 && RemainingBytes < 10 * 1024 * 1024)
            {
                sizeBonus = 25.0;
            }

            // Custom manual priority offset
            double customOffset = CustomOrderIndex * -1.0;

            return basePriority + agingScore + sizeBonus + customOffset;
        }
    }

    /// <summary>
    /// Advanced Download Queue & Scheduler Subsystem.
    /// Manages multi-queue scheduling, dynamic starvation aging, per-queue concurrency enforcement,
    /// manual reordering, crash recovery, and thread-safe slot allocation.
    /// </summary>
    public class DownloadQueueScheduler
    {
        private static readonly Lazy<DownloadQueueScheduler> _lazy = new(() => new DownloadQueueScheduler());
        public static DownloadQueueScheduler Instance => _lazy.Value;

        private readonly ConcurrentDictionary<string, QueuedDownloadItem> _items = new();
        private readonly List<DownloadQueueModel> _queues = new();
        private int _maxActiveDownloads = 4;
        private readonly object _lock = new();
        private readonly string _persistencePath;

        public int MaxActiveDownloads
        {
            get => _maxActiveDownloads;
            set
            {
                lock (_lock)
                {
                    _maxActiveDownloads = Math.Max(1, Math.Min(16, value));
                }
            }
        }

        public int MaxConcurrentDownloads
        {
            get => MaxActiveDownloads;
            set => MaxActiveDownloads = value;
        }

        public int ActiveCount => _items.Values.Count(i => i.State == QueueItemState.Downloading || i.State == QueueItemState.Starting);
        public int QueuedCount => _items.Values.Count(i => i.State == QueueItemState.Queued || i.State == QueueItemState.Retrying);

        public DownloadQueueScheduler() : this(null, 4) { }
        public DownloadQueueScheduler(int maxActiveDownloads) : this(Path.Combine(Path.GetTempPath(), $"EDM_Queue_{Guid.NewGuid():N}"), maxActiveDownloads) { }
        public DownloadQueueScheduler(string? storagePath) : this(storagePath, 4) { }
        public DownloadQueueScheduler(int maxActiveDownloads, string? storagePath) : this(storagePath, maxActiveDownloads) { }
        public DownloadQueueScheduler(string? storagePath, int maxActiveDownloads)
        {
            string dir = storagePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EDM", "state");
            Directory.CreateDirectory(dir);
            _persistencePath = Path.Combine(dir, "queue_state.json");
            _maxActiveDownloads = Math.Max(1, Math.Min(16, maxActiveDownloads));

            InitializeDefaultQueues();
            LoadState();
        }

        private void InitializeDefaultQueues()
        {
            lock (_lock)
            {
                if (!_queues.Any(q => string.Equals(q.Id, "default", StringComparison.OrdinalIgnoreCase)))
                {
                    _queues.Add(new DownloadQueueModel
                    {
                        Id = "default",
                        Name = "Main Download Queue",
                        Priority = QueuePriority.Normal,
                        MaxConcurrentFiles = 3,
                        MaxConnectionsPerFile = 8,
                        IsActive = true,
                        IsRunning = true,
                        Description = "Default primary queue for all active downloads."
                    });
                }
            }
        }

        // ==================== MULTI-QUEUE MANAGEMENT ====================

        public List<DownloadQueueModel> GetQueues()
        {
            lock (_lock)
            {
                return _queues.Select(q => new DownloadQueueModel
                {
                    Id = q.Id,
                    Name = q.Name,
                    Priority = q.Priority,
                    MaxConcurrentFiles = q.MaxConcurrentFiles,
                    MaxConnectionsPerFile = q.MaxConnectionsPerFile,
                    EnableSchedule = q.EnableSchedule,
                    StartTime = q.StartTime,
                    StopTime = q.StopTime,
                    SpeedLimitKbps = q.SpeedLimitKbps,
                    PostAction = q.PostAction,
                    ItemIds = q.ItemIds.ToList(),
                    IsActive = q.IsActive,
                    IsPaused = q.IsPaused,
                    IsRunning = q.IsRunning,
                    Description = q.Description,
                    CreationTimeUtc = q.CreationTimeUtc
                }).ToList();
            }
        }

        public DownloadQueueModel? GetQueue(string queueId)
        {
            lock (_lock)
            {
                return _queues.FirstOrDefault(q => string.Equals(q.Id, queueId, StringComparison.OrdinalIgnoreCase));
            }
        }

        public void AddOrUpdateQueue(DownloadQueueModel queue)
        {
            if (queue == null || string.IsNullOrWhiteSpace(queue.Id)) return;

            lock (_lock)
            {
                int idx = _queues.FindIndex(q => string.Equals(q.Id, queue.Id, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    _queues[idx] = queue;
                }
                else
                {
                    _queues.Add(queue);
                }
                SaveState();
            }
        }

        public bool DeleteQueue(string queueId)
        {
            if (string.Equals(queueId, "default", StringComparison.OrdinalIgnoreCase)) return false; // Cannot delete default queue

            lock (_lock)
            {
                var q = _queues.FirstOrDefault(x => string.Equals(x.Id, queueId, StringComparison.OrdinalIgnoreCase));
                if (q != null)
                {
                    _queues.Remove(q);

                    // Move orphaned items to default queue
                    foreach (var itemId in q.ItemIds)
                    {
                        if (_items.TryGetValue(itemId, out var item))
                        {
                            item.QueueId = "default";
                        }
                    }

                    var defQ = _queues.FirstOrDefault(x => string.Equals(x.Id, "default", StringComparison.OrdinalIgnoreCase));
                    if (defQ != null)
                    {
                        defQ.ItemIds.AddRange(q.ItemIds.Where(id => !defQ.ItemIds.Contains(id)));
                    }

                    SaveState();
                    return true;
                }
                return false;
            }
        }

        public bool RenameQueue(string queueId, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return false;

            lock (_lock)
            {
                var q = _queues.FirstOrDefault(x => string.Equals(x.Id, queueId, StringComparison.OrdinalIgnoreCase));
                if (q != null)
                {
                    q.Name = newName.Trim();
                    SaveState();
                    return true;
                }
                return false;
            }
        }

        public void PauseQueue(string queueId)
        {
            lock (_lock)
            {
                var q = _queues.FirstOrDefault(x => string.Equals(x.Id, queueId, StringComparison.OrdinalIgnoreCase));
                if (q != null)
                {
                    q.IsPaused = true;
                    q.IsRunning = false;
                    SaveState();
                }
            }
        }

        public void ResumeQueue(string queueId)
        {
            lock (_lock)
            {
                var q = _queues.FirstOrDefault(x => string.Equals(x.Id, queueId, StringComparison.OrdinalIgnoreCase));
                if (q != null)
                {
                    q.IsPaused = false;
                    q.IsRunning = true;
                    q.IsActive = true;
                    SaveState();
                }
            }
        }

        public void StopQueue(string queueId)
        {
            lock (_lock)
            {
                var q = _queues.FirstOrDefault(x => string.Equals(x.Id, queueId, StringComparison.OrdinalIgnoreCase));
                if (q != null)
                {
                    q.IsRunning = false;
                    SaveState();
                }
            }
        }

        public void StartQueue(string queueId)
        {
            lock (_lock)
            {
                var q = _queues.FirstOrDefault(x => string.Equals(x.Id, queueId, StringComparison.OrdinalIgnoreCase));
                if (q != null)
                {
                    q.IsRunning = true;
                    q.IsPaused = false;
                    q.IsActive = true;
                    SaveState();
                }
            }
        }

        // ==================== ITEM SCHEDULING & CONCURRENCY ====================

        public void Enqueue(QueuedDownloadItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.DownloadId)) return;

            lock (_lock)
            {
                if (string.IsNullOrWhiteSpace(item.QueueId)) item.QueueId = "default";
                item.State = QueueItemState.Queued;

                _items[item.DownloadId] = item;

                var q = _queues.FirstOrDefault(x => string.Equals(x.Id, item.QueueId, StringComparison.OrdinalIgnoreCase));
                if (q != null && !q.ItemIds.Contains(item.DownloadId))
                {
                    q.ItemIds.Add(item.DownloadId);
                }

                SaveState();
            }
        }

        /// <summary>
        /// Selects the next best queued download item to execute based on highest dynamic queue score,
        /// respecting global and per-queue concurrency limits, queue running state, retry backoff timers, and pause status.
        /// </summary>
        public QueuedDownloadItem? TryGetNextDownloadToStart(string? specificQueueId = null)
        {
            lock (_lock)
            {
                if (ActiveCount >= _maxActiveDownloads) return null;

                // Build set of paused/stopped queues
                var blockedQueueIds = new HashSet<string>(_queues
                    .Where(q => q.IsPaused || !q.IsActive || !q.IsRunning)
                    .Select(q => q.Id), StringComparer.OrdinalIgnoreCase);

                var now = DateTime.UtcNow;
                var candidates = _items.Values
                    .Where(i => i.State == QueueItemState.Queued || 
                               (i.State == QueueItemState.Retrying && (!i.NextRetryTimeUtc.HasValue || i.NextRetryTimeUtc.Value <= now)))
                    .Where(i => !blockedQueueIds.Contains(i.QueueId));

                if (!string.IsNullOrWhiteSpace(specificQueueId))
                {
                    candidates = candidates.Where(i => string.Equals(i.QueueId, specificQueueId, StringComparison.OrdinalIgnoreCase));
                }

                var ordered = candidates
                    .OrderByDescending(i => i.CalculateQueueScore())
                    .ThenBy(i => i.EnqueuedTimeUtc)
                    .ToList();

                foreach (var candidate in ordered)
                {
                    // Check per-queue concurrency limit
                    var queueDef = _queues.FirstOrDefault(q => string.Equals(q.Id, candidate.QueueId, StringComparison.OrdinalIgnoreCase));
                    int queueMaxConcurrent = queueDef?.MaxConcurrentFiles ?? 3;

                    int activeInThisQueue = _items.Values.Count(i =>
                        string.Equals(i.QueueId, candidate.QueueId, StringComparison.OrdinalIgnoreCase) &&
                        (i.State == QueueItemState.Downloading || i.State == QueueItemState.Starting));

                    if (activeInThisQueue < queueMaxConcurrent)
                    {
                        candidate.State = QueueItemState.Starting;
                        SaveState();
                        return candidate;
                    }
                }

                return null;
            }
        }

        public void MarkStarted(string downloadId)
        {
            if (_items.TryGetValue(downloadId, out var item))
            {
                item.State = QueueItemState.Downloading;
            }
        }

        public void MarkCompleted(string downloadId)
        {
            if (_items.TryGetValue(downloadId, out var item))
            {
                item.State = QueueItemState.Completed;
                SaveState();
            }
        }

        public void MarkFailed(string downloadId, bool allowRetry = true, string? reason = null, Exception? exception = null)
        {
            if (_items.TryGetValue(downloadId, out var item))
            {
                bool isTransient = true;
                if (exception != null)
                {
                    var decision = HttpRetryDecisionEngine.EvaluateException(exception, item.RetryCount);
                    if (decision.Action == RetryAction.FailFast || decision.Action == RetryAction.Abort)
                    {
                        isTransient = false;
                    }
                    item.LastFailureReason = reason ?? exception.Message;
                    item.FailureCategory = isTransient ? "Transient" : "Permanent";
                }
                else if (!string.IsNullOrEmpty(reason))
                {
                    item.LastFailureReason = reason;
                }

                if (allowRetry && isTransient && item.RetryCount < item.MaxRetries)
                {
                    item.RetryCount++;
                    item.State = QueueItemState.Retrying;

                    // Compute exponential backoff with jitter
                    double jitter = Random.Shared.NextDouble() * 500.0;
                    double backoffMs = Math.Min(60_000.0, (1000.0 * Math.Pow(2, item.RetryCount)) + jitter);
                    item.NextRetryTimeUtc = DateTime.UtcNow.AddMilliseconds(backoffMs);
                }
                else
                {
                    item.State = QueueItemState.Failed;
                    item.NextRetryTimeUtc = null;
                }
                SaveState();
            }
        }

        public bool RetryNow(string downloadId)
        {
            if (_items.TryGetValue(downloadId, out var item))
            {
                item.State = QueueItemState.Queued;
                item.NextRetryTimeUtc = null;
                SaveState();
                return true;
            }
            return false;
        }

        public bool CancelRetry(string downloadId)
        {
            if (_items.TryGetValue(downloadId, out var item))
            {
                item.State = QueueItemState.Cancelled;
                item.NextRetryTimeUtc = null;
                SaveState();
                return true;
            }
            return false;
        }

        public int RetryAllFailed(string? queueId = null)
        {
            lock (_lock)
            {
                int count = 0;
                var targets = _items.Values.Where(i => i.State == QueueItemState.Failed);
                if (!string.IsNullOrWhiteSpace(queueId))
                {
                    targets = targets.Where(i => string.Equals(i.QueueId, queueId, StringComparison.OrdinalIgnoreCase));
                }

                foreach (var item in targets)
                {
                    item.State = QueueItemState.Queued;
                    item.RetryCount = 0;
                    item.NextRetryTimeUtc = null;
                    count++;
                }

                if (count > 0) SaveState();
                return count;
            }
        }

        public void MarkPaused(string downloadId)
        {
            if (_items.TryGetValue(downloadId, out var item))
            {
                item.State = QueueItemState.Paused;
                SaveState();
            }
        }

        public void MarkCancelled(string downloadId)
        {
            if (_items.TryGetValue(downloadId, out var item))
            {
                item.State = QueueItemState.Cancelled;
                SaveState();
            }
        }

        public void Remove(string downloadId)
        {
            lock (_lock)
            {
                _items.TryRemove(downloadId, out _);
                foreach (var q in _queues)
                {
                    q.ItemIds.Remove(downloadId);
                }
                SaveState();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _items.Clear();
                foreach (var q in _queues) q.ItemIds.Clear();
                SaveState();
            }
        }

        public QueuedDownloadItem? GetItem(string downloadId)
        {
            _items.TryGetValue(downloadId, out var item);
            return item;
        }

        public IReadOnlyList<QueuedDownloadItem> GetOrderedQueue(string? queueId = null)
        {
            lock (_lock)
            {
                var query = _items.Values.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(queueId))
                {
                    query = query.Where(i => string.Equals(i.QueueId, queueId, StringComparison.OrdinalIgnoreCase));
                }

                return query
                    .OrderByDescending(i => i.CalculateQueueScore())
                    .ThenBy(i => i.EnqueuedTimeUtc)
                    .ToList();
            }
        }

        public void SetPriority(string downloadId, DownloadPriority priority)
        {
            if (_items.TryGetValue(downloadId, out var item))
            {
                item.Priority = priority;
                SaveState();
            }
        }

        public bool MoveUp(string downloadId)
        {
            lock (_lock)
            {
                if (!_items.TryGetValue(downloadId, out var target)) return false;

                var waiting = _items.Values
                    .Where(i => string.Equals(i.QueueId, target.QueueId, StringComparison.OrdinalIgnoreCase) && i.State == QueueItemState.Queued)
                    .OrderByDescending(i => i.CalculateQueueScore())
                    .ToList();

                int idx = waiting.FindIndex(i => i.DownloadId == downloadId);
                if (idx > 0)
                {
                    var prev = waiting[idx - 1];
                    target.CustomOrderIndex = prev.CustomOrderIndex - 1;
                    SaveState();
                    return true;
                }
                return false;
            }
        }

        public bool MoveDown(string downloadId)
        {
            lock (_lock)
            {
                if (!_items.TryGetValue(downloadId, out var target)) return false;

                var waiting = _items.Values
                    .Where(i => string.Equals(i.QueueId, target.QueueId, StringComparison.OrdinalIgnoreCase) && i.State == QueueItemState.Queued)
                    .OrderByDescending(i => i.CalculateQueueScore())
                    .ToList();

                int idx = waiting.FindIndex(i => i.DownloadId == downloadId);
                if (idx >= 0 && idx < waiting.Count - 1)
                {
                    var next = waiting[idx + 1];
                    target.CustomOrderIndex = next.CustomOrderIndex + 1;
                    SaveState();
                    return true;
                }
                return false;
            }
        }

        public int GetQueuePosition(string downloadId)
        {
            lock (_lock)
            {
                var waiting = _items.Values
                    .Where(i => i.State == QueueItemState.Queued || i.State == QueueItemState.Retrying)
                    .OrderByDescending(i => i.CalculateQueueScore())
                    .ThenBy(i => i.EnqueuedTimeUtc)
                    .ToList();

                int idx = waiting.FindIndex(i => i.DownloadId == downloadId);
                return idx >= 0 ? idx + 1 : 0;
            }
        }

        // ==================== CRASH RECOVERY ====================

        /// <summary>
        /// Detects stale active downloads (stuck in 'Downloading' or 'Starting' after unexpected app termination)
        /// and safely transitions them to 'Paused' or 'Queued'.
        /// </summary>
        public int RecoverStaleDownloads(IEnumerable<DownloadItem> allDownloads)
        {
            if (allDownloads == null) return 0;

            int recovered = 0;
            lock (_lock)
            {
                foreach (var item in allDownloads)
                {
                    if (item.Status == "Downloading" || item.Status == "Starting" || item.Status == "Connecting...")
                    {
                        item.Status = "Paused";
                        item.TransferRate = "0 B/s";
                        recovered++;

                        string idStr = item.Id.ToString("N");
                        if (_items.TryGetValue(idStr, out var qItem))
                        {
                            qItem.State = QueueItemState.Paused;
                        }
                    }
                }

                // Also heal any internal memory items
                foreach (var qItem in _items.Values.Where(i => i.State == QueueItemState.Downloading || i.State == QueueItemState.Starting))
                {
                    qItem.State = QueueItemState.Paused;
                }

                if (recovered > 0)
                {
                    SaveState();
                    LoggingService.Log($"[DownloadQueueScheduler] Crash Recovery: Recovered {recovered} stale active downloads.");
                }
            }

            return recovered;
        }

        // ==================== PERSISTENCE ====================

        public void SaveState()
        {
            try
            {
                var state = new
                {
                    MaxActiveDownloads = _maxActiveDownloads,
                    Queues = _queues,
                    Items = _items.Values.ToList()
                };

                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_persistencePath, json);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadQueueScheduler] Failed to persist queue state", ex);
            }
        }

        public void LoadState()
        {
            try
            {
                if (!File.Exists(_persistencePath)) return;

                string json = File.ReadAllText(_persistencePath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("MaxActiveDownloads", out var maxProp))
                {
                    _maxActiveDownloads = Math.Max(1, Math.Min(16, maxProp.GetInt32()));
                }

                if (root.TryGetProperty("Queues", out var queuesProp))
                {
                    var loadedQueues = JsonSerializer.Deserialize<List<DownloadQueueModel>>(queuesProp.GetRawText());
                    if (loadedQueues != null && loadedQueues.Count > 0)
                    {
                        _queues.Clear();
                        _queues.AddRange(loadedQueues);
                    }
                }

                if (root.TryGetProperty("Items", out var itemsProp))
                {
                    var loadedItems = JsonSerializer.Deserialize<List<QueuedDownloadItem>>(itemsProp.GetRawText());
                    if (loadedItems != null)
                    {
                        _items.Clear();
                        foreach (var item in loadedItems)
                        {
                            if (item.State == QueueItemState.Downloading || item.State == QueueItemState.Starting)
                            {
                                item.State = QueueItemState.Paused;
                            }
                            _items[item.DownloadId] = item;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadQueueScheduler] Failed to load queue state", ex);
            }
        }
    }
}
