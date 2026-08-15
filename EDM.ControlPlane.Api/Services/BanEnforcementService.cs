using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Services
{
    public interface IBanEnforcementService
    {
        Task<bool> IsUserBannedAsync(Guid userId);
        Task<bool> IsInstallationBannedAsync(Guid installationId);
        Task<bool> IsIpBannedAsync(string ipAddress);
        Task<bool> IsRequestBannedAsync(Guid? userId, Guid? installationId, string? ipAddress);
        Task BanTargetAsync(BanTargetType targetType, string targetValue, string reason, string bannedBy, DateTime? expiresAtUtc = null);
        Task UnbanTargetAsync(BanTargetType targetType, string targetValue);
    }

    public class BanEnforcementService : IBanEnforcementService
    {
        private readonly ControlPlaneDbContext _dbContext;

        public BanEnforcementService(ControlPlaneDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<bool> IsUserBannedAsync(Guid userId)
        {
            string userStr = userId.ToString();
            var now = DateTime.UtcNow;
            return await _dbContext.Bans.AnyAsync(b =>
                b.IsActive &&
                b.TargetType == BanTargetType.UserId &&
                b.TargetValue == userStr &&
                (b.ExpiresAtUtc == null || b.ExpiresAtUtc > now));
        }

        public async Task<bool> IsInstallationBannedAsync(Guid installationId)
        {
            string installStr = installationId.ToString();
            var now = DateTime.UtcNow;
            return await _dbContext.Bans.AnyAsync(b =>
                b.IsActive &&
                b.TargetType == BanTargetType.InstallationId &&
                b.TargetValue == installStr &&
                (b.ExpiresAtUtc == null || b.ExpiresAtUtc > now));
        }

        public async Task<bool> IsIpBannedAsync(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress)) return false;
            var now = DateTime.UtcNow;
            return await _dbContext.Bans.AnyAsync(b =>
                b.IsActive &&
                b.TargetType == BanTargetType.IpRange &&
                b.TargetValue == ipAddress &&
                (b.ExpiresAtUtc == null || b.ExpiresAtUtc > now));
        }

        public async Task<bool> IsRequestBannedAsync(Guid? userId, Guid? installationId, string? ipAddress)
        {
            if (userId.HasValue && await IsUserBannedAsync(userId.Value)) return true;
            if (installationId.HasValue && await IsInstallationBannedAsync(installationId.Value)) return true;
            if (!string.IsNullOrWhiteSpace(ipAddress) && await IsIpBannedAsync(ipAddress)) return true;
            return false;
        }

        public async Task BanTargetAsync(BanTargetType targetType, string targetValue, string reason, string bannedBy, DateTime? expiresAtUtc = null)
        {
            if (string.IsNullOrWhiteSpace(targetValue)) throw new ArgumentNullException(nameof(targetValue));

            var ban = new Ban
            {
                Id = Guid.NewGuid(),
                TargetType = targetType,
                TargetValue = targetValue.Trim(),
                Reason = reason,
                BannedBy = bannedBy,
                IsActive = true,
                ExpiresAtUtc = expiresAtUtc,
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.Bans.Add(ban);

            // If banning a user, revoke all their active sessions
            if (targetType == BanTargetType.UserId && Guid.TryParse(targetValue, out var uId))
            {
                var activeSessions = await _dbContext.Sessions
                    .Where(s => s.UserId == uId && !s.IsRevoked)
                    .ToListAsync();

                foreach (var session in activeSessions)
                {
                    session.IsRevoked = true;
                    session.RevocationReason = "ACCOUNT_BANNED";
                    session.RevokedAtUtc = DateTime.UtcNow;
                }
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task UnbanTargetAsync(BanTargetType targetType, string targetValue)
        {
            if (string.IsNullOrWhiteSpace(targetValue)) throw new ArgumentNullException(nameof(targetValue));

            var activeBans = await _dbContext.Bans
                .Where(b => b.TargetType == targetType && b.TargetValue == targetValue.Trim() && b.IsActive)
                .ToListAsync();

            foreach (var ban in activeBans)
            {
                ban.IsActive = false;
            }

            await _dbContext.SaveChangesAsync();
        }
    }
}
