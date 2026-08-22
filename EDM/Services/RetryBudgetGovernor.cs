using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class RetryBudgetExhaustedException : Exception
    {
        public RetryBudgetExhaustedException(string message) : base(message) { }
    }

    /// <summary>
    /// RetryBudgetGovernor — Enforces finite retry budgets per download and per segment,
    /// calculating jittered exponential backoffs and preventing infinite retry loops on dead connections.
    /// </summary>
    public class RetryBudgetGovernor
    {
        private readonly int _maxTotalRetries;
        private readonly int _maxSegmentRetries;
        private int _currentTotalRetries;
        private readonly ConcurrentDictionary<int, int> _segmentRetries = new();
        private readonly Random _rng = new();
        private readonly object _lock = new();

        public int MaxTotalRetries => _maxTotalRetries;
        public int MaxSegmentRetries => _maxSegmentRetries;
        public int CurrentTotalRetries => Volatile.Read(ref _currentTotalRetries);

        public RetryBudgetGovernor(int maxTotalRetries = 30, int maxSegmentRetries = 8)
        {
            _maxTotalRetries = Math.Max(1, maxTotalRetries);
            _maxSegmentRetries = Math.Max(1, maxSegmentRetries);
        }

        public bool TryRecordRetry(int segmentId, DownloadFailureCategory category, out TimeSpan delay)
        {
            delay = TimeSpan.Zero;

            if (!DownloadErrorClassifier.IsRecoverable(category))
            {
                return false;
            }

            int total = Interlocked.Increment(ref _currentTotalRetries);
            if (total > _maxTotalRetries)
            {
                return false;
            }

            int segCount = _segmentRetries.AddOrUpdate(segmentId, 1, (_, count) => count + 1);
            if (segCount > _maxSegmentRetries)
            {
                return false;
            }

            // Exponential backoff with jitter
            double baseSec = category switch
            {
                DownloadFailureCategory.Http429Throttled => 2.0,
                DownloadFailureCategory.Http5xxServer => 1.0,
                DownloadFailureCategory.Timeout => 0.5,
                DownloadFailureCategory.DnsFailure => 1.5,
                _ => 0.2
            };

            double backoffSec = Math.Min(15.0, baseSec * Math.Pow(1.5, Math.Min(segCount, 6)));
            double jitter;
            lock (_lock)
            {
                jitter = _rng.NextDouble() * 0.4;
            }

            delay = TimeSpan.FromSeconds(backoffSec + jitter);
            return true;
        }

        public void ResetSegment(int segmentId)
        {
            _segmentRetries.TryRemove(segmentId, out _);
        }
    }
}
