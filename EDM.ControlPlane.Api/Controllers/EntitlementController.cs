using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

namespace EDM.ControlPlane.Api.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class EntitlementController : ControllerBase
    {
        private readonly ControlPlaneDbContext _dbContext;
        private readonly ISubscriptionEntitlementService _entitlementService;
        private readonly IGeoPricingService _geoPricingService;
        private readonly ILicenseService _licenseService;

        public EntitlementController(
            ControlPlaneDbContext dbContext,
            ISubscriptionEntitlementService entitlementService,
            IGeoPricingService geoPricingService,
            ILicenseService licenseService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
            _geoPricingService = geoPricingService ?? throw new ArgumentNullException(nameof(geoPricingService));
            _licenseService = licenseService ?? throw new ArgumentNullException(nameof(licenseService));
        }

        /// <summary>
        /// Authoritative Policy Sync for Desktop EDM Client
        /// </summary>
        [HttpPost("entitlements/sync")]
        public async Task<IActionResult> SyncEntitlementAsync([FromBody] EntitlementSyncRequestDto request)
        {
            if (request == null || request.InstallationId == Guid.Empty)
            {
                return BadRequest(new { error = "INVALID_INPUT", message = "InstallationId GUID is required." });
            }

            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var syncReq = request with { ClientIp = clientIp };

            var policy = await _entitlementService.EvaluateAndSyncEntitlementAsync(syncReq);
            return Ok(policy);
        }

        /// <summary>
        /// Public Catalog Endpoint for Website and EDM Checkout
        /// </summary>
        [HttpGet("subscription/plans")]
        public async Task<IActionResult> GetSubscriptionPlansAsync([FromQuery] string? country = null)
        {
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
            string resolvedCountry = !string.IsNullOrWhiteSpace(country) ? country.Trim().ToUpperInvariant() : _geoPricingService.DetectCountryFromHeadersOrIp(clientIp, headers);

            var globalConfig = await _dbContext.GlobalSubscriptionConfigs.FirstOrDefaultAsync() ?? new GlobalSubscriptionConfigRecord();
            var regionPolicies = await _dbContext.RegionPolicies.ToListAsync();
            var geoRules = await _dbContext.GeoPricingRules.ToListAsync();

            var geoRule = await _geoPricingService.GetPricingRuleForCountryAsync(resolvedCountry);
            string region = _geoPricingService.DetectRegionForCountry(resolvedCountry);
            var regionPolicy = regionPolicies.Find(r => string.Equals(r.RegionName, region, StringComparison.OrdinalIgnoreCase));

            bool isAvailable = globalConfig.IsGlobalSubscriptionEnabled;
            if (isAvailable && regionPolicy != null)
            {
                isAvailable = regionPolicy.IsSubscriptionEnabled;
            }
            if (isAvailable && geoRule != null)
            {
                isAvailable = geoRule.IsSubscriptionEnabled;
            }

            return Ok(new
            {
                countryCode = resolvedCountry,
                region = region,
                currency = geoRule.Currency,
                currencySymbol = geoRule.CurrencySymbol,
                isSubscriptionAvailable = isAvailable,
                plans = new object[]
                {
                    new
                    {
                        code = "free_trial",
                        name = "10-Day Free Trial",
                        tier = "Trial",
                        duration = "10 Days",
                        price = 0.00m,
                        formattedPrice = "Free",
                        maxConnections = 64,
                        features = new[] { "Full 32/64 Turbo Sockets", "4K/8K Video Stream Sniffer", "Browser Extensions Integration", "Crash-Proof Persistent Resume" }
                    },
                    new
                    {
                        code = "pro_monthly",
                        name = "Pro Monthly",
                        tier = "Pro",
                        duration = "1 Month",
                        price = geoRule.MonthlyPrice,
                        formattedPrice = _geoPricingService.FormatPrice(geoRule.MonthlyPrice, geoRule.Currency, geoRule.CurrencySymbol) + " / mo",
                        maxConnections = 64,
                        features = new[] { "Uncapped Turbo Multi-Socket Engine", "Priority Video Stream Grabber", "All Browser Integrations", "Dedicated Support" }
                    },
                    new
                    {
                        code = "pro_yearly",
                        name = "Pro Annual",
                        tier = "Pro",
                        duration = "1 Year",
                        price = geoRule.YearlyPrice,
                        formattedPrice = _geoPricingService.FormatPrice(geoRule.YearlyPrice, geoRule.Currency, geoRule.CurrencySymbol) + " / yr",
                        maxConnections = 64,
                        features = new[] { "All Pro Features Included", "2 Months Free Included", "VIP Support Priority" }
                    }
                }
            });
        }

        /// <summary>
        /// Server-Authoritative Promotional / Discount Coupon Code Validation
        /// </summary>
        [HttpPost("pricing/validate-coupon")]
        public async Task<IActionResult> ValidateCouponAsync([FromBody] ValidateCouponRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.CouponCode) || string.IsNullOrWhiteSpace(request.PlanCode))
            {
                return BadRequest(new { valid = false, message = "Coupon code and plan code are required." });
            }

            string code = request.CouponCode.Trim().ToUpperInvariant();
            var promo = await _dbContext.Promotions
                .FirstOrDefaultAsync(p => p.PromoCode == code && p.IsEnabled);

            if (promo == null)
            {
                return Ok(new { valid = false, message = "Invalid coupon code." });
            }

            var now = DateTime.UtcNow;
            if (promo.StartsAtUtc > now)
            {
                return Ok(new { valid = false, message = "This coupon is not active yet." });
            }

            if (promo.EndsAtUtc.HasValue && promo.EndsAtUtc.Value < now)
            {
                return Ok(new { valid = false, message = "This coupon has expired." });
            }

            if (promo.MaxUses.HasValue && promo.CurrentUses >= promo.MaxUses.Value)
            {
                return Ok(new { valid = false, message = "This coupon has reached its maximum usage limit." });
            }

            // User-Specific Restriction
            if (promo.TargetUserId.HasValue)
            {
                if (!request.UserId.HasValue || request.UserId.Value != promo.TargetUserId.Value)
                {
                    return Ok(new { valid = false, message = "This coupon is exclusive to a specific user account." });
                }
            }

            if (!string.IsNullOrWhiteSpace(promo.TargetEmail))
            {
                if (string.IsNullOrWhiteSpace(request.UserEmail) || !string.Equals(request.UserEmail.Trim(), promo.TargetEmail.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return Ok(new { valid = false, message = "This coupon is not valid for your email address." });
                }
            }

            // Once-per-user / Usage limit per user check
            if (request.UserId.HasValue || request.InstallationId.HasValue)
            {
                int userUsageCount = await _dbContext.CouponUsages
                    .CountAsync(u => u.PromotionId == promo.Id && (
                        (request.UserId.HasValue && u.UserId == request.UserId.Value) ||
                        (request.InstallationId.HasValue && u.InstallationId == request.InstallationId.Value)
                    ));

                if (userUsageCount >= promo.MaxUsesPerUser)
                {
                    return Ok(new { valid = false, message = "You have already redeemed this coupon the maximum allowed times." });
                }
            }

            // Geo / Regional Restriction
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
            string country = _geoPricingService.DetectCountryFromHeadersOrIp(clientIp, headers);
            string region = _geoPricingService.DetectRegionForCountry(country);
            var geoRule = await _geoPricingService.GetPricingRuleForCountryAsync(country);

            if (!string.IsNullOrWhiteSpace(promo.TargetCountryCode) && !string.Equals(promo.TargetCountryCode, country, StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new { valid = false, message = "This coupon is only valid for purchases in " + promo.TargetCountryCode + "." });
            }

            if (!string.IsNullOrWhiteSpace(promo.TargetRegion) && !string.Equals(promo.TargetRegion, region, StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new { valid = false, message = "This coupon is only valid in the " + promo.TargetRegion + " region." });
            }

            // Plan Restriction
            if (!string.IsNullOrWhiteSpace(promo.TargetPlanCode) && !string.Equals(promo.TargetPlanCode, request.PlanCode, StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new { valid = false, message = "This coupon is not applicable to the selected subscription plan." });
            }

            decimal basePrice = request.PlanCode.Contains("year", StringComparison.OrdinalIgnoreCase) ? geoRule.YearlyPrice : geoRule.MonthlyPrice;
            decimal discountAmount = 0m;

            if (promo.DiscountPercent.HasValue && promo.DiscountPercent.Value > 0)
            {
                discountAmount = Math.Round(basePrice * (promo.DiscountPercent.Value / 100m), 2);
            }
            else if (promo.DiscountAmount.HasValue && promo.DiscountAmount.Value > 0)
            {
                discountAmount = Math.Min(basePrice, promo.DiscountAmount.Value);
            }

            decimal finalPrice = Math.Max(0m, basePrice - discountAmount);

            return Ok(new
            {
                valid = true,
                couponCode = promo.PromoCode,
                description = promo.Description ?? (promo.DiscountPercent.HasValue ? promo.DiscountPercent.Value + "% discount" : geoRule.CurrencySymbol + promo.DiscountAmount + " discount"),
                currency = geoRule.Currency,
                currencySymbol = geoRule.CurrencySymbol,
                originalPrice = basePrice,
                discountAmount = discountAmount,
                finalPrice = finalPrice,
                formattedOriginalPrice = _geoPricingService.FormatPrice(basePrice, geoRule.Currency, geoRule.CurrencySymbol),
                formattedDiscountAmount = _geoPricingService.FormatPrice(discountAmount, geoRule.Currency, geoRule.CurrencySymbol),
                formattedFinalPrice = _geoPricingService.FormatPrice(finalPrice, geoRule.Currency, geoRule.CurrencySymbol),
                message = "Coupon applied successfully!"
            });
        }
    }

    public record ValidateCouponRequestDto(string CouponCode, string PlanCode, Guid? UserId = null, Guid? InstallationId = null, string? UserEmail = null);
}
