using System;

namespace EDM.ControlPlane.Api.Models
{
    public class UserPasskey
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string CredentialId { get; set; } = string.Empty;
        public string PublicKey { get; set; } = string.Empty;
        public uint SignCount { get; set; } = 0;
        public string DeviceName { get; set; } = "Passkey / Security Key";
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAtUtc { get; set; }

        // Navigation
        public User? User { get; set; }
    }
}
