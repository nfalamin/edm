using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class MediaVariantOption
    {
        public string VariantId { get; set; } = Guid.NewGuid().ToString("N");
        public string QualityLabel { get; set; } = "720p";
        public int Width { get; set; }
        public int Height { get; set; }
        public string Resolution => Width > 0 && Height > 0 ? $"{Width}x{Height}" : QualityLabel;
        public long Bitrate { get; set; } // in bps
        public double FrameRate { get; set; } // in fps
        public string Codec { get; set; } = "h264";
        public string AudioCodec { get; set; } = "aac";
        public long AudioBitrate { get; set; } // in bps
        public string Container { get; set; } = "mp4";
        public bool HasAudio { get; set; } = true;
        public bool IsAudioOnly { get; set; }
        public long EstimatedSizeBytes { get; set; } = -1; // -1 if unknown
        public string DirectUrl { get; set; } = string.Empty;
        public string? AudioStreamUrl { get; set; }
        public bool RequiresFfmpegMerge { get; set; }
        public string FormatArg { get; set; } = string.Empty;

        public string FormattedSize => EstimatedSizeBytes > 0 ? FormatBytes(EstimatedSizeBytes) : "Size: Unknown";

        public string FormattedDetails
        {
            get
            {
                if (IsAudioOnly)
                {
                    string audioInfo = AudioBitrate > 0 ? $"{AudioBitrate / 1000} kbps" : AudioCodec;
                    return $"{Container.ToUpperInvariant()} Audio • {audioInfo} • {FormattedSize}";
                }

                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(QualityLabel)) parts.Add(QualityLabel);
                if (!string.IsNullOrWhiteSpace(Container)) parts.Add(Container.ToUpperInvariant());
                if (!string.IsNullOrWhiteSpace(Codec) && Codec != "none") parts.Add(Codec.ToUpperInvariant());
                if (FrameRate > 0) parts.Add($"{Math.Round(FrameRate)} FPS");
                parts.Add(FormattedSize);
                parts.Add(HasAudio ? "Audio: Included" : "Video-Only");

                return string.Join(" • ", parts);
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"≈ {len:0.##} {sizes[order]}";
        }
    }

    public class MediaVariantResult
    {
        public bool Success { get; set; }
        public bool IsDrmProtected { get; set; }
        public string Title { get; set; } = "Web Video";
        public string SourceUrl { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public List<MediaVariantOption> Variants { get; set; } = new List<MediaVariantOption>();
    }

    public class MediaVariantResolver
    {
        private readonly HttpRequestPipeline _pipeline;
        private readonly YtDlpService _ytDlpService;

        public MediaVariantResolver(YtDlpService? ytDlpService = null)
        {
            _pipeline = new HttpRequestPipeline(SharedHttpClient.Instance);
            _ytDlpService = ytDlpService ?? new YtDlpService();
        }

        public async Task<MediaVariantResult> ResolveVariantsAsync(string mediaUrl, string? cookies = null, CancellationToken cancellationToken = default)
        {
            var result = new MediaVariantResult { SourceUrl = mediaUrl };

            if (string.IsNullOrWhiteSpace(mediaUrl))
            {
                result.ErrorMessage = "Invalid or empty media URL.";
                return result;
            }

            try
            {
                var uri = new Uri(mediaUrl);

                // 1) HLS Stream (.m3u8)
                if (mediaUrl.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
                {
                    return await ResolveHlsVariantsAsync(uri, cookies, cancellationToken).ConfigureAwait(false);
                }

                // 2) DASH Stream (.mpd)
                if (mediaUrl.Contains(".mpd", StringComparison.OrdinalIgnoreCase))
                {
                    return await ResolveDashVariantsAsync(uri, cookies, cancellationToken).ConfigureAwait(false);
                }

                // 3) YouTube Native Stream Resolution
                if (IsYouTubeUrl(mediaUrl))
                {
                    // Fast parallel title prefetch via YouTube oEmbed (50ms)
                    var fastTitleTask = FetchYouTubeTitleFastAsync(mediaUrl, cancellationToken);

                    var ytRes = await ResolveYoutubeNativeVariantsAsync(mediaUrl, cancellationToken).ConfigureAwait(false);
                    if (ytRes.Success && ytRes.Variants.Any())
                    {
                        if (string.IsNullOrWhiteSpace(ytRes.Title))
                        {
                            ytRes.Title = await fastTitleTask.ConfigureAwait(false) ?? "YouTube Video";
                        }
                        return ytRes;
                    }

                    // Fallback to yt-dlp for YouTube
                    var ytDlpRes = await ResolveYtDlpVariantsAsync(mediaUrl, cancellationToken).ConfigureAwait(false);
                    if (ytDlpRes.Success && ytDlpRes.Variants.Any())
                    {
                        if (string.IsNullOrWhiteSpace(ytDlpRes.Title))
                        {
                            ytDlpRes.Title = await fastTitleTask.ConfigureAwait(false) ?? "YouTube Video";
                        }
                        return ytDlpRes;
                    }

                    result.Success = false;
                    result.ErrorMessage = "Unable to resolve YouTube streams. Please ensure yt-dlp is available.";
                    return result;
                }

                // 4) Other Streaming Websites via YtDlpService
                if (IsStreamingSite(mediaUrl))
                {
                    var streamRes = await ResolveYtDlpVariantsAsync(mediaUrl, cancellationToken).ConfigureAwait(false);
                    if (streamRes.Success && streamRes.Variants.Any()) return streamRes;

                    result.Success = false;
                    result.ErrorMessage = "Unable to resolve media streams for this video URL.";
                    return result;
                }

                // 5) Direct MP4/WebM files with HTTP probing
                return await ResolveDirectMediaAsync(uri, cookies, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[MediaVariantResolver] Error resolving variants for '{mediaUrl}'", ex);
                result.ErrorMessage = $"Failed to resolve stream variants: {ex.Message}";
                return result;
            }
        }

        public static bool IsYouTubeUrl(string url)
        {
            return Regex.IsMatch(url, @"youtube\.com|youtu\.be", RegexOptions.IgnoreCase);
        }

        private static async Task<string?> FetchYouTubeTitleFastAsync(string mediaUrl, CancellationToken ct)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(3));

                string oembedUrl = $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(mediaUrl)}&format=json";
                using var req = new HttpRequestMessage(HttpMethod.Get, oembedUrl);
                using var resp = await SharedHttpClient.Instance.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    string json = await resp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    var m = Regex.Match(json, @"""title""\s*:\s*""((?:\\.|[^""\\])*)""");
                    if (m.Success)
                    {
                        return Regex.Unescape(m.Groups[1].Value);
                    }
                }
            }
            catch { }
            return null;
        }

        private async Task<MediaVariantResult> ResolveYoutubeNativeVariantsAsync(string mediaUrl, CancellationToken ct)
        {
            var result = new MediaVariantResult { SourceUrl = mediaUrl };
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(4)); // Fast 4-second cap for native extraction

                var youtube = new YoutubeExplode.YoutubeClient();
                var video = await youtube.Videos.GetAsync(mediaUrl, timeoutCts.Token).ConfigureAwait(false);
                result.Title = video.Title;

                var manifest = await youtube.Videos.Streams.GetManifestAsync(video.Id, timeoutCts.Token).ConfigureAwait(false);
                var audioStreams = manifest.GetAudioOnlyStreams().OrderByDescending(a => a.Bitrate).ToList();
                var bestAudio = audioStreams.FirstOrDefault();

                var formatList = new List<MediaVariantOption>();
                var seenVariants = new HashSet<string>();

                // A. Muxed streams (Video + Audio combined in single file, no FFmpeg merge required)
                var muxedStreams = manifest.GetMuxedStreams().OrderByDescending(m => m.VideoQuality.MaxHeight).ThenByDescending(m => m.Bitrate);
                foreach (var m in muxedStreams)
                {
                    int h = m.VideoQuality.MaxHeight;
                    string container = m.Container.Name.ToLowerInvariant();
                    string seenKey = $"muxed_{h}_{container}";

                    if (seenVariants.Add(seenKey))
                    {
                        int width = (h * 16) / 9;
                        formatList.Add(new MediaVariantOption
                        {
                            QualityLabel = $"{h}p (Direct)",
                            Width = width,
                            Height = h,
                            Bitrate = m.Bitrate.BitsPerSecond,
                            FrameRate = m.VideoQuality.Framerate,
                            Codec = "H.264",
                            Container = container,
                            DirectUrl = m.Url,
                            AudioStreamUrl = null,
                            RequiresFfmpegMerge = false,
                            HasAudio = true,
                            EstimatedSizeBytes = m.Size.Bytes
                        });
                    }
                }

                // B. Adaptive video-only streams (4K 2160p, 1440p, 1080p, 720p60, etc.)
                var videoStreams = manifest.GetVideoOnlyStreams().OrderByDescending(v => v.VideoQuality.MaxHeight).ThenByDescending(v => v.Bitrate);

                foreach (var v in videoStreams)
                {
                    int h = v.VideoQuality.MaxHeight;
                    string container = v.Container.Name.ToLowerInvariant();
                    string seenKey = $"{h}_{container}";

                    if (seenVariants.Add(seenKey))
                    {
                        // Match compatible audio track for container
                        var matchingAudio = audioStreams.FirstOrDefault(a => a.Container.Name.Equals(v.Container.Name, StringComparison.OrdinalIgnoreCase)) ?? bestAudio;
                        long estSize = v.Size.Bytes + (matchingAudio?.Size.Bytes ?? 0);
                        string videoCodec = container.Equals("webm", StringComparison.OrdinalIgnoreCase) ? "VP9" : "H.264";
                        int width = (h * 16) / 9;

                        formatList.Add(new MediaVariantOption
                        {
                            QualityLabel = $"{h}p" + (v.VideoQuality.Framerate > 30 ? $"{v.VideoQuality.Framerate}" : ""),
                            Width = width,
                            Height = h,
                            Bitrate = v.Bitrate.BitsPerSecond,
                            FrameRate = v.VideoQuality.Framerate,
                            Codec = videoCodec,
                            Container = container,
                            DirectUrl = v.Url,
                            AudioStreamUrl = matchingAudio?.Url,
                            RequiresFfmpegMerge = matchingAudio != null,
                            HasAudio = true,
                            EstimatedSizeBytes = estSize
                        });
                    }
                }

                // C. Audio Only option with honest container & codec
                if (bestAudio != null)
                {
                    string audioContainer = bestAudio.Container.Name.ToLowerInvariant();
                    string audioCodec = audioContainer.Equals("webm", StringComparison.OrdinalIgnoreCase) ? "Opus" : "AAC";
                    formatList.Add(new MediaVariantOption
                    {
                        QualityLabel = $"Audio Only ({bestAudio.Bitrate.KiloBitsPerSecond:F0} kbps)",
                        Container = audioContainer,
                        Codec = audioCodec,
                        DirectUrl = bestAudio.Url,
                        IsAudioOnly = true,
                        HasAudio = true,
                        EstimatedSizeBytes = bestAudio.Size.Bytes,
                        Bitrate = bestAudio.Bitrate.BitsPerSecond
                    });
                }

                if (formatList.Any())
                {
                    result.Success = true;
                    result.Variants = formatList;
                    return result;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[MediaVariantResolver] Native YouTube resolution error: {ex.Message}");
            }

            return result;
        }

        private async Task<MediaVariantResult> ResolveDirectMediaAsync(Uri uri, string? cookies, CancellationToken ct)
        {
            var result = new MediaVariantResult { SourceUrl = uri.ToString() };

            // Reject streaming site URLs from being treated as direct static media files
            if (IsStreamingSite(uri.ToString()) || IsYouTubeUrl(uri.ToString()))
            {
                result.Success = false;
                result.ErrorMessage = "Streaming video URL cannot be downloaded as a direct static file.";
                return result;
            }

            long size = -1;
            string mime = "video/mp4";

            try
            {
                var req = new HttpRequestMessage(HttpMethod.Head, uri);
                if (!string.IsNullOrEmpty(cookies)) req.Headers.Add("Cookie", cookies);
                using var resp = await SharedHttpClient.Instance.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    size = resp.Content.Headers.ContentLength ?? -1;
                    mime = resp.Content.Headers.ContentType?.MediaType ?? "video/mp4";
                }
            }
            catch { }

            // Reject HTML web pages
            if (mime.Contains("html", StringComparison.OrdinalIgnoreCase) || mime.Contains("xhtml", StringComparison.OrdinalIgnoreCase))
            {
                result.Success = false;
                result.ErrorMessage = "URL points to an HTML web page, not a media stream.";
                return result;
            }

            string ext = Path.GetExtension(uri.LocalPath).TrimStart('.').ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = "mp4";

            result.Success = true;
            result.Variants.Add(new MediaVariantOption
            {
                QualityLabel = "Direct Stream",
                DirectUrl = uri.ToString(),
                HasAudio = true,
                EstimatedSizeBytes = size,
                Container = ext,
                Codec = mime
            });

            return result;
        }

        private async Task<MediaVariantResult> ResolveHlsVariantsAsync(Uri uri, string? cookies, CancellationToken ct)
        {
            var result = new MediaVariantResult { SourceUrl = uri.ToString() };
            string m3u8Text = await FetchTextAsync(uri, cookies, ct).ConfigureAwait(false);

            var playlist = HlsParser.Parse(m3u8Text, uri);

            if (playlist.IsDrmProtected)
            {
                result.IsDrmProtected = true;
                string sys = !string.IsNullOrEmpty(playlist.DrmSystem) ? playlist.DrmSystem : "DRM";
                result.ErrorMessage = $"This stream is protected by {sys} and cannot be downloaded.";
                return result;
            }

            result.Success = true;

            if (playlist.IsMaster && playlist.Variants.Any())
            {
                var sortedVariants = playlist.Variants.OrderByDescending(v => v.Height).ThenByDescending(v => v.Bandwidth).ToList();

                foreach (var v in sortedVariants)
                {
                    long estSize = -1;
                    if (playlist.TotalDurationSeconds > 0 && v.Bandwidth > 0)
                    {
                        estSize = (long)(playlist.TotalDurationSeconds * (v.Bandwidth / 8.0));
                    }

                    result.Variants.Add(new MediaVariantOption
                    {
                        QualityLabel = v.Height > 0 ? $"{v.Height}p" : "HLS Variant",
                        Width = v.Width,
                        Height = v.Height,
                        Bitrate = v.Bandwidth,
                        FrameRate = v.FrameRate,
                        Codec = string.IsNullOrEmpty(v.Codecs) ? "H.264" : v.Codecs,
                        Container = "mp4",
                        DirectUrl = v.Uri,
                        EstimatedSizeBytes = estSize,
                        HasAudio = v.HasAudio
                    });
                }

                // Add Audio Only option if separate audio tracks exist
                if (playlist.AudioTracks.Any(a => !string.IsNullOrEmpty(a.Uri)))
                {
                    var audioTrack = playlist.AudioTracks.FirstOrDefault(a => a.IsDefault) ?? playlist.AudioTracks.First();
                    result.Variants.Add(new MediaVariantOption
                    {
                        QualityLabel = "Audio Only (AAC)",
                        DirectUrl = audioTrack.Uri,
                        IsAudioOnly = true,
                        HasAudio = true,
                        Container = "m4a",
                        Codec = "AAC"
                    });
                }
            }
            else
            {
                // Single resolution media playlist
                result.Variants.Add(new MediaVariantOption
                {
                    QualityLabel = playlist.IsLive ? "Live Stream (HLS)" : "Standard Stream",
                    DirectUrl = uri.ToString(),
                    Container = "mp4",
                    HasAudio = true
                });
            }

            return result;
        }

        private async Task<MediaVariantResult> ResolveDashVariantsAsync(Uri uri, string? cookies, CancellationToken ct)
        {
            var result = new MediaVariantResult { SourceUrl = uri.ToString() };
            string xmlText = await FetchTextAsync(uri, cookies, ct).ConfigureAwait(false);

            var manifest = DashParser.Parse(xmlText, uri);

            if (manifest.IsDrmProtected)
            {
                result.IsDrmProtected = true;
                string sys = !string.IsNullOrEmpty(manifest.DrmSystem) ? manifest.DrmSystem : "DRM";
                result.ErrorMessage = $"This stream is protected by {sys} and cannot be downloaded.";
                return result;
            }

            result.Success = true;

            var primaryAudio = manifest.AudioRepresentations.FirstOrDefault();

            if (manifest.VideoRepresentations.Any())
            {
                var sortedReps = manifest.VideoRepresentations.OrderByDescending(r => r.Height).ThenByDescending(r => r.Bandwidth).ToList();

                foreach (var rep in sortedReps)
                {
                    result.Variants.Add(new MediaVariantOption
                    {
                        QualityLabel = rep.Height > 0 ? $"{rep.Height}p" : "DASH Variant",
                        Width = rep.Width,
                        Height = rep.Height,
                        Bitrate = rep.Bandwidth,
                        FrameRate = rep.FrameRate,
                        Codec = string.IsNullOrEmpty(rep.Codecs) ? "H.264" : rep.Codecs,
                        Container = "mp4",
                        DirectUrl = uri.ToString(), // Pass manifest URL for DASH downloader
                        RequiresFfmpegMerge = primaryAudio != null,
                        HasAudio = primaryAudio != null
                    });
                }
            }

            if (primaryAudio != null)
            {
                result.Variants.Add(new MediaVariantOption
                {
                    QualityLabel = "Audio Only (AAC)",
                    DirectUrl = uri.ToString(),
                    IsAudioOnly = true,
                    HasAudio = true,
                    Container = "m4a",
                    Codec = string.IsNullOrEmpty(primaryAudio.Codecs) ? "AAC" : primaryAudio.Codecs
                });
            }

            return result;
        }

        private async Task<MediaVariantResult> ResolveYtDlpVariantsAsync(string mediaUrl, CancellationToken ct)
        {
            var result = new MediaVariantResult { SourceUrl = mediaUrl };

            try
            {
                string? infoJson = await _ytDlpService.GetVideoInfoAsync(mediaUrl, ct).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(infoJson))
                {
                    using var doc = JsonDocument.Parse(infoJson);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("title", out var t))
                    {
                        result.Title = t.GetString() ?? "Web Video";
                    }

                    if (root.TryGetProperty("formats", out var formatsElem) && formatsElem.ValueKind == JsonValueKind.Array)
                    {
                        var formatList = new List<MediaVariantOption>();

                        foreach (var fmt in formatsElem.EnumerateArray())
                        {
                            int height = 0;
                            if (fmt.TryGetProperty("height", out var h) && h.ValueKind == JsonValueKind.Number) height = h.GetInt32();

                            int width = 0;
                            if (fmt.TryGetProperty("width", out var w) && w.ValueKind == JsonValueKind.Number) width = w.GetInt32();

                            double fps = 0;
                            if (fmt.TryGetProperty("fps", out var f) && f.ValueKind == JsonValueKind.Number) fps = f.GetDouble();

                            long tbr = 0;
                            if (fmt.TryGetProperty("tbr", out var b) && b.ValueKind == JsonValueKind.Number) tbr = (long)(b.GetDouble() * 1000);

                            string vcodec = fmt.TryGetProperty("vcodec", out var vc) ? vc.GetString() ?? "" : "";
                            string acodec = fmt.TryGetProperty("acodec", out var ac) ? ac.GetString() ?? "" : "";
                            string formatId = fmt.TryGetProperty("format_id", out var fid) ? fid.GetString() ?? "" : "";
                            string ext = fmt.TryGetProperty("ext", out var ex) ? ex.GetString() ?? "mp4" : "mp4";

                            long filesize = -1;
                            if (fmt.TryGetProperty("filesize", out var fs) && fs.ValueKind == JsonValueKind.Number)
                            {
                                filesize = fs.GetInt64();
                            }
                            else if (fmt.TryGetProperty("filesize_approx", out var fsa) && fsa.ValueKind == JsonValueKind.Number)
                            {
                                filesize = fsa.GetInt64();
                            }

                            double abr = 0;
                            if (fmt.TryGetProperty("abr", out var ab) && ab.ValueKind == JsonValueKind.Number) abr = ab.GetDouble();

                            bool isAudioOnly = vcodec == "none" || (string.IsNullOrEmpty(vcodec) && !string.IsNullOrEmpty(acodec) && acodec != "none");
                            bool hasAudio = !string.IsNullOrEmpty(acodec) && acodec != "none";

                            if (height > 0 || isAudioOnly)
                            {
                                string label;
                                if (isAudioOnly)
                                {
                                    string extUpper = ext.ToUpperInvariant();
                                    string bitrateStr = abr > 0 ? $"{Math.Round(abr)} kbps" : "";
                                    label = !string.IsNullOrEmpty(bitrateStr) ? $"{extUpper} Audio ({bitrateStr})" : $"{extUpper} Audio";
                                }
                                else
                                {
                                    label = $"{height}p";
                                    if (fps > 30) label += $"{Math.Round(fps)}";
                                }

                                formatList.Add(new MediaVariantOption
                                {
                                    QualityLabel = label,
                                    Width = width,
                                    Height = height,
                                    FrameRate = fps,
                                    Bitrate = tbr,
                                    Codec = vcodec,
                                    AudioCodec = acodec,
                                    AudioBitrate = (long)(abr * 1000),
                                    Container = ext,
                                    IsAudioOnly = isAudioOnly,
                                    HasAudio = hasAudio || !isAudioOnly,
                                    EstimatedSizeBytes = filesize,
                                    FormatArg = isAudioOnly ? $"-f {formatId}/bestaudio" : $"-f {formatId}+bestaudio/best",
                                    DirectUrl = mediaUrl
                                });
                            }
                        }

                        if (formatList.Any())
                        {
                            result.Success = true;
                            result.Variants = formatList
                                .OrderByDescending(v => v.Height)
                                .ThenByDescending(v => v.EstimatedSizeBytes)
                                .ThenByDescending(v => v.Bitrate)
                                .ToList();
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[MediaVariantResolver] YtDlp metadata query failed: {ex.Message}");
            }

            result.Success = false;
            result.ErrorMessage = "No downloadable media representations found.";
            return result;
        }

        private async Task<string> FetchTextAsync(Uri uri, string? cookies, CancellationToken ct)
        {
            var responseResult = await _pipeline.ExecuteWithRetryAsync(() =>
            {
                var req = new HttpRequestMessage(HttpMethod.Get, uri);
                if (!string.IsNullOrEmpty(cookies)) req.Headers.Add("Cookie", cookies);
                return req;
            }, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

            using var resp = responseResult.Response;
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }

        private static bool IsStreamingSite(string url)
        {
            return Regex.IsMatch(url, @"youtube\.com|youtu\.be|vimeo\.com|dailymotion\.com|twitch\.tv|twitter\.com|x\.com|tiktok\.com|instagram\.com", RegexOptions.IgnoreCase);
        }
    }
}
