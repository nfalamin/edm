using System;

namespace EDM.ControlPlane.Api.Models
{
    public class AdminAction
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid AdminUserId { get; set; }
        public string ActionType { get; set; } = string.Empty; // "USER_BAN", "TOKEN_REVOKE", "RELEASE_PUBLISH"
        public Guid? TargetUserId { get; set; }
        public string DetailsJson { get; set; } = "{}";
        public string? CoarseIpAddress { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? AdminUser { get; set; }
    }
}
