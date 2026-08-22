using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Services
{
    public interface ISubscriptionEntitlementService
    {
        Task<EntitlementPolicyDto> EvaluateAndSyncEntitlementAsync(EntitlementSyncRequestDto request);
        Task<GlobalSubscriptionConfigRecord> GetGlobalConfigAsync();
        Task<GlobalSubscriptionConfigRecord> UpdateGlobalConfigAsync(GlobalSubscriptionConfigRecord config, string adminUsername);
        Task<bool> SetGlobalSubscriptionStatusAsync(bool isEnabled, string reason, string adminUsername);
        Task<bool> SetAsiaSubscriptionStatusAsync(bool isEnabled, string reason, string adminUsername);
        Task<SubscriptionPolicyRecord?> GetPolicyByInstallationIdAsync(Guid installationId);
        Task<List<SubscriptionPolicyRecord>> GetAllSubscriptionPoliciesAsync(int page = 1, int pageSize = 50);
        Task<AdminOverrideRecord> ApplyAdminOverrideAsync(AdminOverrideRecord overrideRecord, string adminUsername);
        Task<bool> RemoveAdminOverrideAsync(Guid overrideId, string adminUsername);
        Task<bool> ExtendTrialAsync(Guid installationId, int additionalDays, string reason, string adminUsername = "SuperAdmin");
        Task<bool> ExtendGracePeriodAsync(Guid installationId, int additionalDays, string reason, string adminUsername = "SuperAdmin");
        Task<bool> SetDeviceBlockStatusAsync(Guid installationId, bool isBlocked, string? reason = null, string adminUsername = "SuperAdmin");
        Task<bool> SetUserBlockStatusAsync(Guid userId, bool isBlocked, string? reason = null, string adminUsername = "SuperAdmin");
        Task<List<AdminOverrideRecord>> GetActiveOverridesAsync();
        string ComputePolicySignature(Guid installationId, string state, int maxConnections, DateTime? expiresAtUtc, DateTime serverTimeUtc);
    }

    public class SubscriptionEntitlementService : ISubscriptionEntitlementService
    {
        private const string SIGNING_SECRET = "EDM_CONTROL_PLANE_SECRET_SIGNING_KEY_2026_V210";
        private readonly ControlPlaneDbContext _dbContext;
        private readonly IGeoPricingService _geoPricingService;
        private readonly IAuditLoggingService _auditLogger;

        public SubscriptionEntitlementService(
            ControlPlaneDbContext dbContext,
            IGeoPricingService geoPricingService,
            IAuditLoggingService auditLogger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _geoPricingService = geoPricingService ?? throw new ArgumentNullException(nameof(geoPricingService));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        public async Task<GlobalSubscriptionConfigRecord> GetGlobalConfigAsync()
        {
            var config = await _dbContext.GlobalSubscriptionConfigs.FirstOrDefaultAsync();
            if (config != null) return config;

            var newConfig = new GlobalSubscriptionConfigRecord();
            _dbContext.GlobalSubscriptionConfigs.Add(newConfig);
            await _dbContext.SaveChangesAsync();
            return newConfig;
        }

        public async Task<GlobalSubscriptionConfigRecord> UpdateGlobalConfigAsync(GlobalSubscriptionConfigRecord updated, string adminUsername)
        {
            var config = await GetGlobalConfigAsync();
            config.IsGlobalSubscriptionEnabled = updated.IsGlobalSubscriptionEnabled;
            config.IsAsiaSubscriptionEnabled = updated.IsAsiaSubscriptionEnabled;
            config.IsTrialEnabled = updated.IsTrialEnabled;
            config.DefaultTrialDurationDays = updated.DefaultTrialDurationDays;
            config.IsGracePeriodEnabled = updated.IsGracePeriodEnabled;
            config.DefaultGraceDurationDays = updated.DefaultGraceDurationDays;
            config.OfflineGraceHours = updated.OfflineGraceHours;
            config.MaxTurboConnections = updated.MaxTurboConnections;
            config.MaxGraceConnections = updated.MaxGraceConnections;
            config.MaxRestrictedConnections = updated.MaxRestrictedConnections;
            config.PaymentSystemEnabled = updated.PaymentSystemEnabled;
            config.PaymentProvider = updated.PaymentProvider;
            config.IsTestMode = updated.IsTestMode;
            config.SupportedCurrencies = updated.SupportedCurrencies;
            config.GlobalFeaturesJson = updated.GlobalFeaturesJson;
            config.UpdatedAtUtc = DateTime.UtcNow;
            config.UpdatedByUsername = adminUsername;

            await _dbContext.SaveChangesAsync();
            await _auditLogger.LogActionAsync(null, adminUsername, "GLOBAL_CONFIG_UPDATED", "GlobalSubscriptionConfig", config.Id.ToString(), "Updated global subscription configuration", Guid.NewGuid().ToString());
            return config;
        }

        public async Task<bool> SetGlobalSubscriptionStatusAsync(bool isEnabled, string reason, string adminUsername)
        {
            var config = await GetGlobalConfigAsync();
            config.IsGlobalSubscriptionEnabled = isEnabled;
            config.UpdatedAtUtc = DateTime.UtcNow;
            config.UpdatedByUsername = adminUsername;

            await _dbContext.SaveChangesAsync();
            string action = isEnabled ? "GLOBAL_SUBSCRIPTION_ENABLED" : "GLOBAL_SUBSCRIPTION_DISABLED";
            await _auditLogger.LogActionAsync(null, adminUsername, action, "GlobalSubscriptionConfig", config.Id.ToString(), $"Global subscription switch set to {isEnabled}. Reason: {reason}", Guid.NewGuid().ToString());
            return true;
        }

        public async Task<bool> SetAsiaSubscriptionStatusAsync(bool isEnabled, string reason, string adminUsername)
        {
            var config = await GetGlobalConfigAsync();
            config.IsAsiaSubscriptionEnabled = isEnabled;
            config.UpdatedAtUtc = DateTime.UtcNow;
            config.UpdatedByUsername = adminUsername;

            await _dbContext.SaveChangesAsync();
            string action = isEnabled ? "ASIA_SUBSCRIPTION_ENABLED" : "ASIA_SUBSCRIPTION_DISABLED";
            await _auditLogger.LogActionAsync(null, adminUsername, action, "GlobalSubscriptionConfig", config.Id.ToString(), $"Asia subscription switch set to {isEnabled}. Reason: {reason}", Guid.NewGuid().ToString());
            return true;
        }

        public async Task<EntitlementPolicyDto> EvaluateAndSyncEntitlementAsync(EntitlementSyncRequestDto request)
        {
            var now = DateTime.UtcNow;
            var globalConfig = await GetGlobalConfigAsync();

            string country = _geoPricingService.DetectCountryFromHeadersOrIp(request.ClientIp, null);
            string region = _geoPricingService.DetectRegionForCountry(country);
            var pricing = await _geoPricingService.GetPricingRuleForCountryAsync(country);

            // Determine if subscription sales are available in this territory
            bool isSubscriptionAvailable = globalConfig.IsGlobalSubscriptionEnabled;
            if (isSubscriptionAvailable && (region == "Asia" || region == "South Asia") && !globalConfig.IsAsiaSubscriptionEnabled)
            {
                isSubscriptionAvailable = false;
            }
            if (isSubscriptionAvailable && !pricing.IsSubscriptionEnabled)
            {
                isSubscriptionAvailable = false;
            }

            var record = await _dbContext.SubscriptionPolicies
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.InstallationId == request.InstallationId);

            if (record == null)
            {
                int trialDays = globalConfig.IsTrialEnabled ? globalConfig.DefaultTrialDurationDays : 0;
                int graceDays = globalConfig.IsGracePeriodEnabled ? globalConfig.DefaultGraceDurationDays : 0;

                record = new SubscriptionPolicyRecord
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    InstallationId = request.InstallationId,
                    CurrentState = trialDays > 0 ? SubscriptionState.TRIAL_ACTIVE : SubscriptionState.FREE_RESTRICTED,
                    TrialStartedAtUtc = now,
                    TrialEndsAtUtc = now.AddDays(trialDays),
                    GraceEndsAtUtc = now.AddDays(trialDays + graceDays),
                    MaxConnections = trialDays > 0 ? globalConfig.MaxTurboConnections : globalConfig.MaxRestrictedConnections,
                    CoarseCountryCode = country,
                    LastSyncedAtUtc = now
                };
                _dbContext.SubscriptionPolicies.Add(record);
            }
            else
            {
                record.LastSyncedAtUtc = now;
                record.CoarseCountryCode = country;
                if (request.UserId.HasValue && record.UserId != request.UserId)
                {
                    record.UserId = request.UserId;
                }
            }

            // Check Admin Overrides
            var activeOverride = await _dbContext.AdminOverrides
                .AsNoTracking()
                .Where(o => o.IsActive && (o.ExpiresAtUtc == null || o.ExpiresAtUtc > now))
                .FirstOrDefaultAsync(o =>
                    (o.TargetType == OverrideTargetType.Device && o.TargetValue == request.InstallationId.ToString()) ||
                    (request.UserId.HasValue && o.TargetType == OverrideTargetType.User && o.TargetValue == request.UserId.ToString()));

            // User or Device Ban Check
            bool isUserBlocked = false;
            string? blockReason = record.BlockReason;

            if (request.UserId.HasValue)
            {
                var user = await _dbContext.Users.FindAsync(request.UserId.Value);
                if (user != null && !user.IsActive)
                {
                    isUserBlocked = true;
                    blockReason = "User account suspended by administrator.";
                }
            }

            bool isDeviceBlocked = record.IsBlocked;

            // State Machine Evaluation
            SubscriptionState finalState;
            int finalMaxConnections;
            string statusMessage;
            var featureFlags = new Dictionary<string, bool>();

            if (activeOverride?.ForceUnblock == true)
            {
                isDeviceBlocked = false;
                isUserBlocked = false;
            }

            if (isDeviceBlocked || isUserBlocked)
            {
                finalState = SubscriptionState.BLOCKED;
                finalMaxConnections = 0;
                statusMessage = blockReason ?? "Your access is blocked by administrator policy.";
                SetFeatures(featureFlags, false);
            }
            else if (activeOverride != null && activeOverride.OverrideState.HasValue)
            {
                finalState = activeOverride.OverrideState.Value;
                finalMaxConnections = activeOverride.OverrideMaxConnections ?? globalConfig.MaxTurboConnections;
                statusMessage = $"Admin Override Active: {activeOverride.Reason}";
                SetFeatures(featureFlags, true);
            }
            else if (record.SubscriptionExpiresAtUtc.HasValue && record.SubscriptionExpiresAtUtc.Value > now)
            {
                finalState = SubscriptionState.SUBSCRIBED;
                finalMaxConnections = globalConfig.MaxTurboConnections;
                statusMessage = "Your Pro subscription is active.";
                SetFeatures(featureFlags, true);
            }
            else if (now <= record.TrialEndsAtUtc && globalConfig.IsTrialEnabled)
            {
                finalState = SubscriptionState.TRIAL_ACTIVE;
                finalMaxConnections = globalConfig.MaxTurboConnections;
                int daysLeft = Math.Max(0, (int)Math.Ceiling((record.TrialEndsAtUtc - now).TotalDays));
                statusMessage = $"Your free trial is active — {daysLeft} day{(daysLeft == 1 ? "" : "s")} remaining.";
                SetFeatures(featureFlags, true);
            }
            else if (now <= record.GraceEndsAtUtc && globalConfig.IsGracePeriodEnabled)
            {
                finalState = SubscriptionState.GRACE_PERIOD;
                finalMaxConnections = globalConfig.MaxGraceConnections;
                int graceLeft = Math.Max(0, (int)Math.Ceiling((record.GraceEndsAtUtc - now).TotalDays));
                statusMessage = $"Your trial has ended. Running in 5-day grace period ({graceLeft} day{(graceLeft == 1 ? "" : "s")} left). Upgrade to restore 64 sockets.";
                SetFeatures(featureFlags, true);
            }
            else
            {
                finalState = SubscriptionState.FREE_RESTRICTED;
                finalMaxConnections = globalConfig.MaxRestrictedConnections;
                statusMessage = "Free Mode: Operating at 16 connections limit. Upgrade to Pro for unlimited speeds.";
                SetFeatures(featureFlags, false);
            }

            record.CurrentState = finalState;
            record.MaxConnections = finalMaxConnections;
            record.UpdatedAtUtc = now;
            await _dbContext.SaveChangesAsync();

            int trialDaysRemaining = Math.Max(0, (int)Math.Ceiling((record.TrialEndsAtUtc - now).TotalDays));
            int graceDaysRemaining = Math.Max(0, (int)Math.Ceiling((record.GraceEndsAtUtc - now).TotalDays));

            DateTime? targetExpiry = record.SubscriptionExpiresAtUtc ?? (finalState == SubscriptionState.TRIAL_ACTIVE ? record.TrialEndsAtUtc : record.GraceEndsAtUtc);
            string signature = ComputePolicySignature(record.InstallationId, finalState.ToString(), finalMaxConnections, targetExpiry, now);

            return new EntitlementPolicyDto(
                InstallationId: record.InstallationId,
                UserId: record.UserId,
                State: finalState.ToString(),
                PlanCode: record.ActivePlanCode ?? "free_trial",
                PlanTier: finalState == SubscriptionState.SUBSCRIBED ? "Pro" : (finalState == SubscriptionState.TRIAL_ACTIVE ? "Trial" : "Free"),
                MaxConnections: finalMaxConnections,
                MaxConcurrentDownloads: record.MaxConcurrentDownloads,
                TrialDaysRemaining: trialDaysRemaining,
                GraceDaysRemaining: graceDaysRemaining,
                ExpiresAtUtc: targetExpiry,
                FeatureFlags: featureFlags,
                IsBlocked: isDeviceBlocked || isUserBlocked,
                BlockReason: blockReason,
                StatusMessage: statusMessage,
                CountryCode: country,
                Currency: pricing.Currency,
                MonthlyPrice: pricing.MonthlyPrice,
                FormattedPrice: _geoPricingService.FormatPrice(pricing.MonthlyPrice, pricing.Currency, pricing.CurrencySymbol),
                IsSubscriptionAvailable: isSubscriptionAvailable,
                PolicyVersion: record.PolicyVersion,
                OfflineGraceHours: globalConfig.OfflineGraceHours,
                ServerTimeUtc: now,
                Signature: signature
            );
        }

        public async Task<SubscriptionPolicyRecord?> GetPolicyByInstallationIdAsync(Guid installationId)
        {
            return await _dbContext.SubscriptionPolicies
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.InstallationId == installationId);
        }

        public async Task<List<SubscriptionPolicyRecord>> GetAllSubscriptionPoliciesAsync(int page = 1, int pageSize = 50)
        {
            return await _dbContext.SubscriptionPolicies
                .Include(p => p.User)
                .AsNoTracking()
                .OrderByDescending(p => p.LastSyncedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<AdminOverrideRecord> ApplyAdminOverrideAsync(AdminOverrideRecord overrideRecord, string adminUsername)
        {
            overrideRecord.Id = Guid.NewGuid();
            overrideRecord.CreatedByUsername = adminUsername;
            overrideRecord.CreatedAtUtc = DateTime.UtcNow;
            overrideRecord.UpdatedAtUtc = DateTime.UtcNow;
            _dbContext.AdminOverrides.Add(overrideRecord);

            await _dbContext.SaveChangesAsync();
            await _auditLogger.LogActionAsync(null, adminUsername, "ADMIN_OVERRIDE_APPLIED", "AdminOverride", overrideRecord.Id.ToString(), $"Applied override on {overrideRecord.TargetType}:{overrideRecord.TargetValue}. Reason: {overrideRecord.Reason}", Guid.NewGuid().ToString());
            return overrideRecord;
        }

        public async Task<bool> RemoveAdminOverrideAsync(Guid overrideId, string adminUsername)
        {
            var ov = await _dbContext.AdminOverrides.FindAsync(overrideId);
            if (ov == null) return false;

            ov.IsActive = false;
            ov.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            await _auditLogger.LogActionAsync(null, adminUsername, "ADMIN_OVERRIDE_REMOVED", "AdminOverride", overrideId.ToString(), "Deactivated admin override", Guid.NewGuid().ToString());
            return true;
        }

        public async Task<bool> ExtendTrialAsync(Guid installationId, int additionalDays, string reason, string adminUsername = "SuperAdmin")
        {
            var policy = await _dbContext.SubscriptionPolicies.FirstOrDefaultAsync(p => p.InstallationId == installationId);
            if (policy == null)
            {
                policy = new SubscriptionPolicyRecord
                {
                    InstallationId = installationId,
                    TrialEndsAtUtc = DateTime.UtcNow.AddDays(additionalDays),
                    GraceEndsAtUtc = DateTime.UtcNow.AddDays(additionalDays + 5),
                    CurrentState = SubscriptionState.TRIAL_ACTIVE
                };
                _dbContext.SubscriptionPolicies.Add(policy);
            }
            else
            {
                DateTime baseDate = policy.TrialEndsAtUtc > DateTime.UtcNow ? policy.TrialEndsAtUtc : DateTime.UtcNow;
                policy.TrialEndsAtUtc = baseDate.AddDays(additionalDays);
                policy.GraceEndsAtUtc = policy.TrialEndsAtUtc.AddDays(5);
                policy.CurrentState = SubscriptionState.TRIAL_ACTIVE;
                policy.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            await _auditLogger.LogActionAsync(null, adminUsername, "TRIAL_EXTENDED", "SubscriptionPolicy", installationId.ToString(), $"Extended trial by {additionalDays} days. Reason: {reason}", Guid.NewGuid().ToString());
            return true;
        }

        public async Task<bool> ExtendGracePeriodAsync(Guid installationId, int additionalDays, string reason, string adminUsername = "SuperAdmin")
        {
            var policy = await _dbContext.SubscriptionPolicies.FirstOrDefaultAsync(p => p.InstallationId == installationId);
            if (policy == null) return false;

            DateTime baseDate = policy.GraceEndsAtUtc > DateTime.UtcNow ? policy.GraceEndsAtUtc : DateTime.UtcNow;
            policy.GraceEndsAtUtc = baseDate.AddDays(additionalDays);
            policy.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            await _auditLogger.LogActionAsync(null, adminUsername, "GRACE_EXTENDED", "SubscriptionPolicy", installationId.ToString(), $"Extended grace by {additionalDays} days. Reason: {reason}", Guid.NewGuid().ToString());
            return true;
        }

        public async Task<bool> SetDeviceBlockStatusAsync(Guid installationId, bool isBlocked, string? reason = null, string adminUsername = "SuperAdmin")
        {
            var policy = await _dbContext.SubscriptionPolicies.FirstOrDefaultAsync(p => p.InstallationId == installationId);
            if (policy == null)
            {
                policy = new SubscriptionPolicyRecord
                {
                    InstallationId = installationId,
                    IsBlocked = isBlocked,
                    BlockReason = reason,
                    CurrentState = isBlocked ? SubscriptionState.BLOCKED : SubscriptionState.TRIAL_ACTIVE
                };
                _dbContext.SubscriptionPolicies.Add(policy);
            }
            else
            {
                policy.IsBlocked = isBlocked;
                policy.BlockReason = isBlocked ? reason : null;
                policy.CurrentState = isBlocked ? SubscriptionState.BLOCKED : SubscriptionState.TRIAL_ACTIVE;
                policy.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            string action = isBlocked ? "DEVICE_BLOCKED" : "DEVICE_UNBLOCKED";
            await _auditLogger.LogActionAsync(null, adminUsername, action, "Device", installationId.ToString(), $"Device {installationId} block status set to {isBlocked}. Reason: {reason}", Guid.NewGuid().ToString());
            return true;
        }

        public async Task<bool> SetUserBlockStatusAsync(Guid userId, bool isBlocked, string? reason = null, string adminUsername = "SuperAdmin")
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            user.IsActive = !isBlocked;
            user.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            string action = isBlocked ? "USER_BLOCKED" : "USER_UNBLOCKED";
            await _auditLogger.LogActionAsync(null, adminUsername, action, "User", userId.ToString(), $"User {user.Username} ({userId}) active set to {!isBlocked}. Reason: {reason}", Guid.NewGuid().ToString());
            return true;
        }

        public async Task<List<AdminOverrideRecord>> GetActiveOverridesAsync()
        {
            var now = DateTime.UtcNow;
            return await _dbContext.AdminOverrides
                .AsNoTracking()
                .Where(o => o.IsActive && (o.ExpiresAtUtc == null || o.ExpiresAtUtc > now))
                .OrderByDescending(o => o.CreatedAtUtc)
                .ToListAsync();
        }

        public string ComputePolicySignature(Guid installationId, string state, int maxConnections, DateTime? expiresAtUtc, DateTime serverTimeUtc)
        {
            string raw = $"{installationId}|{state}|{maxConnections}|{expiresAtUtc:O}|{serverTimeUtc:O}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SIGNING_SECRET));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static void SetFeatures(Dictionary<string, bool> dict, bool isFull)
        {
            dict["premium_download"] = isFull;
            dict["dynamic_segmentation"] = isFull;
            dict["max_connections_64"] = isFull;
            dict["hls"] = isFull;
            dict["dash"] = isFull;
            dict["torrent"] = isFull;
            dict["browser_integration"] = true; // Always on for baseline user experience
            dict["remote_control"] = isFull;
            dict["advanced_scheduler"] = isFull;
            dict["media_quality_selector"] = isFull;
        }
    }
}
