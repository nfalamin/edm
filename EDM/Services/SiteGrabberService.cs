using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace EDM.Services
{
    public class GrabberScanOptions
    {
        public int MaxDepth { get; set; } = 2;
        public int MaxPagesScanned { get; set; } = 500;
        public bool SameDomainOnly { get; set; } = true;
        public List<string> AllowedDomains { get; set; } = new List<string>();
        public List<string> ExcludedDomains { get; set; } = new List<string>();
        public List<string>? IncludeExtensions { get; set; }
        public List<string>? ExcludeExtensions { get; set; }
        public string? UrlPatternRegex { get; set; }
        public long MinFileSizeBytes { get; set; } = 0;
        public long MaxFileSizeBytes { get; set; } = 0;
        public bool FetchMetadataHead { get; set; } = false;
        public bool RespectRobotsTxt { get; set; } = false;
        public int MaxConcurrentRequests { get; set; } = 4;
    }

    public class SiteGrabberItemResult
    {
        public string Url { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public string MimeType { get; set; } = "application/octet-stream";
        public long FileSizeBytes { get; set; } = -1;
        public int DepthFound { get; set; } = 1;
        public bool SelectedForDownload { get; set; } = true;
    }


    public class SiteGrabberProgressInfo
    {
        public int PagesScanned { get; set; }
        public int LinksFound { get; set; }
        public string CurrentUrl { get; set; } = string.Empty;
    }

    public class SiteGrabberProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Untitled Project";
        public string TargetUrl { get; set; } = string.Empty;
        public GrabberScanOptions Options { get; set; } = new GrabberScanOptions();
        public List<SiteGrabberItemResult> DiscoveredItems { get; set; } = new List<SiteGrabberItemResult>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastScannedAt { get; set; } = DateTime.UtcNow;

        public async Task SaveToFileAsync(string filePath)
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
        }

        public static async Task<SiteGrabberProject?> LoadFromFileAsync(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            string json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
            return JsonSerializer.Deserialize<SiteGrabberProject>(json);
        }
    }

    public class SiteGrabberService
    {
        private readonly HttpClient _httpClient;

        public static readonly HashSet<string> DefaultExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mkv", ".webm", ".avi", ".mov", ".mp3", ".wav", ".flac",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg",
            ".zip", ".rar", ".7z", ".tar", ".gz",
            ".pdf", ".exe", ".msi"
        };

        public SiteGrabberService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? SharedHttpClient.Instance;
        }

        public async Task<List<string>> ScanPageAsync(string pageUrl, CancellationToken cancellationToken = default)
        {
            var results = await ScanSiteAsync(pageUrl, new GrabberScanOptions { MaxDepth = 1 }, progress: null, cancellationToken).ConfigureAwait(false);
            return results.Select(r => r.Url).ToList();
        }

        public async Task<List<string>> CrawlWebsiteAsync(string pageUrl, int maxDepth = 2, CancellationToken cancellationToken = default)
        {
            var results = await ScanSiteAsync(pageUrl, new GrabberScanOptions { MaxDepth = maxDepth }, progress: null, cancellationToken).ConfigureAwait(false);
            return results.Select(r => r.Url).ToList();
        }

        public static string NormalizeUrl(string rawUrl)
        {
            try
            {
                if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri)) return rawUrl;
                
                // Strip fragment
                var builder = new UriBuilder(uri) { Fragment = "" };

                // Strip tracking query params
                var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
                query.Remove("utm_source");
                query.Remove("utm_medium");
                query.Remove("utm_campaign");
                query.Remove("fbclid");
                query.Remove("gclid");

                builder.Query = query.ToString();
                return builder.Uri.ToString().TrimEnd('#');
            }
            catch
            {
                return rawUrl;
            }
        }

        public async Task<List<SiteGrabberItemResult>> ScanSiteAsync(
            string pageUrl,
            GrabberScanOptions? options = null,
            IProgress<SiteGrabberProgressInfo>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pageUrl)) throw new ArgumentNullException(nameof(pageUrl));
            options ??= new GrabberScanOptions();

            pageUrl = NormalizeUrl(pageUrl);
            var rootUri = new Uri(pageUrl);

            var visitedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var foundMediaUrls = new Dictionary<string, SiteGrabberItemResult>(StringComparer.OrdinalIgnoreCase);
            var disallowedPaths = new List<string>();

            if (options.RespectRobotsTxt)
            {
                disallowedPaths = await FetchRobotsTxtDisallowedAsync(rootUri, cancellationToken).ConfigureAwait(false);
            }

            var pageQueue = new Queue<(string Url, int Depth)>();
            pageQueue.Enqueue((pageUrl, 1));

            var regex = !string.IsNullOrWhiteSpace(options.UrlPatternRegex)
                ? new Regex(options.UrlPatternRegex, RegexOptions.IgnoreCase | RegexOptions.Compiled)
                : null;

            int pagesScanned = 0;

            while (pageQueue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (pagesScanned >= options.MaxPagesScanned) break;

                var (currentUrl, depth) = pageQueue.Dequeue();


                string normUrl = NormalizeUrl(currentUrl);
                if (visitedPages.Contains(normUrl)) continue;
                visitedPages.Add(normUrl);
                pagesScanned++;

                progress?.Report(new SiteGrabberProgressInfo
                {
                    PagesScanned = pagesScanned,
                    LinksFound = foundMediaUrls.Count,
                    CurrentUrl = normUrl
                });

                string html = string.Empty;
                try
                {
                    html = await _httpClient.GetStringAsync(normUrl, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LoggingService.Log($"[SiteGrabberService] Failed to fetch {normUrl}: {ex.Message}");
                    continue;
                }

                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                var baseUri = new Uri(normUrl);

                var hrefs = (doc.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
                    .Select(n => n.GetAttributeValue("href", null));

                var mediaSrcs = (doc.DocumentNode.SelectNodes("//img[@src] | //video[@src] | //source[@src] | //audio[@src]") ?? Enumerable.Empty<HtmlNode>())
                    .Select(n => n.GetAttributeValue("src", null));

                var allRaw = hrefs.Concat(mediaSrcs).Where(s => !string.IsNullOrWhiteSpace(s));

                foreach (var raw in allRaw)
                {
                    if (!TryResolveAbsolute(raw!, baseUri, out var absUrl)) continue;
                    absUrl = NormalizeUrl(absUrl);

                    if (!Uri.TryCreate(absUrl, UriKind.Absolute, out var itemUri)) continue;

                    // Domain restriction checks
                    if (options.SameDomainOnly && !string.Equals(itemUri.Host, rootUri.Host, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (options.ExcludedDomains.Any(d => itemUri.Host.Contains(d, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    if (options.AllowedDomains.Count > 0 && !options.AllowedDomains.Any(d => itemUri.Host.Contains(d, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    // Robots.txt check
                    if (options.RespectRobotsTxt && disallowedPaths.Any(p => itemUri.AbsolutePath.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var ext = Path.GetExtension(itemUri.AbsolutePath);
                    bool isSubPage = string.IsNullOrEmpty(ext) || ext.Equals(".html", StringComparison.OrdinalIgnoreCase) || ext.Equals(".htm", StringComparison.OrdinalIgnoreCase) || ext.Equals(".php", StringComparison.OrdinalIgnoreCase);

                    if (isSubPage && depth < options.MaxDepth && !visitedPages.Contains(absUrl))
                    {
                        pageQueue.Enqueue((absUrl, depth + 1));
                    }

                    if (!MatchesFilter(absUrl, itemUri, ext, options, regex)) continue;

                    if (!foundMediaUrls.ContainsKey(absUrl))
                    {
                        var resultItem = new SiteGrabberItemResult
                        {
                            Url = absUrl,
                            Extension = ext.ToLowerInvariant(),
                            DepthFound = depth,
                            FileSizeBytes = -1
                        };

                        foundMediaUrls[absUrl] = resultItem;
                    }
                }
            }

            return foundMediaUrls.Values.ToList();
        }

        private async Task<List<string>> FetchRobotsTxtDisallowedAsync(Uri baseUri, CancellationToken ct)
        {
            var list = new List<string>();
            try
            {
                var robotsUri = new Uri(baseUri, "/robots.txt");
                string content = await _httpClient.GetStringAsync(robotsUri, ct).ConfigureAwait(false);
                var lines = content.Split('\n');
                bool inUserAgentAll = false;
                foreach (var line in lines)
                {
                    string l = line.Trim();
                    if (l.StartsWith("User-agent:", StringComparison.OrdinalIgnoreCase))
                    {
                        string agent = l.Substring(11).Trim();
                        inUserAgentAll = agent == "*";
                    }
                    else if (inUserAgentAll && l.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
                    {
                        string path = l.Substring(9).Trim();
                        if (!string.IsNullOrEmpty(path)) list.Add(path);
                    }
                }
            }
            catch { }
            return list;
        }

        private static bool MatchesFilter(string url, Uri uri, string ext, GrabberScanOptions options, Regex? regex)
        {
            if (string.IsNullOrEmpty(ext)) return false;

            if (regex != null && !regex.IsMatch(url)) return false;

            if (options.ExcludeExtensions != null && options.ExcludeExtensions.Count > 0)
            {
                if (options.ExcludeExtensions.Any(e => string.Equals(e.StartsWith('.') ? e : "." + e, ext, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            if (options.IncludeExtensions != null && options.IncludeExtensions.Count > 0)
            {
                return options.IncludeExtensions.Any(e => string.Equals(e.StartsWith('.') ? e : "." + e, ext, StringComparison.OrdinalIgnoreCase));
            }

            return DefaultExtensions.Contains(ext);
        }

        private static bool TryResolveAbsolute(string url, Uri baseUri, out string absolute)
        {
            absolute = string.Empty;
            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var absUri))
                {
                    absolute = absUri.ToString();
                    return true;
                }
                if (url.StartsWith("//"))
                {
                    absolute = baseUri.Scheme + ":" + url;
                    return true;
                }
                if (Uri.TryCreate(baseUri, url, out var rel))
                {
                    absolute = rel.ToString();
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}
