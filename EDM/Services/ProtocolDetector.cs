using System;
using System.Net;
using System.Text.RegularExpressions;

namespace EDM.Services
{
    public enum DownloadProtocolType
    {
        Unknown,
        Http,
        Https,
        Ftp,
        Ftps,
        Sftp,
        BitTorrent,
        Magnet,
        Hls,
        Dash,
        StreamingMedia
    }

    public sealed class ProtocolDetectionResult
    {
        public DownloadProtocolType Protocol { get; init; }
        public string NormalizedUrl { get; init; } = string.Empty;
        public string DisplayScheme { get; init; } = "Unknown";
        public bool SupportsResume { get; init; }
        public bool RequiresAuthentication { get; init; }
        public bool IsStreaming { get; init; }
        public bool IsP2P { get; init; }
    }

    /// <summary>
    /// Centralized protocol detection, validation, and URL sanitization engine.
    /// Accurately detects and classifies download URLs across HTTP, HTTPS, FTP, FTPS, SFTP,
    /// Magnet links, .torrent files, HLS (.m3u8), DASH (.mpd), and media streaming sites.
    /// </summary>
    public static class ProtocolDetector
    {
        private static readonly Regex CredentialsRegex = new(@"^(?<scheme>[a-zA-Z][a-zA-Z0-9+\-.]*:\/\/)(?<user>[^:@\/]+)(:(?<pass>.*))?@(?<host>[^@\/]+(?::\d+)?(?:\/.*)?)$", RegexOptions.Compiled);
        private static readonly Regex YouTubeRegex = new(@"youtube\.com|youtu\.be", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex StreamingSitesRegex = new(@"vimeo\.com|dailymotion\.com|twitch\.tv|twitter\.com|x\.com|tiktok\.com|instagram\.com|facebook\.com|fb\.watch|reddit\.com", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static ProtocolDetectionResult Detect(string urlOrPath)
        {
            if (string.IsNullOrWhiteSpace(urlOrPath))
            {
                return new ProtocolDetectionResult
                {
                    Protocol = DownloadProtocolType.Unknown,
                    NormalizedUrl = string.Empty,
                    DisplayScheme = "None"
                };
            }

            string trimmed = urlOrPath.Trim();

            // 1. Magnet Link
            if (trimmed.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            {
                return new ProtocolDetectionResult
                {
                    Protocol = DownloadProtocolType.Magnet,
                    NormalizedUrl = trimmed,
                    DisplayScheme = "MAGNET",
                    SupportsResume = true,
                    IsP2P = true
                };
            }

            // 2. Local or Remote .torrent file
            if (trimmed.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
            {
                return new ProtocolDetectionResult
                {
                    Protocol = DownloadProtocolType.BitTorrent,
                    NormalizedUrl = trimmed,
                    DisplayScheme = "TORRENT",
                    SupportsResume = true,
                    IsP2P = true
                };
            }

            // 3. HLS (.m3u8) Manifest Stream
            if (trimmed.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
            {
                return new ProtocolDetectionResult
                {
                    Protocol = DownloadProtocolType.Hls,
                    NormalizedUrl = trimmed,
                    DisplayScheme = "HLS",
                    SupportsResume = true,
                    IsStreaming = true
                };
            }

            // 4. DASH (.mpd) Manifest Stream
            if (trimmed.Contains(".mpd", StringComparison.OrdinalIgnoreCase))
            {
                return new ProtocolDetectionResult
                {
                    Protocol = DownloadProtocolType.Dash,
                    NormalizedUrl = trimmed,
                    DisplayScheme = "DASH",
                    SupportsResume = true,
                    IsStreaming = true
                };
            }

            // 5. YouTube & Supported Video Streaming Sites
            if (YouTubeRegex.IsMatch(trimmed) || StreamingSitesRegex.IsMatch(trimmed))
            {
                return new ProtocolDetectionResult
                {
                    Protocol = DownloadProtocolType.StreamingMedia,
                    NormalizedUrl = trimmed,
                    DisplayScheme = "STREAM",
                    SupportsResume = true,
                    IsStreaming = true
                };
            }

            // 6. URI Scheme Parsing
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                string scheme = uri.Scheme.ToLowerInvariant();
                switch (scheme)
                {
                    case "http":
                        return new ProtocolDetectionResult
                        {
                            Protocol = DownloadProtocolType.Http,
                            NormalizedUrl = trimmed,
                            DisplayScheme = "HTTP",
                            SupportsResume = true
                        };
                    case "https":
                        return new ProtocolDetectionResult
                        {
                            Protocol = DownloadProtocolType.Https,
                            NormalizedUrl = trimmed,
                            DisplayScheme = "HTTPS",
                            SupportsResume = true
                        };
                    case "ftp":
                        return new ProtocolDetectionResult
                        {
                            Protocol = DownloadProtocolType.Ftp,
                            NormalizedUrl = trimmed,
                            DisplayScheme = "FTP",
                            SupportsResume = true
                        };
                    case "ftps":
                        return new ProtocolDetectionResult
                        {
                            Protocol = DownloadProtocolType.Ftps,
                            NormalizedUrl = trimmed,
                            DisplayScheme = "FTPS",
                            SupportsResume = true
                        };
                    case "sftp":
                        return new ProtocolDetectionResult
                        {
                            Protocol = DownloadProtocolType.Sftp,
                            NormalizedUrl = trimmed,
                            DisplayScheme = "SFTP",
                            SupportsResume = true
                        };
                }
            }

            return new ProtocolDetectionResult
            {
                Protocol = DownloadProtocolType.Unknown,
                NormalizedUrl = trimmed,
                DisplayScheme = "UNKNOWN"
            };
        }

        /// <summary>
        /// Scrubs user credentials (passwords, auth tokens) from URLs before logging.
        /// Example: "ftp://admin:secret123@ftp.example.com/file.iso" -> "ftp://admin:***@ftp.example.com/file.iso"
        /// </summary>
        public static string SanitizeUrlForLogging(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;

            var match = CredentialsRegex.Match(url);
            if (match.Success)
            {
                string scheme = match.Groups["scheme"].Value;
                string user = match.Groups["user"].Value;
                string host = match.Groups["host"].Value;
                return $"{scheme}{user}:***@{host}";
            }

            return url;
        }

        /// <summary>
        /// Extracts embedded credentials from FTP / FTPS / HTTP URLs if present.
        /// </summary>
        public static bool TryExtractCredentials(string url, out string cleanUrl, out NetworkCredential? credential)
        {
            cleanUrl = url;
            credential = null;

            if (string.IsNullOrWhiteSpace(url)) return false;

            var match = CredentialsRegex.Match(url);
            if (match.Success)
            {
                string scheme = match.Groups["scheme"].Value;
                string user = Uri.UnescapeDataString(match.Groups["user"].Value);
                string pass = match.Groups["pass"].Success ? Uri.UnescapeDataString(match.Groups["pass"].Value) : string.Empty;
                string host = match.Groups["host"].Value;

                cleanUrl = $"{scheme}{host}";
                credential = new NetworkCredential(user, pass);
                return true;
            }

            return false;
        }
    }
}

