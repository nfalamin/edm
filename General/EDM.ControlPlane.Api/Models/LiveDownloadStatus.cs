using System;

namespace EDM.ControlPlane.Api.Models
{
    public class LiveDownloadStatus
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public Guid DeviceId { get; set; }
        public string DownloadId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public long TotalBytes { get; set; } = 0;
        public long DownloadedBytes { get; set; } = 0;
        public double ProgressPercentage { get; set; } = 0.0;
        public double SpeedBytesPerSecond { get; set; } = 0.0;
        public long? EtaSeconds { get; set; }
        public string Status { get; set; } = "Queued"; // Downloading, Paused, Completed, Failed, Queued, Stopped
        public string? ErrorMessage { get; set; }
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
        public Device? Device { get; set; }
    }
}
