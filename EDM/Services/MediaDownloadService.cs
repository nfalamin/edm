using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EDM.Models;

namespace EDM.Services
{
    public class MediaDownloadService
    {
        private readonly ConcurrentDictionary<string, MediaDownloadItem> _detectedItems = new(StringComparer.OrdinalIgnoreCase);

        public event Action<MediaDownloadItem>? MediaDetected;

        public IReadOnlyCollection<MediaDownloadItem> GetDetectedMedia() => _detectedItems.Values.ToList();

        public bool TryRegisterMedia(string mediaUrl, string mimeType, string sourcePage, long sizeBytes = -1, string quality = "Unknown", bool requiresAuth = false)
        {
            if (string.IsNullOrWhiteSpace(mediaUrl)) return false;

            // Reject DRM stream URLs explicitly
            if (mediaUrl.Contains("widevine", StringComparison.OrdinalIgnoreCase) || mediaUrl.Contains("playready", StringComparison.OrdinalIgnoreCase))
            {
                LoggingService.Log($"[MediaDownloadService] Rejecting DRM stream: {mediaUrl}");
                return false;
            }

            var category = CategorizeMime(mimeType, mediaUrl);
            var ext = Path.GetExtension(mediaUrl).TrimStart('.').ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = GetExtensionFromMime(mimeType);

            var item = new MediaDownloadItem
            {
                MediaUrl = mediaUrl,
                MimeType = mimeType,
                Category = category,
                EstimatedSizeBytes = sizeBytes,
                Quality = quality,
                Format = ext,
                SourcePage = sourcePage,
                RequiresAuth = requiresAuth,
                DownloadState = "Detected",
                Selected = true
            };

            if (_detectedItems.TryAdd(mediaUrl, item))
            {
                MediaDetected?.Invoke(item);
                return true;
            }
            return false;
        }

        public void Clear()
        {
            _detectedItems.Clear();
        }

        public static MediaType CategorizeMime(string mimeType, string url)
        {
            if (string.IsNullOrWhiteSpace(mimeType)) mimeType = "";
            mimeType = mimeType.ToLowerInvariant();
            url = url.ToLowerInvariant();

            if (url.Contains(".m3u8") || url.Contains(".mpd") || mimeType.Contains("application/vnd.apple.mpegurl") || mimeType.Contains("application/dash+xml"))
                return MediaType.Manifest;
            if (mimeType.StartsWith("video/") || url.Contains(".mp4") || url.Contains(".mkv") || url.Contains(".webm") || url.Contains(".avi"))
                return MediaType.Video;
            if (mimeType.StartsWith("audio/") || url.Contains(".mp3") || url.Contains(".aac") || url.Contains(".wav") || url.Contains(".flac"))
                return MediaType.Audio;
            if (mimeType.StartsWith("image/") || url.Contains(".jpg") || url.Contains(".png") || url.Contains(".webp") || url.Contains(".gif"))
                return MediaType.Image;
            if (url.Contains(".vtt") || url.Contains(".srt") || mimeType.Contains("text/vtt"))
                return MediaType.Subtitle;

            return MediaType.Video;
        }

        public static string GetExtensionFromMime(string mime)
        {
            if (string.IsNullOrWhiteSpace(mime)) return "mp4";
            mime = mime.Split(';')[0].Trim().ToLowerInvariant();
            return mime switch
            {
                "video/mp4" => "mp4",
                "video/webm" => "webm",
                "video/x-matroska" => "mkv",
                "audio/mpeg" => "mp3",
                "audio/aac" => "aac",
                "audio/flac" => "flac",
                "image/jpeg" => "jpg",
                "image/png" => "png",
                "image/webp" => "webp",
                _ => "mp4"
            };
        }
    }
}
