using System;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    /// <summary>
    /// A process-wide token-bucket bandwidth throttler.
    /// Call SetLimit(kbps) to set global bandwidth limit (0 = unlimited).
    /// Call ThrottleAsync(bytes, ct) to asynchronously wait until the requested
    /// number of bytes can be consumed without exceeding the configured rate.
    ///
    /// Designed to be lightweight and avoid high CPU usage by using Task.Delay
    /// for waiting and a modest refill interval (200ms) for smooth behavior.
    /// </summary>
    public sealed class BandwidthThrottler
    {
        private static readonly BandwidthThrottler _instance = new BandwidthThrottler();
        public static BandwidthThrottler Instance => _instance;

        private readonly object _lock = new object();
        private double _tokens = 0.0; // available bytes
        private double _capacity = double.MaxValue; // capacity in bytes
        private double _bytesPerMs = double.MaxValue; // refill rate in bytes per ms
        private int _limitKbps = 0;
        private System.Threading.Timer? _refillTimer;
        private System.Threading.Timer? _metricsTimer;
        private const int RefillIntervalMs = 200; // 200 ms refill for smoothness
        private const int MetricsIntervalMs = 1000; // 1s metrics
        private long _consumedSinceLastReport = 0;

        public int LimitKbps => _limitKbps;
        public bool IsLimitEnabled => _limitKbps > 0;
        public int LastConfiguredLimitKbps { get; private set; } = 500;
        public event Action<int>? LimitChanged;

        private BandwidthThrottler()
        {
            // start with unlimited
            SetLimit(0);
        }

        public void ToggleLimit()
        {
            if (_limitKbps > 0)
            {
                SetLimit(0);
            }
            else
            {
                SetLimit(LastConfiguredLimitKbps > 0 ? LastConfiguredLimitKbps : 500);
            }
        }

        public void SetLimit(int kbps)
        {
            int updatedLimit;
            lock (_lock)
            {
                _limitKbps = Math.Max(0, kbps);
                updatedLimit = _limitKbps;
                if (_limitKbps > 0)
                {
                    LastConfiguredLimitKbps = _limitKbps;
                }

                if (_limitKbps <= 0)
                {
                    // Unlimited
                    _bytesPerMs = double.MaxValue;
                    _capacity = double.MaxValue;
                    _tokens = double.MaxValue / 2; // large
                    _refillTimer?.Dispose();
                    _refillTimer = null;

                    _metricsTimer?.Dispose();
                    _metricsTimer = null;
                }
                else
                {
                    double bytesPerSec = _limitKbps * 1024.0;
                    _bytesPerMs = bytesPerSec / 1000.0;
                    // capacity: allow bursting up to 2 seconds worth
                    _capacity = Math.Max(4096, bytesPerSec * 2);
                    if (_tokens > _capacity) _tokens = _capacity;

                    if (_refillTimer == null)
                    {
                        _refillTimer = new System.Threading.Timer(RefillCallback, null, RefillIntervalMs, RefillIntervalMs);
                    }

                    if (_metricsTimer == null)
                    {
                        _metricsTimer = new System.Threading.Timer(MetricsCallback, null, MetricsIntervalMs, MetricsIntervalMs);
                    }
                }
            }

            LoggingService.Log($"[BandwidthThrottler] Set limit: {updatedLimit} KB/s");
            try
            {
                LimitChanged?.Invoke(updatedLimit);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[BandwidthThrottler] LimitChanged subscriber failed", ex);
            }
        }

        private void RefillCallback(object? state)
        {
            lock (_lock)
            {
                if (_limitKbps <= 0) return;
                double add = _bytesPerMs * RefillIntervalMs;
                _tokens = Math.Min(_capacity, _tokens + add);
            }
        }

        private void MetricsCallback(object? state)
        {
            try
            {
                double tokens;
                double bytesPerSecConfig;
                int limitKbpsLocal;
                // atomically grab consumed and reset
                long consumed = System.Threading.Interlocked.Exchange(ref _consumedSinceLastReport, 0);

                lock (_lock)
                {
                    tokens = _tokens;
                    limitKbpsLocal = _limitKbps;
                    bytesPerSecConfig = (_bytesPerMs == double.MaxValue) ? double.PositiveInfinity : _bytesPerMs * 1000.0;
                }

                double consumedKbps = consumed / 1024.0;
                string cfg = double.IsInfinity(bytesPerSecConfig) ? "unlimited" : $"{bytesPerSecConfig:F0} B/s";
                LoggingService.Log($"[BandwidthThrottler Metrics] Limit={limitKbpsLocal} KB/s, Tokens={tokens:F0} bytes, ConfigRate={cfg}, ConsumedLastSec={consumed} B (~{consumedKbps:F1} KB/s)");
            }
            catch (Exception ex)
            {
                // Swallow errors to avoid timer termination
                LoggingService.LogException("[BandwidthThrottler] MetricsCallback failed", ex);
            }
        }

        /// <summary>
        /// Asynchronously waits until 'bytes' can be consumed under the configured limit.
        /// If limit is 0 (unlimited) returns immediately.
        /// </summary>
        public async Task ThrottleAsync(int bytes, CancellationToken ct)
        {
            if (bytes <= 0) return;
            // Fast-path for unlimited
            lock (_lock)
            {
                if (_limitKbps <= 0) return;
                // If enough tokens, consume and return
                if (_tokens >= bytes)
                {
                    _tokens -= bytes;
                    System.Threading.Interlocked.Add(ref _consumedSinceLastReport, bytes);
                    return;
                }
            }

            // Otherwise wait in a loop until tokens available or cancelled
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                double need;
                double tokensNow;
                double bytesPerMsLocal;
                lock (_lock)
                {
                    tokensNow = _tokens;
                    bytesPerMsLocal = _bytesPerMs;
                    if (tokensNow >= bytes)
                    {
                        _tokens -= bytes;
                        System.Threading.Interlocked.Add(ref _consumedSinceLastReport, bytes);
                        return;
                    }
                    need = bytes - tokensNow;
                }

                // compute estimated wait ms required
                if (bytesPerMsLocal <= 0 || double.IsInfinity(bytesPerMsLocal) || double.IsNaN(bytesPerMsLocal))
                {
                    await Task.Delay(100, ct).ConfigureAwait(false);
                    continue;
                }

                double waitMs = need / bytesPerMsLocal;
                // Bound wait to refill interval increments for responsiveness
                int delay = (int)Math.Max(1, Math.Min(waitMs, RefillIntervalMs));

                try { await Task.Delay(delay, ct).ConfigureAwait(false); } catch (OperationCanceledException) { throw; } catch { }

                // Loop and try consume again
            }
        }
    }
}
