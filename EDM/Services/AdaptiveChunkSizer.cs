using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    /// <summary>
    /// Determines a dynamic chunk size for segmented downloads based on server, network, RAM and disk conditions.
    /// The result is a suggested per-segment chunk size in bytes (aligned to 64KB).
    /// </summary>
    public class AdaptiveChunkSizer
    {
        private readonly ISettingsService _settings;
        private readonly INetworkService _networkService;

        public AdaptiveChunkSizer(ISettingsService settings, INetworkService networkService)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
        }

        public async Task<long> DetermineChunkSizeAsync(string url, long fileSize, int segments, CancellationToken ct)
        {
            try
            {
                // Basic bounds
                const long minChunk = 64 * 1024; // 64 KB
                const long maxChunk = 256 * 1024 * 1024; // 256 MB

                // Estimate bandwidth (bytes/sec)
                double bandwidthBps = 5.0 * 1024 * 1024; // default 5 Mbps
                try
                {
                    int kbps = _settings.GetActiveBandwidthLimitKbps();  // Use active limit considering schedules
                    if (kbps > 0) bandwidthBps = kbps * 1024.0;
                    else
                    {
                        var netType = _networkService.GetCurrentNetworkType();
                        bandwidthBps = netType switch
                        {
                            NetworkType.Ethernet => 50 * 1024 * 1024,
                            NetworkType.WiFi => 20 * 1024 * 1024,
                            NetworkType.Vpn => 10 * 1024 * 1024,
                            NetworkType.MobileHotspot => 2 * 1024 * 1024,
                            NetworkType.Cellular => 1 * 1024 * 1024,
                            NetworkType.MeteredNetwork => 1 * 1024 * 1024,
                            _ => 5 * 1024 * 1024
                        };
                    }
                }
                catch { }

                // target time window per chunk: chunks should represent roughly 4-12 seconds of data
                double targetSeconds = 8.0;

                // adjust targetSeconds for small files or low segments
                if (fileSize < 5L * 1024 * 1024) targetSeconds = 3.0;
                if (segments <= 2) targetSeconds = Math.Max(4.0, targetSeconds);

                double perChunk = bandwidthBps * targetSeconds;

                // If file is small, reduce chunk size to avoid overhead
                double fileMb = fileSize / (1024.0 * 1024.0);
                if (fileMb < 1.0)
                {
                    perChunk = Math.Min(perChunk, 512 * 1024.0); // 512 KB
                }
                else if (fileMb < 10.0)
                {
                    perChunk = Math.Min(perChunk, 2 * 1024 * 1024.0); // 2 MB
                }

                // Consider available memory: use GC info if available
                try
                {
                    var ginfo = System.GC.GetGCMemoryInfo();
                    long avail = (long)ginfo.TotalAvailableMemoryBytes;
                    // Reserve some memory for other tasks; allow chunks to occupy up to 25% of available memory per segment group
                    double maxPerChunkByRam = Math.Max(minChunk, avail * 0.25 / Math.Max(1, segments));
                    perChunk = Math.Min(perChunk, maxPerChunkByRam);
                }
                catch { }

                // Consider disk free space on target drive: if low, reduce chunk size
                try
                {
                    var root = Path.GetPathRoot(Directory.GetCurrentDirectory()) ?? Path.GetPathRoot(Path.GetTempPath());
                    if (!string.IsNullOrEmpty(root))
                    {
                        var di = new DriveInfo(root);
                        long free = di.AvailableFreeSpace;
                        // don't allow chunks that would consume more than 10% of free space per segment
                        double maxPerChunkByDisk = Math.Max(minChunk, free * 0.10 / Math.Max(1, segments));
                        perChunk = Math.Min(perChunk, maxPerChunkByDisk);
                    }
                }
                catch { }

                // Latency penalty: ping host quickly
                try
                {
                    var host = new Uri(url).Host;
                    using var p = new Ping();
                    var reply = await p.SendPingAsync(host, 250).ConfigureAwait(false);
                    if (reply != null && reply.Status == IPStatus.Success)
                    {
                        if (reply.RoundtripTime > 300) perChunk = Math.Min(perChunk, 512 * 1024.0);
                        else if (reply.RoundtripTime > 150) perChunk = Math.Min(perChunk, 1024 * 1024.0);
                    }
                }
                catch { }

                // Bound and align to 64KB
                long suggested = (long)Math.Round(Math.Min(maxChunk, Math.Max(minChunk, perChunk)));
                const long align = 64 * 1024;
                suggested = Math.Max(minChunk, (suggested / align) * align);
                if (suggested <= 0) suggested = minChunk;

                return suggested;
            }
            catch
            {
                return 4 * 1024 * 1024; // 4 MB fallback — was 256 KB, too small for fast connections
            }
        }
    }
}
