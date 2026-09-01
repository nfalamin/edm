using System;
using System.Collections.Generic;

namespace EDM.ControlPlane.Api.Models
{
    public enum PlanTier
    {
        Free,
        Pro,
        Team,
        Enterprise
    }

    public enum LicenseStatus
    {
        Active,
        Suspended,
        Revoked,
        Expired
    }

    public enum SubscriptionStatus
    {
        Active,
        PastDue,
        Canceled,
        Trialing
    }

    public class Plan
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Code { get; set; } = string.Empty; // e.g. "free", "pro_monthly", "pro_lifetime", "enterprise"
        public string Name { get; set; } = string.Empty;
        public PlanTier Tier { get; set; } = PlanTier.Free;
        public string Description { get; set; } = string.Empty;
        public decimal PriceMonthlyUsd { get; set; } = 0.00m;
        public decimal PriceYearlyUsd { get; set; } = 0.00m;
        public int MaxDevices { get; set; } = 1;
        public int MaxConcurrentDownloads { get; set; } = 2;
        public string FeaturesJson { get; set; } = "[]";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<License> Licenses { get; set; } = new List<License>();
        public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public ICollection<PricingTier> PricingTiers { get; set; } = new List<PricingTier>();
    }

    public class License
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string LicenseKeyHash { get; set; } = string.Empty; // SHA-256 hash of plaintext key
        public string KeyPrefix { get; set; } = string.Empty; // e.g. "EDM-PRO-AB12" for display
        public Guid? UserId { get; set; }
        public Guid PlanId { get; set; }
        public LicenseStatus Status { get; set; } = LicenseStatus.Active;
        public int MaxActivations { get; set; } = 3;
        public int CurrentActivations { get; set; } = 0;
        public DateTime? ExpiresAtUtc { get; set; } // Null for lifetime licenses
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
        public Plan? Plan { get; set; }
        public ICollection<DownloadRecord> DownloadRecords { get; set; } = new List<DownloadRecord>();
    }

    public class Subscription
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public Guid PlanId { get; set; }
        public string? ExternalSubscriptionId { get; set; } // Stripe/Payment gateway ID
        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
        public DateTime CurrentPeriodStartUtc { get; set; } = DateTime.UtcNow;
        public DateTime CurrentPeriodEndUtc { get; set; } = DateTime.UtcNow.AddMonths(1);
        public bool CancelAtPeriodEnd { get; set; } = false;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
        public Plan? Plan { get; set; }
    }
}
