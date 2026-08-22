using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Services
{
    public record GeneratedLicenseResult(
        Guid LicenseId,
        string PlaintextKey,
        string KeyPrefix,
        Guid PlanId,
        string PlanName,
        int MaxActivations,
        DateTime? ExpiresAtUtc);

    public record LicenseValidationResult(
        bool IsValid,
        string Message,
        string? ErrorCode = null,
        License? License = null,
        Plan? Plan = null);

    public interface ILicenseService
    {
        Task<GeneratedLicenseResult> GenerateLicenseAsync(Guid planId, Guid? userId = null, int maxActivations = 3, int? durationDays = null, Guid? adminActorId = null);
        Task<LicenseValidationResult> ValidateAndActivateLicenseAsync(string rawLicenseKey, Guid installationId, string? clientIp = null, string? userAgent = null);
        Task<bool> RevokeLicenseAsync(Guid licenseId, string reason, Guid? adminActorId = null);
        Task<bool> SuspendLicenseAsync(Guid licenseId, string reason, Guid? adminActorId = null);
        Task<bool> ReactivateLicenseAsync(Guid licenseId, Guid? adminActorId = null);
        Task<(int TotalCount, List<License> Licenses)> GetLicensesAsync(int page = 1, int pageSize = 50, string? search = null, LicenseStatus? status = null);
        Task<List<Plan>> GetPlansAsync();
    }

    public class LicenseService : ILicenseService
    {
        private readonly ControlPlaneDbContext _dbContext;
        private readonly ITokenService _tokenService;
        private readonly IAuditLoggingService _auditLogger;

        public LicenseService(
            ControlPlaneDbContext dbContext,
            ITokenService tokenService,
            IAuditLoggingService auditLogger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        public async Task<GeneratedLicenseResult> GenerateLicenseAsync(Guid planId, Guid? userId = null, int maxActivations = 3, int? durationDays = null, Guid? adminActorId = null)
        {
            var plan = await _dbContext.Plans.FindAsync(planId);
            if (plan == null) throw new InvalidOperationException($"Plan '{planId}' does not exist.");

            string rawKey = GenerateSecureKeyString(plan.Tier.ToString().ToUpperInvariant());
            string keyHash = _tokenService.HashToken(rawKey);
            string keyPrefix = rawKey.Substring(0, Math.Min(12, rawKey.Length));

            DateTime? expiresAt = durationDays.HasValue ? DateTime.UtcNow.AddDays(durationDays.Value) : null;

            var license = new License
            {
                Id = Guid.NewGuid(),
                LicenseKeyHash = keyHash,
                KeyPrefix = keyPrefix,
                UserId = userId,
                PlanId = planId,
                Status = LicenseStatus.Active,
                MaxActivations = maxActivations,
                CurrentActivations = 0,
                ExpiresAtUtc = expiresAt,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _dbContext.Licenses.Add(license);
            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: adminActorId,
                actorUsername: "ADMIN",
                action: "LICENSE_GENERATED",
                targetEntity: "License",
                targetId: license.Id.ToString(),
                detailsJson: $"{{\"plan\":\"{plan.Name}\",\"prefix\":\"{keyPrefix}\",\"maxActivations\":{maxActivations}}}",
                correlationId: Guid.NewGuid().ToString("N"));

            return new GeneratedLicenseResult(
                LicenseId: license.Id,
                PlaintextKey: rawKey,
                KeyPrefix: keyPrefix,
                PlanId: plan.Id,
                PlanName: plan.Name,
                MaxActivations: maxActivations,
                ExpiresAtUtc: expiresAt);
        }

        public async Task<LicenseValidationResult> ValidateAndActivateLicenseAsync(string rawLicenseKey, Guid installationId, string? clientIp = null, string? userAgent = null)
        {
            if (string.IsNullOrWhiteSpace(rawLicenseKey))
            {
                return new LicenseValidationResult(false, "License key is required.", "INVALID_INPUT");
            }

            string keyHash = _tokenService.HashToken(rawLicenseKey.Trim());
            var license = await _dbContext.Licenses
                .Include(l => l.Plan)
                .FirstOrDefaultAsync(l => l.LicenseKeyHash == keyHash);

            if (license == null)
            {
                return new LicenseValidationResult(false, "License key is unrecognized or invalid.", "LICENSE_NOT_FOUND");
            }

            if (license.Status == LicenseStatus.Revoked)
            {
                return new LicenseValidationResult(false, "This license has been permanently revoked.", "LICENSE_REVOKED");
            }

            if (license.Status == LicenseStatus.Suspended)
            {
                return new LicenseValidationResult(false, "This license is currently suspended.", "LICENSE_SUSPENDED");
            }

            if (license.ExpiresAtUtc.HasValue && license.ExpiresAtUtc.Value < DateTime.UtcNow)
            {
                license.Status = LicenseStatus.Expired;
                await _dbContext.SaveChangesAsync();
                return new LicenseValidationResult(false, "This license has expired.", "LICENSE_EXPIRED");
            }

            // Find device
            var device = await _dbContext.Devices.FirstOrDefaultAsync(d => d.InstallationId == installationId);

            // Record download / activation telemetry
            _dbContext.DownloadRecords.Add(new DownloadRecord
            {
                Id = Guid.NewGuid(),
                LicenseId = license.Id,
                DeviceId = device?.Id,
                ClientIpCoarse = clientIp,
                UserAgent = userAgent,
                Status = DownloadStatus.Completed,
                DownloadedAtUtc = DateTime.UtcNow
            });

            if (license.CurrentActivations < license.MaxActivations)
            {
                license.CurrentActivations += 1;
                license.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();

            return new LicenseValidationResult(
                IsValid: true,
                Message: "License validated and activated successfully.",
                License: license,
                Plan: license.Plan);
        }

        public async Task<bool> RevokeLicenseAsync(Guid licenseId, string reason, Guid? adminActorId = null)
        {
            var license = await _dbContext.Licenses.FindAsync(licenseId);
            if (license == null) return false;

            license.Status = LicenseStatus.Revoked;
            license.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: adminActorId,
                actorUsername: "ADMIN",
                action: "LICENSE_REVOKED",
                targetEntity: "License",
                targetId: licenseId.ToString(),
                detailsJson: $"{{\"reason\":\"{reason}\"}}",
                correlationId: Guid.NewGuid().ToString("N"));

            return true;
        }

        public async Task<bool> SuspendLicenseAsync(Guid licenseId, string reason, Guid? adminActorId = null)
        {
            var license = await _dbContext.Licenses.FindAsync(licenseId);
            if (license == null) return false;

            license.Status = LicenseStatus.Suspended;
            license.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: adminActorId,
                actorUsername: "ADMIN",
                action: "LICENSE_SUSPENDED",
                targetEntity: "License",
                targetId: licenseId.ToString(),
                detailsJson: $"{{\"reason\":\"{reason}\"}}",
                correlationId: Guid.NewGuid().ToString("N"));

            return true;
        }

        public async Task<bool> ReactivateLicenseAsync(Guid licenseId, Guid? adminActorId = null)
        {
            var license = await _dbContext.Licenses.FindAsync(licenseId);
            if (license == null) return false;

            license.Status = LicenseStatus.Active;
            license.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: adminActorId,
                actorUsername: "ADMIN",
                action: "LICENSE_REACTIVATED",
                targetEntity: "License",
                targetId: licenseId.ToString(),
                detailsJson: "{}",
                correlationId: Guid.NewGuid().ToString("N"));

            return true;
        }

        public async Task<(int TotalCount, List<License> Licenses)> GetLicensesAsync(int page = 1, int pageSize = 50, string? search = null, LicenseStatus? status = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var query = _dbContext.Licenses
                .Include(l => l.Plan)
                .Include(l => l.User)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(l => l.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                query = query.Where(l => l.KeyPrefix.Contains(s) || (l.User != null && (l.User.Email.Contains(s) || l.User.Username.Contains(s))));
            }

            int total = await query.CountAsync();
            var list = await query
                .OrderByDescending(l => l.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (total, list);
        }

        public async Task<List<Plan>> GetPlansAsync()
        {
            return await _dbContext.Plans.OrderBy(p => p.Tier).ToListAsync();
        }

        private static string GenerateSecureKeyString(string prefix)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var chunks = new string[4];
            for (int i = 0; i < 4; i++)
            {
                var bytes = RandomNumberGenerator.GetBytes(4);
                var sb = new StringBuilder(4);
                for (int j = 0; j < 4; j++)
                {
                    sb.Append(chars[bytes[j] % chars.Length]);
                }
                chunks[i] = sb.ToString();
            }

            return $"EDM-{prefix}-{string.Join("-", chunks)}";
        }
    }
}
