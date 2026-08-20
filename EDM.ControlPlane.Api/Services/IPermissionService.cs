using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Services
{
    public interface IPermissionService
    {
        Task<bool> HasPermissionAsync(Guid userId, string permissionCode);
        Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(Guid userId);
        Task<bool> GrantUserPermissionAsync(Guid userId, string permissionCode, Guid? adminActorId = null);
        Task<bool> RevokeUserPermissionAsync(Guid userId, string permissionCode, Guid? adminActorId = null);
        Task EnsureDefaultRolePermissionsAsync();
    }

    public class PermissionService : IPermissionService
    {
        private readonly ControlPlaneDbContext _dbContext;
        private readonly IAuditLoggingService _auditLogger;

        public PermissionService(ControlPlaneDbContext dbContext, IAuditLoggingService auditLogger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        public async Task<bool> HasPermissionAsync(Guid userId, string permissionCode)
        {
            if (string.IsNullOrWhiteSpace(permissionCode)) return false;

            var user = await _dbContext.Users
                .Include(u => u.PermissionOverrides)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || !user.IsActive) return false;

            // 1. Super Admin possesses wildcard bypass for all permissions
            if (user.Role == UserRole.SUPER_ADMIN) return true;

            // 2. Direct user override evaluation (explicit grant or explicit revoke)
            var directOverride = user.PermissionOverrides.FirstOrDefault(o => o.PermissionCode == permissionCode);
            if (directOverride != null)
            {
                return directOverride.IsGranted;
            }

            // 3. Role-based default permission mapping from DB
            var hasRolePerm = await _dbContext.RolePermissions
                .AnyAsync(r => r.Role == user.Role && (r.PermissionCode == permissionCode || r.PermissionCode == Permissions.All));

            if (hasRolePerm) return true;

            // 4. Built-in hardcoded fallback matrix if DB role permissions are not yet seeded
            return EvaluateBuiltinRolePermission(user.Role, permissionCode);
        }

        public async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(Guid userId)
        {
            var user = await _dbContext.Users
                .Include(u => u.PermissionOverrides)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || !user.IsActive) return new HashSet<string>();

            if (user.Role == UserRole.SUPER_ADMIN)
            {
                return new HashSet<string>(Permissions.AllPermissions.Append(Permissions.All));
            }

            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Fetch DB role permissions
            var dbRolePerms = await _dbContext.RolePermissions
                .Where(r => r.Role == user.Role)
                .Select(r => r.PermissionCode)
                .ToListAsync();

            if (dbRolePerms.Any())
            {
                foreach (var p in dbRolePerms) result.Add(p);
            }
            else
            {
                foreach (var p in GetBuiltinPermissionsForRole(user.Role)) result.Add(p);
            }

            // Apply user direct overrides
            foreach (var ov in user.PermissionOverrides)
            {
                if (ov.IsGranted) result.Add(ov.PermissionCode);
                else result.Remove(ov.PermissionCode);
            }

            return result;
        }

        public async Task<bool> GrantUserPermissionAsync(Guid userId, string permissionCode, Guid? adminActorId = null)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            var existing = await _dbContext.UserPermissionOverrides
                .FirstOrDefaultAsync(o => o.UserId == userId && o.PermissionCode == permissionCode);

            if (existing != null)
            {
                existing.IsGranted = true;
            }
            else
            {
                _dbContext.UserPermissionOverrides.Add(new UserPermissionOverride
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PermissionCode = permissionCode,
                    IsGranted = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: adminActorId,
                actorUsername: "ADMIN",
                action: "PERMISSION_GRANTED",
                targetEntity: "User",
                targetId: userId.ToString(),
                detailsJson: $"{{\"permission\":\"{permissionCode}\"}}",
                correlationId: Guid.NewGuid().ToString("N"));

            return true;
        }

        public async Task<bool> RevokeUserPermissionAsync(Guid userId, string permissionCode, Guid? adminActorId = null)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            var existing = await _dbContext.UserPermissionOverrides
                .FirstOrDefaultAsync(o => o.UserId == userId && o.PermissionCode == permissionCode);

            if (existing != null)
            {
                existing.IsGranted = false;
            }
            else
            {
                _dbContext.UserPermissionOverrides.Add(new UserPermissionOverride
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PermissionCode = permissionCode,
                    IsGranted = false,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: adminActorId,
                actorUsername: "ADMIN",
                action: "PERMISSION_REVOKED",
                targetEntity: "User",
                targetId: userId.ToString(),
                detailsJson: $"{{\"permission\":\"{permissionCode}\"}}",
                correlationId: Guid.NewGuid().ToString("N"));

            return true;
        }

        public async Task EnsureDefaultRolePermissionsAsync()
        {
            if (await _dbContext.RolePermissions.AnyAsync()) return;

            var mappings = new List<(UserRole Role, string[] Perms)>
            {
                (UserRole.SUPER_ADMIN, new[] { Permissions.All }),
                (UserRole.ADMIN, new[]
                {
                    Permissions.UsersRead, Permissions.UsersManage,
                    Permissions.ReleasesRead, Permissions.ReleasesCreate, Permissions.ReleasesPublish,
                    Permissions.WebsiteManage, Permissions.PricingManage,
                    Permissions.LicensesManage, Permissions.SupportManage,
                    Permissions.AnalyticsRead, Permissions.SettingsManage, Permissions.SecurityManage,
                    Permissions.SystemHealthRead
                }),
                (UserRole.RELEASE_MANAGER, new[]
                {
                    Permissions.ReleasesRead, Permissions.ReleasesCreate, Permissions.ReleasesPublish, Permissions.ReleasesRollback,
                    Permissions.AnalyticsRead, Permissions.SystemHealthRead
                }),
                (UserRole.SUPPORT, new[]
                {
                    Permissions.UsersRead, Permissions.SupportManage, Permissions.LicensesManage, Permissions.AnalyticsRead
                }),
                (UserRole.ANALYST, new[]
                {
                    Permissions.AnalyticsRead, Permissions.ReleasesRead, Permissions.SystemHealthRead
                }),
                (UserRole.USER, Array.Empty<string>())
            };

            foreach (var (role, perms) in mappings)
            {
                foreach (var perm in perms)
                {
                    _dbContext.RolePermissions.Add(new RolePermission
                    {
                        Id = Guid.NewGuid(),
                        Role = role,
                        PermissionCode = perm,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }
            }

            await _dbContext.SaveChangesAsync();
        }

        private static bool EvaluateBuiltinRolePermission(UserRole role, string permission)
        {
            return role switch
            {
                UserRole.SUPER_ADMIN => true,
                UserRole.ADMIN => permission != Permissions.ReleasesRollback, // Rollback requires explicit release manager or superadmin
                UserRole.RELEASE_MANAGER => permission.StartsWith("releases.") || permission == Permissions.AnalyticsRead || permission == Permissions.SystemHealthRead,
                UserRole.SUPPORT => permission == Permissions.UsersRead || permission == Permissions.SupportManage || permission == Permissions.LicensesManage || permission == Permissions.AnalyticsRead,
                UserRole.ANALYST => permission == Permissions.AnalyticsRead || permission == Permissions.ReleasesRead || permission == Permissions.SystemHealthRead,
                _ => false
            };
        }

        private static IEnumerable<string> GetBuiltinPermissionsForRole(UserRole role)
        {
            return role switch
            {
                UserRole.SUPER_ADMIN => Permissions.AllPermissions.Append(Permissions.All),
                UserRole.ADMIN => Permissions.AllPermissions.Where(p => p != Permissions.ReleasesRollback),
                UserRole.RELEASE_MANAGER => new[] { Permissions.ReleasesRead, Permissions.ReleasesCreate, Permissions.ReleasesPublish, Permissions.ReleasesRollback, Permissions.AnalyticsRead, Permissions.SystemHealthRead },
                UserRole.SUPPORT => new[] { Permissions.UsersRead, Permissions.SupportManage, Permissions.LicensesManage, Permissions.AnalyticsRead },
                UserRole.ANALYST => new[] { Permissions.AnalyticsRead, Permissions.ReleasesRead, Permissions.SystemHealthRead },
                _ => Enumerable.Empty<string>()
            };
        }
    }
}
