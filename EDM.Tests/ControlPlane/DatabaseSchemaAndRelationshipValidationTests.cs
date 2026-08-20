using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

namespace EDM.Tests.ControlPlane
{
    public class DatabaseSchemaAndRelationshipValidationTests
    {
        private ControlPlaneDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
                .UseSqlite($"Data Source=Test_Schema_{Guid.NewGuid():N}.db")
                .Options;

            var db = new ControlPlaneDbContext(options);
            db.Database.EnsureCreated();
            return db;
        }

        private ITokenService CreateTokenService()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Jwt:SecretKey", "EDM_Development_Super_Secret_Key_For_Jwt_Signing_2026_Minimum_256_Bits!" },
                    { "Jwt:Issuer", "EDM.ControlPlane" },
                    { "Jwt:Audience", "EDM.Clients" }
                })
                .Build();

            return new TokenService(config);
        }

        [Fact]
        public async Task All_Sixteen_Plus_Entities_Can_Be_Persisted_And_Queried()
        {
            using var db = CreateInMemoryDbContext();
            var hasher = new Argon2idPasswordHasher();
            var tokenService = CreateTokenService();

            // 1. User
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "test_architect",
                Email = "architect@edm.local",
                PasswordHash = hasher.HashPassword("ComplexSecretPassword123!"),
                Role = UserRole.ADMIN,
                IsActive = true
            };
            db.Users.Add(user);

            // 2. Plan
            var plan = new Plan
            {
                Id = Guid.NewGuid(),
                Code = "pro_annual",
                Name = "Pro Annual",
                Tier = PlanTier.Pro,
                PriceMonthlyUsd = 4.99m,
                PriceYearlyUsd = 49.99m,
                MaxDevices = 5,
                MaxConcurrentDownloads = 10,
                FeaturesJson = "[\"Turbo 32 sockets\",\"Media Sniffer\"]"
            };
            db.Plans.Add(plan);

            // 3. License
            string rawKey = "EDM-PRO-TEST-1234-5678";
            var license = new License
            {
                Id = Guid.NewGuid(),
                LicenseKeyHash = tokenService.HashToken(rawKey),
                KeyPrefix = "EDM-PRO-TEST",
                UserId = user.Id,
                PlanId = plan.Id,
                Status = LicenseStatus.Active,
                MaxActivations = 5,
                CurrentActivations = 1
            };
            db.Licenses.Add(license);

            // 4. Device & Session
            var device = new Device
            {
                Id = Guid.NewGuid(),
                InstallationId = Guid.NewGuid(),
                ClientType = ClientType.DesktopWindows,
                OsVersion = "Windows 11 Build 22631",
                AppVersion = "2.1.0"
            };
            db.Devices.Add(device);

            var session = new Session
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                DeviceId = device.Id,
                AccessTokenHash = tokenService.HashToken("access_token_sample"),
                FamilyId = Guid.NewGuid(),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
            };
            db.Sessions.Add(session);

            // 5. Release & ReleaseArtifact
            var release = new Release
            {
                Id = Guid.NewGuid(),
                Platform = ClientType.DesktopWindows,
                Version = "2.1.0",
                Channel = "stable",
                Title = "EDM 2.1.0",
                MinimumSupportedVersion = "1.0.0",
                IsPublished = true,
                CreatedByUserId = user.Id
            };
            var artifact = new ReleaseArtifact
            {
                Id = Guid.NewGuid(),
                ReleaseId = release.Id,
                ArtifactName = "EDM_Installer.exe",
                Architecture = "x64",
                DownloadUrl = "https://cdn.edm.local/installer.exe",
                Sha256Hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                FileSizeBytes = 10485760
            };
            release.Artifacts.Add(artifact);
            db.Releases.Add(release);

            // 6. DownloadRecord
            var downloadRecord = new DownloadRecord
            {
                Id = Guid.NewGuid(),
                ReleaseArtifactId = artifact.Id,
                LicenseId = license.Id,
                DeviceId = device.Id,
                ClientIpCoarse = "192.168.1.0/24",
                CountryCode = "US",
                BytesTransferred = 10485760,
                Status = DownloadStatus.Completed
            };
            db.DownloadRecords.Add(downloadRecord);

            // 7. WebsiteContent & PricingTier
            var websiteContent = new WebsiteContent
            {
                Id = Guid.NewGuid(),
                SectionKey = "hero",
                Title = "High Speed Downloads",
                ContentJson = "{\"title\":\"Supercharged Download Manager\"}",
                Locale = "en",
                Version = 1,
                UpdatedByUserId = user.Id
            };
            var pricingTier = new PricingTier
            {
                Id = Guid.NewGuid(),
                PlanId = plan.Id,
                DisplayName = "Pro Tier",
                MonthlyPrice = 4.99m,
                YearlyPrice = 49.99m,
                SortOrder = 1,
                IsActive = true
            };
            db.WebsiteContents.Add(websiteContent);
            db.PricingTiers.Add(pricingTier);

            // 8. Announcement & AdminNotification
            var announcement = new Announcement
            {
                Id = Guid.NewGuid(),
                Title = "Scheduled Maintenance",
                Message = "Infrastructure update this Saturday",
                Severity = AnnouncementSeverity.Warning,
                Audience = TargetAudience.All,
                StartsAtUtc = DateTime.UtcNow,
                IsActive = true
            };
            var notification = new AdminNotification
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Title = "Security Alert",
                Message = "New login from unknown device",
                Type = NotificationType.SecurityAlert,
                IsRead = false
            };
            db.Announcements.Add(announcement);
            db.AdminNotifications.Add(notification);

            // 9. SupportTicket & SupportMessage
            var ticket = new SupportTicket
            {
                Id = Guid.NewGuid(),
                TicketNumber = "EDM-TK-9001",
                UserId = user.Id,
                CustomerEmail = "customer@example.com",
                CustomerName = "John Customer",
                Subject = "Download socket question",
                Category = TicketCategory.Technical,
                Priority = TicketPriority.High,
                Status = TicketStatus.Open,
                AssignedAdminId = user.Id
            };
            var message = new SupportMessage
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                SenderName = "John Customer",
                SenderType = MessageSenderType.Customer,
                MessageContent = "How do I configure 32 sockets for multi-stream downloads?"
            };
            ticket.Messages.Add(message);
            db.SupportTickets.Add(ticket);

            // 10. SystemHealthSnapshot & SystemMetric
            var snapshot = new SystemHealthSnapshot
            {
                Id = Guid.NewGuid(),
                ComponentName = "Database",
                Status = HealthStatus.Healthy,
                LatencyMs = 2,
                DetailsJson = "{\"status\":\"ok\"}"
            };
            var metric = new SystemMetric
            {
                Id = Guid.NewGuid(),
                MetricName = "active_downloads",
                MetricValue = 142.0,
                DimensionsJson = "{\"region\":\"us-east\"}"
            };
            db.SystemHealthSnapshots.Add(snapshot);
            db.SystemMetrics.Add(metric);

            // 11. RolePermission & UserPermissionOverride
            var rolePerm = new RolePermission
            {
                Id = Guid.NewGuid(),
                Role = UserRole.ADMIN,
                PermissionCode = Permissions.ReleasesCreate
            };
            var userOverride = new UserPermissionOverride
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                PermissionCode = Permissions.ReleasesRollback,
                IsGranted = true
            };
            db.RolePermissions.Add(rolePerm);
            db.UserPermissionOverrides.Add(userOverride);

            // 12. AuditLog & TelemetryEvent
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorId = user.Id,
                ActorUsername = user.Username,
                Action = "TEST_VALIDATION",
                TargetEntity = "System",
                ResultStatus = "SUCCESS",
                CorrelationId = Guid.NewGuid().ToString("N")
            };
            var telemetry = new TelemetryEvent
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                EventName = "download_completed",
                EventPayloadJson = "{\"bytes\":10485760}"
            };
            db.AuditLogs.Add(auditLog);
            db.TelemetryEvents.Add(telemetry);

            // Save changes to SQLite
            int written = await db.SaveChangesAsync();
            written.Should().BeGreaterThan(10);

            // Verify retrieval & relationships
            var loadedUser = await db.Users
                .Include(u => u.Licenses)
                .Include(u => u.Sessions)
                .Include(u => u.SupportTickets)
                .Include(u => u.PermissionOverrides)
                .FirstOrDefaultAsync(u => u.Id == user.Id);

            loadedUser.Should().NotBeNull();
            loadedUser!.Licenses.Should().HaveCount(1);
            loadedUser.Sessions.Should().HaveCount(1);
            loadedUser.SupportTickets.Should().HaveCount(1);
            loadedUser.PermissionOverrides.Should().HaveCount(1);

            var loadedRelease = await db.Releases
                .Include(r => r.Artifacts)
                .FirstOrDefaultAsync(r => r.Id == release.Id);

            loadedRelease.Should().NotBeNull();
            loadedRelease!.Artifacts.Should().HaveCount(1);
        }

        [Fact]
        public async Task Passwords_And_Tokens_Are_Strictly_Stored_As_Hashes()
        {
            using var db = CreateInMemoryDbContext();
            var hasher = new Argon2idPasswordHasher();
            var tokenService = CreateTokenService();

            string rawPassword = "SuperSecretAdminPassword!999";
            string rawKey = "EDM-PRO-9999-8888-7777-6666";

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "hash_tester",
                Email = "hash@edm.local",
                PasswordHash = hasher.HashPassword(rawPassword),
                Role = UserRole.ADMIN
            };
            db.Users.Add(user);

            var plan = new Plan
            {
                Id = Guid.NewGuid(),
                Code = "hash_plan",
                Name = "Hash Plan",
                Tier = PlanTier.Pro
            };
            db.Plans.Add(plan);

            var license = new License
            {
                Id = Guid.NewGuid(),
                LicenseKeyHash = tokenService.HashToken(rawKey),
                KeyPrefix = "EDM-PRO-9999",
                PlanId = plan.Id
            };
            db.Licenses.Add(license);

            await db.SaveChangesAsync();

            var loadedUser = await db.Users.FirstAsync(u => u.Id == user.Id);
            var loadedLicense = await db.Licenses.FirstAsync(l => l.Id == license.Id);

            // 1. Password must be Argon2id hashed
            loadedUser.PasswordHash.Should().NotBe(rawPassword);
            loadedUser.PasswordHash.Should().StartWith("$argon2id$");
            hasher.VerifyPassword(rawPassword, loadedUser.PasswordHash).Should().BeTrue();

            // 2. License key must be SHA-256 hashed (64 hex characters)
            loadedLicense.LicenseKeyHash.Should().NotBe(rawKey);
            loadedLicense.LicenseKeyHash.Length.Should().Be(64);
            tokenService.HashToken(rawKey).Should().Be(loadedLicense.LicenseKeyHash);
        }

        [Fact]
        public async Task Unique_Constraints_Are_Enforced()
        {
            using var db = CreateInMemoryDbContext();
            var hasher = new Argon2idPasswordHasher();

            var user1 = new User
            {
                Id = Guid.NewGuid(),
                Username = "duplicate_test",
                Email = "unique@edm.local",
                PasswordHash = hasher.HashPassword("Password!123"),
                Role = UserRole.ADMIN
            };
            db.Users.Add(user1);
            await db.SaveChangesAsync();

            // Attempt duplicate email
            var user2 = new User
            {
                Id = Guid.NewGuid(),
                Username = "different_name",
                Email = "unique@edm.local",
                PasswordHash = hasher.HashPassword("Password!123"),
                Role = UserRole.ADMIN
            };
            db.Users.Add(user2);

            Func<Task> act = async () => await db.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }
    }
}
