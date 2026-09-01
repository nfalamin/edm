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

        public bool TwoFactorEnabled { get; set; } = false;
        public string? TwoFactorSecret { get; set; }
        public string? RecoveryEmail { get; set; }
        public bool IsRecoveryEmailVerified { get; set; } = false;
        public string? PendingRecoveryEmail { get; set; }
        public string? RecoveryEmailTokenHash { get; set; }
        public DateTime? RecoveryEmailTokenExpiresAtUtc { get; set; }
        public string? GoogleSubjectId { get; set; }
        public string? FirebaseUid { get; set; }
        public string? DisplayName { get; set; }
        public string? PhotoUrl { get; set; }
        public string? PasswordResetTokenHash { get; set; }
        public DateTime? PasswordResetExpiresAtUtc { get; set; }
        public bool MustChangePassword { get; set; } = false;

        // Navigation
        public ICollection<Session> Sessions { get; set; } = new List<Session>();
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<FeatureEntitlement> FeatureEntitlements { get; set; } = new List<FeatureEntitlement>();
        public ICollection<AdminAction> AdminActions { get; set; } = new List<AdminAction>();
        public ICollection<AdminRecoveryCode> RecoveryCodes { get; set; } = new List<AdminRecoveryCode>();
        public ICollection<UserPasskey> Passkeys { get; set; } = new List<UserPasskey>();
        public ICollection<License> Licenses { get; set; } = new List<License>();
        public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public ICollection<SupportTicket> SupportTickets { get; set; } = new List<SupportTicket>();
        public ICollection<UserPermissionOverride> PermissionOverrides { get; set; } = new List<UserPermissionOverride>();
        public ICollection<AdminNotification> Notifications { get; set; } = new List<AdminNotification>();
    }
}
