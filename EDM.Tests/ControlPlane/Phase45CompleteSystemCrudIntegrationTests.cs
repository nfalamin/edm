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
    public class Phase45CompleteSystemCrudIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public Phase45CompleteSystemCrudIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<(string Token, Guid UserId)> CreateAdminUserAsync()
        {
            string username = "admin_" + Guid.NewGuid().ToString("N")[..8];
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
        public async Task Phase45_User_Crud_Lifecycle_Update_Toggle_Delete()
        {
            var (adminToken, adminId) = await CreateAdminUserAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            // 1. Create a target user
            string targetUser = "user_" + Guid.NewGuid().ToString("N")[..8];
            string targetEmail = $"{targetUser}@test.com";
            var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = targetUser, Email = targetEmail, Password = "UserPassword!123" });
            regRes.EnsureSuccessStatusCode();

            Guid targetUserId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == targetEmail);
                targetUserId = u!.Id;
            }

            // 2. Update target user
            var updateRes = await _client.PutAsJsonAsync($"/api/v1/admin/users/{targetUserId}", new
            {
                DisplayName = "Updated Test User",
                Role = "ANALYST",
                IsActive = true
            });
            updateRes.EnsureSuccessStatusCode();

            // 3. Toggle user status (Suspend)
            var toggleRes = await _client.PostAsync($"/api/v1/admin/users/{targetUserId}/toggle-status", null);
            toggleRes.EnsureSuccessStatusCode();
            var toggleDoc = await toggleRes.Content.ReadFromJsonAsync<JsonElement>();
            toggleDoc.GetProperty("isActive").GetBoolean().Should().BeFalse();

            // 4. Delete user
            var deleteRes = await _client.DeleteAsync($"/api/v1/admin/users/{targetUserId}");
            deleteRes.EnsureSuccessStatusCode();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FindAsync(targetUserId);
                u.Should().BeNull();
            }
        }

        [Fact]
        public async Task Phase45_License_Crud_Generate_Revoke_Extend()
        {
            var (adminToken, _) = await CreateAdminUserAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            // 1. Generate License
            var genRes = await _client.PostAsJsonAsync("/api/v1/admin/licenses", new
            {
                UserEmail = "licensed_user@domain.com",
                Plan = "Pro_Monthly",
                MaxActivations = 5,
                DurationDays = 60
            });
            genRes.EnsureSuccessStatusCode();
            var genDoc = await genRes.Content.ReadFromJsonAsync<JsonElement>();
            string licenseKey = genDoc.GetProperty("licenseKey").GetString()!;
            Guid licenseId = genDoc.GetProperty("licenseId").GetGuid();
            licenseKey.Should().StartWith("EDM-");

            // 2. Extend License
            var extRes = await _client.PostAsJsonAsync($"/api/v1/admin/licenses/{licenseId}/extend", new
            {
                AdditionalDays = 30,
                Reason = "Admin test extension"
            });
            extRes.EnsureSuccessStatusCode();

            // 3. Revoke License
            var revokeRes = await _client.PostAsync($"/api/v1/admin/licenses/{licenseId}/revoke", null);
            revokeRes.EnsureSuccessStatusCode();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var lic = await db.Licenses.FindAsync(licenseId);
                lic.Should().NotBeNull();
                lic!.Status.Should().Be(LicenseStatus.Revoked);
            }
        }

        [Fact]
        public async Task Phase45_Plan_Management_Crud()
        {
            var (adminToken, _) = await CreateAdminUserAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            // 1. Create Plan
            var plan = new Plan
            {
                Code = "turbo_enterprise",
                Name = "Turbo Enterprise Dedicated",
                PriceMonthlyUsd = 49.99m,
                PriceYearlyUsd = 499.00m,
                MaxDevices = 10,
                MaxConcurrentDownloads = 64,
                IsActive = true
            };
            var createRes = await _client.PostAsJsonAsync("/api/v1/admin/plans", plan);
            createRes.EnsureSuccessStatusCode();
            var createdDoc = await createRes.Content.ReadFromJsonAsync<JsonElement>();
            Guid planId = createdDoc.GetProperty("id").GetGuid();

            // 2. Update Plan
            plan.Name = "Turbo Enterprise V2";
            plan.PriceMonthlyUsd = 59.99m;
            var updateRes = await _client.PutAsJsonAsync($"/api/v1/admin/plans/{planId}", plan);
            updateRes.EnsureSuccessStatusCode();

            // 3. List Plans
            var listRes = await _client.GetAsync("/api/v1/admin/plans");
            if (!listRes.IsSuccessStatusCode)
            {
                var errStr = await listRes.Content.ReadAsStringAsync();
                throw new Exception($"GET /api/v1/admin/plans failed with {listRes.StatusCode}: {errStr}");
            }
            var listDoc = await listRes.Content.ReadFromJsonAsync<JsonElement>();
            listDoc.ValueKind.Should().Be(JsonValueKind.Array);
            var plans = listDoc.EnumerateArray().ToList();
            plans.Should().Contain(p => p.GetProperty("id").GetGuid() == planId && p.GetProperty("priceMonthlyUsd").GetDecimal() == 59.99m);

            // 4. Delete Plan
            var delRes = await _client.DeleteAsync($"/api/v1/admin/plans/{planId}");
            delRes.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task Phase45_Transactions_And_Coupons_Endpoints()
        {
            var (adminToken, _) = await CreateAdminUserAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            // 1. Query Transactions
            var txnRes = await _client.GetAsync("/api/v1/admin/transactions?page=1&pageSize=10");
            txnRes.EnsureSuccessStatusCode();

            // 2. Query Receipt
            var receiptRes = await _client.GetAsync("/api/v1/admin/transactions/TXN-10001");
            receiptRes.EnsureSuccessStatusCode();
            var receiptDoc = await receiptRes.Content.ReadFromJsonAsync<JsonElement>();
            receiptDoc.GetProperty("status").GetString().Should().Be("Succeeded");

            // 3. Create & List Coupon
            var coupon = new PromotionRecord
            {
                PromoCode = "PHASE45TEST",
                DiscountPercent = 25,
                IsEnabled = true
            };
            var createCpnRes = await _client.PostAsJsonAsync("/api/v1/admin/coupons", coupon);
            createCpnRes.EnsureSuccessStatusCode();

            var listCpnRes = await _client.GetAsync("/api/v1/admin/coupons");
            listCpnRes.EnsureSuccessStatusCode();
            var cpnList = await listCpnRes.Content.ReadFromJsonAsync<List<PromotionRecord>>();
            cpnList.Should().Contain(c => c.PromoCode == "PHASE45TEST");
        }

        [Fact]
        public async Task Phase45_Deep_Dive_Analytics_Endpoints()
        {
            var (adminToken, _) = await CreateAdminUserAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            // 1. Revenue deep dive
            var revRes = await _client.GetAsync("/api/v1/admin/analytics/revenue");
            revRes.EnsureSuccessStatusCode();
            var revDoc = await revRes.Content.ReadFromJsonAsync<JsonElement>();
            revDoc.GetProperty("mrr").GetDecimal().Should().BeGreaterThan(0);

            // 2. Feature analytics deep dive
            var featRes = await _client.GetAsync("/api/v1/admin/analytics/features");
            featRes.EnsureSuccessStatusCode();
            var featDoc = await featRes.Content.ReadFromJsonAsync<JsonElement>();
            featDoc.GetProperty("totalTelemetryEvents").GetInt32().Should().BeGreaterThan(0);
        }
    }
}
