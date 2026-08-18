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
        public int OptimalObservedConnections { get; set; } = 8;
        public double AverageRttMs { get; set; } = 50.0;
        public double MovingThroughputBps { get; set; }
        public double PeakThroughputBps { get; set; }
        public int SuccessfulDownloadsCount { get; set; }
        public int RateLimit429Count { get; set; }
        public double ConfidenceScore { get; set; } = 1.0; // 0.0 to 1.0
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public TimeSpan Ttl { get; set; } = TimeSpan.FromHours(2);

        public bool IsExpired => DateTime.UtcNow - LastUpdated > Ttl;
    }

    /// <summary>
    /// Thread-safe learning cache of remote server capabilities with confidence scoring,
    /// rate-limit memory, exponential decay, and intelligent initial connection estimation.
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

        /// <summary>
        /// Recommends an optimal starting connection count for a new download based on learned server capability and file size.
        /// </summary>
        public int GetRecommendedInitialConnections(Uri uri, long? fileSize = null, int userConfiguredMax = 32)
        {
            if (uri == null) return 4;

            if (TryGet(uri, out var cap))
            {
                // Decay confidence over time (10% decay per 30 minutes of inactivity)
                var elapsed = DateTime.UtcNow - cap.LastUpdated;
                double decayFactor = Math.Max(0.2, 1.0 - (elapsed.TotalMinutes / 300.0));
                double effectiveConfidence = cap.ConfidenceScore * decayFactor;

                if (!cap.SupportsRange) return 1;

                if (cap.IsThrottlingDetected)
                {
                    return Math.Clamp(cap.ConcurrencyCap, 1, Math.Min(4, userConfiguredMax));
                }

                if (effectiveConfidence >= 0.5 && cap.OptimalObservedConnections > 0)
                {
                    int recommended = Math.Clamp(cap.OptimalObservedConnections, 2, userConfiguredMax);
                    if (fileSize.HasValue && fileSize.Value < 5 * 1024 * 1024)
                    {
                        recommended = Math.Min(recommended, 2);
                    }
                    return recommended;
                }
            }

            // Default conservative start
            return Math.Min(4, userConfiguredMax);
        }

        public void RecordResponse(
            Uri uri,
            HttpStatusCode statusCode,
            double rttMs,
            double throughputBps,
            bool supportsRange = true,
            int activeConnections = 0)
        {
            if (uri == null) return;
            string key = GetHostKey(uri);
            var cap = _cache.GetOrAdd(key, k => new ServerCapability { HostKey = k });

            lock (cap)
            {
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

                    if (throughputBps > cap.PeakThroughputBps)
                    {
                        cap.PeakThroughputBps = throughputBps;
                        if (activeConnections > 0)
                        {
                            cap.OptimalObservedConnections = activeConnections;
                        }
                    }
                }

                if (statusCode == (HttpStatusCode)429 || statusCode == HttpStatusCode.ServiceUnavailable)
                {
                    cap.IsThrottlingDetected = true;
                    cap.LastRateLimitTime = DateTime.UtcNow;
                    cap.RateLimit429Count++;
                    cap.ConcurrencyCap = Math.Max(2, (activeConnections > 0 ? activeConnections : cap.ConcurrencyCap) / 2);
                    cap.ConfidenceScore = Math.Min(1.0, cap.ConfidenceScore + 0.2);
                }
                else if (statusCode == HttpStatusCode.OK || statusCode == HttpStatusCode.PartialContent)
                {
                    cap.SuccessfulDownloadsCount++;
                    cap.ConfidenceScore = Math.Min(1.0, cap.ConfidenceScore + 0.1);

                    // If no rate limit for > 30s, gradually recover concurrency cap
                    if (cap.IsThrottlingDetected && cap.LastRateLimitTime.HasValue &&
                        DateTime.UtcNow - cap.LastRateLimitTime.Value > TimeSpan.FromSeconds(30))
                    {
                        cap.IsThrottlingDetected = false;
                        cap.ConcurrencyCap = Math.Min(32, cap.ConcurrencyCap + 2);
                    }
                }
            }
        }

        public void Clear()
        {
            _cache.Clear();
        }
    }
}
