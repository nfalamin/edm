using System;
using System.Collections.Generic;

namespace EDM.ControlPlane.Api.Models
{
    public class ReleaseArtifact
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ReleaseId { get; set; }
        public string ArtifactName { get; set; } = string.Empty; // e.g. "EDM_Setup.exe", "EDM-v2.0.0-Portable.zip"
        public string Architecture { get; set; } = "x64"; // "x64", "arm64", "x86"
        public string DownloadUrl { get; set; } = string.Empty;
        public string Sha256Hash { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string StorageProvider { get; set; } = "local";
        public string? StoragePath { get; set; }
        public string? SignatureBase64 { get; set; } // RSA signature of artifact/manifest
        public long DownloadCount { get; set; } = 0;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public Release? Release { get; set; }
        public ICollection<DownloadRecord> DownloadRecords { get; set; } = new List<DownloadRecord>();
    }
}
