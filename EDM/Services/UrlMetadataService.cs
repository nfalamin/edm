using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    /// <summary>
    /// Extracts metadata (title, available resolutions, formats) from URLs.
    /// Uses YtDlpService to query video information without downloading.
    /// </summary>
    public class UrlMetadataService
    {
        private readonly YtDlpService _ytDlpService;
        private readonly ISettingsService _settingsService;

        public UrlMetadataService(YtDlpService ytDlpService, ISettingsService settingsService)
        {
            _ytDlpService = ytDlpService ?? throw new ArgumentNullException(nameof(ytDlpService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        /// <summary>
        /// Represents video metadata extracted from a URL.
        /// </summary>
        public class VideoMetadata
        {
            /// <summary>
            /// Video title from source.
            /// </summary>
            public string Title { get; set; } = string.Empty;

            /// <summary>
            /// Available video resolutions (e.g., "1080p", "720p", "480p").
            /// </summary>
            public List<string> AvailableResolutions { get; set; } = new();

            /// <summary>
            /// Available formats (e.g., "video", "audio").
            /// </summary>
            public List<string> AvailableFormats { get; set; } = new();

            /// <summary>
            /// Maximum resolution available (e.g., "1080p").
            /// </summary>
            public string MaxResolution { get; set; } = "720p";

            /// <summary>
            /// Video duration in seconds.
            /// </summary>
            public int DurationSeconds { get; set; }

            /// <summary>
            /// File size in bytes (if available).
            /// </summary>
            public long FileSizeBytes { get; set; } = -1;  // -1 = unknown

            /// <summary>
            /// MIME type / Content-Type (e.g., "video/mp4", "audio/mpeg")
            /// </summary>
            public string ContentType { get; set; } = string.Empty;

            /// <summary>
            /// Whether metadata was successfully extracted.
            /// </summary>
            public bool IsValid => !string.IsNullOrEmpty(Title) && AvailableResolutions.Count > 0;
        }

        /// <summary>
        /// Extracts metadata from a URL without downloading the content.
        /// </summary>
        /// <param name="url">The URL to extract metadata from</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Video metadata or null if extraction fails</returns>
        public async Task<VideoMetadata?> ExtractMetadataAsync(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            try
            {
                LoggingService.Log($"[UrlMetadataService] Extracting metadata from URL: {url}");

                // Use YtDlpService to fetch metadata
                var metadataJson = await _ytDlpService.GetVideoInfoAsync(url, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(metadataJson))
                {
                    LoggingService.LogWarning($"[UrlMetadataService] No metadata returned for URL: {url}");
                    return null;
                }

                // Parse metadata (mock implementation - extend based on actual YtDlpService response format)
                var metadata = ParseMetadata(metadataJson, url);

                // Attempt to detect file size and content type via HTTP HEAD request (non-blocking)
                try
                {
                    var (fileSize, contentType) = await DetectFileMetadataAsync(url, cancellationToken).ConfigureAwait(false);
                    metadata.FileSizeBytes = fileSize;
                    metadata.ContentType = contentType;
                }
                catch (Exception ex)
                {
                    LoggingService.Log($"[UrlMetadataService] Failed to detect file metadata: {ex.Message}");
                    // Continue - this is optional metadata
                }

                if (metadata?.IsValid == true)
                {
                    LoggingService.Log($"[UrlMetadataService] Extracted metadata - Title: {metadata.Title}, Max Resolution: {metadata.MaxResolution}, Size: {FormatFileSize(metadata.FileSizeBytes)}, Type: {metadata.ContentType}");
                }

                return metadata;
            }
            catch (OperationCanceledException)
            {
                LoggingService.LogWarning("[UrlMetadataService] Metadata extraction cancelled");
                return null;
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[UrlMetadataService.ExtractMetadataAsync]", ex);
                return null;
            }
        }

        /// <summary>
        /// Filters available resolutions to only show those <= max available resolution.
        /// </summary>
        public static List<string> FilterResolutions(List<string> availableResolutions, string maxResolution)
        {
            if (availableResolutions == null || availableResolutions.Count == 0)
                return new List<string> { "720p", "480p", "360p" };

            var maxHeight = ParseResolutionHeight(maxResolution);
            var filtered = new List<string>();

            var standardResolutions = new[] { "2160p", "1440p", "1080p", "720p", "480p", "360p", "240p" };
            foreach (var res in standardResolutions)
            {
                var height = ParseResolutionHeight(res);
                if (height <= maxHeight && availableResolutions.Contains(res))
                    filtered.Add(res);
            }

            return filtered.Count > 0 ? filtered : availableResolutions;
        }

        /// <summary>
        /// Parses resolution string to height in pixels (e.g., "720p" -> 720).
        /// </summary>
        private static int ParseResolutionHeight(string resolution)
        {
            if (string.IsNullOrEmpty(resolution))
                return 720;

            if (int.TryParse(resolution.TrimEnd('p'), out int height))
                return height;

            var lowerRes = resolution.ToLower();
            if (lowerRes == "4k" || lowerRes == "uhd") return 2160;
            if (lowerRes == "2k" || lowerRes == "qhd") return 1440;
            if (lowerRes == "hd") return 1080;
            if (lowerRes == "sd") return 480;
            return 720;
        }

        /// <summary>
        /// Parses metadata JSON response from YtDlpService.
        /// This is a simplified implementation - extend based on actual YtDlp output.
        /// </summary>
        private VideoMetadata ParseMetadata(string metadataJson, string url)
        {
            var metadata = new VideoMetadata();

            try
            {
                // Extract title (basic parsing - extend based on YtDlp JSON format)
                if (metadataJson.Contains("\"title\""))
                {
                    var titleStart = metadataJson.IndexOf("\"title\"") + 8;
                    var titleEnd = metadataJson.IndexOf("\"", titleStart);
                    if (titleEnd > titleStart)
                    {
                        metadata.Title = metadataJson.Substring(titleStart, titleEnd - titleStart)
                            .Trim('"', ' ');
                    }
                }

                // Set default title from URL if not found
                if (string.IsNullOrEmpty(metadata.Title))
                {
                    metadata.Title = ExtractTitleFromUrl(url);
                }

                // Extract available formats (simplified)
                if (metadataJson.Contains("\"formats\""))
                {
                    // Parse formats array - implementation depends on YtDlp output
                    // For now, assume common formats are available
                    metadata.AvailableFormats = new List<string> { "mp4", "webm", "mkv" };
                    metadata.AvailableResolutions = new List<string> { "1080p", "720p", "480p", "360p" };
                    metadata.MaxResolution = "1080p";
                }
                else
                {
                    // Fallback to common resolutions
                    metadata.AvailableFormats = new List<string> { "mp4" };
                    metadata.AvailableResolutions = new List<string> { "720p", "480p", "360p" };
                    metadata.MaxResolution = "720p";
                }

                // Extract duration 
                if (metadataJson.Contains("\"duration\""))
                {
                    var durationStart = metadataJson.IndexOf("\"duration\"") + 10;
                    var durationEnd = metadataJson.IndexOf(",", durationStart);
                    if (durationEnd < 0) durationEnd = metadataJson.IndexOf("}", durationStart);

                    if (durationEnd > durationStart)
                    {
                        var durationStr = metadataJson.Substring(durationStart, durationEnd - durationStart)
                            .Trim(':', ' ');
                        if (int.TryParse(durationStr, out int duration))
                            metadata.DurationSeconds = duration;
                    }
                }

                return metadata;
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[UrlMetadataService.ParseMetadata]", ex);

                // Return partial metadata with at least title
                metadata.Title = ExtractTitleFromUrl(url);
                metadata.AvailableResolutions = new List<string> { "720p", "480p", "360p" };
                metadata.MaxResolution = "720p";
                return metadata;
            }
        }

        /// <summary>
        /// Extracts a reasonable title from URL when metadata parsing fails.
        /// </summary>
        private string ExtractTitleFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var filename = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
                if (!string.IsNullOrEmpty(filename))
                    return System.Net.WebUtility.UrlDecode(filename);

                // Fallback: use domain name
                return uri.Host.Replace("www.", "").Split('.')[0];
            }
            catch
            {
                return "Download";
            }
        }

        /// <summary>
        /// Attempts to detect file size and content type by making an HTTP HEAD request.
        /// This is non-blocking and safe (no file download occurs).
        /// </summary>
        private async Task<(long sizeBytes, string contentType)> DetectFileMetadataAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient(new System.Net.Http.SocketsHttpHandler
                {
                    AllowAutoRedirect = true,
                    MaxConnectionsPerServer = 1
                })
                {
                    Timeout = TimeSpan.FromSeconds(5)
                })
                {
                    // Set a generic User-Agent to avoid blocks
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) EDM/1.0");

                    using (var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, url))
                    {
                        using (var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                long sizeBytes = response.Content.Headers.ContentLength ?? -1;
                                string contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                                return (sizeBytes, contentType);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[UrlMetadataService.DetectFileMetadataAsync] Failed: {ex.Message}");
            }

            return (-1, string.Empty);
        }

        /// <summary>
        /// Converts bytes to human-readable format (e.g., 1.5 MB).
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            if (bytes < 0) return "Unknown";
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }
    }
}
