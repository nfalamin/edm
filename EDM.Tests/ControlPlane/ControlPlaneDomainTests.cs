using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;
using EDM.ControlPlane.Api.Controllers;

namespace EDM.Tests.ControlPlane
{
    public class ControlPlaneDomainTests
    {
        private ControlPlaneDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
                .UseSqlite("DataSource=:memory:")
                .Options;

            var context = new ControlPlaneDbContext(options);
            context.Database.OpenConnection();
            context.Database.EnsureCreated();
            return context;
        }

        [Fact]
        public void Argon2idPasswordHasher_HashesAndVerifiesPasswordCorrectly()
        {
            var hasher = new Argon2idPasswordHasher();
            string rawPassword = "P@ssw0rdSecure!2026";

            string hash = hasher.HashPassword(rawPassword);

            hash.Should().StartWith("$argon2id$");
            hasher.VerifyPassword(rawPassword, hash).Should().BeTrue();
            hasher.VerifyPassword("WrongPassword123", hash).Should().BeFalse();
        }

        [Fact]
        public void PrivacySafeDeviceService_GeneratesValidInstallationIdAndMasksIp()
        {
            var deviceService = new PrivacySafeDeviceService();

            Guid installId1 = deviceService.GenerateInstallationId();
            Guid installId2 = deviceService.GenerateInstallationId();

            installId1.Should().NotBeEmpty();
            installId2.Should().NotBeEmpty();
            installId1.Should().NotBe(installId2);

            string maskedIpv4 = deviceService.AnonymizeIpAddress("192.168.1.155");
            maskedIpv4.Should().Be("192.168.1.0");

            string maskedIpv6 = deviceService.AnonymizeIpAddress("2001:0db8:85a3:0000:0000:8a2e:0370:7334");
            maskedIpv6.Should().StartWith("2001:db8:85a3::");
        }

        [Fact]
        public async Task ControlPlaneDbContext_PersistsEntitiesAndRelationshipsInSqlite()
        {
            using var db = CreateInMemoryDbContext();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "admin_super",
                Email = "super@edm.com",
                PasswordHash = "$argon2id$v=19$samplehash",
                Role = UserRole.SUPER_ADMIN,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            var device = new Device
            {
                Id = Guid.NewGuid(),
                InstallationId = Guid.NewGuid(),
                ClientType = ClientType.DesktopWindows,
                OsVersion = "Windows 11 Pro 24H2",
                AppVersion = "2.0.0",
                CoarseCountryCode = "US",
                CreatedAtUtc = DateTime.UtcNow
            };

            var release = new Release
            {
                Id = Guid.NewGuid(),
                Platform = ClientType.DesktopWindows,
                Version = "2.0.0",
                MinimumSupportedVersion = "1.0.0",
                Title = "EDM 2.0.0 Production",
                ReleaseNotes = "Full IDM feature parity and control plane foundation.",
                IsMandatory = false,
                Severity = ReleaseSeverity.Standard,
                PublishedAtUtc = DateTime.UtcNow
            };

            var artifact = new ReleaseArtifact
            {
                Id = Guid.NewGuid(),
                ReleaseId = release.Id,
                ArtifactName = "EDM_Setup.exe",
                DownloadUrl = "https://releases.edm.com/desktop/EDM_Setup.exe",
                Sha256Hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                FileSizeBytes = 3450000
            };

            db.Users.Add(user);
            db.Devices.Add(device);
            db.Releases.Add(release);
            db.ReleaseArtifacts.Add(artifact);

            await db.SaveChangesAsync();

            var retrievedRelease = await db.Releases
                .Include(r => r.Artifacts)
                .FirstOrDefaultAsync(r => r.Version == "2.0.0");

            retrievedRelease.Should().NotBeNull();
            retrievedRelease!.Artifacts.Should().HaveCount(1);
            retrievedRelease.Artifacts.First().ArtifactName.Should().Be("EDM_Setup.exe");
        }

        [Fact]
        public async Task UpdateController_ReturnsLatestReleaseCorrectly()
        {
            using var db = CreateInMemoryDbContext();

            var release = new Release
            {
                Id = Guid.NewGuid(),
                Platform = ClientType.DesktopWindows,
                Version = "2.5.0",
                MinimumSupportedVersion = "2.0.0",
                Title = "EDM 2.5.0 High Speed",
                ReleaseNotes = "Ultra speed update.",
                IsMandatory = true,
                Severity = ReleaseSeverity.Critical,
                PublishedAtUtc = DateTime.UtcNow
            };

            var artifact = new ReleaseArtifact
            {
                Id = Guid.NewGuid(),
                ReleaseId = release.Id,
                ArtifactName = "EDM_Setup.exe",
                DownloadUrl = "https://releases.edm.com/desktop/EDM_Setup_2.5.0.exe",
                Sha256Hash = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899",
                FileSizeBytes = 4000000
            };

            db.Releases.Add(release);
            db.ReleaseArtifacts.Add(artifact);
            await db.SaveChangesAsync();

            var releaseService = new ReleaseService(db);
            var controller = new UpdateController(releaseService, db);

            var checkRequest = new UpdateCheckRequest(
                Platform: ClientType.DesktopWindows,
                CurrentVersion: "2.0.0",
                InstallationId: Guid.NewGuid());

            var result = await controller.CheckForUpdateAsync(checkRequest);
            result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();

            var okObj = (Microsoft.AspNetCore.Mvc.OkObjectResult)result.Result!;
            var response = (UpdateCheckResponse)okObj.Value!;

            response.UpdateAvailable.Should().BeTrue();
            response.LatestVersion.Should().Be("2.5.0");
            response.IsMandatory.Should().BeTrue();
            response.DownloadUrl.Should().Be("https://releases.edm.com/desktop/EDM_Setup_2.5.0.exe");
        }

        [Fact]
        public async Task AuditLoggingService_AppendsImmutableRecordWithCorrelationId()
        {
            using var db = CreateInMemoryDbContext();
            var deviceService = new PrivacySafeDeviceService();
            var auditService = new AuditLoggingService(db, deviceService);

            Guid actorId = Guid.NewGuid();
            string correlationId = "corr-test-12345";

            await auditService.LogActionAsync(
                actorId: actorId,
                actorUsername: "sec_admin",
                action: "POLICY_CHANGED",
                targetEntity: "UpdatePolicy",
                targetId: "Desktop_Stable",
                detailsJson: "{\"rollout\": 50}",
                correlationId: correlationId,
                resultStatus: "SUCCESS",
                rawIpAddress: "10.0.0.45");

            var logEntry = await db.AuditLogs.FirstOrDefaultAsync(l => l.CorrelationId == correlationId);
            logEntry.Should().NotBeNull();
            logEntry!.ActorUsername.Should().Be("sec_admin");
            logEntry.Action.Should().Be("POLICY_CHANGED");
            logEntry.CoarseIpAddress.Should().Be("10.0.0.0");
        }
    }
}
