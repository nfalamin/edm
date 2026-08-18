using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Services
{
    public interface IContentAndPricingService
    {
        Task<WebsiteContent?> GetSectionContentAsync(string sectionKey, string locale = "en");
        Task<WebsiteContent> UpsertSectionContentAsync(string sectionKey, string title, string contentJson, string locale = "en", Guid? adminActorId = null);
        Task<List<WebsiteContent>> GetAllSectionsAsync(string locale = "en");
        Task<List<PricingTier>> GetPricingTiersAsync(bool activeOnly = true);
        Task<PricingTier> UpsertPricingTierAsync(Guid? id, Guid planId, string displayName, decimal monthly, decimal yearly, string currency, string featuresJson, string? badge, bool isHighlighted, int sortOrder, bool isActive, Guid? adminActorId = null);
    }

    public class ContentAndPricingService : IContentAndPricingService
    {
        private readonly ControlPlaneDbContext _dbContext;
        private readonly IAuditLoggingService _auditLogger;

        public ContentAndPricingService(ControlPlaneDbContext dbContext, IAuditLoggingService auditLogger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        public async Task<WebsiteContent?> GetSectionContentAsync(string sectionKey, string locale = "en")
        {
            return await _dbContext.WebsiteContents
                .FirstOrDefaultAsync(c => c.SectionKey == sectionKey && c.Locale == locale && c.IsPublished);
        }

        public async Task<WebsiteContent> UpsertSectionContentAsync(string sectionKey, string title, string contentJson, string locale = "en", Guid? adminActorId = null)
        {
            var content = await _dbContext.WebsiteContents
                .FirstOrDefaultAsync(c => c.SectionKey == sectionKey && c.Locale == locale);

            if (content == null)
            {
                content = new WebsiteContent
                {
                    Id = Guid.NewGuid(),
                    SectionKey = sectionKey.Trim().ToLowerInvariant(),
                    Title = title.Trim(),
                    ContentJson = contentJson,
                    Locale = locale.Trim().ToLowerInvariant(),
                    IsPublished = true,
                    Version = 1,
                    UpdatedByUserId = adminActorId,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _dbContext.WebsiteContents.Add(content);
            }
            else
            {
                content.Title = title.Trim();
                content.ContentJson = contentJson;
                content.Version += 1;
                content.UpdatedByUserId = adminActorId;
                content.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: adminActorId,
                actorUsername: "ADMIN",
                action: "WEBSITE_CONTENT_UPDATED",
                targetEntity: "WebsiteContent",
                targetId: content.SectionKey,
                detailsJson: $"{{\"version\":{content.Version},\"locale\":\"{locale}\"}}",
                correlationId: Guid.NewGuid().ToString("N"));

            return content;
        }

        public async Task<List<WebsiteContent>> GetAllSectionsAsync(string locale = "en")
        {
            return await _dbContext.WebsiteContents
                .Where(c => c.Locale == locale)
                .OrderBy(c => c.SectionKey)
                .ToListAsync();
        }

        public async Task<List<PricingTier>> GetPricingTiersAsync(bool activeOnly = true)
        {
            var query = _dbContext.PricingTiers.Include(p => p.Plan).AsQueryable();
            if (activeOnly) query = query.Where(p => p.IsActive);
            return await query.OrderBy(p => p.SortOrder).ToListAsync();
        }

        public async Task<PricingTier> UpsertPricingTierAsync(Guid? id, Guid planId, string displayName, decimal monthly, decimal yearly, string currency, string featuresJson, string? badge, bool isHighlighted, int sortOrder, bool isActive, Guid? adminActorId = null)
        {
            PricingTier? tier = null;
            if (id.HasValue && id.Value != Guid.Empty)
            {
                tier = await _dbContext.PricingTiers.FindAsync(id.Value);
            }

            if (tier == null)
            {
                tier = new PricingTier
                {
                    Id = id ?? Guid.NewGuid(),
                    PlanId = planId,
                    DisplayName = displayName.Trim(),
                    MonthlyPrice = monthly,
                    YearlyPrice = yearly,
                    Currency = currency.Trim().ToUpperInvariant(),
                    FeaturesListJson = featuresJson,
                    BadgeText = badge,
                    IsHighlighted = isHighlighted,
                    SortOrder = sortOrder,
                    IsActive = isActive,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _dbContext.PricingTiers.Add(tier);
            }
            else
            {
                tier.PlanId = planId;
                tier.DisplayName = displayName.Trim();
                tier.MonthlyPrice = monthly;
                tier.YearlyPrice = yearly;
                tier.Currency = currency.Trim().ToUpperInvariant();
                tier.FeaturesListJson = featuresJson;
                tier.BadgeText = badge;
                tier.IsHighlighted = isHighlighted;
                tier.SortOrder = sortOrder;
                tier.IsActive = isActive;
                tier.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: adminActorId,
                actorUsername: "ADMIN",
                action: "PRICING_TIER_UPDATED",
                targetEntity: "PricingTier",
                targetId: tier.Id.ToString(),
                detailsJson: $"{{\"name\":\"{tier.DisplayName}\",\"monthly\":{tier.MonthlyPrice},\"yearly\":{tier.YearlyPrice}}}",
                correlationId: Guid.NewGuid().ToString("N"));

            return tier;
        }
    }
}
