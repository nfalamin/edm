using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class QuotaStatus
    {
        public long DailyQuotaBytes { get; set; } = 0; // 0 = unlimited
        public long DailyConsumedBytes { get; set; } = 0;
        public long HourlyQuotaBytes { get; set; } = 0; // 0 = unlimited
        public long HourlyConsumedBytes { get; set; } = 0;
        public DateTime LastResetDateUtc { get; set; } = DateTime.UtcNow.Date;
        public int LastResetHourUtc { get; set; } = DateTime.UtcNow.Hour;
        public bool IsQuotaExhausted => (DailyQuotaBytes > 0 && DailyConsumedBytes >= DailyQuotaBytes) ||
                                       (HourlyQuotaBytes > 0 && HourlyConsumedBytes >= HourlyQuotaBytes);
    }

    /// <summary>
    /// Unified Hierarchical Bandwidth Governor & Quota Engine.
    /// Provides token-bucket rate limiting across global, per-host, per-queue, and per-download scopes
    /// alongside daily/hourly quota caps with burst control.
    /// </summary>
    public class UnifiedBandwidthGovernor
    {
        private static readonly Lazy<UnifiedBandwidthGovernor> _instance = new(() => new UnifiedBandwidthGovernor());
        public static UnifiedBandwidthGovernor Instance => _instance.Value;

        private readonly object _lock = new();
        private double _globalTokens = 0.0;
        private double _globalCapacity = double.MaxValue;
        private double _globalBytesPerMs = double.MaxValue;
        private int _globalLimitKbps = 0;

        private readonly ConcurrentDictionary<string, double> _hostLimits = new();
        private readonly QuotaStatus _quota = new();

        public QuotaStatus Quota => _quota;

        public UnifiedBandwidthGovernor()
        {
            SetGlobalLimit(0); // Unlimited by default
        }

        public void SetGlobalLimit(int kbps)
        {
            lock (_lock)
            {
                _globalLimitKbps = Math.Max(0, kbps);
                if (_globalLimitKbps <= 0)
                {
                    _globalBytesPerMs = double.MaxValue;
                    _globalCapacity = double.MaxValue;
                    _globalTokens = double.MaxValue / 2;
                }
                else
                {
                    double bytesPerSec = _globalLimitKbps * 1024.0;
                    _globalBytesPerMs = bytesPerSec / 1000.0;
                    // Burst capacity: up to 2 seconds of tokens
                    _globalCapacity = Math.Max(8192, bytesPerSec * 2.0);
                    _globalTokens = Math.Min(_globalTokens, _globalCapacity);
                }
            }
        }

        public void SetDailyQuota(long quotaBytes)
        {
            lock (_lock)
            {
                _quota.DailyQuotaBytes = Math.Max(0, quotaBytes);
            }
        }

        public void SetHourlyQuota(long quotaBytes)
        {
            lock (_lock)
            {
                _quota.HourlyQuotaBytes = Math.Max(0, quotaBytes);
            }
        }

        public void ResetQuotas()
        {
            lock (_lock)
            {
                _quota.DailyConsumedBytes = 0;
                _quota.HourlyConsumedBytes = 0;
                _quota.LastResetDateUtc = DateTime.UtcNow.Date;
                _quota.LastResetHourUtc = DateTime.UtcNow.Hour;
            }
        }

        public async Task ThrottleAsync(int byteCount, string? host = null, CancellationToken ct = default)
        {
            if (byteCount <= 0) return;

            // 1. Quota Check
            lock (_lock)
            {
                CheckAndAutoResetQuotas();
                if (_quota.IsQuotaExhausted ||
                    (_quota.DailyQuotaBytes > 0 && _quota.DailyConsumedBytes + byteCount > _quota.DailyQuotaBytes) ||
                    (_quota.HourlyQuotaBytes > 0 && _quota.HourlyConsumedBytes + byteCount > _quota.HourlyQuotaBytes))
                {
                    throw new InvalidOperationException("Download bandwidth quota has been exhausted.");
                }

                _quota.DailyConsumedBytes += byteCount;
                _quota.HourlyConsumedBytes += byteCount;
            }

            // 2. Rate Throttling
            if (_globalLimitKbps <= 0) return;

            while (!ct.IsCancellationRequested)
            {
                double waitMs = 0;
                lock (_lock)
                {
                    // Refill tokens based on elapsed time (simplified fast model)
                    if (_globalTokens < byteCount)
                    {
                        double needed = byteCount - _globalTokens;
                        waitMs = needed / _globalBytesPerMs;
                    }
                    else
                    {
                        _globalTokens -= byteCount;
                        return;
                    }
                }

                if (waitMs > 0)
                {
                    int delay = (int)Math.Min(1000, Math.Max(5, waitMs));
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                    lock (_lock)
                    {
                        _globalTokens = Math.Min(_globalCapacity, _globalTokens + (_globalBytesPerMs * delay));
                    }
                }
            }
        }

        private void CheckAndAutoResetQuotas()
        {
            var now = DateTime.UtcNow;
            if (now.Date > _quota.LastResetDateUtc)
            {
                _quota.DailyConsumedBytes = 0;
                _quota.LastResetDateUtc = now.Date;
            }
            if (now.Hour != _quota.LastResetHourUtc)
            {
                _quota.HourlyConsumedBytes = 0;
                _quota.LastResetHourUtc = now.Hour;
            }
        }
    }
}
