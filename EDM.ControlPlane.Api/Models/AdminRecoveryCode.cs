using System;

namespace EDM.ControlPlane.Api.Models
{
    public class AdminRecoveryCode
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string CodeHash { get; set; } = string.Empty;
        public bool IsUsed { get; set; } = false;
        public DateTime? UsedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
    }
}
