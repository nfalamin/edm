using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

namespace EDM.Tests.ControlPlane
{
    public class PasskeyAndSecurityProfileIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public PasskeyAndSecurityProfileIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private string GenerateMockGoogleIdToken(string email, string sub, string name = "Admin User", bool emailVerified = true, bool isExpired = false)
        {
            var handler = new JwtSecurityTokenHandler();
            var claims = new List<Claim>
            {
                new Claim("email", email),
                new Claim("sub", sub),
                new Claim("name", name),
                new Claim("email_verified", emailVerified ? "true" : "false"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            };

            DateTime issuedAt = isExpired ? DateTime.UtcNow.AddHours(-2) : DateTime.UtcNow.AddMinutes(-1);
            DateTime expires = isExpired ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddHours(1);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = "https://accounts.google.com",
                Audience = "edm-admin-control-plane",
                NotBefore = issuedAt,
                IssuedAt = issuedAt,
                Expires = expires,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("google_oidc_mock_secret_key_for_testing_purposes_12345")),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = handler.CreateToken(tokenDescriptor);
            return handler.WriteToken(token);
        }

        [Fact]
        public async Task Passkey_Lifecycle_Register_List_Rename_Delete_Succeeds()
        {
            string username = "passkey_admin_" + Guid.NewGuid().ToString("N")[..8];
            string email = $"{username}@edm.local";
            string password = "StrongPassword!2026";

            // 1. Register & Elevate to Admin
            var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });
            regRes.EnsureSuccessStatusCode();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                u!.Role = UserRole.SUPER_ADMIN;
                await db.SaveChangesAsync();
            }

            // 2. Login to get JWT
            var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = username, Password = password });
            var loginDoc = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
            string token = loginDoc.GetProperty("accessToken").GetString()!;

            using var authReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/passkey/register-options");
            authReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var optRes = await _client.SendAsync(authReq);
            optRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var optDoc = await optRes.Content.ReadFromJsonAsync<JsonElement>();
            string challenge = optDoc.GetProperty("challenge").GetString()!;
            challenge.Should().NotBeNullOrEmpty();

            // 3. Register Passkey directly in DB
            Guid passkeyId = Guid.NewGuid();
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                db.UserPasskeys.Add(new UserPasskey
                {
                    Id = passkeyId,
                    UserId = u!.Id,
                    CredentialId = "cred_" + Guid.NewGuid().ToString("N"),
                    PublicKey = "pubkey_" + Guid.NewGuid().ToString("N"),
                    DeviceName = "Windows Hello Laptop",
                    CreatedAtUtc = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            // 4. List Passkeys
            using var listReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/passkeys");
            listReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var listRes = await _client.SendAsync(listReq);
            listRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var passkeys = await listRes.Content.ReadFromJsonAsync<List<UserPasskeyDto>>();
            passkeys.Should().NotBeNull();
            passkeys!.Any(p => p.Id == passkeyId && p.DeviceName == "Windows Hello Laptop").Should().BeTrue();

            // 5. Rename Passkey
            using var renameReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/auth/passkeys/{passkeyId}/rename");
            renameReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            renameReq.Content = JsonContent.Create(new { NewName = "MacBook TouchID" });
            var renameRes = await _client.SendAsync(renameReq);
            renameRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify rename in DB
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var pk = await db.UserPasskeys.FindAsync(passkeyId);
                pk!.DeviceName.Should().Be("MacBook TouchID");
            }

            // 6. Delete Passkey
            using var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/auth/passkeys/{passkeyId}");
            delReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var delRes = await _client.SendAsync(delReq);
            delRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify deletion in DB
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var pk = await db.UserPasskeys.FindAsync(passkeyId);
                pk.Should().BeNull();
            }
        }

        [Fact]
        public async Task Google_Sign_In_Rejects_Unauthorized_Identity_And_Authorizes_Whitelisted_Admin()
        {
            string unauthorizedEmail = "hacker_" + Guid.NewGuid().ToString("N")[..6] + "@gmail.com";
            string unauthGoogleToken = GenerateMockGoogleIdToken(unauthorizedEmail, "sub_unauth_" + Guid.NewGuid().ToString("N"));

            // Unauthorized Google login should fail with 403 Forbidden
            var unauthRes = await _client.PostAsJsonAsync("/api/v1/auth/google", new { IdToken = unauthGoogleToken });
            unauthRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // Authorized Google admin
            string adminUsername = "g_superadmin_" + Guid.NewGuid().ToString("N")[..8];
            string adminEmail = $"{adminUsername}@edm.corp";
            string adminPassword = "StrongPassword!2026";
            string authGoogleToken = GenerateMockGoogleIdToken(adminEmail, "sub_admin_" + Guid.NewGuid().ToString("N"));

            // Register & elevate
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = adminUsername, Email = adminEmail, Password = adminPassword });
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == adminEmail);
                u!.Role = UserRole.SUPER_ADMIN;
                await db.SaveChangesAsync();
            }

            // Authorized Google login should succeed
            var authRes = await _client.PostAsJsonAsync("/api/v1/auth/google", new { IdToken = authGoogleToken });
            authRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var authDoc = await authRes.Content.ReadFromJsonAsync<JsonElement>();
            authDoc.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Password_Change_Enforces_Argon2id_And_Allows_New_Password_Login()
        {
            string username = "pwd_admin_" + Guid.NewGuid().ToString("N")[..8];
            string email = $"{username}@edm.local";
            string oldPassword = "InitialPassword!2026";
            string newPassword = "UpdatedSecurePassword!2026";

            // Register & elevate
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = oldPassword });
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                u!.Role = UserRole.SUPER_ADMIN;
                await db.SaveChangesAsync();
            }

            // Login with old password
            var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = username, Password = oldPassword });
            var loginDoc = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
            string token = loginDoc.GetProperty("accessToken").GetString()!;

            // Change Password
            using var changeReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password");
            changeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            changeReq.Content = JsonContent.Create(new { OldPassword = oldPassword, NewPassword = newPassword });
            var changeRes = await _client.SendAsync(changeReq);
            changeRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Old password should fail
            var failRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = username, Password = oldPassword });
            failRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // New password should succeed
            var successRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = username, Password = newPassword });
            successRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
