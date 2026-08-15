using System;

namespace EDM.ControlPlane.Api.Models
{
    public class UpdatePolicy
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ClientType Platform { get; set; } = ClientType.DesktopWindows;
        public string Channel { get; set; } = "stable"; // "stable", "beta", "nightly"
        public int RolloutPercentage { get; set; } = 100; // 0 to 100
        public string TargetVersion { get; set; } = string.Empty;
        public string MinimumSupportedVersion { get; set; } = "1.0.0";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
