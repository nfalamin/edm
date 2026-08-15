using System;

namespace EDM.ControlPlane.Api.Models
{
    public class FeatureEntitlement
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string FeatureCode { get; set; } = string.Empty; // e.g. "MAX_SEGMENTS_32", "TURBO_VPN", "BATCH_UNLIMITED"
        public bool IsEnabled { get; set; } = true;
        public DateTime? ExpiresAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
    }
}
