using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace EDM.Services
{
    public class GlobalDownloadResourceLease
    {
        public string DownloadId { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public DownloadPriority Priority { get; set; } = DownloadPriority.Normal;
        public int RequestedConnections { get; set; } = 8;
        public int AllocatedConnections { get; set; } = 4;
        public double AllocatedBandwidthBps { get; set; }
        public long TotalBytes { get; set; }
        public long RemainingBytes { get; set; }
        public double CurrentThroughputBps { get; set; }
        public double ExpectedThroughputGain { get; set; } = 0.10;
        public DateTime LeaseStartTime { get; set; } = DateTime.UtcNow;
        public DateTime LastRequestTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Calculates dynamic utility score based on Priority, Expected Gain, Aging, and Completion status.
        /// </summary>
        public double CalculateUtilityScore()
        {
            double basePriority = Priority switch
            {
                DownloadPriority.Urgent => 8.0,
                DownloadPriority.High => 4.0,
                DownloadPriority.Normal => 2.0,
                DownloadPriority.Low => 1.0,
                _ => 2.0
            };

            // Priority Aging: 10% boost per minute waiting
            double waitingMinutes = (DateTime.UtcNow - LastRequestTime).TotalMinutes;
            double agingFactor = 1.0 + Math.Min(3.0, waitingMinutes * 0.10);

            // Expected Gain multiplier
            double gainFactor = 1.0 + Math.Clamp(ExpectedThroughputGain, 0.0, 1.0);

            // Completion-Aware Boost: Give up to 50% boost if file is nearly finished (< 5 MB remaining)
            double completionFactor = 1.0;
            if (RemainingBytes > 0 && RemainingBytes < 5 * 1024 * 1024)
            {
                completionFactor = 1.5;
            }

            return basePriority * agingFactor * gainFactor * completionFactor;
        }
    }

    /// <summary>
    /// GlobalResourceManager — Centralized intelligence authority for process-wide socket concurrency,
    /// priority-weighted fairness, completion-aware boosting, and bandwidth budgeting across all active downloads.
    /// </summary>
    public sealed class GlobalResourceManager
    {
        private static readonly Lazy<GlobalResourceManager> _lazy = new(() => new GlobalResourceManager());
        public static GlobalResourceManager Instance => _lazy.Value;

        private readonly ConcurrentDictionary<string, GlobalDownloadResourceLease> _activeLeases = new();
        private int _globalMaxConnections = 64;
        private double _globalMaxBandwidthBps = 0; // 0 = unlimited
        private readonly object _lock = new();

        public int GlobalMaxConnections
        {
            get => Volatile.Read(ref _globalMaxConnections);
            set
            {
                Volatile.Write(ref _globalMaxConnections, Math.Max(4, value));
                lock (_lock) RecalculateAllBudgetsUnsafe();
            }
        }

        public double GlobalMaxBandwidthBps
        {
            get => Volatile.Read(ref _globalMaxBandwidthBps);
            set
            {
                Volatile.Write(ref _globalMaxBandwidthBps, Math.Max(0, value));
                lock (_lock) RecalculateAllBudgetsUnsafe();
            }
        }

        public int ActiveDownloadCount => _activeLeases.Count;

        public int TotalAllocatedConnections
        {
            get { lock (_lock) return _activeLeases.Values.Sum(l => l.AllocatedConnections); }
        }

        public GlobalResourceManager(int globalMaxConnections = 64, double globalMaxBandwidthBps = 0)
        {
            _globalMaxConnections = Math.Max(4, globalMaxConnections);
            _globalMaxBandwidthBps = Math.Max(0, globalMaxBandwidthBps);
        }

        /// <summary>
        /// Registers a download and acquires its authoritative connection and bandwidth allocation.
        /// </summary>
        public (int AllocatedConnections, double AllocatedBandwidthBps) AcquireLease(
            string downloadId,
            string host,
            int requestedConnections,
            long totalBytes = 0,
            long remainingBytes = 0,
            DownloadPriority priority = DownloadPriority.Normal)
        {
            if (string.IsNullOrWhiteSpace(downloadId)) downloadId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(host)) host = "localhost";

            lock (_lock)
            {
                var lease = _activeLeases.GetOrAdd(downloadId, id => new GlobalDownloadResourceLease
                {
                    DownloadId = id,
                    Host = host,
                    Priority = priority,
                    RequestedConnections = requestedConnections,
                    TotalBytes = totalBytes,
                    RemainingBytes = remainingBytes > 0 ? remainingBytes : totalBytes,
                    LeaseStartTime = DateTime.UtcNow,
                    LastRequestTime = DateTime.UtcNow
                });

                lease.Host = host;
                lease.Priority = priority;
                lease.RequestedConnections = requestedConnections;
                lease.TotalBytes = totalBytes;
                lease.RemainingBytes = remainingBytes > 0 ? remainingBytes : totalBytes;
                lease.LastRequestTime = DateTime.UtcNow;

                RecalculateAllBudgetsUnsafe();
                return (lease.AllocatedConnections, lease.AllocatedBandwidthBps);
            }
        }

        /// <summary>
        /// Updates a download's progress, remaining work, and scaling requests.
        /// </summary>
        public (int AllocatedConnections, double AllocatedBandwidthBps) UpdateLease(
            string downloadId,
            int requestedConnections,
            long remainingBytes,
            double currentSpeedBps,
            double expectedGain = 0.10)
        {
            lock (_lock)
            {
                if (_activeLeases.TryGetValue(downloadId, out var lease))
                {
                    lease.RequestedConnections = requestedConnections;
                    lease.RemainingBytes = remainingBytes;
                    lease.CurrentThroughputBps = currentSpeedBps;
                    lease.ExpectedThroughputGain = expectedGain;
                    lease.LastRequestTime = DateTime.UtcNow;

                    RecalculateAllBudgetsUnsafe();
                    return (lease.AllocatedConnections, lease.AllocatedBandwidthBps);
                }

                return (Math.Clamp(requestedConnections, 1, _globalMaxConnections), 0);
            }
        }

        /// <summary>
        /// Releases a download's resource lease when it pauses, finishes, or fails.
        /// </summary>
        public void ReleaseLease(string downloadId)
        {
            if (string.IsNullOrWhiteSpace(downloadId)) return;
            lock (_lock)
            {
                _activeLeases.TryRemove(downloadId, out _);
                RecalculateAllBudgetsUnsafe();
            }
        }

        private void RecalculateAllBudgetsUnsafe()
        {
            if (_activeLeases.IsEmpty) return;

            int globalMax = Volatile.Read(ref _globalMaxConnections);
            double globalBandwidth = Volatile.Read(ref _globalMaxBandwidthBps);
            var list = _activeLeases.Values.ToList();

            if (list.Count == 1)
            {
                list[0].AllocatedConnections = Math.Min(list[0].RequestedConnections, globalMax);
                list[0].AllocatedBandwidthBps = globalBandwidth;
                return;
            }

            // Step 1: Minimum Guarantee (at least 1 connection per download)
            int remainingBudget = globalMax;
            foreach (var lease in list)
            {
                lease.AllocatedConnections = 1;
                remainingBudget--;
            }

            if (remainingBudget > 0)
            {
                // Step 2: Utility-Scored Weighted Allocation
                double totalUtility = list.Sum(l => l.CalculateUtilityScore());
                if (totalUtility <= 0) totalUtility = 1.0;

                foreach (var lease in list)
                {
                    double share = (lease.CalculateUtilityScore() / totalUtility) * remainingBudget;
                    int additional = (int)Math.Round(share);
                    int desiredTotal = Math.Min(lease.RequestedConnections, lease.AllocatedConnections + additional);
                    int granted = Math.Min(desiredTotal - lease.AllocatedConnections, remainingBudget);

                    lease.AllocatedConnections += Math.Max(0, granted);
                    remainingBudget -= Math.Max(0, granted);
                }

                // Final bound clamping
                foreach (var lease in list)
                {
                    lease.AllocatedConnections = Math.Clamp(lease.AllocatedConnections, 1, lease.RequestedConnections);
                }
            }

            // Step 3: Bandwidth Allocation
            if (globalBandwidth > 0)
            {
                double totalUtility = list.Sum(l => l.CalculateUtilityScore());
                if (totalUtility <= 0) totalUtility = 1.0;

                foreach (var lease in list)
                {
                    lease.AllocatedBandwidthBps = (lease.CalculateUtilityScore() / totalUtility) * globalBandwidth;
                }
            }
            else
            {
                foreach (var lease in list) lease.AllocatedBandwidthBps = 0;
            }
        }

        public IReadOnlyList<GlobalDownloadResourceLease> GetActiveLeasesSnapshot()
        {
            lock (_lock)
            {
                return _activeLeases.Values.ToList();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _activeLeases.Clear();
            }
        }
    }
}
