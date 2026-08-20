using System;

namespace EDM.ControlPlane.Api.Models
{
    public class RolePermission
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public UserRole Role { get; set; }
        public string PermissionCode { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class UserPermissionOverride
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string PermissionCode { get; set; } = string.Empty;
        public bool IsGranted { get; set; } = true; // true = grant, false = revoke
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
    }
}
