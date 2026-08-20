using System;

namespace EDM.ControlPlane.Api.Models
{
    public enum RemoteCommandType
    {
        StartDownload,
        PauseDownload,
        ResumeDownload,
        CancelDownload,
        RetryDownload,
        DeleteDownload,
        AddUrl,
        QueueControl,
        SpeedLimit
    }

    public enum RemoteCommandStatus
    {
        Pending,
        Received,
        Executing,
        Completed,
        Failed,
        Expired
    }

    public class RemoteCommand
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public Guid DeviceId { get; set; }
        public RemoteCommandType CommandType { get; set; }
        public string? TargetDownloadId { get; set; }
        public string? PayloadJson { get; set; }
        public RemoteCommandStatus Status { get; set; } = RemoteCommandStatus.Pending;
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? AcknowledgedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddHours(24);

        // Navigation
        public User? User { get; set; }
        public Device? Device { get; set; }
    }
}
