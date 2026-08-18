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
    public class DashboardBackendIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public DashboardBackendIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<string> GetSuperAdminTokenAsync()
        {
            string username = $"super_{Guid.NewGuid():N}".Substring(0, 16);
            string email = $"{username}@edm.test";
            string password = "SuperSecretPassword!2026";

            var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Username = username,
                Email = email,
                Password = password
            });
            regRes.EnsureSuccessStatusCode();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstAsync(x => x.Email == email);
                u.Role = UserRole.SUPER_ADMIN;
                u.IsActive = true;
                await db.SaveChangesAsync();
            }

            var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                UsernameOrEmail = email,
                Password = password,
                RememberDevice = true
            });
            loginRes.EnsureSuccessStatusCode();

            var loginData = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
            return loginData.GetProperty("accessToken").GetString()!;
        }

        [Fact]
        public async Task Dashboard_Summary_And_Analytics_Return_Live_Database_Metrics()
        {
            string token = await GetSuperAdminTokenAsync();

            // 1. Dashboard Summary
            var sumReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/dashboard/summary");
            sumReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var sumRes = await _client.SendAsync(sumReq);
            sumRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var summary = await sumRes.Content.ReadFromJsonAsync<JsonElement>();
            summary.TryGetProperty("totalUsers", out var totalUsers).Should().BeTrue();
            totalUsers.GetInt32().Should().BeGreaterThan(0);
            summary.TryGetProperty("serverTimeUtc", out _).Should().BeTrue();

            // 2. Download Analytics
            var dlReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/analytics/downloads?range=30d");
            dlReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var dlRes = await _client.SendAsync(dlReq);
            dlRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var dlData = await dlRes.Content.ReadFromJsonAsync<JsonElement>();
            dlData.GetProperty("range").GetString().Should().Be("30d");

            // 3. User Analytics
            var usrReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/analytics/users?range=7d");
            usrReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var usrRes = await _client.SendAsync(usrReq);
            usrRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Platforms & Versions
            var platReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/analytics/platforms");
            platReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var platRes = await _client.SendAsync(platReq);
            platRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task User_Management_Real_Crud_And_Dynamic_Permission_Overrides()
        {
            string token = await GetSuperAdminTokenAsync();

            // 1. List Users
            var listReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/users?page=1&pageSize=10");
            listReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var listRes = await _client.SendAsync(listReq);
            listRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var listDoc = await listRes.Content.ReadFromJsonAsync<JsonElement>();
            listDoc.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);

            // 2. Create a test user to ban and override
            string targetUsername = $"target_{Guid.NewGuid():N}".Substring(0, 16);
            var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Username = targetUsername,
                Email = $"{targetUsername}@edm.local",
                Password = "Password123!"
            });
            var regDoc = await regRes.Content.ReadFromJsonAsync<JsonElement>();
            string userIdStr = regDoc.GetProperty("user").GetProperty("id").GetString()!;

            // 3. Dynamic Permission Grant
            var grantReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{userIdStr}/permissions/grant");
            grantReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            grantReq.Content = JsonContent.Create(new { PermissionCode = Permissions.ReleasesRollback });
            var grantRes = await _client.SendAsync(grantReq);
            grantRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Ban User
            var banReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/ban");
            banReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            banReq.Content = JsonContent.Create(new
            {
                TargetType = 0, // UserId
                TargetValue = userIdStr,
                Reason = "Integration test ban",
                DurationDays = 7
            });
            var banRes = await _client.SendAsync(banReq);
            banRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 5. Query user details
            var detailReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/users/{userIdStr}");
            detailReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var detailRes = await _client.SendAsync(detailReq);
            detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var detailDoc = await detailRes.Content.ReadFromJsonAsync<JsonElement>();
            detailDoc.GetProperty("isActive").GetBoolean().Should().BeFalse();

            // 6. Unban User
            var unbanReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/unban");
            unbanReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            unbanReq.Content = JsonContent.Create(new
            {
                TargetType = 0,
                TargetValue = userIdStr
            });
            var unbanRes = await _client.SendAsync(unbanReq);
            unbanRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 7. Revoke All Sessions
            var revokeReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/revoke-user-sessions/{userIdStr}");
            revokeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var revokeRes = await _client.SendAsync(revokeReq);
            revokeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Licensing_And_Plans_Real_Crud_Lifecycle()
        {
            string token = await GetSuperAdminTokenAsync();

            // 1. Get Commercial Plans
            var plansReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/licenses/plans");
            plansReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var plansRes = await _client.SendAsync(plansReq);
            plansRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var plansList = await plansRes.Content.ReadFromJsonAsync<List<JsonElement>>();
            plansList.Should().NotBeEmpty();
            string planId = plansList![0].GetProperty("id").GetString()!;

            // 2. Generate Commercial License
            var genReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/licenses/generate");
            genReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            genReq.Content = JsonContent.Create(new
            {
                PlanId = Guid.Parse(planId),
                MaxActivations = 5,
                DurationDays = 365
            });
            var genRes = await _client.SendAsync(genReq);
            genRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var genDoc = await genRes.Content.ReadFromJsonAsync<JsonElement>();
            string licenseId = genDoc.GetProperty("licenseId").GetString()!;
            string plainKey = genDoc.GetProperty("plaintextKey").GetString()!;
            plainKey.Should().StartWith("EDM-");

            // 3. Suspend License
            var suspReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/licenses/{licenseId}/suspend");
            suspReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            suspReq.Content = JsonContent.Create(new { Reason = "Temporary test suspension" });
            var suspRes = await _client.SendAsync(suspReq);
            suspRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Reactivate License
            var reactReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/licenses/{licenseId}/reactivate");
            reactReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var reactRes = await _client.SendAsync(reactReq);
            reactRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 5. Revoke License
            var revReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/licenses/{licenseId}/revoke");
            revReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            revReq.Content = JsonContent.Create(new { Reason = "Permanent revocation" });
            var revRes = await _client.SendAsync(revReq);
            revRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Support_Tickets_Real_Threading_And_Status_Lifecycle()
        {
            string token = await GetSuperAdminTokenAsync();

            // 1. Create Customer Support Ticket
            var createTicketRes = await _client.PostAsJsonAsync("/api/v1/support/tickets", new
            {
                CustomerName = "Alice Support Customer",
                CustomerEmail = "alice@example.com",
                Subject = "Cannot activate license key on secondary PC",
                Message = "Hello, my license activation failed with code ACTIVATION_LIMIT_EXCEEDED.",
                Category = 0, // Technical
                Priority = 1  // High
            });
            createTicketRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var ticketCreatedDoc = await createTicketRes.Content.ReadFromJsonAsync<JsonElement>();
            string ticketId = ticketCreatedDoc.GetProperty("ticketId").GetString()!;

            // 2. Query Ticket Details
            var getTicketReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/support/tickets/{ticketId}");
            getTicketReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var getTicketRes = await _client.SendAsync(getTicketReq);
            getTicketRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var ticketDoc = await getTicketRes.Content.ReadFromJsonAsync<JsonElement>();
            ticketDoc.GetProperty("subject").GetString().Should().Be("Cannot activate license key on secondary PC");

            // 3. Post Staff Reply
            var replyReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/support/tickets/{ticketId}/reply");
            replyReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            replyReq.Content = JsonContent.Create(new
            {
                MessageContent = "We have increased your device activation limit by +2 devices. Please try again."
            });
            var replyRes = await _client.SendAsync(replyReq);
            replyRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Update Status to Resolved
            var statusReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/support/tickets/{ticketId}/status");
            statusReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            statusReq.Content = JsonContent.Create(new { Status = 2 }); // Resolved
            var statusRes = await _client.SendAsync(statusReq);
            statusRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Notifications_And_Announcements_Real_Backend_Lifecycle()
        {
            string token = await GetSuperAdminTokenAsync();

            // 1. Fetch Notifications
            var notifReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/notifications");
            notifReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var notifRes = await _client.SendAsync(notifReq);
            notifRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 2. Mark Notifications as Read
            var markReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/notifications/mark-read");
            markReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var markRes = await _client.SendAsync(markReq);
            markRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. Create Announcement
            var createAnnReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/announcements");
            createAnnReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            createAnnReq.Content = JsonContent.Create(new
            {
                Title = "Scheduled Server Upgrade",
                Message = "We are upgrading our CDN infrastructure this Sunday at 02:00 UTC.",
                Severity = AnnouncementSeverity.Maintenance,
                Audience = TargetAudience.All
            });
            var createAnnRes = await _client.SendAsync(createAnnReq);
            createAnnRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Query Announcements
            var getAnnReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/announcements");
            getAnnReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var getAnnRes = await _client.SendAsync(getAnnReq);
            getAnnRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var annList = await getAnnRes.Content.ReadFromJsonAsync<List<JsonElement>>();
            annList.Should().NotBeEmpty();
        }

        [Fact]
        public async Task System_Health_Diagnostic_Probes_Are_Operational()
        {
            string token = await GetSuperAdminTokenAsync();

            var req = new HttpRequestMessage(HttpMethod.Get, "/health/diagnostics");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var res = await _client.SendAsync(req);
            res.StatusCode.Should().Be(HttpStatusCode.OK);

            var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
            doc.TryGetProperty("overallStatus", out _).Should().BeTrue();
            doc.TryGetProperty("components", out var components).Should().BeTrue();
            components.TryGetProperty("Database", out var dbComp).Should().BeTrue();
            dbComp.GetProperty("status").GetInt32().Should().Be(0); // Healthy
        }
    }
}
