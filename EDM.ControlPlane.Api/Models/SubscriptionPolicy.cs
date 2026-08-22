using System;
using System.Collections.Generic;

namespace EDM.ControlPlane.Api.Models
{
    public enum SubscriptionState
    {
        TRIAL_ACTIVE,
        TRIAL_EXPIRED,
        GRACE_PERIOD,
        FREE_RESTRICTED,
        SUBSCRIBED,
        SUBSCRIPTION_EXPIRED,
        SUSPENDED,
        BLOCKED,
        ADMIN_OVERRIDE
    }

    public enum OverrideTargetType
    {
        User,
        Device,
        Country,
        Region,
        Plan,
        Global
    }

    public class GlobalSubscriptionConfigRecord
    {
        public Guid Id { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
        public bool IsGlobalSubscriptionEnabled { get; set; } = true;
        public bool IsAsiaSubscriptionEnabled { get; set; } = true;
        public bool IsTrialEnabled { get; set; } = true;
        public int DefaultTrialDurationDays { get; set; } = 10;
        public bool IsGracePeriodEnabled { get; set; } = true;
        public int DefaultGraceDurationDays { get; set; } = 5;
        public int OfflineGraceHours { get; set; } = 72;
        public int MaxTurboConnections { get; set; } = 64;
        public int MaxGraceConnections { get; set; } = 32;
        public int MaxRestrictedConnections { get; set; } = 16;
        public bool PaymentSystemEnabled { get; set; } = false;
        public string PaymentProvider { get; set; } = "None"; // "Stripe", "Paddle", "bKash", "None"
        public bool IsTestMode { get; set; } = true;
        public string SupportedCurrencies { get; set; } = "BDT,INR,PKR,USD,EUR,GBP,ZAR,AED,SAR,JPY,CNY";
        public string GlobalFeaturesJson { get; set; } = @"{\""premium_download\"":true,\""dynamic_segmentation\"":true,\""max_connections_64\"":true,\""hls\"":true,\""dash\"":true,\""torrent\"":true,\""browser_integration\"":true,\""remote_control\"":true,\""advanced_scheduler\"":true,\""media_quality_selector\"":true}";
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public string UpdatedByUsername { get; set; } = "System";
    }

