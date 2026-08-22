using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using EDM;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

namespace EDM.Tests
{
    public class SubscriptionAndGeoPricingTests
    {
        private ControlPlaneDbContext CreateInMemoryDbContext(string dbName)
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"edm_test_{dbName}_{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            var context = new ControlPlaneDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        private class DummyAuditLogger : IAuditLoggingService
        {
            public readonly List<string> LoggedActions = new();

            public Task LogActionAsync(
                Guid? actorId,
                string actorUsername,
                string action,
                string targetEntity,
                string? targetId,
                string detailsJson,
                string correlationId,
                string resultStatus = "SUCCESS",
                string? rawIpAddress = null)
            {
                LoggedActions.Add(action);
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task FreeTrial_InitialEvaluation_Grants10DaysAnd64Connections()
        {
            var db = CreateInMemoryDbContext("Trial1");
            var audit = new DummyAuditLogger();
            var geoService = new GeoPricingService(db, audit);
            var entitlementService = new SubscriptionEntitlementService(db, geoService, audit);

            var installationId = Guid.NewGuid();
            var request = new EntitlementSyncRequestDto(installationId, null, "2.1.0", "Win11", "TestPC", "103.145.112.45");

            var policy = await entitlementService.EvaluateAndSyncEntitlementAsync(request);

            Assert.Equal("TRIAL_ACTIVE", policy.State);
            Assert.Equal(64, policy.MaxConnections);
            Assert.Equal(10, policy.TrialDaysRemaining);
            Assert.False(policy.IsBlocked);
            Assert.True(policy.FeatureFlags["premium_download"]);
            Assert.True(policy.FeatureFlags["max_connections_64"]);
            Assert.Contains("free trial is active", policy.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GracePeriod_AfterTrialExpires_RestrictsTo32Connections()
        {
            var db = CreateInMemoryDbContext("Grace1");
            var audit = new DummyAuditLogger();
            var geoService = new GeoPricingService(db, audit);
            var entitlementService = new SubscriptionEntitlementService(db, geoService, audit);

            var installationId = Guid.NewGuid();
            
            var policyRecord = new SubscriptionPolicyRecord
            {
                Id = Guid.NewGuid(),
                InstallationId = installationId,
                CurrentState = SubscriptionState.TRIAL_ACTIVE,
                TrialStartedAtUtc = DateTime.UtcNow.AddDays(-11),
                TrialEndsAtUtc = DateTime.UtcNow.AddDays(-1),
                GraceEndsAtUtc = DateTime.UtcNow.AddDays(4),
                MaxConnections = 64
            };
            db.SubscriptionPolicies.Add(policyRecord);
            await db.SaveChangesAsync();

            var request = new EntitlementSyncRequestDto(installationId);
            var policy = await entitlementService.EvaluateAndSyncEntitlementAsync(request);

            Assert.Equal("GRACE_PERIOD", policy.State);
            Assert.Equal(32, policy.MaxConnections);
            Assert.Equal(0, policy.TrialDaysRemaining);
            Assert.True(policy.GraceDaysRemaining > 0);
            Assert.Contains("trial has ended", policy.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task FreeRestricted_AfterGraceExpires_RestrictsTo16Connections()
        {
            var db = CreateInMemoryDbContext("Restricted1");
            var audit = new DummyAuditLogger();
            var geoService = new GeoPricingService(db, audit);
            var entitlementService = new SubscriptionEntitlementService(db, geoService, audit);

            var installationId = Guid.NewGuid();
            
            var policyRecord = new SubscriptionPolicyRecord
            {
                Id = Guid.NewGuid(),
                InstallationId = installationId,
                CurrentState = SubscriptionState.TRIAL_ACTIVE,
                TrialStartedAtUtc = DateTime.UtcNow.AddDays(-20),
                TrialEndsAtUtc = DateTime.UtcNow.AddDays(-10),
                GraceEndsAtUtc = DateTime.UtcNow.AddDays(-5),
                MaxConnections = 64
            };
            db.SubscriptionPolicies.Add(policyRecord);
            await db.SaveChangesAsync();

            var request = new EntitlementSyncRequestDto(installationId);
            var policy = await entitlementService.EvaluateAndSyncEntitlementAsync(request);

            Assert.Equal("FREE_RESTRICTED", policy.State);
            Assert.Equal(16, policy.MaxConnections);
            Assert.False(policy.FeatureFlags["premium_download"]);
            Assert.False(policy.FeatureFlags["max_connections_64"]);
            Assert.Contains("Free Mode", policy.StatusMessage);
        }

        [Fact]
        public async Task GlobalSubscriptionSwitch_WhenDisabled_DisablesSubscriptionAvailability_WithoutRevokingSubscribers()
        {
            var db = CreateInMemoryDbContext("GlobalSwitch1");
            var audit = new DummyAuditLogger();
            var geoService = new GeoPricingService(db, audit);
            var entitlementService = new SubscriptionEntitlementService(db, geoService, audit);

            // 1. Turn Global Switch OFF
            await entitlementService.SetGlobalSubscriptionStatusAsync(false, "System maintenance", "SuperAdmin");
            Assert.Contains("GLOBAL_SUBSCRIPTION_DISABLED", audit.LoggedActions);

            var installationId = Guid.NewGuid();
            var request = new EntitlementSyncRequestDto(installationId);

            var policy = await entitlementService.EvaluateAndSyncEntitlementAsync(request);
            Assert.False(policy.IsSubscriptionAvailable);

            // 2. Existing subscriber maintains SUBSCRIBED state
            var subRecord = new SubscriptionPolicyRecord
            {
                Id = Guid.NewGuid(),
                InstallationId = Guid.NewGuid(),
                CurrentState = SubscriptionState.SUBSCRIBED,
                SubscriptionExpiresAtUtc = DateTime.UtcNow.AddDays(30),
                MaxConnections = 64
            };
            db.SubscriptionPolicies.Add(subRecord);
            await db.SaveChangesAsync();

            var subPolicy = await entitlementService.EvaluateAndSyncEntitlementAsync(new EntitlementSyncRequestDto(subRecord.InstallationId));
            Assert.Equal("SUBSCRIBED", subPolicy.State);
            Assert.Equal(64, subPolicy.MaxConnections);
            Assert.False(subPolicy.IsSubscriptionAvailable);
        }

        [Fact]
        public async Task AsiaMasterSwitch_WhenDisabled_DisablesAsianCountries_LeavesWestAvailable()
        {
            var db = CreateInMemoryDbContext("AsiaSwitch1");
            var audit = new DummyAuditLogger();
            var geoService = new GeoPricingService(db, audit);
            var entitlementService = new SubscriptionEntitlementService(db, geoService, audit);

            // 1. Turn Asia Switch OFF
            await entitlementService.SetAsiaSubscriptionStatusAsync(false, "Regional banking gateway update", "SuperAdmin");
            Assert.Contains("ASIA_SUBSCRIPTION_DISABLED", audit.LoggedActions);

            // 2. Bangladesh Request (Asia) => Subscription NOT available
            var bdPolicy = await entitlementService.EvaluateAndSyncEntitlementAsync(new EntitlementSyncRequestDto(Guid.NewGuid(), ClientIp: "103.145.112.45"));
            Assert.False(bdPolicy.IsSubscriptionAvailable);

            // 3. United States Request (North America) => Subscription IS available
            var usPolicy = await entitlementService.EvaluateAndSyncEntitlementAsync(new EntitlementSyncRequestDto(Guid.NewGuid(), ClientIp: "142.250.190.46"));
            Assert.True(usPolicy.IsSubscriptionAvailable);
        }

        [Fact]
        public async Task RegionPolicyResolution_Resolves_Europe_Africa_And_MiddleEast()
        {
            var db = CreateInMemoryDbContext("Region1");
            var audit = new DummyAuditLogger();
            var geoService = new GeoPricingService(db, audit);

            // 1. France (FR -> Europe)
            var frRule = await geoService.GetPricingRuleForCountryAsync("FR");
            Assert.Equal("Europe", frRule.Region);
            Assert.Equal("EUR", frRule.Currency);
            Assert.Equal(7.99m, frRule.MonthlyPrice);

            // 2. Egypt (EG -> Africa)
            var egRule = await geoService.GetPricingRuleForCountryAsync("EG");
            Assert.Equal("Africa", egRule.Region);
            Assert.Equal(2.99m, egRule.MonthlyPrice);

            // 3. Saudi Arabia (SA -> Middle East)
            var saRule = await geoService.GetPricingRuleForCountryAsync("SA");
            Assert.Equal("Middle East", saRule.Region);
            Assert.Equal(5.99m, saRule.MonthlyPrice);
        }

        [Fact]
        public async Task DeviceBlock_And_Unblock_WorksAuthoritatively()
        {
            var db = CreateInMemoryDbContext("Test_Block_1");
            var audit = new DummyAuditLogger();
            var geoService = new GeoPricingService(db, audit);
            var entitlementService = new SubscriptionEntitlementService(db, geoService, audit);

            var installationId = Guid.NewGuid();
            var request = new EntitlementSyncRequestDto(installationId);

            await entitlementService.EvaluateAndSyncEntitlementAsync(request);

            // Admin Blocks Device
            await entitlementService.SetDeviceBlockStatusAsync(installationId, true, "Security policy violation");

            var blockedPolicy = await entitlementService.EvaluateAndSyncEntitlementAsync(request);
            Assert.Equal("BLOCKED", blockedPolicy.State);
            Assert.Equal(0, blockedPolicy.MaxConnections);
            Assert.True(blockedPolicy.IsBlocked);

            // Admin Unblocks Device
            await entitlementService.SetDeviceBlockStatusAsync(installationId, false);

            var unblockedPolicy = await entitlementService.EvaluateAndSyncEntitlementAsync(request);
            Assert.Equal("TRIAL_ACTIVE", unblockedPolicy.State);
            Assert.Equal(64, unblockedPolicy.MaxConnections);
            Assert.False(unblockedPolicy.IsBlocked);
        }

        [Fact]
        public async Task TrialExtension_ExtendsDays_And_RestoresActiveState()
        {
            var db = CreateInMemoryDbContext("Test_Extend_1");
            var audit = new DummyAuditLogger();
            var geoService = new GeoPricingService(db, audit);
            var entitlementService = new SubscriptionEntitlementService(db, geoService, audit);

            var installationId = Guid.NewGuid();
            var request = new EntitlementSyncRequestDto(installationId);

            await entitlementService.EvaluateAndSyncEntitlementAsync(request);

            await entitlementService.ExtendTrialAsync(installationId, 15, "Special beta tester extension");

            var policy = await entitlementService.EvaluateAndSyncEntitlementAsync(request);
            Assert.Equal("TRIAL_ACTIVE", policy.State);
            Assert.True(policy.TrialDaysRemaining >= 15);
        }

        [Fact]
        public async Task GeoPricing_Resolves_BD_IN_PK_Asia_And_US_Accurately()
        {
            var db = CreateInMemoryDbContext("Test_GeoPricing_1");
            var audit = new DummyAuditLogger();
            var geoService = new GeoPricingService(db, audit);

            // 1. Bangladesh
            var bdRule = await geoService.GetPricingRuleForCountryAsync("BD");
            Assert.Equal("BDT", bdRule.Currency);
            Assert.Equal(63.00m, bdRule.MonthlyPrice);
            Assert.Equal("৳63", geoService.FormatPrice(bdRule.MonthlyPrice, bdRule.Currency, bdRule.CurrencySymbol));

            // 2. India
            var inRule = await geoService.GetPricingRuleForCountryAsync("IN");
            Assert.Equal("INR", inRule.Currency);
            Assert.Equal(63.00m, inRule.MonthlyPrice);
            Assert.Equal("₹63", geoService.FormatPrice(inRule.MonthlyPrice, inRule.Currency, inRule.CurrencySymbol));

            // 3. Pakistan
            var pkRule = await geoService.GetPricingRuleForCountryAsync("PK");
            Assert.Equal("PKR", pkRule.Currency);
            Assert.Equal(63.00m, pkRule.MonthlyPrice);
            Assert.Equal("₨63", geoService.FormatPrice(pkRule.MonthlyPrice, pkRule.Currency, pkRule.CurrencySymbol));

            // 4. Other Asian country (e.g. Thailand - TH)
            var asiaRule = await geoService.GetPricingRuleForCountryAsync("TH");
            Assert.Equal(2.99m, asiaRule.MonthlyPrice);

            // 5. United States
            var usRule = await geoService.GetPricingRuleForCountryAsync("US");
            Assert.Equal("USD", usRule.Currency);
            Assert.Equal(9.99m, usRule.MonthlyPrice);
            Assert.Equal("$9.99", geoService.FormatPrice(usRule.MonthlyPrice, usRule.Currency, usRule.CurrencySymbol));
        }

        [Fact]
        public void CryptographicSignature_Validation_MatchesAuthoritativeServerKey()
        {
            const string key = "EDM_CONTROL_PLANE_SECRET_SIGNING_KEY_2026_V210";
            var installationId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            string raw = $"{installationId}|TRIAL_ACTIVE|64|{now.AddDays(10):O}|{now:O}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
            string expectedSig = Convert.ToHexString(hash).ToLowerInvariant();

            var policy = new ClientEntitlementPolicy(
                InstallationId: installationId,
                UserId: null,
                State: "TRIAL_ACTIVE",
                PlanCode: "free_trial",
                PlanTier: "Trial",
                MaxConnections: 64,
                MaxConcurrentDownloads: 8,
                TrialDaysRemaining: 10,
                GraceDaysRemaining: 5,
                ExpiresAtUtc: now.AddDays(10),
                FeatureFlags: null,
                IsBlocked: false,
                BlockReason: null,
                StatusMessage: "Trial Active",
                CountryCode: "BD",
                Currency: "BDT",
                MonthlyPrice: 63.00m,
                FormattedPrice: "৳63 / mo",
                PolicyVersion: 1,
                OfflineGraceHours: 72,
                ServerTimeUtc: now,
                Signature: expectedSig);

            Assert.Equal(expectedSig, policy.Signature);
        }
    }
}
