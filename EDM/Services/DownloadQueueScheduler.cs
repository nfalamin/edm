using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace EDM.Services
{
    public enum QueueItemState
    {
        Queued,
        Starting,
        Downloading,
        Paused,
        Completed,
        Failed
    }

    public class QueuedDownloadItem
    {
        public string DownloadId { get; set; } = Guid.NewGuid().ToString("N");
        public string Url { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public DownloadPriority Priority { get; set; } = DownloadPriority.Normal;
        public long TotalBytes { get; set; }
        public long RemainingBytes { get; set; }
        public DateTime EnqueuedTimeUtc { get; set; } = DateTime.UtcNow;
        public QueueItemState State { get; set; } = QueueItemState.Queued;

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

            // Priority Aging: +2.0 score per minute waiting
            double waitingMinutes = (DateTime.UtcNow - EnqueuedTimeUtc).TotalMinutes;
            double agingScore = waitingMinutes * 2.0;

            // Completion / Small-File bonus: Smaller/near-complete files get up to +25 score
            double sizeBonus = 0.0;
            if (RemainingBytes > 0 && RemainingBytes < 10 * 1024 * 1024)
            {
                sizeBonus = 25.0;
            }

            return basePriority + agingScore + sizeBonus;
        }
    }

    /// <summary>
    /// DownloadQueueScheduler — Multi-download queue manager with deterministic priority scheduling,
    /// priority aging to prevent starvation, and completion-aware slot recycling.
    /// </summary>
    public class DownloadQueueScheduler
    {
        private static readonly Lazy<DownloadQueueScheduler> _lazy = new(() => new DownloadQueueScheduler());
        public static DownloadQueueScheduler Instance => _lazy.Value;

        private readonly ConcurrentDictionary<string, QueuedDownloadItem> _items = new();
        private int _maxActiveDownloads = 4;
        private readonly object _lock = new();

        public int MaxActiveDownloads
        {
            get => _maxActiveDownloads;
            set => _maxActiveDownloads = Math.Max(1, value);
        }

        public int QueuedCount => _items.Values.Count(i => i.State == QueueItemState.Queued);
        public int ActiveCount => _items.Values.Count(i => i.State == QueueItemState.Downloading || i.State == QueueItemState.Starting);

        public DownloadQueueScheduler(int maxActiveDownloads = 4)
        {
            _maxActiveDownloads = Math.Max(1, maxActiveDownloads);
        }

        public void Enqueue(QueuedDownloadItem item)
        {
            if (item == null) return;
            item.State = QueueItemState.Queued;
            _items[item.DownloadId] = item;
        }

        /// <summary>
        /// Selects the next best queued download item to execute based on highest dynamic queue score.
        /// </summary>
        public QueuedDownloadItem? TryGetNextDownloadToStart()
        {
            lock (_lock)
            {
                if (ActiveCount >= _maxActiveDownloads) return null;

                var best = _items.Values
                    .Where(i => i.State == QueueItemState.Queued)
                    .OrderByDescending(i => i.CalculateQueueScore())
                    .ThenBy(i => i.EnqueuedTimeUtc)
                    .FirstOrDefault();

                if (best != null)
                {
                    best.State = QueueItemState.Starting;
                }

                return best;
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
            }
        }

        public void MarkFailed(string downloadId)
        {
            if (_items.TryGetValue(downloadId, out var item))
            {
                item.State = QueueItemState.Failed;
            }
        }

        public void MarkPaused(string downloadId)
        {
            if (_items.TryGetValue(downloadId, out var item))
            {
                item.State = QueueItemState.Paused;
            }
        }

        public void Remove(string downloadId)
        {
            _items.TryRemove(downloadId, out _);
        }

        public void Clear()
        {
            _items.Clear();
        }
    }
}
