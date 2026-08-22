using System;

namespace EDM.ControlPlane.Api.Models
{
    public enum BanTargetType
    {
        UserId,
        InstallationId,
        IpRange
    }

    public class Ban
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public BanTargetType TargetType { get; set; } = BanTargetType.InstallationId;
        public string TargetValue { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string BannedBy { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime? ExpiresAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
