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
        public bool HasAudio { get; set; } = true;
        public bool IsAudioOnly { get; set; }
        public long EstimatedSizeBytes { get; set; } = -1; // -1 if unknown
        public string DirectUrl { get; set; } = string.Empty;
        public string? AudioStreamUrl { get; set; }
        public bool RequiresFfmpegMerge { get; set; }
        public string FormatArg { get; set; } = string.Empty;
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

                // 3) Streaming Websites (YouTube, Vimeo, Twitch, etc.) via YtDlpService
                if (IsStreamingSite(mediaUrl))
                {
                    return await ResolveYtDlpVariantsAsync(mediaUrl, cancellationToken).ConfigureAwait(false);
                }

                // 4) Fallback for Direct MP4/WebM files
                result.Success = true;
                result.Variants.Add(new MediaVariantOption
                {
                    QualityLabel = "Direct Video Stream",
                    DirectUrl = mediaUrl,
                    HasAudio = true
                });
                return result;
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[MediaVariantResolver] Error resolving variants for '{mediaUrl}'", ex);
                result.ErrorMessage = $"Failed to resolve stream variants: {ex.Message}";
                return result;
            }
        }

        private async Task<MediaVariantResult> ResolveHlsVariantsAsync(Uri uri, string? cookies, CancellationToken ct)
        {
            var result = new MediaVariantResult { SourceUrl = uri.ToString() };
            string m3u8Text = await FetchTextAsync(uri, cookies, ct).ConfigureAwait(false);

            var playlist = HlsParser.Parse(m3u8Text, uri);

            if (playlist.IsDrmProtected)
            {
                result.IsDrmProtected = true;
                result.ErrorMessage = "This stream is DRM-protected and cannot be downloaded.";
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
                        Codec = string.IsNullOrEmpty(v.Codecs) ? "h264" : v.Codecs,
                        DirectUrl = v.Uri,
                        EstimatedSizeBytes = estSize,
                        HasAudio = v.HasAudio
                    });
                }

                // Add Best Quality option
                var best = sortedVariants.First();
                result.Variants.Insert(0, new MediaVariantOption
                {
                    QualityLabel = "Best Quality",
                    Width = best.Width,
                    Height = best.Height,
                    Bitrate = best.Bandwidth,
                    FrameRate = best.FrameRate,
                    Codec = best.Codecs,
                    DirectUrl = best.Uri,
                    HasAudio = true
                });

                // Add Audio Only option if audio tracks exist
                if (playlist.AudioTracks.Any(a => !string.IsNullOrEmpty(a.Uri)))
                {
                    var audioTrack = playlist.AudioTracks.FirstOrDefault(a => a.IsDefault) ?? playlist.AudioTracks.First();
                    result.Variants.Add(new MediaVariantOption
                    {
                        QualityLabel = "Audio Only (MP3)",
                        DirectUrl = audioTrack.Uri,
                        IsAudioOnly = true,
                        HasAudio = true,
                        Codec = "aac/mp3",
                        RequiresFfmpegMerge = true
                    });
                }
            }
            else
            {
                // Single resolution media playlist
                result.Variants.Add(new MediaVariantOption
                {
                    QualityLabel = "Standard Quality",
                    DirectUrl = uri.ToString(),
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
                result.ErrorMessage = "This stream is DRM-protected and cannot be downloaded.";
                return result;
            }

            result.Success = true;

            string? primaryAudioUrl = manifest.AudioRepresentations.FirstOrDefault()?.SegmentUrls.FirstOrDefault();

            if (manifest.VideoRepresentations.Any())
            {
                var sortedReps = manifest.VideoRepresentations.OrderByDescending(r => r.Height).ThenByDescending(r => r.Bandwidth).ToList();

                foreach (var rep in sortedReps)
                {
                    string firstSeg = rep.SegmentUrls.FirstOrDefault() ?? uri.ToString();
                    result.Variants.Add(new MediaVariantOption
                    {
                        QualityLabel = rep.Height > 0 ? $"{rep.Height}p" : "DASH Variant",
                        Width = rep.Width,
                        Height = rep.Height,
                        Bitrate = rep.Bandwidth,
                        FrameRate = rep.FrameRate,
                        Codec = string.IsNullOrEmpty(rep.Codecs) ? "h264" : rep.Codecs,
                        DirectUrl = firstSeg,
                        AudioStreamUrl = primaryAudioUrl,
                        RequiresFfmpegMerge = !string.IsNullOrEmpty(primaryAudioUrl),
                        HasAudio = !string.IsNullOrEmpty(primaryAudioUrl)
                    });
                }

                var best = sortedReps.First();
                result.Variants.Insert(0, new MediaVariantOption
                {
                    QualityLabel = "Best Quality",
                    Width = best.Width,
                    Height = best.Height,
                    Bitrate = best.Bandwidth,
                    FrameRate = best.FrameRate,
                    Codec = best.Codecs,
                    DirectUrl = best.SegmentUrls.FirstOrDefault() ?? uri.ToString(),
                    AudioStreamUrl = primaryAudioUrl,
                    RequiresFfmpegMerge = !string.IsNullOrEmpty(primaryAudioUrl),
                    HasAudio = true
                });
            }

            if (manifest.AudioRepresentations.Any())
            {
                var audioRep = manifest.AudioRepresentations.First();
                result.Variants.Add(new MediaVariantOption
                {
                    QualityLabel = "Audio Only (MP3)",
                    DirectUrl = audioRep.SegmentUrls.FirstOrDefault() ?? uri.ToString(),
                    IsAudioOnly = true,
                    HasAudio = true,
                    Codec = audioRep.Codecs
                });
            }

            return result;
        }

        private async Task<MediaVariantResult> ResolveYtDlpVariantsAsync(string mediaUrl, CancellationToken ct)
        {
            var result = new MediaVariantResult { SourceUrl = mediaUrl };

            try
            {
                string infoJson = await _ytDlpService.GetVideoInfoAsync(mediaUrl, ct).ConfigureAwait(false);
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
                            string formatId = fmt.TryGetProperty("format_id", out var fid) ? fid.GetString() ?? "" : "";
                            string ext = fmt.TryGetProperty("ext", out var ex) ? ex.GetString() ?? "mp4" : "mp4";

                            bool isAudioOnly = vcodec == "none";

                            if (height > 0 || isAudioOnly)
                            {
                                string label = isAudioOnly ? "Audio Only (MP3)" : $"{height}p";
                                if (fps > 30) label += $"{Math.Round(fps)}";

                                formatList.Add(new MediaVariantOption
                                {
                                    QualityLabel = label,
                                    Width = width,
                                    Height = height,
                                    FrameRate = fps,
                                    Bitrate = tbr,
                                    Codec = vcodec,
                                    IsAudioOnly = isAudioOnly,
                                    FormatArg = isAudioOnly ? "-f bestaudio" : $"-f {formatId}+bestaudio/best",
                                    DirectUrl = mediaUrl
                                });
                            }
                        }

                        if (formatList.Any())
                        {
                            result.Success = true;
                            result.Variants = formatList.OrderByDescending(v => v.Height).ThenByDescending(v => v.Bitrate).ToList();

                            // Add Best Quality
                            result.Variants.Insert(0, new MediaVariantOption
                            {
                                QualityLabel = "Best Quality",
                                FormatArg = "-f bestvideo+bestaudio/best",
                                DirectUrl = mediaUrl
                            });
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[MediaVariantResolver] YtDlp metadata query failed: {ex.Message}");
            }

            // Fallback
            result.Success = true;
            result.Variants.Add(new MediaVariantOption { QualityLabel = "Best Quality", DirectUrl = mediaUrl });
            result.Variants.Add(new MediaVariantOption { QualityLabel = "720p", DirectUrl = mediaUrl });
            result.Variants.Add(new MediaVariantOption { QualityLabel = "Audio Only (MP3)", DirectUrl = mediaUrl, IsAudioOnly = true });
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
