using System;

namespace EDM.ControlPlane.Api.Models
{
    public class WebsiteContent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string SectionKey { get; set; } = string.Empty; // e.g. "hero", "features", "faq", "footer", "privacy", "terms"
        public string Title { get; set; } = string.Empty;
        public string ContentJson { get; set; } = "{}";
        public string Locale { get; set; } = "en";
        public bool IsPublished { get; set; } = true;
        public int Version { get; set; } = 1;
        public Guid? UpdatedByUserId { get; set; }
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? UpdatedByUser { get; set; }
    }

    public class PricingTier
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PlanId { get; set; }
        public string DisplayName { get; set; } = string.Empty; // e.g. "Pro Yearly"
        public decimal MonthlyPrice { get; set; } = 0.00m;
        public decimal YearlyPrice { get; set; } = 0.00m;
        public string Currency { get; set; } = "USD";
        public string FeaturesListJson { get; set; } = "[]";
        public string? BadgeText { get; set; } // e.g. "Most Popular", "Best Value"
        public bool IsHighlighted { get; set; } = false;
        public int SortOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public Plan? Plan { get; set; }
    }
}
