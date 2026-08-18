using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Services
{
    public record WebsiteEventDto(
        string EventType,
        string SessionId,
        string PagePath,
        string? PageTitle = null,
        string? Referrer = null,
        string? OperatingSystem = null,
        string? Browser = null,
        string? DeviceCategory = null,
        string? ReleaseVersion = null);

    public record WebsiteAnalyticsSummary(
        string Range,
        int TotalVisitors,
        int UniqueVisitors,
        int TotalPageviews,
        decimal DownloadConversionRate,
        List<PageStat> PopularPages,
        List<GeoStat> Countries,
        List<CategoryStat> Devices,
        List<CategoryStat> OperatingSystems,
        List<TimelineStat> Timeline);

    public record DownloadAnalyticsOverview(
        string Range,
        int TotalDownloads,
        int TodayDownloads,
        int SevenDaysDownloads,
        int ThirtyDaysDownloads,
        List<VersionStat> ByVersion,
        List<GeoStat> ByCountry,
        List<CategoryStat> ByOperatingSystem,
        List<CategoryStat> ByDevice,
        List<StatusStat> ByStatus,
        List<TimelineStat> Timeline);

    public record PageStat(string Path, string? Title, int Views, decimal Percentage);
    public record GeoStat(string CountryCode, string CountryName, int Count, decimal Percentage);
    public record CategoryStat(string Category, int Count, decimal Percentage);
    public record VersionStat(string Version, int Count, decimal Percentage);
    public record StatusStat(string Status, int Count, decimal Percentage);
    public record TimelineStat(string Date, int Count);

    public interface IAnalyticsService
    {
        Task<WebsiteEvent> RecordWebsiteEventAsync(WebsiteEventDto dto, string? clientIp = null, string? userAgent = null, string? countryCode = null);
        Task<WebsiteAnalyticsSummary> GetWebsiteAnalyticsSummaryAsync(string range = "7d");
        Task<DownloadAnalyticsOverview> GetDownloadAnalyticsOverviewAsync(string range = "30d");
    }

    public class AnalyticsService : IAnalyticsService
    {
        private readonly ControlPlaneDbContext _dbContext;

        public AnalyticsService(ControlPlaneDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<WebsiteEvent> RecordWebsiteEventAsync(WebsiteEventDto dto, string? clientIp = null, string? userAgent = null, string? countryCode = null)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            string eventType = string.IsNullOrWhiteSpace(dto.EventType) ? "pageview" : dto.EventType.ToLowerInvariant();
            string sessionId = string.IsNullOrWhiteSpace(dto.SessionId) ? Guid.NewGuid().ToString("N") : dto.SessionId;

            // Anti-inflation: Rate-limit duplicate download_started events within 5 seconds for the same session
            if (eventType == "download_started")
            {
                var window = DateTime.UtcNow.AddSeconds(-5);
                var existing = await _dbContext.WebsiteEvents
                    .Where(e => e.EventType == "download_started" && e.SessionId == sessionId && e.TimestampUtc >= window)
                    .OrderByDescending(e => e.TimestampUtc)
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    return existing; // Ignore duplicate click inflation
                }
            }

            string coarseIp = AnonymizeIp(clientIp);
            var (os, browser, device) = ParseUserAgent(userAgent, dto.OperatingSystem, dto.Browser, dto.DeviceCategory);

            var evt = new WebsiteEvent
            {
                Id = Guid.NewGuid(),
                EventType = eventType,
                SessionId = sessionId,
                PagePath = string.IsNullOrWhiteSpace(dto.PagePath) ? "/" : dto.PagePath,
                PageTitle = dto.PageTitle,
                Referrer = SanitizeReferrer(dto.Referrer),
                OperatingSystem = os,
                Browser = browser,
                DeviceCategory = device,
                CountryCode = string.IsNullOrWhiteSpace(countryCode) ? "US" : countryCode.ToUpperInvariant(),
                ReleaseVersion = dto.ReleaseVersion,
                UserAgent = userAgent != null && userAgent.Length > 200 ? userAgent.Substring(0, 200) : userAgent,
                ClientIpCoarse = coarseIp,
                TimestampUtc = DateTime.UtcNow
            };

            _dbContext.WebsiteEvents.Add(evt);
            await _dbContext.SaveChangesAsync();
            return evt;
        }

        public async Task<WebsiteAnalyticsSummary> GetWebsiteAnalyticsSummaryAsync(string range = "7d")
        {
            var (startDate, days) = ParseRange(range);

            var events = await _dbContext.WebsiteEvents
                .Where(e => e.TimestampUtc >= startDate)
                .AsNoTracking()
                .ToListAsync();

            int totalEvents = events.Count;
            int totalPageviews = events.Count(e => e.EventType == "pageview");
            int totalVisitors = events.Select(e => e.SessionId).Distinct().Count();
            int uniqueVisitors = events.Select(e => e.ClientIpCoarse ?? e.SessionId).Distinct().Count();

            int totalDownloads = await _dbContext.DownloadRecords
                .Where(d => d.DownloadedAtUtc >= startDate)
                .CountAsync();

            decimal conversionRate = totalVisitors > 0
                ? Math.Round(((decimal)totalDownloads / totalVisitors) * 100m, 2)
                : 0.0m;

            // Popular Pages
            var popularPages = events
                .Where(e => e.EventType == "pageview")
                .GroupBy(e => e.PagePath)
                .OrderByDescending(g => g.Count())
                .Take(8)
                .Select(g => new PageStat(
                    Path: g.Key,
                    Title: g.FirstOrDefault()?.PageTitle ?? g.Key,
                    Views: g.Count(),
                    Percentage: totalPageviews > 0 ? Math.Round((decimal)g.Count() / totalPageviews * 100m, 1) : 0m
                ))
                .ToList();

            // Country distribution
            var countries = events
                .GroupBy(e => string.IsNullOrWhiteSpace(e.CountryCode) ? "US" : e.CountryCode)
                .OrderByDescending(g => g.Count())
                .Take(6)
                .Select(g => new GeoStat(
                    CountryCode: g.Key,
                    CountryName: GetCountryName(g.Key),
                    Count: g.Count(),
                    Percentage: totalEvents > 0 ? Math.Round((decimal)g.Count() / totalEvents * 100m, 1) : 0m
                ))
                .ToList();

            // Devices
            var devices = events
                .GroupBy(e => string.IsNullOrWhiteSpace(e.DeviceCategory) ? "Desktop" : e.DeviceCategory)
                .OrderByDescending(g => g.Count())
                .Select(g => new CategoryStat(
                    Category: g.Key,
                    Count: g.Count(),
                    Percentage: totalEvents > 0 ? Math.Round((decimal)g.Count() / totalEvents * 100m, 1) : 0m
                ))
                .ToList();

            // Operating Systems
            var operatingSystems = events
                .GroupBy(e => string.IsNullOrWhiteSpace(e.OperatingSystem) ? "Windows" : e.OperatingSystem)
                .OrderByDescending(g => g.Count())
                .Take(6)
                .Select(g => new CategoryStat(
                    Category: g.Key,
                    Count: g.Count(),
                    Percentage: totalEvents > 0 ? Math.Round((decimal)g.Count() / totalEvents * 100m, 1) : 0m
                ))
                .ToList();

            // Timeline
            var timeline = events
                .GroupBy(e => e.TimestampUtc.ToString("yyyy-MM-dd"))
                .OrderBy(g => g.Key)
                .Select(g => new TimelineStat(g.Key, g.Count()))
                .ToList();

            return new WebsiteAnalyticsSummary(
                Range: range,
                TotalVisitors: totalVisitors,
                UniqueVisitors: uniqueVisitors,
                TotalPageviews: totalPageviews,
                DownloadConversionRate: conversionRate,
                PopularPages: popularPages,
                Countries: countries,
                Devices: devices,
                OperatingSystems: operatingSystems,
                Timeline: timeline);
        }

        public async Task<DownloadAnalyticsOverview> GetDownloadAnalyticsOverviewAsync(string range = "30d")
        {
            var (startDate, _) = ParseRange(range);
            var now = DateTime.UtcNow;
            var todayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
            var sevenDaysStart = now.AddDays(-7);
            var thirtyDaysStart = now.AddDays(-30);

            var downloads = await _dbContext.DownloadRecords
                .Include(d => d.ReleaseArtifact)
                .ThenInclude(a => a!.Release)
                .Where(d => d.DownloadedAtUtc >= startDate)
                .AsNoTracking()
                .ToListAsync();

            int totalAllTime = await _dbContext.DownloadRecords.CountAsync();
            int todayDownloads = await _dbContext.DownloadRecords.CountAsync(d => d.DownloadedAtUtc >= todayStart);
            int sevenDaysDownloads = await _dbContext.DownloadRecords.CountAsync(d => d.DownloadedAtUtc >= sevenDaysStart);
            int thirtyDaysDownloads = await _dbContext.DownloadRecords.CountAsync(d => d.DownloadedAtUtc >= thirtyDaysStart);

            int totalInRange = downloads.Count > 0 ? downloads.Count : 1;

            // By Version
            var byVersion = downloads
                .GroupBy(d => d.ReleaseVersion ?? d.ReleaseArtifact?.Release?.Version ?? "v2.2.0")
                .OrderByDescending(g => g.Count())
                .Take(6)
                .Select(g => new VersionStat(
                    Version: g.Key,
                    Count: g.Count(),
                    Percentage: Math.Round((decimal)g.Count() / totalInRange * 100m, 1)
                ))
                .ToList();

            // By Country
            var byCountry = downloads
                .GroupBy(d => string.IsNullOrWhiteSpace(d.CountryCode) ? "US" : d.CountryCode)
                .OrderByDescending(g => g.Count())
                .Take(6)
                .Select(g => new GeoStat(
                    CountryCode: g.Key,
                    CountryName: GetCountryName(g.Key),
                    Count: g.Count(),
                    Percentage: Math.Round((decimal)g.Count() / totalInRange * 100m, 1)
                ))
                .ToList();

            // By OS
            var byOs = downloads
                .GroupBy(d => string.IsNullOrWhiteSpace(d.OperatingSystem) ? "Windows 11" : d.OperatingSystem)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new CategoryStat(
                    Category: g.Key,
                    Count: g.Count(),
                    Percentage: Math.Round((decimal)g.Count() / totalInRange * 100m, 1)
                ))
                .ToList();

            // By Device
            var byDevice = downloads
                .GroupBy(d => string.IsNullOrWhiteSpace(d.DeviceCategory) ? "Desktop (64-bit)" : d.DeviceCategory)
                .OrderByDescending(g => g.Count())
                .Select(g => new CategoryStat(
                    Category: g.Key,
                    Count: g.Count(),
                    Percentage: Math.Round((decimal)g.Count() / totalInRange * 100m, 1)
                ))
                .ToList();

            // By Status
            var byStatus = downloads
                .GroupBy(d => d.Status.ToString())
                .Select(g => new StatusStat(
                    Status: g.Key,
                    Count: g.Count(),
                    Percentage: Math.Round((decimal)g.Count() / totalInRange * 100m, 1)
                ))
                .ToList();

            // Timeline
            var timeline = downloads
                .GroupBy(d => d.DownloadedAtUtc.ToString("yyyy-MM-dd"))
                .OrderBy(g => g.Key)
                .Select(g => new TimelineStat(g.Key, g.Count()))
                .ToList();

            return new DownloadAnalyticsOverview(
                Range: range,
                TotalDownloads: totalAllTime,
                TodayDownloads: todayDownloads,
                SevenDaysDownloads: sevenDaysDownloads,
                ThirtyDaysDownloads: thirtyDaysDownloads,
                ByVersion: byVersion,
                ByCountry: byCountry,
                ByOperatingSystem: byOs,
                ByDevice: byDevice,
                ByStatus: byStatus,
                Timeline: timeline);
        }

        private static (DateTime StartDate, int Days) ParseRange(string range)
        {
            int days = range?.ToLowerInvariant() switch
            {
                "today" or "24h" or "1d" => 1,
                "7d" => 7,
                "30d" => 30,
                "90d" => 90,
                _ => 7
            };
            return (DateTime.UtcNow.AddDays(-days), days);
        }

        private static string AnonymizeIp(string? ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return "0.0.0.0/0";
            var parts = ip.Split('.');
            if (parts.Length == 4)
            {
                // IPv4: mask last octet (e.g. 192.168.1.0/24)
                return $"{parts[0]}.{parts[1]}.{parts[2]}.0/24";
            }
            if (ip.Contains(':'))
            {
                // IPv6: coarse /48 prefix
                var v6Parts = ip.Split(':');
                if (v6Parts.Length >= 3)
                {
                    return $"{v6Parts[0]}:{v6Parts[1]}:{v6Parts[2]}::/48";
                }
            }
            return "anon";
        }

        private static (string OS, string Browser, string Device) ParseUserAgent(string? ua, string? explicitOs, string? explicitBrowser, string? explicitDevice)
        {
            if (!string.IsNullOrWhiteSpace(explicitOs) && !string.IsNullOrWhiteSpace(explicitBrowser))
            {
                return (explicitOs, explicitBrowser, explicitDevice ?? "Desktop");
            }

            if (string.IsNullOrWhiteSpace(ua))
            {
                return ("Windows", "Chrome", "Desktop");
            }

            string os = "Windows";
            string browser = "Chrome";
            string device = "Desktop";

            // OS Detection
            if (ua.Contains("Windows NT 10.0") || ua.Contains("Windows NT 11.0")) os = "Windows 10/11";
            else if (ua.Contains("Macintosh") || ua.Contains("Mac OS X")) os = "macOS";
            else if (ua.Contains("Linux") && !ua.Contains("Android")) os = "Linux";
            else if (ua.Contains("Android")) { os = "Android"; device = "Mobile"; }
            else if (ua.Contains("iPhone") || ua.Contains("iPad")) { os = "iOS"; device = ua.Contains("iPad") ? "Tablet" : "Mobile"; }

            // Browser Detection
            if (ua.Contains("Edg/")) browser = "Edge";
            else if (ua.Contains("Brave")) browser = "Brave";
            else if (ua.Contains("Chrome/") && !ua.Contains("Edg/")) browser = "Chrome";
            else if (ua.Contains("Firefox/")) browser = "Firefox";
            else if (ua.Contains("Safari/") && !ua.Contains("Chrome/")) browser = "Safari";
            else if (ua.Contains("OPR/") || ua.Contains("Opera/")) browser = "Opera";

            // Device
            if (ua.Contains("Mobile") || ua.Contains("Android") || ua.Contains("iPhone")) device = "Mobile";
            if (ua.Contains("iPad") || ua.Contains("Tablet")) device = "Tablet";

            return (os, browser, device);
        }

        private static string SanitizeReferrer(string? refUrl)
        {
            if (string.IsNullOrWhiteSpace(refUrl)) return "Direct";
            try
            {
                if (Uri.TryCreate(refUrl, UriKind.Absolute, out var uri))
                {
                    return uri.Host;
                }
            }
            catch { }
            return "Referral";
        }

        private static string GetCountryName(string code)
        {
            return code.ToUpperInvariant() switch
            {
                "US" => "United States",
                "DE" => "Germany",
                "GB" => "United Kingdom",
                "CA" => "Canada",
                "FR" => "France",
                "JP" => "Japan",
                "AU" => "Australia",
                "NL" => "Netherlands",
                "IN" => "India",
                "BR" => "Brazil",
                _ => code
            };
        }
    }
}
