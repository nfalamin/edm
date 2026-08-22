using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Services
{
    public interface IGeoPricingService
    {
        Task<GeoPricingRuleRecord> GetPricingRuleForCountryAsync(string? countryCode);
        Task<List<GeoPricingRuleRecord>> GetAllPricingRulesAsync();
        Task<GeoPricingRuleRecord> UpsertPricingRuleAsync(GeoPricingRuleRecord rule, string adminUsername);
        Task<bool> DeletePricingRuleAsync(Guid ruleId, string adminUsername);
        Task<List<RegionPolicyRecord>> GetAllRegionPoliciesAsync();
        Task<RegionPolicyRecord> UpsertRegionPolicyAsync(RegionPolicyRecord regionPolicy, string adminUsername);
        string FormatPrice(decimal price, string currency, string symbol);
        string DetectCountryFromHeadersOrIp(string? clientIp, IDictionary<string, string>? headers);
        string DetectRegionForCountry(string countryCode);
    }

    public class GeoPricingService : IGeoPricingService
    {
        private readonly ControlPlaneDbContext _dbContext;
        private readonly IAuditLoggingService _auditLogger;

        public GeoPricingService(ControlPlaneDbContext dbContext, IAuditLoggingService auditLogger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        public async Task<GeoPricingRuleRecord> GetPricingRuleForCountryAsync(string? countryCode)
        {
            string code = string.IsNullOrWhiteSpace(countryCode) ? "GLOBAL" : countryCode.ToUpperInvariant().Trim();

            // 1. Direct Country-Level Rule
            var countryRule = await _dbContext.GeoPricingRules
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.CountryCode == code && r.IsActive);

            if (countryRule != null) return countryRule;

            // 2. Region-Level Resolution
            string region = DetectRegionForCountry(code);
            var regionalRule = await _dbContext.GeoPricingRules
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.CountryCode == region && r.IsActive);

            if (regionalRule != null) return regionalRule;

            // 3. Region Policy Fallback
            var regionPolicy = await _dbContext.RegionPolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RegionName == region);

            if (regionPolicy != null)
            {
                return new GeoPricingRuleRecord
                {
                    Id = regionPolicy.Id,
                    CountryCode = code,
                    Region = regionPolicy.RegionName,
                    Currency = regionPolicy.DefaultCurrency,
                    CurrencySymbol = regionPolicy.DefaultCurrencySymbol,
                    MonthlyPrice = regionPolicy.DefaultMonthlyPrice,
                    YearlyPrice = regionPolicy.DefaultYearlyPrice,
                    IsSubscriptionEnabled = regionPolicy.IsSubscriptionEnabled,
                    Description = regionPolicy.Description
                };
            }

            // 4. Global Fallback Rule
            var globalRule = await _dbContext.GeoPricingRules
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.CountryCode == "GLOBAL" && r.IsActive);

            return globalRule ?? new GeoPricingRuleRecord
            {
                CountryCode = "GLOBAL",
                Region = "Global",
                Currency = "USD",
                CurrencySymbol = "$",
                MonthlyPrice = 4.99m,
                YearlyPrice = 49.99m,
                IsSubscriptionEnabled = true,
                Description = "Global Fallback Tier ($4.99/mo)"
            };
        }

        public async Task<List<GeoPricingRuleRecord>> GetAllPricingRulesAsync()
        {
            return await _dbContext.GeoPricingRules.AsNoTracking().OrderBy(r => r.CountryCode).ToListAsync();
        }

        public async Task<GeoPricingRuleRecord> UpsertPricingRuleAsync(GeoPricingRuleRecord rule, string adminUsername)
        {
            var existing = await _dbContext.GeoPricingRules.FirstOrDefaultAsync(r => r.CountryCode == rule.CountryCode.ToUpperInvariant().Trim());
            if (existing != null)
            {
                existing.Region = rule.Region;
                existing.Currency = rule.Currency;
                existing.CurrencySymbol = rule.CurrencySymbol;
                existing.MonthlyPrice = rule.MonthlyPrice;
                existing.YearlyPrice = rule.YearlyPrice;
                existing.PromotionalPrice = rule.PromotionalPrice;
                existing.PromoCode = rule.PromoCode;
                existing.IsSubscriptionEnabled = rule.IsSubscriptionEnabled;
                existing.TrialEnabled = rule.TrialEnabled;
                existing.IsActive = rule.IsActive;
                existing.Description = rule.Description;
                existing.UpdatedAtUtc = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                await _auditLogger.LogActionAsync(null, adminUsername, "GEO_PRICING_UPDATED", "GeoPricingRule", existing.Id.ToString(), $"Updated pricing rule for {existing.CountryCode} ({existing.Currency} {existing.MonthlyPrice}/mo)", Guid.NewGuid().ToString());
                return existing;
            }
            else
            {
                rule.Id = Guid.NewGuid();
                rule.CountryCode = rule.CountryCode.ToUpperInvariant().Trim();
                rule.CreatedAtUtc = DateTime.UtcNow;
                rule.UpdatedAtUtc = DateTime.UtcNow;
                _dbContext.GeoPricingRules.Add(rule);

                await _dbContext.SaveChangesAsync();
                await _auditLogger.LogActionAsync(null, adminUsername, "GEO_PRICING_CREATED", "GeoPricingRule", rule.Id.ToString(), $"Created pricing rule for {rule.CountryCode} ({rule.Currency} {rule.MonthlyPrice}/mo)", Guid.NewGuid().ToString());
                return rule;
            }
        }

        public async Task<bool> DeletePricingRuleAsync(Guid ruleId, string adminUsername)
        {
            var rule = await _dbContext.GeoPricingRules.FindAsync(ruleId);
            if (rule == null) return false;

            _dbContext.GeoPricingRules.Remove(rule);
            await _dbContext.SaveChangesAsync();
            await _auditLogger.LogActionAsync(null, adminUsername, "GEO_PRICING_DELETED", "GeoPricingRule", ruleId.ToString(), $"Deleted pricing rule for {rule.CountryCode}", Guid.NewGuid().ToString());
            return true;
        }

        public async Task<List<RegionPolicyRecord>> GetAllRegionPoliciesAsync()
        {
            return await _dbContext.RegionPolicies.AsNoTracking().OrderBy(r => r.RegionName).ToListAsync();
        }

        public async Task<RegionPolicyRecord> UpsertRegionPolicyAsync(RegionPolicyRecord regionPolicy, string adminUsername)
        {
            var existing = await _dbContext.RegionPolicies.FirstOrDefaultAsync(r => r.RegionName == regionPolicy.RegionName);
            if (existing != null)
            {
                existing.IsSubscriptionEnabled = regionPolicy.IsSubscriptionEnabled;
                existing.DefaultCurrency = regionPolicy.DefaultCurrency;
                existing.DefaultCurrencySymbol = regionPolicy.DefaultCurrencySymbol;
                existing.DefaultMonthlyPrice = regionPolicy.DefaultMonthlyPrice;
                existing.DefaultYearlyPrice = regionPolicy.DefaultYearlyPrice;
                existing.Description = regionPolicy.Description;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                return existing;
            }
            else
            {
                regionPolicy.Id = Guid.NewGuid();
                regionPolicy.UpdatedAtUtc = DateTime.UtcNow;
                _dbContext.RegionPolicies.Add(regionPolicy);
                await _dbContext.SaveChangesAsync();
                return regionPolicy;
            }
        }

        public string FormatPrice(decimal price, string currency, string symbol)
        {
            string cleanSymbol = string.IsNullOrWhiteSpace(symbol) ? "$" : symbol;
            return currency.ToUpperInvariant() switch
            {
                "BDT" or "INR" or "PKR" => $"{cleanSymbol}{Math.Floor(price):F0}",
                "JPY" => $"{cleanSymbol}{price:F0}",
                _ => $"{cleanSymbol}{price:F2}"
            };
        }

        public string DetectCountryFromHeadersOrIp(string? clientIp, IDictionary<string, string>? headers)
        {
            if (headers != null)
            {
                if (headers.TryGetValue("CF-IPCountry", out var cfCountry) && !string.IsNullOrWhiteSpace(cfCountry) && cfCountry.Length == 2)
                    return cfCountry.ToUpperInvariant();
                if (headers.TryGetValue("X-Country-Code", out var xCountry) && !string.IsNullOrWhiteSpace(xCountry) && xCountry.Length == 2)
                    return xCountry.ToUpperInvariant();
            }

            if (!string.IsNullOrWhiteSpace(clientIp))
            {
                if (clientIp.StartsWith("103.145.") || clientIp.StartsWith("103.205.") || clientIp.StartsWith("103.230.")) return "BD";
                if (clientIp.StartsWith("103.21.") || clientIp.StartsWith("103.22.") || clientIp.StartsWith("49.204.")) return "IN";
                if (clientIp.StartsWith("111.119.") || clientIp.StartsWith("175.107.")) return "PK";
                if (clientIp.StartsWith("82.165.") || clientIp.StartsWith("212.58.")) return "GB";
                if (clientIp.StartsWith("142.250.") || clientIp.StartsWith("172.217.") || clientIp.StartsWith("8.8.")) return "US";
            }

            return "BD"; // Default target
        }

        public string DetectRegionForCountry(string countryCode)
        {
            return countryCode.ToUpperInvariant() switch
            {
                "BD" or "IN" or "PK" or "NP" or "LK" or "BT" or "MV" => "South Asia",
                "TH" or "VN" or "ID" or "MY" or "PH" or "SG" or "JP" or "KR" or "CN" or "TW" or "HK" or "ASIA" => "Asia",
                "US" or "CA" or "MX" => "North America",
                "GB" or "DE" or "FR" or "IT" or "ES" or "NL" or "SE" or "NO" or "PL" => "Europe",
                "AE" or "SA" or "QA" or "KW" or "OM" or "BH" => "Middle East",
                "ZA" or "NG" or "EG" or "KE" or "GH" => "Africa",
                "BR" or "AR" or "CL" or "CO" or "PE" => "South America",
                "AU" or "NZ" => "Oceania",
                _ => "Global"
            };
        }
    }
}
