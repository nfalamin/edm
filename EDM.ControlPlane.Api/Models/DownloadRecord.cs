using System;

namespace EDM.ControlPlane.Api.Models
{
    public enum DownloadStatus
    {
        Completed,
        Interrupted,
        Failed,
        Cancelled
    }

    public class DownloadRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? ReleaseArtifactId { get; set; }
        public Guid? LicenseId { get; set; }
        public Guid? DeviceId { get; set; }
        public string? ClientIpCoarse { get; set; } // Anonymized / subnet-level
        public string? CountryCode { get; set; }
        public string? UserAgent { get; set; }
        public string? ReleaseVersion { get; set; }
        public string? OperatingSystem { get; set; }
        public string? Browser { get; set; }
        public string? DeviceCategory { get; set; }
        public string? Referrer { get; set; }
        public Guid? UserId { get; set; }
        public string? Url { get; set; }
        public string? Host { get; set; }
        public string? FileName { get; set; }
        public string? Category { get; set; }
        public string? Sha256Hash { get; set; }
        public long BytesTransferred { get; set; } = 0;
        public int ConnectionsCount { get; set; } = 1;
        public int RetryCount { get; set; } = 0;
        public int? HttpStatusCode { get; set; } = 200;
        public double SpeedBytesPerSecond { get; set; } = 0;
        public long DurationSeconds { get; set; } = 0;
        public DownloadStatus Status { get; set; } = DownloadStatus.Completed;
        public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime DownloadedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public ReleaseArtifact? ReleaseArtifact { get; set; }
        public License? License { get; set; }
        public Device? Device { get; set; }
        public User? User { get; set; }
    }
}
