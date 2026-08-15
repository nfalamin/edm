using System;

namespace EDM.Models
{
    public enum MediaType
    {
        Video,
        Audio,
        Image,
        Subtitle,
        Manifest
    }

    public class MediaDownloadItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string MediaUrl { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public MediaType Category { get; set; } = MediaType.Video;
        public long EstimatedSizeBytes { get; set; } = -1;
        public string Quality { get; set; } = "Unknown";
        public string Format { get; set; } = "mp4";
        public string SourcePage { get; set; } = string.Empty;
        public bool RequiresAuth { get; set; } = false;
        public string DownloadState { get; set; } = "Detected";
        public bool Selected { get; set; } = true;
    }
}