    public class RegionPolicyRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string RegionName { get; set; } = string.Empty; // "Asia", "North America", "Europe", "Africa", "South America", "Oceania", "Middle East", "Global"
        public bool IsSubscriptionEnabled { get; set; } = true;
        public string DefaultCurrency { get; set; } = "USD";
        public string DefaultCurrencySymbol { get; set; } = "$";
        public decimal DefaultMonthlyPrice { get; set; } = 4.99m;
        public decimal DefaultYearlyPrice { get; set; } = 49.99m;
        public string Description { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class SubscriptionPolicyRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? UserId { get; set; }
        public Guid InstallationId { get; set; }
        public SubscriptionState CurrentState { get; set; } = SubscriptionState.TRIAL_ACTIVE;
        public DateTime TrialStartedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime TrialEndsAtUtc { get; set; } = DateTime.UtcNow.AddDays(10);
        public DateTime GraceEndsAtUtc { get; set; } = DateTime.UtcNow.AddDays(15);
        public DateTime? SubscriptionExpiresAtUtc { get; set; }
        public string? ActivePlanCode { get; set; } = "free_trial";
        public int MaxConnections { get; set; } = 64;
        public int MaxConcurrentDownloads { get; set; } = 8;
        public bool IsBlocked { get; set; } = false;
        public string? BlockReason { get; set; }
        public string? CoarseCountryCode { get; set; } = "US";
        public string FeaturesJson { get; set; } = @"{\""premium_download\"":true,\""dynamic_segmentation\"":true,\""max_connections_64\"":true,\""hls\"":true,\""dash\"":true,\""torrent\"":true,\""browser_integration\"":true,\""remote_control\"":true,\""advanced_scheduler\"":true,\""media_quality_selector\"":true}";
        public int PolicyVersion { get; set; } = 1;
        public DateTime LastSyncedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
    }

    public class AdminOverrideRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public OverrideTargetType TargetType { get; set; } = OverrideTargetType.User;
        public string TargetValue { get; set; } = string.Empty; // UserId, InstallationId, CountryCode or RegionName
        public SubscriptionState? OverrideState { get; set; }
        public int? OverrideMaxConnections { get; set; }
        public int? ExtendTrialDays { get; set; }
        public int? ExtendGraceDays { get; set; }
        public string? OverrideCountryCode { get; set; }
        public string? OverrideFeaturesJson { get; set; }
        public bool? ForceUnblock { get; set; }
        public bool? ForceSubscriptionEnabled { get; set; }
        public bool IsActive { get; set; } = true;
        public string Reason { get; set; } = string.Empty;
        public Guid? CreatedByAdminId { get; set; }
        public string CreatedByUsername { get; set; } = "SuperAdmin";
        public DateTime? ExpiresAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class GeoPricingRuleRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string CountryCode { get; set; } = string.Empty; // e.g. "BD", "IN", "PK", "US", "GLOBAL"
        public string Region { get; set; } = string.Empty; // e.g. "South Asia", "Asia", "North America", "Europe", "Global"
        public string Currency { get; set; } = "USD";
        public string CurrencySymbol { get; set; } = "$";
        public decimal MonthlyPrice { get; set; } = 9.99m;
        public decimal YearlyPrice { get; set; } = 79.99m;
        public decimal? PromotionalPrice { get; set; }
        public string? PromoCode { get; set; }
        public bool IsSubscriptionEnabled { get; set; } = true;
        public bool TrialEnabled { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public string? FeaturesOverrideJson { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class PromotionRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string PromoCode { get; set; } = string.Empty;
        public decimal? DiscountPercent { get; set; }
        public decimal? DiscountAmount { get; set; }
        public string? Currency { get; set; }
        public string? TargetCountryCode { get; set; }
        public string? TargetRegion { get; set; }
        public string? TargetPlanCode { get; set; }
        public int? MaxUses { get; set; }
        public int CurrentUses { get; set; } = 0;
        public DateTime StartsAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? EndsAtUtc { get; set; }
        public bool IsEnabled { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public record SwitchRequestDto(bool IsEnabled, string? Reason = null);

    public record EntitlementSyncRequestDto(
        Guid InstallationId,
        Guid? UserId = null,
        string? AppVersion = null,
        string? OsVersion = null,
        string? DeviceName = null,
        string? ClientIp = null);

    public record EntitlementPolicyDto(
        Guid InstallationId,
        Guid? UserId,
        string State,
        string PlanCode,
        string PlanTier,
        int MaxConnections,
        int MaxConcurrentDownloads,
        int TrialDaysRemaining,
        int GraceDaysRemaining,
        DateTime? ExpiresAtUtc,
        Dictionary<string, bool> FeatureFlags,
        bool IsBlocked,
        string? BlockReason,
        string StatusMessage,
        string CountryCode,
        string Currency,
        decimal MonthlyPrice,
        string FormattedPrice,
        bool IsSubscriptionAvailable,
        int PolicyVersion,
        int OfflineGraceHours,
        DateTime ServerTimeUtc,
        string Signature);

    public record GlobalSubscriptionConfigDto(
        bool IsGlobalSubscriptionEnabled,
        bool IsAsiaSubscriptionEnabled,
        bool IsTrialEnabled,
        int DefaultTrialDurationDays,
        bool IsGracePeriodEnabled,
        int DefaultGraceDurationDays,
        int OfflineGraceHours,
        int MaxTurboConnections,
        int MaxGraceConnections,
        int MaxRestrictedConnections,
        bool PaymentSystemEnabled,
        string PaymentProvider,
        bool IsTestMode,
        string SupportedCurrencies,
        Dictionary<string, bool> GlobalFeatureFlags,
        DateTime UpdatedAtUtc,
        string UpdatedByUsername);
}
