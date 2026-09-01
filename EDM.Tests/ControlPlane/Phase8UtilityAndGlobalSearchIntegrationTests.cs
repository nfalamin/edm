using System;
using System.Collections.Generic;
using System.Linq;
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
    public class Phase8UtilityAndGlobalSearchIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public Phase8UtilityAndGlobalSearchIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<(string Token, Guid UserId)> CreateAdminUserAsync()
        {
            string username = "admin_p8_" + Guid.NewGuid().ToString("N")[..8];
            string email = $"{username}@edm.local";
            string password = "AdminPassword!2026";

            var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });
            regRes.EnsureSuccessStatusCode();

            Guid userId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                u!.Role = UserRole.SUPER_ADMIN;
                u.IsActive = true;
                await db.SaveChangesAsync();
                userId = u.Id;
            }

            var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = username, Password = password });
            var doc = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
            string token = doc.GetProperty("accessToken").GetString()!;

            return (token, userId);
        }

        [Fact]
        public async Task Phase8_System_Health_And_Api_Status_Probes()
        {
            var (adminToken, _) = await CreateAdminUserAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            var res = await _client.GetAsync("/api/v1/admin/system/health");
            res.StatusCode.Should().Be(HttpStatusCode.OK);

            var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
            doc.GetProperty("isHealthy").GetBoolean().Should().BeTrue();
            doc.TryGetProperty("components", out var comps).Should().BeTrue();
            comps.ValueKind.Should().Be(JsonValueKind.Object);
        }

        [Fact]
        public async Task Phase8_Multi_Domain_Search_Aggregates()
        {
            var (adminToken, _) = await CreateAdminUserAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            // 1. Search Users
            var usersRes = await _client.GetAsync("/api/v1/admin/users?search=admin&limit=5");
            usersRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var usersDoc = await usersRes.Content.ReadFromJsonAsync<JsonElement>();
            usersDoc.GetProperty("users").GetArrayLength().Should().BeGreaterOrEqualTo(1);

            // 2. Search Releases
            var relRes = await _client.GetAsync("/api/v1/admin/updates?includeDrafts=true");
            relRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. Search Licenses
            var licRes = await _client.GetAsync("/api/v1/admin/licenses");
            licRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Phase8_Consolidated_Audit_Report_Export_Data()
        {
            var (adminToken, _) = await CreateAdminUserAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            // Query audit logs
            var auditRes = await _client.GetAsync("/api/v1/admin/audit-logs?limit=50");
            auditRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Query country pricing
            var pricingRes = await _client.GetAsync("/api/v1/admin/country-pricing");
            pricingRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Query transactions
            var txnRes = await _client.GetAsync("/api/v1/admin/transactions");
            txnRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
