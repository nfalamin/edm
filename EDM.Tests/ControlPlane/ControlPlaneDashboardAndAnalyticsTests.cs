using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;

namespace EDM.Tests.ControlPlane
{
    public class ControlPlaneDashboardAndAnalyticsTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public ControlPlaneDashboardAndAnalyticsTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<string> GetAdminTokenAsync()
        {
            string adminUser = "superadmin_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{adminUser}@edm.control";
            string pwd = "SuperAdminPassword!2026";

            // Register
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = adminUser, Email = email, Password = pwd });

            // Upgrade user to SUPER_ADMIN directly in test DB
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                if (u != null)
                {
                    u.Role = UserRole.SUPER_ADMIN;
                    await db.SaveChangesAsync();
                }
            }

            // Login
            var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = pwd });
            var doc = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
            return doc.GetProperty("accessToken").GetString()!;
        }

        [Fact]
        public async Task Telemetry_RecordsValidEvent_And_RejectsDisallowedEvent()
        {
            Guid installId = Guid.NewGuid();

            // 1. Valid telemetry event
            var validRes = await _client.PostAsJsonAsync("/api/v1/telemetry/event", new
            {
                InstallationId = installId,
                EventName = "download_completed",
                Payload = new { url = "https://example.com/file.iso", sizeBytes = 104857600, durationMs = 2400 }
            });
            validRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 2. Disallowed event name
            var invalidRes = await _client.PostAsJsonAsync("/api/v1/telemetry/event", new
            {
                InstallationId = installId,
                EventName = "malicious_unregistered_event_name",
                Payload = new { test = 123 }
            });
            invalidRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task DashboardSummary_ReturnsAccurateAggregatesFromDatabase()
        {
            string adminToken = await GetAdminTokenAsync();

            var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/dashboard/summary");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            var res = await _client.SendAsync(req);
            res.StatusCode.Should().Be(HttpStatusCode.OK);

            var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
            doc.GetProperty("totalUsers").GetInt32().Should().BeGreaterThan(0);
            doc.GetProperty("currentRelease").GetString().Should().NotBeNull();
        }

        [Fact]
        public async Task Analytics_ReturnsDownloadAndPlatformMetrics()
        {
            string adminToken = await GetAdminTokenAsync();

            // Downloads analytics
            var dlReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/analytics/downloads?range=7d");
            dlReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            var dlRes = await _client.SendAsync(dlReq);
            dlRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Platforms analytics
            var platReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/analytics/platforms");
            platReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            var platRes = await _client.SendAsync(platReq);
            platRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task ReleaseManagement_CreateAndArchiveRelease_Workflow()
        {
            string adminToken = await GetAdminTokenAsync();
            string newVer = $"3.{Random.Shared.Next(10, 99)}.0";

            // Create Release
            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            createReq.Content = JsonContent.Create(new
            {
                Platform = 0, // DesktopWindows
                Version = newVer,
                MinimumSupportedVersion = "2.0.0",
                Title = $"EDM v{newVer} Turbo Release",
                ReleaseNotes = "High speed download engine improvements.",
                IsMandatory = false,
                Severity = 0, // Standard
                Artifacts = new[]
                {
                    new
                    {
                        ArtifactName = $"EDM_Setup_v{newVer}.exe",
                        DownloadUrl = $"https://releases.edm.com/desktop/EDM_Setup_v{newVer}.exe",
                        Sha256Hash = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899",
                        FileSizeBytes = 3600000,
                        SignatureBase64 = (string?)null
                    }
                }
            });

            var createRes = await _client.SendAsync(createReq);
            createRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var createDoc = await createRes.Content.ReadFromJsonAsync<JsonElement>();
            Guid releaseId = Guid.Parse(createDoc.GetProperty("releaseId").GetString()!);

            // Verify update check returns this new release
            var checkRes = await _client.PostAsJsonAsync("/api/v1/updates/check", new
            {
                Platform = 0,
                CurrentVersion = "1.0.0",
                InstallationId = Guid.NewGuid()
            });
            checkRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var checkDoc = await checkRes.Content.ReadFromJsonAsync<JsonElement>();
            checkDoc.GetProperty("updateAvailable").GetBoolean().Should().BeTrue();
            checkDoc.GetProperty("latestVersion").GetString().Should().Be(newVer);

            // Archive the release
            var arcReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/admin/releases/{releaseId}/archive");
            arcReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            var arcRes = await _client.SendAsync(arcReq);
            arcRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task DeviceAndSessionInspection_ReturnsData()
        {
            string adminToken = await GetAdminTokenAsync();

            // Devices
            var devReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/devices");
            devReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            var devRes = await _client.SendAsync(devReq);
            devRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Sessions
            var sessReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/sessions");
            sessReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            var sessRes = await _client.SendAsync(sessReq);
            sessRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Audit Logs
            var auditReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/audit-logs");
            auditReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            var auditRes = await _client.SendAsync(auditReq);
            auditRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
