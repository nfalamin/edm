using System;
using System.Diagnostics;
using System.Threading;

namespace EDM.Services
{
    /// <summary>
    /// MonotonicSpeedTracker — High-precision, zero-allocation byte throughput calculator
    /// powered by Stopwatch monotonic timestamps. Calculates Instantaneous, Rolling (EWMA),
    /// Average, and Peak throughput metrics with zero wall-clock jitter.
    /// </summary>
    public sealed class MonotonicSpeedTracker
    {
        private readonly long _startTimestamp;
        private long _lastSampleTimestamp;
        private long _lastSampleBytes;
        private long _totalBytes;

        private double _instantaneousSpeedBps;
        private double _rollingSpeedBps;
        private double _peakSpeedBps;
        private readonly double _ewmaAlpha;
        private readonly object _syncLock = new();

        public MonotonicSpeedTracker(double ewmaAlpha = 0.25)
        {
            _startTimestamp = Stopwatch.GetTimestamp();
            _lastSampleTimestamp = _startTimestamp;
            _lastSampleBytes = 0;
            _totalBytes = 0;
            _ewmaAlpha = Math.Clamp(ewmaAlpha, 0.05, 0.95);
        }

        /// <summary>Total bytes recorded so far.</summary>
        public long TotalBytes => Volatile.Read(ref _totalBytes);

        /// <summary>Instantaneous throughput in bytes/second from the latest sampling window.</summary>
        public double InstantaneousSpeedBps
        {
            get { lock (_syncLock) return _instantaneousSpeedBps; }
        }

        /// <summary>Smoothed rolling throughput in bytes/second (EWMA).</summary>
        public double RollingSpeedBps
        {
            get { lock (_syncLock) return _rollingSpeedBps; }
        }

        /// <summary>Overall average throughput in bytes/second from start to current time.</summary>
        public double AverageSpeedBps
        {
            get
            {
                long current = Stopwatch.GetTimestamp();
                double elapsedSec = Math.Max(0.000001, (current - _startTimestamp) / (double)Stopwatch.Frequency);
                return Volatile.Read(ref _totalBytes) / elapsedSec;
            }
        }

        /// <summary>Peak observed rolling throughput in bytes/second.</summary>
        public double PeakSpeedBps
        {
            get { lock (_syncLock) return _peakSpeedBps; }
        }

        /// <summary>Total elapsed time in seconds using monotonic clock.</summary>
        public double ElapsedSeconds
        {
            get
            {
                long current = Stopwatch.GetTimestamp();
                return Math.Max(0.0001, (current - _startTimestamp) / (double)Stopwatch.Frequency);
            }
        }

        /// <summary>
        /// Records newly downloaded bytes and recalculates speed metrics.
        /// </summary>
        /// <param name="absoluteTotalBytes">Current cumulative bytes downloaded.</param>
        public void RecordProgress(long absoluteTotalBytes)
        {
            long now = Stopwatch.GetTimestamp();
            lock (_syncLock)
            {
                Interlocked.Exchange(ref _totalBytes, absoluteTotalBytes);
                double elapsedSec = (now - _lastSampleTimestamp) / (double)Stopwatch.Frequency;

                // Minimum 50ms interval between speed calculations to prevent divide-by-zero or spike anomalies
                if (elapsedSec >= 0.05)
                {
                    long bytesDelta = absoluteTotalBytes - _lastSampleBytes;
                    if (bytesDelta >= 0)
                    {
                        double instBps = bytesDelta / elapsedSec;
                        _instantaneousSpeedBps = instBps;

                        if (_rollingSpeedBps <= 0)
                        {
                            _rollingSpeedBps = instBps;
                        }
                        else
                        {
                            _rollingSpeedBps = (_ewmaAlpha * instBps) + ((1.0 - _ewmaAlpha) * _rollingSpeedBps);
                        }

                        if (_rollingSpeedBps > _peakSpeedBps)
                        {
                            _peakSpeedBps = _rollingSpeedBps;
                        }
                    }

                    _lastSampleTimestamp = now;
                    _lastSampleBytes = absoluteTotalBytes;
                }
            }
        }

        /// <summary>
        /// Resets the speed tracking baseline.
        /// </summary>
        public void Reset(long initialBytes = 0)
        {
            lock (_syncLock)
            {
                long now = Stopwatch.GetTimestamp();
                _lastSampleTimestamp = now;
                _lastSampleBytes = initialBytes;
                Interlocked.Exchange(ref _totalBytes, initialBytes);
                _instantaneousSpeedBps = 0;
                _rollingSpeedBps = 0;
                _peakSpeedBps = 0;
            }
        }
    }
}
