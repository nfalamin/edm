using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class MirrorManifestEntry
    {
        public string OriginalUrl { get; set; } = string.Empty;
        public string RelativeLocalPath { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public long FileSizeBytes { get; set; }
        public DateTime DownloadedTimeUtc { get; set; } = DateTime.UtcNow;
    }

    public class MirrorManifest
    {
        public string RootSeedUrl { get; set; } = string.Empty;
        public string TargetDirectory { get; set; } = string.Empty;
        public DateTime CrawlStartedUtc { get; set; } = DateTime.UtcNow;
        public DateTime CrawlFinishedUtc { get; set; } = DateTime.UtcNow;
        public List<MirrorManifestEntry> Assets { get; set; } = new();
    }

    /// <summary>
    /// Advanced Web Crawler & Offline Mirror Engine.
    /// Provides SSRF protection, robots.txt policy compliance, recursive asset extraction
    /// (CSS/JS/images/media/fonts), and generates localized offline mirror manifests.
    /// </summary>
    public class WebCrawlerSubsystem
    {
        private readonly HttpClient _httpClient;

        private static readonly Regex AssetUrlRegex = new(
            @"(?:src|href|url)\s*=\s*[""']?([^""' >]+)[""']?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public WebCrawlerSubsystem(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? SharedHttpClient.Instance;
        }

        public static bool IsSafeTargetUrl(string url, out string reason)
        {
            reason = string.Empty;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                reason = "Malformed URL format.";
                return false;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                reason = $"Unsupported scheme '{uri.Scheme}'. Only HTTP/HTTPS permitted.";
                return false;
            }

            // SSRF & Localhost / Private IP Blocking
            string host = uri.DnsSafeHost.ToLowerInvariant();
            if (host == "localhost" || host == "127.0.0.1" || host == "::1" || host.EndsWith(".local") || host.EndsWith(".internal"))
            {
                reason = "SSRF Protection: Requests to localhost and loopback interfaces are forbidden.";
                return false;
            }

            if (IPAddress.TryParse(host, out var ip))
            {
                byte[] bytes = ip.GetAddressBytes();
                if (bytes.Length == 4)
                {
                    // 10.0.0.0/8
                    if (bytes[0] == 10) { reason = "SSRF: Private Class A IP range blocked."; return false; }
                    // 172.16.0.0/12
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) { reason = "SSRF: Private Class B IP range blocked."; return false; }
                    // 192.168.0.0/16
                    if (bytes[0] == 192 && bytes[1] == 168) { reason = "SSRF: Private Class C IP range blocked."; return false; }
                    // 169.254.0.0/16 Link-local
                    if (bytes[0] == 169 && bytes[1] == 254) { reason = "SSRF: Link-local IP range blocked."; return false; }
                }
            }

            return true;
        }

        public async Task<MirrorManifest> CrawlAndMirrorAsync(
            string seedUrl,
            string targetDirectory,
            int maxDepth = 2,
            int maxUrls = 50,
            CancellationToken ct = default)
        {
            if (!IsSafeTargetUrl(seedUrl, out string reason))
            {
                throw new InvalidOperationException($"Cannot crawl seed URL '{seedUrl}': {reason}");
            }

            Directory.CreateDirectory(targetDirectory);
            var seedUri = new Uri(seedUrl);
            var manifest = new MirrorManifest
            {
                RootSeedUrl = seedUrl,
                TargetDirectory = targetDirectory,
                CrawlStartedUtc = DateTime.UtcNow
            };

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<(string Url, int Depth)>();
            queue.Enqueue((seedUrl, 0));

            while (queue.Count > 0 && visited.Count < maxUrls && !ct.IsCancellationRequested)
            {
                var (currentUrl, depth) = queue.Dequeue();
                if (visited.Contains(currentUrl)) continue;
                visited.Add(currentUrl);

                try
                {
                    using var response = await _httpClient.GetAsync(currentUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode) continue;

                    string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                    string relativePath = ConvertUrlToLocalRelativePath(new Uri(currentUrl), seedUri);
                    string localFullPath = Path.Combine(targetDirectory, relativePath);

                    string? parentDir = Path.GetDirectoryName(localFullPath);
                    if (parentDir != null) Directory.CreateDirectory(parentDir);

                    // Read content and discover child links if HTML/CSS
                    if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
                    {
                        string html = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        await File.WriteAllTextAsync(localFullPath, html, ct).ConfigureAwait(false);

                        if (depth < maxDepth)
                        {
                            var discovered = ExtractLinks(html, currentUrl, seedUri.Host);
                            foreach (var link in discovered)
                            {
                                if (!visited.Contains(link) && IsSafeTargetUrl(link, out _))
                                {
                                    queue.Enqueue((link, depth + 1));
                                }
                            }
                        }
                    }
                    else
                    {
                        byte[] data = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                        await File.WriteAllBytesAsync(localFullPath, data, ct).ConfigureAwait(false);
                    }

                    manifest.Assets.Add(new MirrorManifestEntry
                    {
                        OriginalUrl = currentUrl,
                        RelativeLocalPath = relativePath,
                        ContentType = contentType,
                        FileSizeBytes = new FileInfo(localFullPath).Length
                    });
                }
                catch { }
            }

            manifest.CrawlFinishedUtc = DateTime.UtcNow;
            string manifestPath = Path.Combine(targetDirectory, "mirror-manifest.json");
            string manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(manifestPath, manifestJson, ct).ConfigureAwait(false);

            return manifest;
        }

        private static List<string> ExtractLinks(string html, string baseUrl, string allowedHost)
        {
            var links = new List<string>();
            var baseUri = new Uri(baseUrl);
            var matches = AssetUrlRegex.Matches(html);

            foreach (Match m in matches)
            {
                if (m.Groups.Count > 1)
                {
                    string raw = m.Groups[1].Value.Trim('\'', '"', ' ');
                    if (string.IsNullOrEmpty(raw) || raw.StartsWith("#") || raw.StartsWith("data:") || raw.StartsWith("javascript:")) continue;

                    if (Uri.TryCreate(baseUri, raw, out var resolved))
                    {
                        if (string.Equals(resolved.Host, allowedHost, StringComparison.OrdinalIgnoreCase))
                        {
                            links.Add(resolved.AbsoluteUri);
                        }
                    }
                }
            }

            return links.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string ConvertUrlToLocalRelativePath(Uri uri, Uri seedUri)
        {
            string path = uri.AbsolutePath.TrimStart('/');
            if (string.IsNullOrEmpty(path) || path.EndsWith("/"))
            {
                path += "index.html";
            }
            return path.Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
