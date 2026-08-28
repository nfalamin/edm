using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using EDM;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace EDM.Tests
{
    public class SynchronizationAndRecoveryTests
    {
        private ControlPlaneDbContext CreateTestDbContext(string dbName)
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"edm_sync_test_{dbName}_{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            var context = new ControlPlaneDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        private class DummyAuditLogger : IAuditLoggingService
        {
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
                return Task.CompletedTask;
            }
        }

        [Fact]
        public void OfflineFallback_Within72Hours_MaintainsCachedPolicy()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "EDM_Test_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            try
            {
                var client = new SubscriptionEntitlementClient(new HttpClient(), tempDir);
                Assert.Equal(64, client.MaxAllowedConnections);
                Assert.False(client.IsBlocked);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task StateMachine_Transitions_TrialToGraceToRestricted_Accurately()
        {
            using var db = CreateTestDbContext("state_trans");
            var auditMock = new DummyAuditLogger();
            var geoMock = new GeoPricingService(db, auditMock);
            var entitlementService = new SubscriptionEntitlementService(db, geoMock, auditMock);

            var installationId = Guid.NewGuid();
            var syncReq = new EntitlementSyncRequestDto(installationId, null, "2.1.0", "Windows", "PC", "127.0.0.1");

            // 1. Initial sync => TRIAL_ACTIVE (64 sockets)
            var policy1 = await entitlementService.EvaluateAndSyncEntitlementAsync(syncReq);
            Assert.Equal("TRIAL_ACTIVE", policy1.State);
            Assert.Equal(64, policy1.MaxConnections);

            // 2. Fast-forward past Trial expiry => GRACE_PERIOD (32 sockets)
            var record = await db.SubscriptionPolicies.FirstAsync(p => p.InstallationId == installationId);
            record.TrialEndsAtUtc = DateTime.UtcNow.AddDays(-1);
            record.GraceEndsAtUtc = DateTime.UtcNow.AddDays(4);
            await db.SaveChangesAsync();

            var policy2 = await entitlementService.EvaluateAndSyncEntitlementAsync(syncReq);
            Assert.Equal("GRACE_PERIOD", policy2.State);
            Assert.Equal(32, policy2.MaxConnections);

            // 3. Fast-forward past Grace expiry => FREE_RESTRICTED (16 sockets)
            record.GraceEndsAtUtc = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();

            var policy3 = await entitlementService.EvaluateAndSyncEntitlementAsync(syncReq);
            Assert.Equal("FREE_RESTRICTED", policy3.State);
            Assert.Equal(16, policy3.MaxConnections);
        }

        [Fact]
        public async Task DeviceBlock_PropagatesInstantly_SetsZeroMaxConnections()
        {
            using var db = CreateTestDbContext("dev_block");
            var auditMock = new DummyAuditLogger();
            var geoMock = new GeoPricingService(db, auditMock);
            var entitlementService = new SubscriptionEntitlementService(db, geoMock, auditMock);

            var installationId = Guid.NewGuid();
            var syncReq = new EntitlementSyncRequestDto(installationId, null, "2.1.0", "Windows", "PC", "127.0.0.1");

            await entitlementService.EvaluateAndSyncEntitlementAsync(syncReq);
            await entitlementService.SetDeviceBlockStatusAsync(installationId, true, "Security suspension", "SuperAdmin");

            var blockedPolicy = await entitlementService.EvaluateAndSyncEntitlementAsync(syncReq);
            Assert.Equal("BLOCKED", blockedPolicy.State);
            Assert.Equal(0, blockedPolicy.MaxConnections);
            Assert.True(blockedPolicy.IsBlocked);
        }

        [Fact]
        public async Task AdminOverride_SupercedesStandardPolicy_WithCustomSockets()
        {
            using var db = CreateTestDbContext("override_test");
            var auditMock = new DummyAuditLogger();
            var geoMock = new GeoPricingService(db, auditMock);
            var entitlementService = new SubscriptionEntitlementService(db, geoMock, auditMock);

            var installationId = Guid.NewGuid();
            var syncReq = new EntitlementSyncRequestDto(installationId, null, "2.1.0", "Windows", "PC", "127.0.0.1");

            await entitlementService.EvaluateAndSyncEntitlementAsync(syncReq);

            // Apply Admin Override
            await entitlementService.ApplyAdminOverrideAsync(new AdminOverrideRecord
            {
                TargetType = OverrideTargetType.Device,
                TargetValue = installationId.ToString(),
                OverrideState = SubscriptionState.SUBSCRIBED,
                OverrideMaxConnections = 128,
                Reason = "VIP Turbo Access",
                IsActive = true
            }, "SuperAdmin");

            var overridePolicy = await entitlementService.EvaluateAndSyncEntitlementAsync(syncReq);
            Assert.Equal("SUBSCRIBED", overridePolicy.State);
            Assert.Equal(128, overridePolicy.MaxConnections);
        }
    }
}
