using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class ServerAnalysisReport
    {
        public string TargetUrl { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public bool SupportsRange { get; set; }
        public long ContentLength { get; set; }
        public string? ContentType { get; set; }
        public string? ETag { get; set; }
        public Version HttpVersion { get; set; } = System.Net.HttpVersion.Version11;
        public double RoundTripTimeMs { get; set; }
        public string? DetectedCdn { get; set; }
        public int RecommendedSegments { get; set; } = 8;
        public string? ServerSoftware { get; set; }
        public int HealthScore { get; set; } = 100;
        public long EstimatedMemoryUsageBytes { get; set; } = 8 * 1024 * 1024;
    }

    /// <summary>
    /// Smart Pre-Flight Download Analyzer & Performance Intelligence Subsystem.
    /// Probes remote endpoints, detects CDN edges, computes dynamic connection sizing,
    /// evaluates health scores (0-100), predicts system resource footprint,
    /// and detects duplicate normalized downloads.
    /// </summary>
    public class SmartDownloadAnalyzer
    {
        private static readonly Lazy<SmartDownloadAnalyzer> _instance = new(() => new SmartDownloadAnalyzer());
        public static SmartDownloadAnalyzer Instance => _instance.Value;

        private readonly HttpClient _httpClient;

        public SmartDownloadAnalyzer(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? SharedHttpClient.Instance;
        }

        public async Task<ServerAnalysisReport> AnalyzeEndpointAsync(string url, CancellationToken ct = default)
        {
            var uri = new Uri(url);
            var report = new ServerAnalysisReport
            {
                TargetUrl = url,
                Host = uri.Host
            };

            var sw = Stopwatch.StartNew();

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, url);
                req.Headers.Range = new RangeHeaderValue(0, 0);

                using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                sw.Stop();
                report.RoundTripTimeMs = sw.Elapsed.TotalMilliseconds;
                report.HttpVersion = resp.Version;

                // 1. Range Support
                report.SupportsRange = resp.Headers.AcceptRanges.Contains("bytes") ||
                                       resp.StatusCode == HttpStatusCode.PartialContent ||
                                       resp.Content.Headers.ContentRange != null;

                // 2. File Length
                if (resp.Content.Headers.ContentRange?.Length.HasValue == true)
                {
                    report.ContentLength = resp.Content.Headers.ContentRange.Length.Value;
                }
                else if (resp.Content.Headers.ContentLength.HasValue)
                {
                    report.ContentLength = resp.Content.Headers.ContentLength.Value;
                }

                report.ContentType = resp.Content.Headers.ContentType?.MediaType;
                report.ETag = resp.Headers.ETag?.Tag;

                // 3. Detect CDN & Server Header
                if (resp.Headers.Server != null)
                {
                    report.ServerSoftware = resp.Headers.Server.ToString();
                }

                if (resp.Headers.Contains("CF-RAY") || resp.Headers.Server?.ToString().Contains("cloudflare", StringComparison.OrdinalIgnoreCase) == true)
                {
                    report.DetectedCdn = "Cloudflare";
                }
                else if (resp.Headers.Contains("X-Amz-Cf-Id") || resp.Headers.Server?.ToString().Contains("cloudfront", StringComparison.OrdinalIgnoreCase) == true)
                {
                    report.DetectedCdn = "Amazon CloudFront";
                }
                else if (resp.Headers.Contains("X-Fastly-Request-ID"))
                {
                    report.DetectedCdn = "Fastly";
                }
                else if (resp.Headers.Contains("X-Cache") && resp.Headers.GetValues("X-Cache").ToString()?.Contains("Akamai") == true)
                {
                    report.DetectedCdn = "Akamai";
                }

                // 4. Calculate optimal segment count & health score
                report.RecommendedSegments = CalculateOptimalSegments(report.ContentLength, report.RoundTripTimeMs, report.SupportsRange);
                report.HealthScore = CalculateHealthScore(report.SupportsRange, report.RoundTripTimeMs, report.ContentLength);
                report.EstimatedMemoryUsageBytes = (long)report.RecommendedSegments * 1024 * 1024; // 1 MB per active segment ring buffer
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SmartDownloadAnalyzer] Probe failed", ex);
                report.RecommendedSegments = 1;
                report.HealthScore = 30;
            }

            return report;
        }

        public int CalculateHealthScore(bool supportsRange, double rttMs, long contentLength)
        {
            int score = 100;
            if (!supportsRange) score -= 30; // Severe penalty for lack of pause/resume
            if (rttMs > 300) score -= 20;
            else if (rttMs > 150) score -= 10;
            if (contentLength <= 0) score -= 15; // Unknown size stream
            return Math.Clamp(score, 10, 100);
        }

        public bool IsDuplicateUrl(string url1, string url2)
        {
            if (string.Equals(url1, url2, StringComparison.OrdinalIgnoreCase)) return true;

            try
            {
                var u1 = new Uri(url1);
                var u2 = new Uri(url2);

                // Compare scheme, host, and path disregarding tracking query parameters (utm_*, etc.)
                if (u1.Scheme.Equals(u2.Scheme, StringComparison.OrdinalIgnoreCase) &&
                    u1.Host.Equals(u2.Host, StringComparison.OrdinalIgnoreCase) &&
                    u1.AbsolutePath.Equals(u2.AbsolutePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        public int CalculateOptimalSegments(long contentLength, double rttMs, bool supportsRange)
        {
            if (!supportsRange || contentLength <= 0) return 1;

            // Small files (< 2 MB) -> 1 connection to avoid TCP handshake overhead
            if (contentLength < 2 * 1024 * 1024) return 1;
            if (contentLength < 10 * 1024 * 1024) return 4;

            // Medium to Large files (10 MB to 100 MB)
            if (contentLength < 100 * 1024 * 1024)
            {
                return rttMs > 150 ? 12 : 8;
            }

            // Very Large files (> 100 MB) on high RTT latency connections
            if (rttMs > 200) return 24;
            if (rttMs > 100) return 16;
            return 8;
        }
    }
}
