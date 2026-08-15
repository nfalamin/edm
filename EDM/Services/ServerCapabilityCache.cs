using System;
using System.Collections.Concurrent;
using System.Net;

namespace EDM.Services
{
    public class ServerCapability
    {
        public string HostKey { get; set; } = string.Empty;
        public bool SupportsRange { get; set; } = true;
        public string AcceptRangesHeader { get; set; } = "bytes";
        public System.Version HttpVersion { get; set; } = System.Net.HttpVersion.Version11;
        public string? ServerSoftware { get; set; }
        public bool IsThrottlingDetected { get; set; }
        public DateTime? LastRateLimitTime { get; set; }
        public int ConcurrencyCap { get; set; } = 32;
        public double AverageRttMs { get; set; } = 50.0;
        public double MovingThroughputBps { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public TimeSpan Ttl { get; set; } = TimeSpan.FromHours(1);

        public bool IsExpired => DateTime.UtcNow - LastUpdated > Ttl;
    }

    /// <summary>
    /// Thread-safe in-memory cache of remote server capabilities (Range support, HTTP version,
    /// throttling status, RTT estimates, and per-host concurrency budgets).
    /// </summary>
    public class ServerCapabilityCache
    {
        private static readonly Lazy<ServerCapabilityCache> _instance = new(() => new ServerCapabilityCache());
        public static ServerCapabilityCache Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, ServerCapability> _cache = new(StringComparer.OrdinalIgnoreCase);

        public ServerCapabilityCache() { }

        public static string GetHostKey(Uri uri)
        {
            if (uri == null) return string.Empty;
            return $"{uri.Scheme}://{uri.Host}:{uri.Port}".ToLowerInvariant();
        }

        public bool TryGet(Uri uri, out ServerCapability capability)
        {
            capability = null!;
            if (uri == null) return false;
            string key = GetHostKey(uri);
            if (_cache.TryGetValue(key, out var cached))
            {
                if (!cached.IsExpired)
                {
                    capability = cached;
                    return true;
                }
                _cache.TryRemove(key, out _);
            }
            return false;
        }

        public void Set(Uri uri, ServerCapability capability)
        {
            if (uri == null || capability == null) return;
            string key = GetHostKey(uri);
            capability.HostKey = key;
            capability.LastUpdated = DateTime.UtcNow;
            _cache[key] = capability;
        }

        public void RecordResponse(Uri uri, HttpStatusCode statusCode, double rttMs, double throughputBps, bool supportsRange = true)
        {
            if (uri == null) return;
            string key = GetHostKey(uri);
            var cap = _cache.GetOrAdd(key, k => new ServerCapability { HostKey = k });

            cap.LastUpdated = DateTime.UtcNow;
            cap.SupportsRange = supportsRange;

            if (rttMs > 0)
            {
                cap.AverageRttMs = cap.AverageRttMs > 0
                    ? (0.2 * rttMs) + (0.8 * cap.AverageRttMs)
                    : rttMs;
            }

            if (throughputBps > 0)
            {
                cap.MovingThroughputBps = cap.MovingThroughputBps > 0
                    ? (0.3 * throughputBps) + (0.7 * cap.MovingThroughputBps)
                    : throughputBps;
            }

            if (statusCode == (HttpStatusCode)429 || statusCode == HttpStatusCode.ServiceUnavailable)
            {
                cap.IsThrottlingDetected = true;
                cap.LastRateLimitTime = DateTime.UtcNow;
                cap.ConcurrencyCap = Math.Max(2, cap.ConcurrencyCap / 2);
            }
            else if (statusCode == HttpStatusCode.OK || statusCode == HttpStatusCode.PartialContent)
            {
                // If no rate limit for > 30s, gradually recover concurrency cap
                if (cap.IsThrottlingDetected && cap.LastRateLimitTime.HasValue &&
                    DateTime.UtcNow - cap.LastRateLimitTime.Value > TimeSpan.FromSeconds(30))
                {
                    cap.IsThrottlingDetected = false;
                    cap.ConcurrencyCap = Math.Min(32, cap.ConcurrencyCap + 2);
                }
            }
        }

        public void Clear()
        {
            _cache.Clear();
        }
    }
}
