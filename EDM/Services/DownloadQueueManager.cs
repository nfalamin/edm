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
    public class DownloadQueueManager : IDisposable, IAsyncDisposable
    {
        private sealed class QueueEntry
        {
            public string ItemId { get; }
            public string QueueId { get; }
            public Func<CancellationToken, Task> Work { get; }
            public QueuePriority Priority { get; }

            public QueueEntry(string itemId, string queueId, QueuePriority priority, Func<CancellationToken, Task> work)
            {
                ItemId = itemId;
                QueueId = queueId;
                Priority = priority;
                Work = work ?? throw new ArgumentNullException(nameof(work));
            }
        }

        private readonly List<QueueEntry> _queue = new();
        private readonly object _queueLock = new();
        private readonly SemaphoreSlim _itemsAvailable = new(0);
        private readonly CancellationTokenSource _cts = new();
        private readonly Task[] _workers;
        private readonly List<DownloadQueueModel> _queues = new();
        private readonly string _storagePath;

        public DownloadQueueManager(int maxParallel)
        {
            if (maxParallel <= 0) throw new ArgumentOutOfRangeException(nameof(maxParallel));

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _storagePath = Path.Combine(appData, "EDM", "queues.json");

            LoadQueuesFromDisk();

            _workers = new Task[maxParallel];
            for (int i = 0; i < maxParallel; i++)
            {
                _workers[i] = Task.Run(() => WorkerLoopAsync(_cts.Token));
            }
        }

        public List<DownloadQueueModel> GetQueues()
        {
            lock (_queueLock)
            {
                return _queues.ToList();
            }
        }

        public void AddOrUpdateQueue(DownloadQueueModel queue)
        {
            lock (_queueLock)
            {
                int idx = _queues.FindIndex(q => q.Id == queue.Id);
                if (idx >= 0) _queues[idx] = queue;
                else _queues.Add(queue);
                SaveQueuesToDisk();
            }
        }

        public ValueTask EnqueueAsync(Func<CancellationToken, Task> work)
        {
            return EnqueueAsync(null, null, QueuePriority.Normal, work);
        }

        public ValueTask EnqueueAsync(string? itemId, Func<CancellationToken, Task> work)
        {
            return EnqueueAsync(itemId, null, QueuePriority.Normal, work);
        }

        public ValueTask EnqueueAsync(string? itemId, string? queueId, QueuePriority priority, Func<CancellationToken, Task> work)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));
            lock (_queueLock)
            {
                _queue.Add(new QueueEntry(itemId ?? Guid.NewGuid().ToString("N"), queueId ?? "main", priority, work));
                _queue.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }
            _itemsAvailable.Release();
            return ValueTask.CompletedTask;
        }

        public bool Reprioritize(string itemId, int newPosition)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            lock (_queueLock)
            {
                int idx = _queue.FindIndex(q => string.Equals(q.ItemId, itemId, StringComparison.Ordinal));
                if (idx < 0) return false;
                var entry = _queue[idx];
                _queue.RemoveAt(idx);
                if (newPosition < 0) newPosition = 0;
                if (newPosition > _queue.Count) newPosition = _queue.Count;
                _queue.Insert(newPosition, entry);

                // Sync with queue definition model and persist state
                var qModel = _queues.FirstOrDefault(q => q.Id == entry.QueueId);
                if (qModel != null && qModel.ItemIds.Contains(itemId))
                {
                    qModel.ItemIds.Remove(itemId);
                    int insertIdx = Math.Clamp(newPosition, 0, qModel.ItemIds.Count);
                    qModel.ItemIds.Insert(insertIdx, itemId);
                    SaveQueuesToDisk();
                }

                return true;
            }
        }

        private async Task WorkerLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await _itemsAvailable.WaitAsync(ct).ConfigureAwait(false);

                    QueueEntry? entry = null;
                    lock (_queueLock)
                    {
                        if (_queue.Count > 0)
                        {
                            entry = _queue[0];
                            _queue.RemoveAt(0);
                        }
                    }

                    if (entry == null) continue;

                    try
                    {
                        await entry.Work(ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                    catch (Exception ex)
                    {
                        LoggingService.LogException("[DownloadQueueManager] Task execution failed", ex);
                    }

                    // Check if queue completed and post-action is configured
                    CheckQueueCompletionPostAction(entry.QueueId);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadQueueManager] WorkerLoop failed", ex);
            }
        }

        private void CheckQueueCompletionPostAction(string queueId)
        {
            lock (_queueLock)
            {
                bool remainingInQueue = _queue.Any(q => q.QueueId == queueId);
                if (!remainingInQueue)
                {
                    var targetQueue = _queues.FirstOrDefault(q => q.Id == queueId);
                    if (targetQueue != null && targetQueue.PostAction != PostQueueAction.None)
                    {
                        LoggingService.Log($"[DownloadQueueManager] Queue '{targetQueue.Name}' completed. Triggering post action: {targetQueue.PostAction}");
                        ExecutePostAction(targetQueue.PostAction);
                    }
                }
            }
        }

        private static void ExecutePostAction(PostQueueAction action, string? targetPath = null)
        {
            switch (action)
            {
                case PostQueueAction.Shutdown:
                    NativePowerActions.ShutdownMachine();
                    break;
                case PostQueueAction.Sleep:
                    NativePowerActions.SleepMachine();
                    break;
                case PostQueueAction.Hibernate:
                    NativePowerActions.HibernateMachine();
                    break;
                case PostQueueAction.Restart:
                    NativePowerActions.RestartMachine();
                    break;
                case PostQueueAction.OpenFile:
                    if (!string.IsNullOrEmpty(targetPath)) NativePowerActions.OpenFile(targetPath);
                    break;
                case PostQueueAction.OpenFolder:
                    if (!string.IsNullOrEmpty(targetPath)) NativePowerActions.OpenFolder(targetPath);
                    break;
                case PostQueueAction.PlaySound:
                    NativePowerActions.PlaySoundNotification();
                    break;
                case PostQueueAction.ExecuteApp:
                    if (!string.IsNullOrEmpty(targetPath)) NativePowerActions.ExecuteApplication(targetPath);
                    break;
            }
        }

        private void LoadQueuesFromDisk()
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    string json = File.ReadAllText(_storagePath);
                    var loaded = JsonSerializer.Deserialize<List<DownloadQueueModel>>(json);
                    if (loaded != null)
                    {
                        _queues.Clear();
                        _queues.AddRange(loaded);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DownloadQueueManager] Error loading queues from disk: {ex.Message}");
            }

            if (!_queues.Any())
            {
                _queues.Add(new DownloadQueueModel { Name = "Main Queue", Priority = QueuePriority.Normal });
            }
        }

        private void SaveQueuesToDisk()
        {
            try
            {
                string dir = Path.GetDirectoryName(_storagePath)!;
                Directory.CreateDirectory(dir);
                string tempPath = _storagePath + ".tmp";
                string json = JsonSerializer.Serialize(_queues, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _storagePath, overwrite: true);
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DownloadQueueManager] Error saving queues to disk: {ex.Message}");
            }
        }


        public void Dispose()
        {
            try
            {
                _cts.Cancel();
                for (int i = 0; i < _workers.Length; i++) _itemsAvailable.Release();
            }
            catch { }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _cts.Cancel();
                for (int i = 0; i < _workers.Length; i++) _itemsAvailable.Release();
                await Task.WhenAll(_workers).ConfigureAwait(false);
            }
            catch { }
        }
    }
}
