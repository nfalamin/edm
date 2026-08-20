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
    public class SuperAdminMfaSecurityIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public SuperAdminMfaSecurityIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task SuperAdmin_2FA_Setup_Challenge_And_Totp_Verification_Flow()
        {
            string username = "superadmin_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{username}@edm.control";
            string password = "SuperAdminPassword!2026";

            // 1. Register user
            var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });
            regRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 2. Elevate to SUPER_ADMIN directly in DB
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                u.Should().NotBeNull();
                u!.Role = UserRole.SUPER_ADMIN;
                await db.SaveChangesAsync();
            }

            // 3. Login Step 1 (Without 2FA initially)
            var login1 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            login1.StatusCode.Should().Be(HttpStatusCode.OK);
            var doc1 = await login1.Content.ReadFromJsonAsync<JsonElement>();
            string accessToken = doc1.GetProperty("accessToken").GetString()!;

            // 4. Setup 2FA
            var setupReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/setup");
            setupReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var setupRes = await _client.SendAsync(setupReq);
            setupRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var setupDoc = await setupRes.Content.ReadFromJsonAsync<JsonElement>();
            string secret = setupDoc.GetProperty("secret").GetString()!;
            secret.Should().NotBeNullOrEmpty();

            // 5. Generate valid code and confirm 2FA
            var totpService = new TotpService();
            string validCode = totpService.GenerateCurrentCode(secret);

            var confirmReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/confirm");
            confirmReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            confirmReq.Content = JsonContent.Create(new { Code = validCode });
            var confirmRes = await _client.SendAsync(confirmReq);
            string confirmBody = await confirmRes.Content.ReadAsStringAsync();
            confirmRes.StatusCode.Should().Be(HttpStatusCode.OK, $"Confirm failed with: {confirmBody}");

            var confirmDoc = JsonSerializer.Deserialize<JsonElement>(confirmBody);
            var recoveryCodes = confirmDoc.GetProperty("recoveryCodes").EnumerateArray().Select(x => x.GetString()!).ToList();
            recoveryCodes.Should().HaveCount(8);

            // 6. Attempt Login again -> Must require 2FA challenge!
            var login2 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            login2.StatusCode.Should().Be(HttpStatusCode.OK);
            var doc2 = await login2.Content.ReadFromJsonAsync<JsonElement>();
            doc2.GetProperty("requires2FA").GetBoolean().Should().BeTrue();
            string ticket = doc2.GetProperty("twoFactorTicket").GetString()!;
            ticket.Should().NotBeNullOrEmpty();

            // 7. Verify with invalid code -> Fails
            var invalidVerify = await _client.PostAsJsonAsync("/api/v1/auth/2fa/verify", new
            {
                TwoFactorTicket = ticket,
                Code = "999999",
                IsRecoveryCode = false
            });
            invalidVerify.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // 8. Re-attempt Login to get fresh ticket and verify with VALID TOTP
            var login3 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var doc3 = await login3.Content.ReadFromJsonAsync<JsonElement>();
            string ticket3 = doc3.GetProperty("twoFactorTicket").GetString()!;
            string freshCode = totpService.GenerateCurrentCode(secret);

            var validVerify = await _client.PostAsJsonAsync("/api/v1/auth/2fa/verify", new
            {
                TwoFactorTicket = ticket3,
                Code = freshCode,
                IsRecoveryCode = false
            });
            validVerify.StatusCode.Should().Be(HttpStatusCode.OK);
            var verifyDoc = await validVerify.Content.ReadFromJsonAsync<JsonElement>();
            string finalToken = verifyDoc.GetProperty("accessToken").GetString()!;
            finalToken.Should().NotBeNullOrEmpty();

            // 9. Access protected Super Admin endpoint
            var adminReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/dashboard/summary");
            adminReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", finalToken);
            var adminRes = await _client.SendAsync(adminReq);
            adminRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task SuperAdmin_RecoveryCode_Successfully_Unlocks_And_Cannot_Be_Reused()
        {
            string username = "recuser_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{username}@edm.control";
            string password = "SuperAdminPassword!2026";

            // Register & elevate
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                u!.Role = UserRole.SUPER_ADMIN;
                await db.SaveChangesAsync();
            }

            // Login
            var login1 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var doc1 = await login1.Content.ReadFromJsonAsync<JsonElement>();
            string accessToken = doc1.GetProperty("accessToken").GetString()!;

            // Setup 2FA
            var setupReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/setup");
            setupReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var setupRes = await _client.SendAsync(setupReq);
            var setupDoc = await setupRes.Content.ReadFromJsonAsync<JsonElement>();
            string secret = setupDoc.GetProperty("secret").GetString()!;

            var totpService = new TotpService();
            string validCode = totpService.GenerateCurrentCode(secret);

            var confirmReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/confirm");
            confirmReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            confirmReq.Content = JsonContent.Create(new { Code = validCode });
            var confirmRes = await _client.SendAsync(confirmReq);
            string confirmBody = await confirmRes.Content.ReadAsStringAsync();
            confirmRes.StatusCode.Should().Be(HttpStatusCode.OK, $"Confirm failed with: {confirmBody}");
            var confirmDoc = JsonSerializer.Deserialize<JsonElement>(confirmBody);
            var recoveryCodes = confirmDoc.GetProperty("recoveryCodes").EnumerateArray().Select(x => x.GetString()!).ToList();
            string selectedRecoveryCode = recoveryCodes[0];

            // Login and trigger 2FA challenge
            var login2 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var doc2 = await login2.Content.ReadFromJsonAsync<JsonElement>();
            string ticket = doc2.GetProperty("twoFactorTicket").GetString()!;

            // Verify using recovery code
            var recVerify = await _client.PostAsJsonAsync("/api/v1/auth/2fa/verify", new
            {
                TwoFactorTicket = ticket,
                Code = selectedRecoveryCode,
                IsRecoveryCode = true
            });
            recVerify.StatusCode.Should().Be(HttpStatusCode.OK);

            // Re-attempt login and try to reuse the SAME recovery code -> MUST BE REJECTED
            var login3 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var doc3 = await login3.Content.ReadFromJsonAsync<JsonElement>();
            string ticket3 = doc3.GetProperty("twoFactorTicket").GetString()!;

            var reuseVerify = await _client.PostAsJsonAsync("/api/v1/auth/2fa/verify", new
            {
                TwoFactorTicket = ticket3,
                Code = selectedRecoveryCode,
                IsRecoveryCode = true
            });
            reuseVerify.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task PasswordReset_Flow_Successfully_Resets_Password_And_Invalidates_Sessions()
        {
            string username = "pwdreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{username}@edm.control";
            string oldPwd = "OldPassword!2026";
            string newPwd = "NewPassword!2026";

            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = oldPwd });

            // Initial Login
            var login1 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = oldPwd });
            var doc1 = await login1.Content.ReadFromJsonAsync<JsonElement>();
            string token1 = doc1.GetProperty("accessToken").GetString()!;

            // Request Password Reset
            var forgotRes = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { Email = email });
            forgotRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var forgotDoc = await forgotRes.Content.ReadFromJsonAsync<JsonElement>();
            string resetToken = forgotDoc.GetProperty("resetToken").GetString()!;
            resetToken.Should().NotBeNullOrEmpty();

            // Perform Reset
            var resetRes = await _client.PostAsJsonAsync("/api/v1/auth/reset-password", new { Token = resetToken, NewPassword = newPwd });
            resetRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Old token must be invalidated
            var oldAuthReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            oldAuthReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token1);
            var oldAuthRes = await _client.SendAsync(oldAuthReq);
            oldAuthRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // Old password login fails
            var failLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = oldPwd });
            failLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // New password login succeeds
            var successLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = newPwd });
            successLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Csrf_Token_Endpoint_Returns_Valid_Token()
        {
            var res = await _client.GetAsync("/api/v1/auth/csrf-token");
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
            string token = doc.GetProperty("csrfToken").GetString()!;
            token.Should().NotBeNullOrEmpty();
            token.Split(':').Length.Should().Be(3);
        }
    }
}
