using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EDM.Services.Telemetry
{
    /// <summary>
    /// Offline-First Resilient Telemetry Queue.
    /// Safely spools events to local storage when offline and drains in batches when online.
    /// </summary>
    public class TelemetryQueueService
    {
        private static readonly Lazy<TelemetryQueueService> _instance = new(() => new TelemetryQueueService());
        public static TelemetryQueueService Instance => _instance.Value;

        private readonly string _queueFilePath;
        private readonly List<TelemetryEvent> _queue = new();
        private readonly object _lock = new();
        private const int MaxQueueCapacity = 2000;

        public TelemetryQueueService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string edmDir = Path.Combine(appData, "EDM");
            Directory.CreateDirectory(edmDir);
            _queueFilePath = Path.Combine(edmDir, "telemetry_queue.json");

            LoadQueue();
        }

        public void Enqueue(TelemetryEvent evt)
        {
            if (evt == null) return;
            lock (_lock)
            {
                _queue.Add(evt);
                while (_queue.Count > MaxQueueCapacity)
                {
                    _queue.RemoveAt(0); // Drop oldest
                }
                SaveQueue();
            }
        }

        public List<TelemetryEvent> DequeueBatch(int batchSize = 25)
        {
            lock (_lock)
            {
                int count = Math.Min(batchSize, _queue.Count);
                var batch = _queue.Take(count).ToList();
                _queue.RemoveRange(0, count);
                SaveQueue();
                return batch;
            }
        }

        public int GetPendingCount()
        {
            lock (_lock)
            {
                return _queue.Count;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _queue.Clear();
                SaveQueue();
            }
        }

        private void LoadQueue()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_queueFilePath))
                    {
                        string json = File.ReadAllText(_queueFilePath);
                        var items = JsonSerializer.Deserialize<List<TelemetryEvent>>(json);
                        if (items != null)
                        {
                            _queue.Clear();
                            _queue.AddRange(items);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[TelemetryQueueService] Failed to load queue", ex);
                }
            }
        }

        private void SaveQueue()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = false };
                string json = JsonSerializer.Serialize(_queue, options);
                File.WriteAllText(_queueFilePath, json);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[TelemetryQueueService] Failed to save queue", ex);
            }
        }
    }
}
