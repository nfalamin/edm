using System;

namespace EDM.ControlPlane.Api.Models
{
    public enum AnnouncementSeverity
    {
        Info,
        Warning,
        Critical,
        Maintenance
    }

    public enum TargetAudience
    {
        All,
        FreeUsers,
        ProUsers,
        Admins
    }

    public class Announcement
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public AnnouncementSeverity Severity { get; set; } = AnnouncementSeverity.Info;
        public TargetAudience Audience { get; set; } = TargetAudience.All;
        public ClientType? TargetPlatform { get; set; } // Null for all platforms
        public DateTime StartsAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? EndsAtUtc { get; set; }
        public bool IsDismissible { get; set; } = true;
        public string? ActionUrl { get; set; }
        public string? ActionText { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public enum NotificationType
    {
        SecurityAlert,
        NewRelease,
        SystemIssue,
        BillingEvent,
        UserAction
    }

    public class AdminNotification
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? UserId { get; set; } // Null for all admins
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; } = NotificationType.SystemIssue;
        public bool IsRead { get; set; } = false;
        public string? LinkUrl { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
    }
}
