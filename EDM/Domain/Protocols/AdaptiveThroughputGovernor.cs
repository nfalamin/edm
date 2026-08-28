using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Domain.Protocols
{
    /// <summary>
    /// High-performance, lock-free throughput governor.
    /// Monitors real-time transfer rates, calculates Exponential Moving Average (EMA) speeds,
    /// dynamically tunes active concurrency streams, and enforces per-download rate limits.
    /// </summary>
    public sealed class AdaptiveThroughputGovernor
    {
        private long _totalBytesDownloaded;
        private long _windowBytesDownloaded;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private long _lastTimestampTicks;

        private double _currentSpeed;
        private double _averageSpeed;
        private double _peakSpeed;
        private const double EmaAlpha = 0.25; // 25% current, 75% history smoothing factor

        private int _optimalConnectionCount;
        private readonly int _minConnections;
        private readonly int _maxConnections;
        private long? _rateLimitBytesPerSec;

        public AdaptiveThroughputGovernor(int initialConnections = 8, int minConnections = 2, int maxConnections = 32)
        {
            _minConnections = Math.Max(1, minConnections);
            _maxConnections = Math.Max(_minConnections, maxConnections);
            _optimalConnectionCount = Math.Clamp(initialConnections, _minConnections, _maxConnections);
            _lastTimestampTicks = _stopwatch.ElapsedTicks;
        }

        public void SetRateLimit(long? bytesPerSecond)
        {
            _rateLimitBytesPerSec = bytesPerSecond > 0 ? bytesPerSecond : null;
        }

        public void SetSpeedLimit(long bytesPerSecond) => SetRateLimit(bytesPerSecond);

        public void RecordBytes(long bytes)
        {
            if (bytes <= 0) return;
            Interlocked.Add(ref _totalBytesDownloaded, bytes);
            Interlocked.Add(ref _windowBytesDownloaded, bytes);
        }

        public long TotalBytes => Interlocked.Read(ref _totalBytesDownloaded);
        public int OptimalConnectionCount => Volatile.Read(ref _optimalConnectionCount);
        public double CurrentSpeed => Volatile.Read(ref _currentSpeed);
        public double AverageSpeed => Volatile.Read(ref _averageSpeed);
        public double PeakSpeed => Volatile.Read(ref _peakSpeed);

        /// <summary>
        /// Periodic evaluation hook (e.g. called every 200-500ms) to compute transfer metrics,
        /// smooth EMA speed, and dynamically scale connection concurrency.
        /// </summary>
        public EngineProgressReport SampleMetrics(long? totalContentLength, int currentActiveConnections, bool canResume, string? status)
        {
            long currentTicks = _stopwatch.ElapsedTicks;
            long elapsedTicks = currentTicks - Interlocked.Exchange(ref _lastTimestampTicks, currentTicks);
            double elapsedSeconds = (double)elapsedTicks / Stopwatch.Frequency;

            if (elapsedSeconds > 0.05)
            {
                long windowBytes = Interlocked.Exchange(ref _windowBytesDownloaded, 0);
                double instantaneousSpeed = windowBytes / elapsedSeconds;

                // Exponential Moving Average calculation for silky smooth UI graph
                double previousEma = _averageSpeed;
                double newEma = previousEma <= 0 
                    ? instantaneousSpeed 
                    : (EmaAlpha * instantaneousSpeed) + ((1.0 - EmaAlpha) * previousEma);

                Volatile.Write(ref _currentSpeed, instantaneousSpeed);
                Volatile.Write(ref _averageSpeed, newEma);

                if (instantaneousSpeed > _peakSpeed)
                {
                    Volatile.Write(ref _peakSpeed, instantaneousSpeed);
                }

                // Dynamic Concurrency Scaling (Auto-tuning segment count)
                AdjustOptimalConcurrency(instantaneousSpeed, previousEma);
            }

            return new EngineProgressReport
            {
                BytesReceived = TotalBytes,
                TotalBytes = totalContentLength,
                CurrentSpeedBytesPerSec = CurrentSpeed,
                AverageSpeedBytesPerSec = AverageSpeed,
                PeakSpeedBytesPerSec = PeakSpeed,
                ActiveConnections = currentActiveConnections,
                CanResume = canResume,
                StatusText = status
            };
        }

        private void AdjustOptimalConcurrency(double currentSpeed, double previousSpeed)
        {
            if (previousSpeed <= 0) return;

            // If speed improved by >15%, expand concurrency towards max connections
            if (currentSpeed > previousSpeed * 1.15 && _optimalConnectionCount < _maxConnections)
            {
                Interlocked.Increment(ref _optimalConnectionCount);
            }
            // If speed degraded by >25% and we have high connections, scale back to relieve server bottleneck
            else if (currentSpeed < previousSpeed * 0.75 && _optimalConnectionCount > _minConnections)
            {
                Interlocked.Decrement(ref _optimalConnectionCount);
            }
        }

        /// <summary>
        /// Lock-free speed limiter throttling check (synchronous fallback).
        /// </summary>
        public void ApplyRateLimiting(int bytesRead)
        {
            long? limit = _rateLimitBytesPerSec;
            if (!limit.HasValue || limit.Value <= 0 || bytesRead <= 0) return;

            double expectedSeconds = (double)bytesRead / limit.Value;
            int delayMs = (int)(expectedSeconds * 1000);
            if (delayMs > 0 && delayMs < 100)
            {
                Thread.Sleep(delayMs);
            }
        }

        /// <summary>
        /// Asynchronous non-blocking speed limiter throttling check.
        /// </summary>
        public async Task ApplyRateLimitingAsync(int bytesRead, CancellationToken cancellationToken = default)
        {
            long? limit = _rateLimitBytesPerSec;
            if (!limit.HasValue || limit.Value <= 0 || bytesRead <= 0) return;

            double expectedSeconds = (double)bytesRead / limit.Value;
            int delayMs = (int)(expectedSeconds * 1000);
            if (delayMs > 0 && delayMs < 100)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
