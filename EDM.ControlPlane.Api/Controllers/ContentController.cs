using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EDM.ControlPlane.Api.Middleware;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

namespace EDM.ControlPlane.Api.Controllers
{
    public record UpdateContentRequestDto(
        string Title,
        string ContentJson,
        string Locale = "en");

    public record UpsertPricingTierRequestDto(
        Guid? Id,
        Guid PlanId,
        string DisplayName,
        decimal MonthlyPrice,
        decimal YearlyPrice,
        string Currency = "USD",
        string FeaturesListJson = "[]",
        string? BadgeText = null,
        bool IsHighlighted = false,
        int SortOrder = 0,
        bool IsActive = true);

    [ApiController]
    [Route("api/v1")]
    public class ContentController : ControllerBase
    {
        private readonly IContentAndPricingService _contentService;
        private readonly IContentManagerService _contentManager;

        public ContentController(
            IContentAndPricingService contentService,
            IContentManagerService contentManager)
        {
            _contentService = contentService ?? throw new ArgumentNullException(nameof(contentService));
            _contentManager = contentManager ?? throw new ArgumentNullException(nameof(contentManager));
        }

        private string GetEditorName()
        {
            return User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value ?? "Admin";
        }

        private Guid? GetAdminUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
        }

        // ==========================================
        // 0. DOCUMENT CMS (ABOUT, PRIVACY, TERMS, FAQ, HELP, DOCS, NOTES, ANNOUNCEMENTS)
        // ==========================================
        [HttpGet("content/doc/{slug}")]
        public async Task<IActionResult> GetPublicDocumentAsync(string slug)
        {
            var doc = await _contentManager.GetDocumentBySlugAsync(slug, publishedOnly: true);
            if (doc == null)
            {
                return NotFound(new { error = "DOC_NOT_FOUND", message = $"Published document '{slug}' not found." });
            }
            return Ok(doc);
        }

        [HttpGet("content/documents")]
        public async Task<IActionResult> GetPublicDocumentsListAsync()
        {
            var docs = await _contentManager.GetAllDocumentsAsync(includeDrafts: false);
            return Ok(docs);
        }

        [Authorize]
        [RequirePermission(Permissions.WebsiteManage)]
        [HttpGet("admin/content/documents")]
        public async Task<IActionResult> GetAllAdminDocumentsAsync([FromQuery] bool includeDrafts = true)
        {
            var docs = await _contentManager.GetAllDocumentsAsync(includeDrafts);
            return Ok(docs);
        }

        [Authorize]
        [RequirePermission(Permissions.WebsiteManage)]
        [HttpGet("admin/content/documents/{id}")]
        public async Task<IActionResult> GetAdminDocumentByIdAsync(Guid id)
        {
            var doc = await _contentManager.GetDocumentByIdAsync(id);
            if (doc == null) return NotFound(new { error = "NOT_FOUND", message = "Document not found." });
            return Ok(doc);
        }

        [Authorize]
        [RequirePermission(Permissions.WebsiteManage)]
        [HttpPost("admin/content/documents/{id}/draft")]
        public async Task<IActionResult> SaveDocumentDraftAsync(Guid id, [FromBody] SaveDocumentDraftDto dto)
        {
            if (dto == null) return BadRequest(new { error = "INVALID_PAYLOAD" });
            var updated = await _contentManager.SaveDraftAsync(id, dto, GetEditorName(), GetAdminUserId());
            return Ok(updated);
        }

        [Authorize]
        [RequirePermission(Permissions.WebsiteManage)]
        [HttpPost("admin/content/documents/{id}/publish")]
        public async Task<IActionResult> PublishDocumentAsync(Guid id)
        {
            var published = await _contentManager.PublishDocumentAsync(id, GetEditorName(), GetAdminUserId());
            return Ok(published);
        }

