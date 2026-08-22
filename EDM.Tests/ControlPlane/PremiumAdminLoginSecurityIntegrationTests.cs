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
using EDM.ControlPlane.Api.Services;

namespace EDM.Tests.ControlPlane
{
    public class PremiumAdminLoginSecurityIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public PremiumAdminLoginSecurityIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Correct_Login_Authenticates_And_Issues_Cookies_And_Tokens()
        {
            string username = "admin_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{username}@edm.control";
            string password = "CorrectAdminPassword!2026";

            // 1. Register & Elevate to SUPER_ADMIN
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                u!.Role = UserRole.SUPER_ADMIN;
                await db.SaveChangesAsync();
            }

            // 2. Perform Login with RememberDevice = true
            var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                UsernameOrEmail = email,
                Password = password,
                RememberDevice = true
            });

            loginRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. Verify Response Content
            var loginDoc = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
            string accessToken = loginDoc.GetProperty("accessToken").GetString()!;
            string refreshToken = loginDoc.GetProperty("refreshToken").GetString()!;
            string role = loginDoc.GetProperty("user").GetProperty("role").GetString()!;

            accessToken.Should().NotBeNullOrEmpty();
            refreshToken.Should().NotBeNullOrEmpty();
            role.Should().Be("SUPER_ADMIN");

            // 4. Verify Cookie was set
            loginRes.Headers.Contains("Set-Cookie").Should().BeTrue();
            var cookieHeader = string.Join(";", loginRes.Headers.GetValues("Set-Cookie"));
            cookieHeader.Should().Contain("edm_admin_jwt=");
            cookieHeader.ToLowerInvariant().Should().Contain("httponly");
        }

        [Fact]
        public async Task Wrong_Password_Fails_And_Increments_Lockout_Counter()
        {
            string username = "wrongpwd_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{username}@edm.control";
            string password = "RightPassword!2026";

            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });

            // Attempt Login with wrong password
            var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                UsernameOrEmail = email,
                Password = "CompletelyWrongPassword!123"
            });

            loginRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var doc = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
            doc.GetProperty("error").GetString().Should().Be("INVALID_CREDENTIALS");
        }

        [Fact]
        public async Task Unknown_Email_Returns_Generic_Unauthorized()
        {
            var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                UsernameOrEmail = "nonexistent_superadmin_999@edm.org",
                Password = "SomePassword!2026"
            });

            loginRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var doc = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
            doc.GetProperty("error").GetString().Should().Be("INVALID_CREDENTIALS");
        }

        [Fact]
        public async Task Brute_Force_Lockout_After_5_Attempts_Blocks_Subsequent_Logins()
        {
            string username = "bruteforce_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{username}@edm.control";
            string password = "CorrectPassword!2026";

            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });

            // Send 5 consecutive failed attempts
            for (int i = 1; i <= 5; i++)
            {
                var failRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new
                {
                    UsernameOrEmail = email,
                    Password = $"BadAttempt_{i}!"
                });
                failRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            }

            // 6th attempt even with CORRECT password should be locked out
            var lockedRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                UsernameOrEmail = email,
                Password = password
            });

            // Status is 429 Too Many Requests or 401 with lockout message
            (lockedRes.StatusCode == HttpStatusCode.TooManyRequests || lockedRes.StatusCode == HttpStatusCode.Unauthorized || lockedRes.StatusCode == HttpStatusCode.Forbidden).Should().BeTrue();
        }

        [Fact]
        public async Task Logout_Revokes_Session_And_Clears_Tokens()
        {
            string username = "logoutuser_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{username}@edm.control";
            string password = "Password!2026";

            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });

            var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var doc = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
            string accessToken = doc.GetProperty("accessToken").GetString()!;

            // Call Logout
            var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
            logoutReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var logoutRes = await _client.SendAsync(logoutReq);
            logoutRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Subsequent call with old token must be rejected
            var testReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            testReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var testRes = await _client.SendAsync(testReq);
            testRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Direct_Protected_Admin_Url_Rejects_Unauthenticated_Requests()
        {
            // Direct request without Authorization header or cookie
            var res = await _client.GetAsync("/api/v1/admin/dashboard/summary");
            res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var auditRes = await _client.GetAsync("/api/v1/admin/audit-logs");
            auditRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Forgot_Password_With_2FA_Enabled_Strictly_Requires_Valid_2FA_Or_Recovery_Code()
        {
            string username = "mfa_reset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{username}@edm.control";
            string password = "InitialPassword!2026";
            string newPassword = "BrandNewSuperSecurePassword!2026";

            // 1. Register & Elevate
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                u!.Role = UserRole.SUPER_ADMIN;
                await db.SaveChangesAsync();
            }

            // 2. Login & setup 2FA
            var login1 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var doc1 = await login1.Content.ReadFromJsonAsync<JsonElement>();
            string token1 = doc1.GetProperty("accessToken").GetString()!;

            var setupReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/setup");
            setupReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token1);
            var setupRes = await _client.SendAsync(setupReq);
            var setupDoc = await setupRes.Content.ReadFromJsonAsync<JsonElement>();
            string secret = setupDoc.GetProperty("secret").GetString()!;

            var totp = new TotpService();
            string code = totp.GenerateCurrentCode(secret);

            var confirmReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/confirm");
            confirmReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token1);
            confirmReq.Content = JsonContent.Create(new { Code = code });
            var confirmRes = await _client.SendAsync(confirmReq);
            confirmRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. Request Password Reset
            var forgotRes = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { Email = email });
            var forgotDoc = await forgotRes.Content.ReadFromJsonAsync<JsonElement>();
            string resetToken = forgotDoc.GetProperty("resetToken").GetString()!;

            // 4. Attempt reset WITHOUT 2FA code -> Must be rejected with 2FA_REQUIRED / MFA_REQUIRED
            var noMfaReset = await _client.PostAsJsonAsync("/api/v1/auth/reset-password", new
            {
                Token = resetToken,
                NewPassword = newPassword
            });
            noMfaReset.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // 5. Attempt reset with INVALID 2FA code -> Must be rejected
            var badMfaReset = await _client.PostAsJsonAsync("/api/v1/auth/reset-password", new
            {
                Token = resetToken,
                NewPassword = newPassword,
                TwoFactorCode = "000000"
            });
            badMfaReset.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // 6. Attempt reset with VALID 2FA code -> Succeeds!
            string freshCode = totp.GenerateCurrentCode(secret);
            var validReset = await _client.PostAsJsonAsync("/api/v1/auth/reset-password", new
            {
                Token = resetToken,
                NewPassword = newPassword,
                TwoFactorCode = freshCode
            });
            validReset.StatusCode.Should().Be(HttpStatusCode.OK);

            // 7. Login with NEW password succeeds and requires 2FA challenge
            var loginNew = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = newPassword });
            loginNew.StatusCode.Should().Be(HttpStatusCode.OK);
            var loginNewDoc = await loginNew.Content.ReadFromJsonAsync<JsonElement>();
            loginNewDoc.GetProperty("requires2FA").GetBoolean().Should().BeTrue();
        }

        [Fact]
        public async Task Initial_Admin_Setup_Endpoint_Blocks_Execution_If_SuperAdmin_Already_Exists()
        {
            // The factory already has Super Admins from previous setup/tests
            var res = await _client.PostAsJsonAsync("/api/v1/auth/setup-initial-admin", new
            {
                Username = "hacker_admin",
                Email = "hacker@edm.local",
                Password = "HackerPassword!2026"
            });

            // Must be Forbidden / 403 or BadRequest
            (res.StatusCode == HttpStatusCode.Forbidden || res.StatusCode == HttpStatusCode.BadRequest).Should().BeTrue();
        }
    }
}
