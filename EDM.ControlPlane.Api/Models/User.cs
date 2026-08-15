using System;
using System.Collections.Generic;

namespace EDM.ControlPlane.Api.Models
{
    public enum UserRole
    {
        USER,
        ANALYST,
        SUPPORT,
        RELEASE_MANAGER,
        ADMIN,
        SUPER_ADMIN
    }

    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.USER;
        public bool IsActive { get; set; } = true;
        public bool IsEmailVerified { get; set; } = false;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Session> Sessions { get; set; } = new List<Session>();
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<FeatureEntitlement> FeatureEntitlements { get; set; } = new List<FeatureEntitlement>();
        public ICollection<AdminAction> AdminActions { get; set; } = new List<AdminAction>();
    }
}
