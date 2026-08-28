using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    /// <summary>
    /// Heuristic engine that determines an optimal number of connections to use for segmented downloads
    /// based on runtime measurements: latency, configured bandwidth limits, file size and server capabilities.
    /// It is intentionally conservative and non-invasive (avoids large probe downloads).
    /// </summary>
    public class AdaptiveConnectionManager
    {
        private readonly ISettingsService _settings;
        private readonly INetworkService _networkService;

        public AdaptiveConnectionManager(ISettingsService settings, INetworkService networkService)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
        }

        /// <summary>
        /// Decide connection count for a download.
        /// </summary>
        /// <param name="url">Absolute download url</param>
        /// <param name="fileSize">Total bytes if known</param>
        /// <param name="serverSupportsRange">Whether server supports Range requests</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Recommended number of parallel connections (1..16)</returns>
        public async Task<int> DetermineConnectionCountAsync(string url, long? fileSize, bool serverSupportsRange, CancellationToken ct)
        {
            try
            {
                if (!serverSupportsRange) return 1;

                // Respect a user override first (if set in settings)
                try
                {
                    int userOverride = _settings.GetConnectionLimitOverride();
                    if (userOverride > 0) return Math.Min(Math.Max(1, userOverride), 32);
                }
                catch (Exception ex) { LoggingService.LogException("[AdaptiveConnectionManager] Reading user override failed", ex); }

                // Default aggressive bounds — 32 max threads for maximum throughput
                const int minConns = 1;
                const int maxConns = 32;

                // Estimate bandwidth (bytes/sec)
                double bandwidthBps = 0;

                try
                {
                    int kbps = _settings.GetActiveBandwidthLimitKbps();  // Use active limit considering schedules
                    if (kbps > 0)
                    {
                        bandwidthBps = kbps * 1024.0;
                    }
                    else
                    {
                        // Map network type to a rough bandwidth estimate (aggressive upper-end)
                        var netType = _networkService.GetCurrentNetworkType();
                        bandwidthBps = netType switch
                        {
                            NetworkType.Ethernet => 200 * 1024 * 1024, // 200 Mbps
                            NetworkType.WiFi => 100 * 1024 * 1024,     // 100 Mbps
                            NetworkType.Vpn => 50 * 1024 * 1024,
                            NetworkType.MobileHotspot => 10 * 1024 * 1024,
                            NetworkType.Cellular => 5 * 1024 * 1024,
                            NetworkType.MeteredNetwork => 2 * 1024 * 1024,
                            _ => 50 * 1024 * 1024
                        };
                    }
                }
                catch (Exception ex) { LoggingService.LogException("[AdaptiveConnectionManager] Estimating bandwidth failed", ex); bandwidthBps = 50 * 1024 * 1024; }

                long rttMs = await GetHostLatencyMsAsync(url).ConfigureAwait(false);

                // Aggressive heuristic: lower per-connection target ~1 Mbps so we spawn more connections
                double perConnectionTarget = 1.0 * 1024 * 1024; // was 2 Mbps

                // Adjust per-connection target by latency: higher latency -> larger per-connection target
                if (rttMs > 150) perConnectionTarget *= 1.5; // high latency -> slightly fewer connections
                else if (rttMs < 30) perConnectionTarget *= 0.25; // very low latency -> spawn many connections

                int baseConns = (int)Math.Round(Math.Max(1.0, bandwidthBps / perConnectionTarget));

                // Factor file size: for very small files, prefer fewer connections to avoid overhead
                if (fileSize.HasValue)
                {
                    var mb = fileSize.Value / (1024.0 * 1024.0);
                    if (mb < 1.0)
                    {
                        baseConns = Math.Min(baseConns, 4);    // was 2
                    }
                    else if (mb < 5.0)
                    {
                        baseConns = Math.Min(baseConns, 16);   // was 8
                    }
                    else if (mb < 50.0)
                    {
                        baseConns = Math.Min(baseConns, 32);   // was 16
                    }
                    // Files >= 50MB: full 64 threads
                    else
                    {
                        baseConns = Math.Min(baseConns, 64);
                    }
                }

                // Penalize for metered networks or VPN
                try
                {
                    if (_networkService.IsMeteredNetwork() || _networkService.IsVpnActive())
                    {
                        baseConns = Math.Max(minConns, (int)Math.Ceiling(baseConns / 2.0));
                    }
                }
                catch (Exception ex) { LoggingService.LogException("[AdaptiveConnectionManager] Penalize metered/vpn check failed", ex); }

                // Latency soft clamp (more tolerant than before)
                if (rttMs > 500) baseConns = Math.Max(minConns, Math.Min(baseConns, 8));
                if (rttMs > 1000) baseConns = 1;

                // Server capability cache integration (check if throttling or rate-limiting is active for this domain)
                if (Uri.TryCreate(url, UriKind.Absolute, out var parsedUri) &&
                    ServerCapabilityCache.Instance.TryGet(parsedUri, out var serverCap))
                {
                    if (serverCap.IsThrottlingDetected)
                    {
                        baseConns = Math.Min(baseConns, serverCap.ConcurrencyCap);
                    }
                    if (!serverCap.SupportsRange)
                    {
                        return 1;
                    }
                }

                // Apply per-host connection budget sharing to prevent host starvation
                string host = new Uri(url).Host;
                int currentHostActiveCount = _hostActiveDownloads.TryGetValue(host, out var count) ? count : 1;
                if (currentHostActiveCount > 1)
                {
                    // Scale connection budget per download on the same host (max 16 total per host)
                    int budgetPerDownload = Math.Max(1, 16 / currentHostActiveCount);
                    baseConns = Math.Min(baseConns, budgetPerDownload);
                }

                // Global connection budget fairness (max 128 connections globally)
                int currentGlobalActive = Volatile.Read(ref _globalActiveConnections);
                int remainingGlobalBudget = Math.Max(1, MaxGlobalConnections - currentGlobalActive);
                baseConns = Math.Min(baseConns, remainingGlobalBudget);

                // Bound and return
                int final = Math.Clamp(baseConns, minConns, maxConns);
                return final;

            }
            catch
            {
                return 4; // default conservative
            }
        }

        private static int _globalActiveConnections = 0;
        public const int MaxGlobalConnections = 128;

        public static int GlobalActiveConnections => Volatile.Read(ref _globalActiveConnections);

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _hostActiveDownloads = new();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (long RttMs, DateTime ExpiresAt)> _pingCache = new();
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        public static void RegisterActiveHostDownload(string url)
        {
            try
            {
                Interlocked.Increment(ref _globalActiveConnections);
                var host = new Uri(url).Host;
                _hostActiveDownloads.AddOrUpdate(host, 1, (_, current) => current + 1);
            }
            catch { }
        }

        public static void UnregisterActiveHostDownload(string url)
        {
            try
            {
                Interlocked.Decrement(ref _globalActiveConnections);
                var host = new Uri(url).Host;
                _hostActiveDownloads.AddOrUpdate(host, 0, (_, current) => Math.Max(0, current - 1));
            }
            catch { }
        }

        private static async Task<long> GetHostLatencyMsAsync(string url)
        {
            try
            {
                var host = new Uri(url).Host;
                if (_pingCache.TryGetValue(host, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
                {
                    return cached.RttMs;
                }

                long rttMs = 200;
                using var p = new Ping();
                var reply = await p.SendPingAsync(host, 250).ConfigureAwait(false);
                if (reply != null && reply.Status == IPStatus.Success)
                {
                    rttMs = Math.Max(1, reply.RoundtripTime);
                }

                _pingCache[host] = (rttMs, DateTime.UtcNow.Add(CacheTtl));
                return rttMs;
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[AdaptiveConnectionManager] Ping/Roundtrip measurement failed", ex);
                return 200;
            }
        }
    }
}
