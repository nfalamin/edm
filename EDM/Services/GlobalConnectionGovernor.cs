using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EDM.Models;

namespace EDM.Services
{
    public enum DownloadPriority
    {
        Low = 1,
        Normal = 2,
        High = 3,
        Urgent = 4
    }

    public class ActiveDownloadLease
    {
        public string DownloadId { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public DownloadPriority Priority { get; set; } = DownloadPriority.Normal;
        public int RequestedConnections { get; set; } = 8;
        public int AllocatedConnections { get; set; } = 4;
        public DateTime LeaseStartTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// GlobalConnectionGovernor — Process-wide connection budget governor.
    /// Distributes total available network connections fairly across multiple simultaneous downloads,
    /// respecting download priority and preventing socket exhaustion.
    /// </summary>
    public sealed class GlobalConnectionGovernor
    {
        private static readonly Lazy<GlobalConnectionGovernor> _lazy = new(() => new GlobalConnectionGovernor());
        public static GlobalConnectionGovernor Instance => _lazy.Value;

        private readonly ConcurrentDictionary<string, ActiveDownloadLease> _activeLeases = new();
        private int _globalMaxConnections = 64;
        private readonly object _lock = new();

        public int GlobalMaxConnections
        {
            get => Volatile.Read(ref _globalMaxConnections);
            set => Volatile.Write(ref _globalMaxConnections, Math.Max(4, value));
        }

        public int ActiveDownloadCount => _activeLeases.Count;

        public int TotalAllocatedConnections
        {
            get
            {
                lock (_lock)
                {
                    return _activeLeases.Values.Sum(l => l.AllocatedConnections);
                }
            }
        }

        public GlobalConnectionGovernor(int globalMax = 64)
        {
            _globalMaxConnections = Math.Max(4, globalMax);
        }

        /// <summary>
        /// Registers a download lease and calculates its authoritative connection budget.
        /// </summary>
        public int AcquireConnectionBudget(string downloadId, string host, int requested, DownloadPriority priority = DownloadPriority.Normal)
        {
            if (string.IsNullOrEmpty(downloadId)) downloadId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrEmpty(host)) host = "localhost";

            lock (_lock)
            {
                var lease = _activeLeases.GetOrAdd(downloadId, id => new ActiveDownloadLease
                {
                    DownloadId = id,
                    Host = host,
                    Priority = priority,
                    RequestedConnections = requested,
                    AllocatedConnections = requested
                });

                lease.Host = host;
                lease.Priority = priority;
                lease.RequestedConnections = requested;

                RecalculateBudgetsUnsafe();
                return lease.AllocatedConnections;
            }
        }

        /// <summary>
        /// Updates a download's allocated budget on dynamic scaling requests.
        /// </summary>
        public int RequestBudgetAdjustment(string downloadId, int target)
        {
            lock (_lock)
            {
                if (_activeLeases.TryGetValue(downloadId, out var lease))
                {
                    lease.RequestedConnections = target;
                    RecalculateBudgetsUnsafe();
                    return lease.AllocatedConnections;
                }
                return Math.Clamp(target, 1, _globalMaxConnections);
            }
        }

        /// <summary>
        /// Releases a download's connection lease when it finishes or pauses.
        /// </summary>
        public void ReleaseConnectionBudget(string downloadId)
        {
            lock (_lock)
            {
                _activeLeases.TryRemove(downloadId, out _);
                RecalculateBudgetsUnsafe();
            }
        }

        private void RecalculateBudgetsUnsafe()
        {
            if (_activeLeases.IsEmpty) return;

            int globalMax = Volatile.Read(ref _globalMaxConnections);
            var list = _activeLeases.Values.ToList();

            if (list.Count == 1)
            {
                list[0].AllocatedConnections = Math.Min(list[0].RequestedConnections, globalMax);
                return;
            }

            // Total priority weight
            double totalWeight = list.Sum(l => (int)l.Priority);
            int remainingBudget = globalMax;

            // Step 1: Assign minimum guarantee (at least 1 connection per download)
            foreach (var lease in list)
            {
                lease.AllocatedConnections = 1;
                remainingBudget--;
            }

            if (remainingBudget <= 0) return;

            // Step 2: Distribute remaining budget proportionally by priority and requested count
            foreach (var lease in list)
            {
                double share = ((int)lease.Priority / totalWeight) * remainingBudget;
                int additional = (int)Math.Round(share);
                int desiredTotal = Math.Min(lease.RequestedConnections, lease.AllocatedConnections + additional);
                int granted = Math.Min(desiredTotal - lease.AllocatedConnections, remainingBudget);

                lease.AllocatedConnections += Math.Max(0, granted);
                remainingBudget -= Math.Max(0, granted);
            }

            // Cap at requested connections and bounds
            foreach (var lease in list)
            {
                lease.AllocatedConnections = Math.Clamp(lease.AllocatedConnections, 1, lease.RequestedConnections);
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
