using System;
using System.Collections.Generic;

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
        public string Title { get; set; } = string.Empty;
        public string MediaUrl { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public MediaType Category { get; set; } = MediaType.Video;
        public long EstimatedSizeBytes { get; set; } = -1;
        public string Quality { get; set; } = "Unknown";
        public string Format { get; set; } = "mp4";
        public string SourcePage { get; set; } = string.Empty;
        public bool RequiresAuth { get; set; } = false;
        public byte[]? EncryptedCookies { get; set; }
        public byte[]? EncryptedAuthHeader { get; set; }
        public Dictionary<string, string> CustomHeaders { get; set; } = new();
        public bool IsLive { get; set; } = false;
        public bool IsDrmProtected { get; set; } = false;
        public string DrmSystem { get; set; } = string.Empty;
        public string DownloadState { get; set; } = "Detected";
        public bool Selected { get; set; } = true;
    }
}

