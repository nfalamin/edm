using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

namespace EDM.ControlPlane.Api.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class EntitlementController : ControllerBase
    {
        private readonly ISubscriptionEntitlementService _entitlementService;
        private readonly IGeoPricingService _geoPricingService;
        private readonly ILicenseService _licenseService;

        public EntitlementController(
            ISubscriptionEntitlementService entitlementService,
            IGeoPricingService geoPricingService,
            ILicenseService licenseService)
        {
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
        /// Public Geo-Pricing Endpoint for Website & Desktop
        /// </summary>
        [HttpGet("pricing/geo")]
        public async Task<IActionResult> GetGeoPricingAsync([FromQuery] string? country = null)
        {
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            string countryCode = country ?? _geoPricingService.DetectCountryFromHeadersOrIp(clientIp, null);
            var rule = await _geoPricingService.GetPricingRuleForCountryAsync(countryCode);

            return Ok(new
            {
                countryCode = rule.CountryCode,
                region = rule.Region,
                currency = rule.Currency,
                currencySymbol = rule.CurrencySymbol,
                monthlyPrice = rule.MonthlyPrice,
                yearlyPrice = rule.YearlyPrice,
                promotionalPrice = rule.PromotionalPrice,
                formattedMonthly = _geoPricingService.FormatPrice(rule.MonthlyPrice, rule.Currency, rule.CurrencySymbol),
                formattedYearly = _geoPricingService.FormatPrice(rule.YearlyPrice, rule.Currency, rule.CurrencySymbol),
                isSubscriptionEnabled = rule.IsSubscriptionEnabled,
                description = rule.Description
            });
        }

        /// <summary>
        /// Public Subscription Plans Endpoint with Localized Pricing for Website
        /// </summary>
        [HttpGet("subscription/plans")]
        public async Task<IActionResult> GetPublicPlansAsync([FromQuery] string? country = null)
        {
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            string countryCode = country ?? _geoPricingService.DetectCountryFromHeadersOrIp(clientIp, null);
            var geoRule = await _geoPricingService.GetPricingRuleForCountryAsync(countryCode);

            var globalConfig = await _entitlementService.GetGlobalConfigAsync();
            bool isAvailable = globalConfig.IsGlobalSubscriptionEnabled && geoRule.IsSubscriptionEnabled;
            if (isAvailable && (geoRule.Region == "Asia" || geoRule.Region == "South Asia") && !globalConfig.IsAsiaSubscriptionEnabled)
            {
                isAvailable = false;
            }

            return Ok(new
            {
                detectedCountry = geoRule.CountryCode,
                region = geoRule.Region,
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
    }
}
