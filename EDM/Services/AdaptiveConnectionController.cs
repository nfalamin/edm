using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace EDM.Services
{
    public class ConnectionTelemetrySample
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public double AggregateThroughputBps { get; set; }
        public double RollingThroughputBps { get; set; }
        public double AverageRttMs { get; set; }
        public double ServerResponseTimeMs { get; set; }
        public int ActiveConnections { get; set; }
        public int ErrorCount { get; set; }
        public int RetryCount { get; set; }
        public int Http429Count { get; set; }
        public int Http5xxCount { get; set; }
        public int ConnectionResetCount { get; set; }
        public int TimeoutCount { get; set; }
        public int CompletedSegments { get; set; }
        public long BytesDownloaded { get; set; }
        public long RemainingBytes { get; set; }
    }

    public class AdaptiveConnectionController
    {
        private readonly object _lock = new();
        private readonly ConcurrentQueue<ConnectionTelemetrySample> _history = new();
        private readonly Stopwatch _cooldownStopwatch = new();

        private int _currentConnections;
        private readonly int _minConnections;
        private readonly int _maxConnections;
        private readonly TimeSpan _cooldownInterval = TimeSpan.FromMilliseconds(1500);

        public int CurrentConnections => _currentConnections;
        public int MinConnections => _minConnections;
        public int MaxConnections => _maxConnections;

        public AdaptiveConnectionController(int initialConnections = 8, int minConnections = 2, int maxConnections = 32)
        {
            _minConnections = Math.Max(1, minConnections);
            _maxConnections = Math.Min(64, Math.Max(_minConnections, maxConnections));
            _currentConnections = Math.Clamp(initialConnections, _minConnections, _maxConnections);
        }

        public void ResetCooldown()
        {
            lock (_lock)
            {
                _cooldownStopwatch.Reset();
            }
        }

        public void RecordTelemetry(
            double aggregateThroughputBps,
            double averageRttMs,
            int errorCount,
            int retryCount = 0,
            int http429Count = 0,
            int http5xxCount = 0,
            int timeoutCount = 0,
            long bytesDownloaded = 0,
            long remainingBytes = 0,
            int completedSegments = 0)
        {
            double rollingBps = aggregateThroughputBps;
            var sampleArray = _history.ToArray();
            if (sampleArray.Length > 0)
            {
                // Smooth rolling throughput using exponential moving average (alpha = 0.3)
                rollingBps = (0.3 * aggregateThroughputBps) + (0.7 * sampleArray.Last().RollingThroughputBps);
            }

            var sample = new ConnectionTelemetrySample
            {
                Timestamp = DateTime.UtcNow,
                AggregateThroughputBps = aggregateThroughputBps,
                RollingThroughputBps = rollingBps,
                AverageRttMs = averageRttMs,
                ServerResponseTimeMs = averageRttMs,
                ErrorCount = errorCount,
                RetryCount = retryCount,
                Http429Count = http429Count,
                Http5xxCount = http5xxCount,
                TimeoutCount = timeoutCount,
                ActiveConnections = _currentConnections,
                BytesDownloaded = bytesDownloaded,
                RemainingBytes = remainingBytes,
                CompletedSegments = completedSegments
            };

            _history.Enqueue(sample);
            while (_history.Count > 30)
            {
                _history.TryDequeue(out _);
            }
        }

        public int EvaluateConnectionCount(long totalFileSize, bool isMeteredNetwork, int? userLimit = null)
        {
            lock (_lock)
            {
                int effectiveMax = _maxConnections;
                if (userLimit.HasValue && userLimit.Value > 0)
                {
                    effectiveMax = Math.Min(effectiveMax, userLimit.Value);
                }

                // Small File Optimization Tiers:
                // 1. Tiny file (< 1 MB): 1 worker
                if (totalFileSize > 0 && totalFileSize < 1 * 1024 * 1024)
                {
                    _currentConnections = 1;
                    return _currentConnections;
                }

                // 2. Small file (1 MB - 5 MB): Cap at 4 workers
                if (totalFileSize > 0 && totalFileSize < 5 * 1024 * 1024)
                {
                    _currentConnections = Math.Min(_currentConnections, 4);
                    return _currentConnections;
                }

                // 3. Large file (5 MB - 50 MB): Cap at 16 workers
                if (totalFileSize > 0 && totalFileSize < 50 * 1024 * 1024)
                {
                    effectiveMax = Math.Min(effectiveMax, 16);
                }


                // Metered network optimization: Cap at 4 connections
                if (isMeteredNetwork)
                {
                    _currentConnections = Math.Min(_currentConnections, 4);
                    return _currentConnections;
                }

                // Hysteresis cooldown window (1.5s) to prevent rapid connection oscillation
                if (_cooldownStopwatch.IsRunning && _cooldownStopwatch.Elapsed < _cooldownInterval)
                {
                    return _currentConnections;
                }

                var samples = _history.ToArray();
                if (samples.Length == 0) return _currentConnections;

                var recent = samples.TakeLast(3).ToList();
                int totalErrors = recent.Sum(s => s.ErrorCount + s.Http429Count + s.Http5xxCount + s.TimeoutCount);

                // DECREASE POLICY: Immediately reduce concurrency on HTTP 429/5xx, timeouts, or retries
                if (totalErrors > 0)
                {
                    // Conservative decrement: reduce by 2 connections per step
                    _currentConnections = Math.Max(_minConnections, _currentConnections - 2);
                    _cooldownStopwatch.Restart();
                    LoggingService.Log($"[AdaptiveConnectionController] Backed off to {_currentConnections} connections due to server errors / rate limits.");
                    return _currentConnections;
                }

                if (samples.Length < 3) return _currentConnections;

                // Latency spike check (RTT rose > 50% over baseline)
                double avgRecentRtt = recent.Average(s => s.AverageRttMs);
                double baselineRtt = samples.First().AverageRttMs;
                if (baselineRtt > 0 && avgRecentRtt >= baselineRtt * 1.5 && _currentConnections > _minConnections)
                {
                    _currentConnections = Math.Max(_minConnections, _currentConnections - 1);
                    _cooldownStopwatch.Restart();
                    LoggingService.Log($"[AdaptiveConnectionController] Reduced to {_currentConnections} connections due to latency spike (RTT: {avgRecentRtt:F0}ms).");
                    return _currentConnections;
                }


                // INCREASE POLICY: Require 3 consecutive samples showing consistent throughput gain (> 15%)
                double latestBps = recent.Last().RollingThroughputBps;
                double prevBps = recent.First().RollingThroughputBps;

                if (prevBps > 0)
                {
                    double gain = (latestBps - prevBps) / prevBps;
                    if (gain > 0.15 && _currentConnections < effectiveMax)
                    {
                        // Conservative step increment: 4 -> 6 -> 8 -> 10 (+2 per step)
                        _currentConnections = Math.Min(effectiveMax, _currentConnections + 2);
                        _cooldownStopwatch.Restart();
                        LoggingService.Log($"[AdaptiveConnectionController] Scaled up to {_currentConnections} connections (Rolling Gain: {gain * 100:F1}%).");
                    }
                    else if (gain < -0.12 && _currentConnections > _minConnections)
                    {
                        // Scale down when throughput drops (> 12%)
                        _currentConnections = Math.Max(_minConnections, _currentConnections - 1);
                        _cooldownStopwatch.Restart();
                        LoggingService.Log($"[AdaptiveConnectionController] Reduced to {_currentConnections} connections due to throughput drop ({gain * 100:F1}%).");
                    }

                }

                return _currentConnections;
            }
        }
    }
}
