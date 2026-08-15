using System;

namespace EDM.ControlPlane.Api.Models
{
    public class AuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? ActorId { get; set; } // Admin User Id or null for system
        public string ActorUsername { get; set; } = "SYSTEM";
        public string Action { get; set; } = string.Empty; // e.g. "USER_BANNED", "RELEASE_PUBLISHED", "POLICY_UPDATED"
        public string TargetEntity { get; set; } = string.Empty; // e.g. "User", "Release", "UpdatePolicy"
        public string? TargetId { get; set; }
        public string DetailsJson { get; set; } = "{}"; // Redacted audit metadata
        public string CorrelationId { get; set; } = string.Empty;
        public string ResultStatus { get; set; } = "SUCCESS"; // "SUCCESS", "FAILURE", "DENIED"
        public string? CoarseIpAddress { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
