using System;

namespace EDM.ControlPlane.Api.Models
{
    public class RefreshToken
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
        public Guid DeviceId { get; set; }
        public Guid FamilyId { get; set; } // Ties all tokens in a rotation lineage
        public string TokenHash { get; set; } = string.Empty; // SHA-256 hash of plaintext token
        public bool IsUsed { get; set; } = false; // Set to true when successfully rotated
        public bool IsRevoked { get; set; } = false;
        public string? ReplacedByTokenHash { get; set; } // Link to next token in rotation chain
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UsedAtUtc { get; set; }
        public DateTime? RevokedAtUtc { get; set; }

        // Navigation
        public Session? Session { get; set; }
        public User? User { get; set; }
        public Device? Device { get; set; }
    }
}
