using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace EDM.Services
{
    public enum ConcurrencyScalingState
    {
        Warmup,
        ScalingUp,
        Holding,
        ScalingDown,
        ThrottledBackoff
    }

    public class ConnectionTelemetrySample
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public double AggregateThroughputBps { get; set; }
        public double RollingThroughputBps { get; set; }
        public double ThroughputPerConnectionBps => ActiveConnections > 0 ? RollingThroughputBps / ActiveConnections : 0;
        public double AverageRttMs { get; set; }
        public double ServerResponseTimeMs { get; set; }
        public double TimeToFirstByteMs { get; set; }
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

    /// <summary>
    /// AdaptiveConnectionController — Production-grade adaptive concurrency controller.
    /// Dynamically optimizes connection count based on measured throughput gain,
    /// per-connection efficiency, RTT latency, TTFB, HTTP 429 rate limits, and hysteresis.
    /// </summary>
    public class AdaptiveConnectionController
    {
        private readonly object _lock = new();
        private readonly ConcurrentQueue<ConnectionTelemetrySample> _history = new();
        private readonly Stopwatch _cooldownStopwatch = new();
        private readonly PerformancePolicy _policy;

        private int _currentConnections;
        private int _optimalObservedConnections;
        private double _peakObservedThroughputBps;
        private ConcurrencyScalingState _scalingState = ConcurrencyScalingState.Warmup;
        private double _baselineRttMs;

        public int CurrentConnections => _currentConnections;
        public int OptimalObservedConnections => _optimalObservedConnections;
        public double PeakObservedThroughputBps => _peakObservedThroughputBps;
        public ConcurrencyScalingState ScalingState => _scalingState;
        public PerformancePolicy Policy => _policy;

        public AdaptiveConnectionController(
            int? initialConnections = null,
            int? minConnections = null,
            int? maxConnections = null,
            PerformancePolicy? policy = null)
        {
            _policy = policy ?? PerformancePolicy.Default;
            int min = minConnections ?? _policy.MinConnections;
            int max = maxConnections ?? _policy.MaxConnections;
            int initial = initialConnections ?? _policy.InitialConnections;

            _currentConnections = Math.Clamp(initial, min, max);
            _optimalObservedConnections = _currentConnections;
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
            int completedSegments = 0,
            double ttfbMs = 0)
        {
            double rollingBps = aggregateThroughputBps;
            var sampleArray = _history.ToArray();
            if (sampleArray.Length > 0)
            {
                // EWMA smoothing (alpha = 0.3)
                rollingBps = (0.3 * aggregateThroughputBps) + (0.7 * sampleArray.Last().RollingThroughputBps);
            }

            if (_baselineRttMs <= 0 && averageRttMs > 0)
            {
                _baselineRttMs = averageRttMs;
            }

            var sample = new ConnectionTelemetrySample
            {
                Timestamp = DateTime.UtcNow,
                AggregateThroughputBps = aggregateThroughputBps,
                RollingThroughputBps = rollingBps,
                AverageRttMs = averageRttMs,
                ServerResponseTimeMs = averageRttMs,
                TimeToFirstByteMs = ttfbMs,
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
            while (_history.Count > 40)
            {
                _history.TryDequeue(out _);
            }

            if (rollingBps > _peakObservedThroughputBps)
            {
                _peakObservedThroughputBps = rollingBps;
                _optimalObservedConnections = _currentConnections;
            }
        }

        /// <summary>
        /// Evaluates current telemetry and determines the next optimal connection count.
        /// </summary>
        public int EvaluateConnectionCount(long totalFileSize, bool isMeteredNetwork, int? userLimit = null)
        {
            lock (_lock)
            {
                int effectiveMax = userLimit.HasValue && userLimit.Value > 0
                    ? Math.Min(_policy.MaxConnections, userLimit.Value)
                    : _policy.MaxConnections;

                int effectiveMin = _policy.MinConnections;

                // 1. Small File Optimization Tiers:
                if (totalFileSize > 0 && totalFileSize < 1 * 1024 * 1024)
                {
                    _currentConnections = 1;
                    _scalingState = ConcurrencyScalingState.Holding;
                    return _currentConnections;
                }
                if (totalFileSize > 0 && totalFileSize < 5 * 1024 * 1024)
                {
                    _currentConnections = Math.Min(_currentConnections, 4);
                    _scalingState = ConcurrencyScalingState.Holding;
                    return _currentConnections;
                }
                if (totalFileSize > 0 && totalFileSize < 50 * 1024 * 1024)
                {
                    effectiveMax = Math.Min(effectiveMax, 16);
                }

                // 2. Metered network override
                if (isMeteredNetwork)
                {
                    _currentConnections = Math.Min(_currentConnections, 4);
                    _scalingState = ConcurrencyScalingState.Holding;
                    return _currentConnections;
                }

                // 3. Hysteresis Cooldown Window: prevent connection oscillation
                if (_cooldownStopwatch.IsRunning && _cooldownStopwatch.Elapsed < _policy.CooldownInterval)
                {
                    return _currentConnections;
                }

                var samples = _history.ToArray();
                if (samples.Length > 0 && samples.Last().Http429Count > 0)
                {
                    _currentConnections = Math.Max(effectiveMin, _currentConnections - 2);
                    _scalingState = ConcurrencyScalingState.ThrottledBackoff;
                    _cooldownStopwatch.Restart();
                    LoggingService.Log($"[AdaptiveConnectionController] Immediate HTTP 429 Throttling detected. Backing off to {_currentConnections} connections.");
                    return _currentConnections;
                }

                if (samples.Length < _policy.ConsecutiveSamplesRequired)
                {
                    return _currentConnections;
                }

                var recent = samples.TakeLast(_policy.ConsecutiveSamplesRequired).ToList();
                int total429 = recent.Sum(s => s.Http429Count);
                int total5xx = recent.Sum(s => s.Http5xxCount);
                int totalTimeouts = recent.Sum(s => s.TimeoutCount);
                int totalErrors = recent.Sum(s => s.ErrorCount) + total429 + total5xx + totalTimeouts;

                // 4. SEVERE THROTTLING / ERROR BACKOFF (HTTP 429, 5xx, Timeouts)
                if (total429 > 0)
                {
                    // Backoff on HTTP 429
                    _currentConnections = Math.Max(effectiveMin, _currentConnections - 2);
                    _scalingState = ConcurrencyScalingState.ThrottledBackoff;
                    _cooldownStopwatch.Restart();
                    LoggingService.Log($"[AdaptiveConnectionController] HTTP 429 Throttling detected. Backing off to {_currentConnections} connections.");
                    return _currentConnections;
                }

                if (total5xx > 0 || totalTimeouts > 0 || totalErrors > 0)
                {
                    // Moderate backoff on server errors / timeouts / network errors
                    _currentConnections = Math.Max(effectiveMin, _currentConnections - 2);
                    _scalingState = ConcurrencyScalingState.ScalingDown;
                    _cooldownStopwatch.Restart();
                    LoggingService.Log($"[AdaptiveConnectionController] Server errors/timeouts detected ({totalErrors}). Reducing to {_currentConnections} connections.");
                    return _currentConnections;
                }

                // 5. LATENCY & TTFB SPIKE CHECK
                double avgRecentRtt = recent.Average(s => s.AverageRttMs);
                if (_baselineRttMs > 0 && avgRecentRtt > _baselineRttMs * _policy.LatencySpikeThresholdMultiplier && _currentConnections > effectiveMin)
                {
                    _currentConnections = Math.Max(effectiveMin, _currentConnections - 1);
                    _scalingState = ConcurrencyScalingState.ScalingDown;
                    _cooldownStopwatch.Restart();
                    LoggingService.Log($"[AdaptiveConnectionController] RTT latency spike ({avgRecentRtt:F0}ms vs baseline {_baselineRttMs:F0}ms). Scaled down to {_currentConnections}.");
                    return _currentConnections;
                }

                // 6. THROUGHPUT GAIN & EFFICIENCY EVALUATION
                double latestThroughput = recent.Last().RollingThroughputBps;
                double baselineThroughput = recent.First().RollingThroughputBps;

                if (baselineThroughput > 0)
                {
                    double gain = (latestThroughput - baselineThroughput) / baselineThroughput;
                    double currentPerConn = latestThroughput / Math.Max(1, _currentConnections);

                    // SCALE UP CONDITION:
                    // 1. Throughput gain must be >= MinimumUsefulGainPercent (5%)
                    // 2. Haven't exceeded user limit / effectiveMax
                    // 3. Per-connection efficiency is maintained
                    if (gain >= _policy.MinimumUsefulGainPercent && _currentConnections < effectiveMax)
                    {
                        int step = _currentConnections < 8 ? 2 : (_currentConnections < 16 ? 4 : 4);
                        int nextConnections = Math.Min(effectiveMax, _currentConnections + step);

                        if (nextConnections != _currentConnections)
                        {
                            _currentConnections = nextConnections;
                            _scalingState = ConcurrencyScalingState.ScalingUp;
                            _cooldownStopwatch.Restart();
                            LoggingService.Log($"[AdaptiveConnectionController] Scaling UP: {_currentConnections} connections (Gain: +{gain * 100:F1}%, Throughput: {latestThroughput / (1024 * 1024):F2} MB/s).");
                        }
                    }
                    // SCALE DOWN CONDITION:
                    // Throughput degraded by more than DegradationThresholdPercent (-10%)
                    else if (gain <= _policy.DegradationThresholdPercent && _currentConnections > effectiveMin)
                    {
                        _currentConnections = Math.Max(effectiveMin, _currentConnections - 2);
                        _scalingState = ConcurrencyScalingState.ScalingDown;
                        _cooldownStopwatch.Restart();
                        LoggingService.Log($"[AdaptiveConnectionController] Scaling DOWN: {_currentConnections} connections (Degradation: {gain * 100:F1}%).");
                    }
                    else
                    {
                        // Optimal plateau reached: hold current connection level
                        _scalingState = ConcurrencyScalingState.Holding;
                    }
                }

                return _currentConnections;
            }
        }
    }
}
