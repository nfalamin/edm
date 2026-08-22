using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    /// <summary>
    /// TokenBucketBandwidthLimiter — High-precision, non-blocking asynchronous token bucket rate limiter.
    /// Supports smooth bandwidth shaping, burst capacity, and zero busy-wait CPU loops.
    /// </summary>
    public sealed class TokenBucketBandwidthLimiter
    {
        private double _bytesPerSecond;
        private double _bucketCapacity;
        private double _currentTokens;
        private long _lastRefillTimestamp;
        private readonly object _lock = new();

        public double BytesPerSecond
        {
            get { lock (_lock) return _bytesPerSecond; }
            set
            {
                lock (_lock)
                {
                    RefillTokensUnsafe();
                    _bytesPerSecond = Math.Max(0, value);
                    _bucketCapacity = _bytesPerSecond > 0 ? Math.Max(256 * 1024, _bytesPerSecond * 0.5) : 0;
                    _currentTokens = Math.Min(_currentTokens, _bucketCapacity);
                }
            }
        }

        public TokenBucketBandwidthLimiter(double bytesPerSecond = 0, double? maxBurstBytes = null)
        {
            _bytesPerSecond = Math.Max(0, bytesPerSecond);
            _bucketCapacity = maxBurstBytes ?? (_bytesPerSecond > 0 ? Math.Max(256 * 1024, _bytesPerSecond * 0.5) : 1024 * 1024);
            _currentTokens = _bucketCapacity;
            _lastRefillTimestamp = Stopwatch.GetTimestamp();
        }

        private void RefillTokensUnsafe()
        {
            long now = Stopwatch.GetTimestamp();
            double elapsedSec = (now - _lastRefillTimestamp) / (double)Stopwatch.Frequency;

            if (elapsedSec > 0 && _bytesPerSecond > 0)
            {
                double newTokens = elapsedSec * _bytesPerSecond;
                _currentTokens = Math.Min(_bucketCapacity, _currentTokens + newTokens);
                _lastRefillTimestamp = now;
            }
            else if (_bytesPerSecond <= 0)
            {
                _currentTokens = _bucketCapacity;
                _lastRefillTimestamp = now;
            }
        }

        /// <summary>
        /// Consumes byte tokens and applies precise asynchronous delay if the bucket is exhausted.
        /// </summary>
        public async Task ThrottleAsync(int bytesToWrite, CancellationToken cancellationToken = default)
        {
            if (bytesToWrite <= 0) return;

            TimeSpan delay = TimeSpan.Zero;
            lock (_lock)
            {
                if (_bytesPerSecond <= 0) return; // Unlimited speed

                RefillTokensUnsafe();

                if (_currentTokens >= bytesToWrite)
                {
                    _currentTokens -= bytesToWrite;
                    return; // Served immediately from burst bucket
                }

                // Insufficient tokens: calculate required wait time
                double shortage = bytesToWrite - _currentTokens;
                _currentTokens = 0; // Bucket empty
                double waitSeconds = shortage / _bytesPerSecond;
                delay = TimeSpan.FromSeconds(Math.Min(5.0, waitSeconds));
            }

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
