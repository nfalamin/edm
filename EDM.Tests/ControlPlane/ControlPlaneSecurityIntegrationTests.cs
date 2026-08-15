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
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;

namespace EDM.Tests.ControlPlane
{
    public class ControlPlaneTestFactory : WebApplicationFactory<EDM.ControlPlane.Api.Program>
    {
        private readonly string _dbName = "Test_Db_" + Guid.NewGuid().ToString("N");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Replace DbContext with in-memory SQLite for testing
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ControlPlaneDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ControlPlaneDbContext>(options =>
                {
                    options.UseSqlite($"Data Source={_dbName}.db");
                });

                // Build service provider and create database
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                db.Database.EnsureCreated();
            });
        }
    }

    public class ControlPlaneSecurityIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public ControlPlaneSecurityIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task FullAuthLifecycle_Register_Login_ProtectedAccess_Refresh_ReuseDetection()
        {
            string username = "testuser_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{username}@edm.test";
            string password = "SecurePassword!2026";
            Guid installationId = Guid.NewGuid();

            // 1. Register
            var regResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Username = username,
                Email = email,
                Password = password
            });
            regResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 2. Login
            var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                UsernameOrEmail = email,
                Password = password,
                InstallationId = installationId
            });
            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var loginDoc = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
            string accessToken1 = loginDoc.GetProperty("accessToken").GetString()!;
            string refreshToken1 = loginDoc.GetProperty("refreshToken").GetString()!;
            accessToken1.Should().NotBeNullOrEmpty();
            refreshToken1.Should().NotBeNullOrEmpty();

            // 3. Access Protected Endpoint (/api/v1/auth/me)
            var authReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            authReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken1);
            var meResponse = await _client.SendAsync(authReq);
            meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var meDoc = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
            meDoc.GetProperty("username").GetString().Should().Be(username);

            // 4. Refresh Token Rotation (valid refresh)
            var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new
            {
                RefreshToken = refreshToken1,
                InstallationId = installationId
            });
            refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var refreshDoc = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>();
            string accessToken2 = refreshDoc.GetProperty("accessToken").GetString()!;
            string refreshToken2 = refreshDoc.GetProperty("refreshToken").GetString()!;
            accessToken2.Should().NotBe(accessToken1);
            refreshToken2.Should().NotBe(refreshToken1);

            // 5. Security Check: Replay Old Refresh Token (refreshToken1) -> REUSE ATTACK
            var replayResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new
            {
                RefreshToken = refreshToken1,
                InstallationId = installationId
            });
            replayResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // 6. Verify that because of reuse attack, the entire session is revoked and accessToken2 is blocked
            var authReq2 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            authReq2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken2);
            var revokedResponse = await _client.SendAsync(authReq2);
            revokedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task AntiIDOR_UserCannotAccessOrRevokeOtherUsersSessions()
        {
            // Register User A
            string userA = "usera_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = userA, Email = $"{userA}@edm.test", Password = "PasswordA!2026" });
            var loginA = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = $"{userA}@edm.test", Password = "PasswordA!2026" });
            var docA = await loginA.Content.ReadFromJsonAsync<JsonElement>();
            string tokenA = docA.GetProperty("accessToken").GetString()!;

            // Register User B
            string userB = "userb_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = userB, Email = $"{userB}@edm.test", Password = "PasswordB!2026" });
            var loginB = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = $"{userB}@edm.test", Password = "PasswordB!2026" });
            var docB = await loginB.Content.ReadFromJsonAsync<JsonElement>();
            string tokenB = docB.GetProperty("accessToken").GetString()!;

            // User A gets their sessions
            var sessReqA = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/sessions");
            sessReqA.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
            var sessResA = await _client.SendAsync(sessReqA);
            var listA = await sessResA.Content.ReadFromJsonAsync<List<JsonElement>>();
            listA.Should().NotBeNull();
            Guid sessionAId = Guid.Parse(listA![0].GetProperty("id").GetString()!);

            // User B attempts IDOR attack to delete User A's session
            var idorDeleteReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/auth/sessions/{sessionAId}");
            idorDeleteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
            var idorRes = await _client.SendAsync(idorDeleteReq);

            // Must be blocked with 404 (or 403)
            idorRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task RBAC_RegularUserCannotAccessAdminEndpoints()
        {
            string user = "reguser_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = user, Email = $"{user}@edm.test", Password = "RegPassword!2026" });
            var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = $"{user}@edm.test", Password = "RegPassword!2026" });
            var doc = await login.Content.ReadFromJsonAsync<JsonElement>();
            string token = doc.GetProperty("accessToken").GetString()!;

            // Regular user attempts to call /api/v1/admin/users
            var adminReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/users");
            adminReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var res = await _client.SendAsync(adminReq);

            // Server-side RBAC must reject with 403 Forbidden
            res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task BanEnforcement_ActiveBanBlocksAuthenticatedRequests()
        {
            string user = "banuser_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{user}@edm.test";
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = user, Email = email, Password = "BanPassword!2026" });
            var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = "BanPassword!2026" });
            var doc = await login.Content.ReadFromJsonAsync<JsonElement>();
            string token = doc.GetProperty("accessToken").GetString()!;
            string userIdStr = doc.GetProperty("user").GetProperty("id").GetString()!;
            Guid userId = Guid.Parse(userIdStr);

            // Verify access works before ban
            var req1 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var res1 = await _client.SendAsync(req1);
            res1.StatusCode.Should().Be(HttpStatusCode.OK);

            // Apply ban directly in DB
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                db.Bans.Add(new Ban
                {
                    Id = Guid.NewGuid(),
                    TargetType = BanTargetType.UserId,
                    TargetValue = userId.ToString(),
                    Reason = "Terms of service violation",
                    BannedBy = "AUTOMATED_TEST",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            // Next authenticated request must be intercepted with 403 Forbidden
            var req2 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var res2 = await _client.SendAsync(req2);
            res2.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task PasswordChange_InvalidatesOtherSessions()
        {
            string user = "pwduser_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{user}@edm.test";
            string oldPwd = "OldPassword!2026";
            string newPwd = "NewPassword!2026";

            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = user, Email = email, Password = oldPwd });

            // Session 1
            var login1 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = oldPwd });
            var doc1 = await login1.Content.ReadFromJsonAsync<JsonElement>();
            string token1 = doc1.GetProperty("accessToken").GetString()!;

            // Session 2
            var login2 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = oldPwd });
            var doc2 = await login2.Content.ReadFromJsonAsync<JsonElement>();
            string token2 = doc2.GetProperty("accessToken").GetString()!;

            // Change password from Session 2
            var changeReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password");
            changeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token2);
            changeReq.Content = JsonContent.Create(new { OldPassword = oldPwd, NewPassword = newPwd });
            var changeRes = await _client.SendAsync(changeReq);
            changeRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Session 1 should now be revoked and blocked
            var checkReq1 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            checkReq1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token1);
            var checkRes1 = await _client.SendAsync(checkReq1);
            checkRes1.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // Session 2 remains valid
            var checkReq2 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            checkReq2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token2);
            var checkRes2 = await _client.SendAsync(checkReq2);
            checkRes2.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Concurrency_SimultaneousRefresh_OnlyOneSucceeds()
        {
            string user = "raceuser_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{user}@edm.test";
            string pwd = "Password!2026";

            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = user, Email = email, Password = pwd });
            var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = pwd });
            var doc = await login.Content.ReadFromJsonAsync<JsonElement>();
            string initialRefresh = doc.GetProperty("refreshToken").GetString()!;

            // Execute two concurrent refresh requests with the EXACT SAME initial refresh token
            var task1 = _client.PostAsJsonAsync("/api/v1/auth/refresh", new { RefreshToken = initialRefresh });
            var task2 = _client.PostAsJsonAsync("/api/v1/auth/refresh", new { RefreshToken = initialRefresh });

            var responses = await Task.WhenAll(task1, task2);

            int successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
            int failCount = responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized);

            // Exactly one must succeed, the other must be rejected (reuse / already used)
            successCount.Should().Be(1);
            failCount.Should().Be(1);
        }
    }
}
