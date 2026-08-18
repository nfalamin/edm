using System;
using System.Threading;

namespace EDM.Services
{
    public enum NetworkSpeedTier
    {
        Offline,
        VerySlow, // < 1 MB/s
        Slow,     // 1 - 5 MB/s
        Normal,   // 5 - 25 MB/s
        Fast,     // 25 - 100 MB/s
        VeryFast  // > 100 MB/s
    }

    /// <summary>
    /// NetworkQualityProfiler — Dynamically profiles network performance from real telemetry
    /// and suggests optimal system-wide concurrency and segmentation parameters.
    /// </summary>
    public sealed class NetworkQualityProfiler
    {
        private static readonly Lazy<NetworkQualityProfiler> _lazy = new(() => new NetworkQualityProfiler());
        public static NetworkQualityProfiler Instance => _lazy.Value;

        private double _rollingThroughputBps;
        private double _averageRttMs = 30.0;
        private readonly object _lock = new();

        public double RollingThroughputBps
        {
            get { lock (_lock) return _rollingThroughputBps; }
        }

        public double AverageRttMs
        {
            get { lock (_lock) return _averageRttMs; }
        }

        public NetworkSpeedTier CurrentTier
        {
            get
            {
                if (!NetworkTransitionManager.Instance.IsNetworkAvailable) return NetworkSpeedTier.Offline;

                double mbps = RollingThroughputBps / (1024.0 * 1024.0);
                if (mbps <= 0.05) return NetworkSpeedTier.Normal; // Default assumption when starting
                if (mbps < 1.0) return NetworkSpeedTier.VerySlow;
                if (mbps < 5.0) return NetworkSpeedTier.Slow;
                if (mbps < 25.0) return NetworkSpeedTier.Normal;
                if (mbps < 100.0) return NetworkSpeedTier.Fast;
                return NetworkSpeedTier.VeryFast;
            }
        }

        public int RecommendedGlobalConnections => CurrentTier switch
        {
            NetworkSpeedTier.Offline => 1,
            NetworkSpeedTier.VerySlow => 4,
            NetworkSpeedTier.Slow => 8,
            NetworkSpeedTier.Normal => 16,
            NetworkSpeedTier.Fast => 32,
            NetworkSpeedTier.VeryFast => 64,
            _ => 16
        };

        public void RecordSample(double throughputBps, double rttMs = 0)
        {
            if (throughputBps < 0) return;
            lock (_lock)
            {
                if (_rollingThroughputBps <= 0)
                {
                    _rollingThroughputBps = throughputBps;
                }
                else
                {
                    _rollingThroughputBps = (0.25 * throughputBps) + (0.75 * _rollingThroughputBps);
                }

                if (rttMs > 0)
                {
                    _averageRttMs = (0.2 * rttMs) + (0.8 * _averageRttMs);
                }
            }
        }
    }
}
