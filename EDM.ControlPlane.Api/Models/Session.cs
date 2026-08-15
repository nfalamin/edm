using System;
using System.Collections.Generic;

namespace EDM.ControlPlane.Api.Models
{
    public class Session
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public Guid DeviceId { get; set; }
        public Guid FamilyId { get; set; } = Guid.NewGuid(); // Token family root identifier
        public string AccessTokenHash { get; set; } = string.Empty; // SHA-256 hash of active token
        public string? CoarseIpAddress { get; set; } // Masked client IP (/24 or /48)
        public string UserAgent { get; set; } = string.Empty;
        public bool IsRevoked { get; set; } = false;
        public string? RevocationReason { get; set; } // e.g. "USER_LOGOUT", "REUSE_DETECTED", "PASSWORD_CHANGED"
        public DateTime LastActivityAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? RevokedAtUtc { get; set; }

        // Navigation
        public User? User { get; set; }
        public Device? Device { get; set; }
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    }
}
