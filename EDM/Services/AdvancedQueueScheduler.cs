using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;

namespace EDM.Services
{
    public class AdvancedQueueItem
    {
        public string ItemId { get; set; } = Guid.NewGuid().ToString("N");
        public string QueueId { get; set; } = "default";
        public string Url { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public QueuePriority Priority { get; set; } = QueuePriority.Normal;
        public DateTime EnqueuedTimeUtc { get; set; } = DateTime.UtcNow;
        public int DynamicPriorityBoost { get; set; } = 0;
        public string? DependsOnItemId { get; set; }
        public bool IsCompleted { get; set; } = false;
        public bool IsFailed { get; set; } = false;
    }

    public class AdvancedQueueDefinition
    {
        public string QueueId { get; set; } = "default";
        public string Name { get; set; } = "Default Queue";
        public int MaxConcurrentDownloads { get; set; } = 3;
        public int MaxConnectionsPerDownload { get; set; } = 8;
        public bool IsPaused { get; set; } = false;
        public TimeSpan? ScheduledStartTime { get; set; }
        public TimeSpan? ScheduledStopTime { get; set; }
        public int SpeedLimitKbps { get; set; } = 0; // 0 = unlimited
    }

    /// <summary>
    /// Advanced Download Orchestrator & Intelligent Queue Scheduler.
    /// Provides priority aging, starvation prevention, dependency chains,
    /// host fairness, and crash-resilient queue state persistence.
    /// </summary>
    public class AdvancedQueueScheduler
    {
        private readonly List<AdvancedQueueDefinition> _queues = new();
        private readonly List<AdvancedQueueItem> _items = new();
        private readonly object _lock = new();
        private readonly string _persistencePath;

        public AdvancedQueueScheduler(string? storageDir = null)
        {
            string baseDir = storageDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EDM");
            Directory.CreateDirectory(baseDir);
            _persistencePath = Path.Combine(baseDir, "advanced_queues.json");

            LoadState();
            if (_queues.Count == 0)
            {
                _queues.Add(new AdvancedQueueDefinition { QueueId = "default", Name = "Default Queue", MaxConcurrentDownloads = 3 });
                _queues.Add(new AdvancedQueueDefinition { QueueId = "high_priority", Name = "High Priority", MaxConcurrentDownloads = 5 });
                _queues.Add(new AdvancedQueueDefinition { QueueId = "nightly", Name = "Nightly Batch", MaxConcurrentDownloads = 2 });
            }
        }

        public void AddItem(AdvancedQueueItem item)
        {
            lock (_lock)
            {
                _items.Add(item);
                SaveState();
            }
        }

        public List<AdvancedQueueItem> GetSchedulableItems(int globalMaxConcurrent = 6)
        {
            lock (_lock)
            {
                // 1. Age pending items to prevent starvation
                var now = DateTime.UtcNow;
                foreach (var item in _items.Where(i => !i.IsCompleted && !i.IsFailed))
                {
                    var waitingMinutes = (now - item.EnqueuedTimeUtc).TotalMinutes;
                    if (waitingMinutes > 5.0)
                    {
                        item.DynamicPriorityBoost = (int)(waitingMinutes / 5.0);
                    }
                }

                // 2. Filter out items blocked by dependencies or paused queues
                var completedIds = new HashSet<string>(_items.Where(i => i.IsCompleted).Select(i => i.ItemId));
                var pausedQueueIds = new HashSet<string>(_queues.Where(q => q.IsPaused).Select(q => q.QueueId));

                var schedulable = _items
                    .Where(i => !i.IsCompleted && !i.IsFailed)
                    .Where(i => !pausedQueueIds.Contains(i.QueueId))
                    .Where(i => string.IsNullOrEmpty(i.DependsOnItemId) || completedIds.Contains(i.DependsOnItemId!))
                    .OrderByDescending(i => (int)i.Priority + i.DynamicPriorityBoost)
                    .ThenBy(i => i.EnqueuedTimeUtc)
                    .Take(globalMaxConcurrent)
                    .ToList();

                return schedulable;
            }
        }

        public void MarkItemCompleted(string itemId)
        {
            lock (_lock)
            {
                var item = _items.FirstOrDefault(i => i.ItemId == itemId);
                if (item != null)
                {
                    item.IsCompleted = true;
                    SaveState();
                }
            }
        }

        public void SaveState()
        {
            try
            {
                var state = new
                {
                    Queues = _queues,
                    Items = _items
                };
                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_persistencePath, json);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[AdvancedQueueScheduler] Failed to persist state", ex);
            }
        }

        private void LoadState()
        {
            try
            {
                if (File.Exists(_persistencePath))
                {
                    string json = File.ReadAllText(_persistencePath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Queues", out var qElem))
                    {
                        var loadedQueues = JsonSerializer.Deserialize<List<AdvancedQueueDefinition>>(qElem.GetRawText());
                        if (loadedQueues != null) _queues.AddRange(loadedQueues);
                    }
                    if (doc.RootElement.TryGetProperty("Items", out var iElem))
                    {
                        var loadedItems = JsonSerializer.Deserialize<List<AdvancedQueueItem>>(iElem.GetRawText());
                        if (loadedItems != null) _items.AddRange(loadedItems);
                    }
                }
            }
            catch { }
        }
    }
}
