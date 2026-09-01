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
    public class Phase9ProductionSecurityAndIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public Phase9ProductionSecurityAndIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<(string Token, Guid UserId, string Email)> CreateUserWithRoleAsync(UserRole role)
        {
            string username = "p9_" + role.ToString().ToLower() + "_" + Guid.NewGuid().ToString("N")[..6];
            string email = $"{username}@edm.local";
            string password = "StrongPassword!2026";

            var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });
            regRes.EnsureSuccessStatusCode();

            Guid userId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                u!.Role = role;
                u.IsActive = true;
                await db.SaveChangesAsync();
                userId = u.Id;
            }

            var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = username, Password = password });
            var doc = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
            string token = doc.GetProperty("accessToken").GetString()!;

            return (token, userId, email);
        }

        [Fact]
        public async Task Phase9_Authentication_Lifecycle_And_Unauthorized_Rejection()
        {
            // 1. Missing Token -> 401 Unauthorized
            var unauthReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/users");
            var unauthRes = await _client.SendAsync(unauthReq);
            unauthRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // 2. Invalid / Tampered Token -> 401 Unauthorized
            var badTokenReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/users");
            badTokenReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.invalid.signature");
            var badTokenRes = await _client.SendAsync(badTokenReq);
            badTokenRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // 3. Valid Login & Token -> 200 OK
            var (superToken, _, _) = await CreateUserWithRoleAsync(UserRole.SUPER_ADMIN);
            var authReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/users");
            authReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superToken);
            var authRes = await _client.SendAsync(authReq);
            authRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Phase9_Role_Based_Authorization_Enforcement()
        {
            var (supportToken, _, _) = await CreateUserWithRoleAsync(UserRole.SUPPORT);
            var (superToken, _, _) = await CreateUserWithRoleAsync(UserRole.SUPER_ADMIN);

            // 1. SUPPORT role attempting sensitive admin action (e.g. modifying plans or deleting user) -> 403 Forbidden
            var forbiddenReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/users/{Guid.NewGuid()}");
            forbiddenReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supportToken);
            var forbiddenRes = await _client.SendAsync(forbiddenReq);
            forbiddenRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // 2. SUPER_ADMIN role accessing full management -> 200 OK / non-403
            var superReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/users");
            superReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superToken);
            var superRes = await _client.SendAsync(superReq);
            superRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Phase9_Security_Headers_And_Protection_Audit()
        {
            var res = await _client.GetAsync("/health");
            res.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify essential security response headers
            res.Headers.Contains("X-Content-Type-Options").Should().BeTrue();
            res.Headers.GetValues("X-Content-Type-Options").First().Should().Be("nosniff");

            res.Headers.Contains("X-Frame-Options").Should().BeTrue();
            res.Headers.GetValues("X-Frame-Options").First().Should().Be("DENY");

            res.Headers.Contains("Referrer-Policy").Should().BeTrue();
        }

        [Fact]
        public async Task Phase9_Global_Exception_Masking_And_Correlation_Audit()
        {
            var (adminToken, _, _) = await CreateUserWithRoleAsync(UserRole.SUPER_ADMIN);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            // Accessing a nonexistent GUID triggers clean 404 or structured handled error without raw stack trace
            var notFoundRes = await _client.GetAsync($"/api/v1/admin/users/{Guid.NewGuid()}");
            notFoundRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var doc = await notFoundRes.Content.ReadFromJsonAsync<JsonElement>();
            doc.TryGetProperty("error", out _).Should().BeTrue();
        }
    }
}