        [Authorize]
        [RequirePermission(Permissions.WebsiteManage)]
        [HttpPost("admin/content/documents/{id}/unpublish")]
        public async Task<IActionResult> UnpublishDocumentAsync(Guid id)
        {
            var unpublished = await _contentManager.UnpublishDocumentAsync(id, GetEditorName(), GetAdminUserId());
            return Ok(unpublished);
        }

        [Authorize]
        [RequirePermission(Permissions.WebsiteManage)]
        [HttpPost("admin/content/documents/{id}/replace-file")]
        public async Task<IActionResult> ReplaceDocumentFileAsync(Guid id, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "NO_FILE", message = "Please upload a valid markdown file." });
            }

            using var stream = file.OpenReadStream();
            var doc = await _contentManager.ReplaceFileAsync(id, stream, file.FileName, GetEditorName(), GetAdminUserId());
            return Ok(doc);
        }

        [Authorize]
        [RequirePermission(Permissions.WebsiteManage)]
        [HttpGet("admin/content/documents/{id}/history")]
        public async Task<IActionResult> GetDocumentHistoryAsync(Guid id)
        {
            var history = await _contentManager.GetRevisionHistoryAsync(id);
            return Ok(history);
        }

        [Authorize]
        [RequirePermission(Permissions.WebsiteManage)]
        [HttpPost("admin/content/documents/{id}/restore/{version}")]
        public async Task<IActionResult> RestoreDocumentRevisionAsync(Guid id, int version)
        {
            var doc = await _contentManager.RestoreRevisionAsync(id, version, GetEditorName(), GetAdminUserId());
            return Ok(doc);
        }

        [Authorize]
        [RequirePermission(Permissions.WebsiteManage)]
        [HttpPost("admin/content/scan-workspace")]
        public async Task<IActionResult> ScanContentWorkspaceAsync()
        {
            int discovered = await _contentManager.ScanLocalContentWorkspaceAsync();
            return Ok(new { success = true, discoveredCount = discovered, message = $"Scanned local content workspace. Discovered/Updated {discovered} documents." });
        }

        // ==========================================
        // 1. WEBSITE CONTENT
        // ==========================================
        [HttpGet("content/{sectionKey}")]
        public async Task<IActionResult> GetSectionContentAsync(string sectionKey, [FromQuery] string locale = "en")
        {
            var content = await _contentService.GetSectionContentAsync(sectionKey, locale);
            if (content == null)
            {
                return NotFound(new { error = "SECTION_NOT_FOUND", message = $"Section '{sectionKey}' not found." });
            }

            return Ok(new
            {
                content.Id,
                content.SectionKey,
                content.Title,
                content.ContentJson,
                content.Locale,
                content.IsPublished,
                content.Version,
                content.UpdatedAtUtc
            });
        }

        [HttpGet("content")]
        public async Task<IActionResult> GetAllSectionsAsync([FromQuery] string locale = "en")
        {
            var sections = await _contentService.GetAllSectionsAsync(locale);
            return Ok(sections.ConvertAll(s => new
            {
                s.Id,
                s.SectionKey,
                s.Title,
                s.ContentJson,
                s.Locale,
                s.IsPublished,
                s.Version,
                s.UpdatedAtUtc
            }));
        }

        [Authorize]
        [RequirePermission(Permissions.WebsiteManage)]
        [HttpPut("content/{sectionKey}")]
        public async Task<IActionResult> UpdateSectionContentAsync(string sectionKey, [FromBody] UpdateContentRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(new { error = "INVALID_PAYLOAD", message = "Title is required." });
            }

            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            var updated = await _contentService.UpsertSectionContentAsync(
                sectionKey: sectionKey,
                title: request.Title,
                contentJson: request.ContentJson,
                locale: string.IsNullOrWhiteSpace(request.Locale) ? "en" : request.Locale,
                adminActorId: adminId);

            return Ok(new
            {
                updated.Id,
                updated.SectionKey,
                updated.Title,
                updated.ContentJson,
                updated.Locale,
                updated.IsPublished,
                updated.Version,
                updated.UpdatedAtUtc
            });
        }

        // ==========================================
        // 2. PRICING TIERS
        // ==========================================
        [HttpGet("pricing")]
        public async Task<IActionResult> GetPricingTiersAsync([FromQuery] bool activeOnly = true)
        {
            var tiers = await _contentService.GetPricingTiersAsync(activeOnly);
            return Ok(tiers.ConvertAll(t => new
            {
                t.Id,
                t.PlanId,
                PlanName = t.Plan?.Name,
                t.DisplayName,
                t.MonthlyPrice,
                t.YearlyPrice,
                t.Currency,
                t.FeaturesListJson,
                t.BadgeText,
                t.IsHighlighted,
                t.SortOrder,
                t.IsActive
            }));
        }

        [Authorize]
        [RequirePermission(Permissions.PricingManage)]
        [HttpPost("pricing")]
        public async Task<IActionResult> UpsertPricingTierAsync([FromBody] UpsertPricingTierRequestDto request)
        {
            if (request == null || request.PlanId == Guid.Empty || string.IsNullOrWhiteSpace(request.DisplayName))
            {
                return BadRequest(new { error = "INVALID_PAYLOAD", message = "PlanId and DisplayName are required." });
            }

            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            var tier = await _contentService.UpsertPricingTierAsync(
                id: request.Id,
                planId: request.PlanId,
                displayName: request.DisplayName,
                monthly: request.MonthlyPrice,
                yearly: request.YearlyPrice,
                currency: request.Currency,
                featuresJson: request.FeaturesListJson,
                badge: request.BadgeText,
                isHighlighted: request.IsHighlighted,
                sortOrder: request.SortOrder,
                isActive: request.IsActive,
                adminActorId: adminId);

            return Ok(new
            {
                tier.Id,
                tier.PlanId,
                tier.DisplayName,
                tier.MonthlyPrice,
                tier.YearlyPrice,
                tier.Currency,
                tier.FeaturesListJson,
                tier.BadgeText,
                tier.IsHighlighted,
                tier.SortOrder,
                tier.IsActive
            });
        }
        // ==========================================
        // 3. SERVER-SIDE PROMOTIONS & COUPONS
        // ==========================================
        [HttpGet("promotions/verify")]
        public IActionResult VerifyCoupon([FromQuery] string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return BadRequest(new { isValid = false, message = "Coupon code is required." });
            }

            string cleanCode = code.Trim().ToUpperInvariant();

            // Default valid server-side promotional rules
            var validCoupons = new System.Collections.Generic.Dictionary<string, (string discount, decimal percent, DateTime expires, int maxUses, int currentUses)>
            {
                ["SUMMER50"] = ("50% OFF", 0.50m, new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc), 1000, 142),
                ["EDMPRO10"] = ("10% OFF", 0.10m, new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc), 5000, 488),
                ["TURBO2026"] = ("20% OFF", 0.20m, new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc), 2000, 210)
            };

            if (validCoupons.TryGetValue(cleanCode, out var coupon))
            {
                if (DateTime.UtcNow > coupon.expires)
                {
                    return Ok(new { isValid = false, code = cleanCode, message = "This coupon code has expired." });
                }

                if (coupon.currentUses >= coupon.maxUses)
                {
                    return Ok(new { isValid = false, code = cleanCode, message = "This coupon has reached its maximum usage limit." });
                }

                return Ok(new
                {
                    isValid = true,
                    code = cleanCode,
                    discount = coupon.discount,
                    discountPercentage = (int)(coupon.percent * 100),
                    expiresAtUtc = coupon.expires,
                    message = $"Coupon '{cleanCode}' applied successfully ({coupon.discount})!"
                });
            }

            return Ok(new { isValid = false, code = cleanCode, message = "Invalid coupon code." });
        }
    }
}
