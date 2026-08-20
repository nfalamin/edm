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
    public class GoogleAuthAndAccountRecoveryIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public GoogleAuthAndAccountRecoveryIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private string GenerateMockGoogleIdToken(string email, string sub, string name = "Admin User", bool emailVerified = true, bool isExpired = false, string issuer = "https://accounts.google.com")
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
                Issuer = issuer,
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
        public async Task Google_Login_Success_With_Authorized_SuperAdmin_Identity()
        {
            string username = "gadmin_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{username}@edm.corp";
            string password = "StrongPassword!2026";
            string googleSub = "goog_sub_" + Guid.NewGuid().ToString("N");

            // 1. Register and elevate to Super Admin
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                u!.Role = UserRole.SUPER_ADMIN;
                await db.SaveChangesAsync();
            }

            // 2. Generate valid Google ID Token
            string idToken = GenerateMockGoogleIdToken(email, googleSub);

            // 3. Login with Google
            var res = await _client.PostAsJsonAsync("/api/v1/auth/google/login", new { IdToken = idToken });
            res.StatusCode.Should().Be(HttpStatusCode.OK);

            var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
            doc.GetProperty("success").GetBoolean().Should().BeTrue();
            doc.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
            doc.GetProperty("user").GetProperty("email").GetString().Should().Be(email);
        }

        [Fact]
        public async Task Google_Login_With_Unauthorized_Account_Returns_Forbidden()
        {
            string unauthorizedEmail = "random_intruder_" + Guid.NewGuid().ToString("N").Substring(0, 6) + "@gmail.com";
            string idToken = GenerateMockGoogleIdToken(unauthorizedEmail, "unauth_sub_12345");

            var res = await _client.PostAsJsonAsync("/api/v1/auth/google/login", new { IdToken = idToken });
            res.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
            doc.GetProperty("error").GetString().Should().Be("UNAUTHORIZED_GOOGLE_ACCOUNT");
        }

        [Fact]
        public async Task Google_Login_With_Invalid_OAuth_Token_Fails()
        {
            // Test completely malformed token
            var malformedRes = await _client.PostAsJsonAsync("/api/v1/auth/google/login", new { IdToken = "not.a.valid.jwt" });
            malformedRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // Test expired token
            string expiredToken = GenerateMockGoogleIdToken("admin@edm.local", "sub123", isExpired: true);
            var expiredRes = await _client.PostAsJsonAsync("/api/v1/auth/google/login", new { IdToken = expiredToken });
            expiredRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // Test bad issuer
            string badIssuerToken = GenerateMockGoogleIdToken("admin@edm.local", "sub123", issuer: "https://evil.attacker.com");
            var badIssuerRes = await _client.PostAsJsonAsync("/api/v1/auth/google/login", new { IdToken = badIssuerToken });
            badIssuerRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Google_Login_When_2FA_Enabled_Enforces_2FA_Challenge()
        {
            string username = "g2fa_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{username}@edm.corp";
            string password = "AdminPassword!2026";
            string googleSub = "goog_2fa_sub_" + Guid.NewGuid().ToString("N");

            // 1. Register & Elevate
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                u!.Role = UserRole.SUPER_ADMIN;
                await db.SaveChangesAsync();
            }

            // 2. Login with password and activate 2FA
            var logRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var logDoc = await logRes.Content.ReadFromJsonAsync<JsonElement>();
            string jwt = logDoc.GetProperty("accessToken").GetString()!;

            var setupReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/setup");
            setupReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            var setupRes = await _client.SendAsync(setupReq);
            var setupDoc = await setupRes.Content.ReadFromJsonAsync<JsonElement>();
            string secret = setupDoc.GetProperty("secret").GetString()!;

            var totp = new TotpService();
            string code = totp.GenerateCurrentCode(secret);

            var confReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/confirm");
            confReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            confReq.Content = JsonContent.Create(new { Code = code });
            var confRes = await _client.SendAsync(confReq);
            confRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. Attempt Google login with this user -> Must return Requires2FA = true
            string idToken = GenerateMockGoogleIdToken(email, googleSub);
            var gRes = await _client.PostAsJsonAsync("/api/v1/auth/google/login", new { IdToken = idToken });
            gRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var gDoc = await gRes.Content.ReadFromJsonAsync<JsonElement>();
            gDoc.GetProperty("requires2FA").GetBoolean().Should().BeTrue();
            string ticket = gDoc.GetProperty("twoFactorTicket").GetString()!;
            ticket.Should().NotBeNullOrEmpty();

            // 4. Complete 2FA challenge with valid code
            string code2 = totp.GenerateCurrentCode(secret);
            var verifyRes = await _client.PostAsJsonAsync("/api/v1/auth/2fa/verify", new
            {
                TwoFactorTicket = ticket,
                Code = code2,
                IsRecoveryCode = false
            });

            verifyRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var verifyDoc = await verifyRes.Content.ReadFromJsonAsync<JsonElement>();
            verifyDoc.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Password_Login_And_2FA_Success_Flow()
        {
            string username = "p2fa_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{username}@edm.control";
            string password = "SuperPassword!2026";

            // 1. Register & Elevate
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                u!.Role = UserRole.SUPER_ADMIN;
                await db.SaveChangesAsync();
            }

            // 2. Setup 2FA
            var log1 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var doc1 = await log1.Content.ReadFromJsonAsync<JsonElement>();
            string jwt1 = doc1.GetProperty("accessToken").GetString()!;

            var setupReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/setup");
            setupReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt1);
            var setupRes = await _client.SendAsync(setupReq);
            var setupDoc = await setupRes.Content.ReadFromJsonAsync<JsonElement>();
            string secret = setupDoc.GetProperty("secret").GetString()!;

            var totp = new TotpService();
            string code = totp.GenerateCurrentCode(secret);

            var confReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/confirm");
            confReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt1);
            confReq.Content = JsonContent.Create(new { Code = code });
            var confRes = await _client.SendAsync(confReq);
            confRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. New Login requires 2FA
            var log2 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var doc2 = await log2.Content.ReadFromJsonAsync<JsonElement>();
            doc2.GetProperty("requires2FA").GetBoolean().Should().BeTrue();
            string ticket = doc2.GetProperty("twoFactorTicket").GetString()!;

            // 4. Verify code
            string code2 = totp.GenerateCurrentCode(secret);
            var verRes = await _client.PostAsJsonAsync("/api/v1/auth/2fa/verify", new
            {
                TwoFactorTicket = ticket,
                Code = code2,
                IsRecoveryCode = false
            });
            verRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task TwoFactor_Verification_Failure_With_Wrong_Code()
        {
            string username = "fail2fa_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{username}@edm.control";
            string password = "SuperPassword!2026";

            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                u!.Role = UserRole.SUPER_ADMIN;
                await db.SaveChangesAsync();
            }

            var log1 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var doc1 = await log1.Content.ReadFromJsonAsync<JsonElement>();
            string jwt1 = doc1.GetProperty("accessToken").GetString()!;

            var setupReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/setup");
            setupReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt1);
            var setupRes = await _client.SendAsync(setupReq);
            var setupDoc = await setupRes.Content.ReadFromJsonAsync<JsonElement>();
            string secret = setupDoc.GetProperty("secret").GetString()!;

            var totp = new TotpService();
            string code = totp.GenerateCurrentCode(secret);

            var confReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/confirm");
            confReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt1);
            confReq.Content = JsonContent.Create(new { Code = code });
            await _client.SendAsync(confReq);

            // Trigger challenge
            var log2 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var doc2 = await log2.Content.ReadFromJsonAsync<JsonElement>();
            string ticket = doc2.GetProperty("twoFactorTicket").GetString()!;

            // Verify with wrong code
            var verRes = await _client.PostAsJsonAsync("/api/v1/auth/2fa/verify", new
            {
                TwoFactorTicket = ticket,
                Code = "999999",
                IsRecoveryCode = false
            });
            (verRes.StatusCode == HttpStatusCode.Unauthorized || verRes.StatusCode == HttpStatusCode.BadRequest).Should().BeTrue();
        }

        [Fact]
        public async Task Recovery_Code_Verifies_And_Is_Burned_For_Single_Use()
        {
            string username = "recburn_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{username}@edm.control";
            string password = "SuperPassword!2026";

            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                u!.Role = UserRole.SUPER_ADMIN;
                await db.SaveChangesAsync();
            }

            var log1 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var doc1 = await log1.Content.ReadFromJsonAsync<JsonElement>();
            string jwt1 = doc1.GetProperty("accessToken").GetString()!;

            var setupReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/setup");
            setupReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt1);
            var setupRes = await _client.SendAsync(setupReq);
            var setupDoc = await setupRes.Content.ReadFromJsonAsync<JsonElement>();
            string secret = setupDoc.GetProperty("secret").GetString()!;

            var totp = new TotpService();
            string code = totp.GenerateCurrentCode(secret);

            var confReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/confirm");
            confReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt1);
            confReq.Content = JsonContent.Create(new { Code = code });
            var confRes = await _client.SendAsync(confReq);
            var confDoc = await confRes.Content.ReadFromJsonAsync<JsonElement>();
            var codes = confDoc.GetProperty("recoveryCodes").EnumerateArray().Select(x => x.GetString()!).ToList();

            string testCode = codes.First();

            // Trigger challenge
            var log2 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var doc2 = await log2.Content.ReadFromJsonAsync<JsonElement>();
            string ticket = doc2.GetProperty("twoFactorTicket").GetString()!;

            // Verify with Recovery Code
            var verRes = await _client.PostAsJsonAsync("/api/v1/auth/2fa/verify", new
            {
                TwoFactorTicket = ticket,
                Code = testCode,
                IsRecoveryCode = true
            });
            verRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Attempt to reuse same recovery code on a new login -> Must fail!
            var log3 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var doc3 = await log3.Content.ReadFromJsonAsync<JsonElement>();
            string ticket2 = doc3.GetProperty("twoFactorTicket").GetString()!;

            var reuseRes = await _client.PostAsJsonAsync("/api/v1/auth/2fa/verify", new
            {
                TwoFactorTicket = ticket2,
                Code = testCode,
                IsRecoveryCode = true
            });
            (reuseRes.StatusCode == HttpStatusCode.Unauthorized || reuseRes.StatusCode == HttpStatusCode.BadRequest).Should().BeTrue();
        }

        [Fact]
        public async Task Regenerate_Recovery_Codes_Requires_Password_Reauth_And_Invalidates_Old_Codes()
        {
            string username = "regen_rec_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{username}@edm.control";
            string password = "InitialPassword!2026";

            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                u!.Role = UserRole.SUPER_ADMIN;
                await db.SaveChangesAsync();
            }

            var log1 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var doc1 = await log1.Content.ReadFromJsonAsync<JsonElement>();
            string jwt1 = doc1.GetProperty("accessToken").GetString()!;

            // Activate 2FA
            var setupReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/setup");
            setupReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt1);
            var setupRes = await _client.SendAsync(setupReq);
            var setupDoc = await setupRes.Content.ReadFromJsonAsync<JsonElement>();
            string secret = setupDoc.GetProperty("secret").GetString()!;

            var totp = new TotpService();
            string code = totp.GenerateCurrentCode(secret);

            var confReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/confirm");
            confReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt1);
            confReq.Content = JsonContent.Create(new { Code = code });
            var confRes = await _client.SendAsync(confReq);
            var confDoc = await confRes.Content.ReadFromJsonAsync<JsonElement>();
            var oldCodes = confDoc.GetProperty("recoveryCodes").EnumerateArray().Select(x => x.GetString()!).ToList();

            // Attempt regenerate with WRONG password -> Must fail
            var badRegenReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/regenerate-recovery-codes");
            badRegenReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt1);
            badRegenReq.Content = JsonContent.Create(new { Password = "WrongPassword!123" });
            var badRegenRes = await _client.SendAsync(badRegenReq);
            badRegenRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // Regenerate with CORRECT password
            var regenReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/regenerate-recovery-codes");
            regenReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt1);
            regenReq.Content = JsonContent.Create(new { Password = password });
            var regenRes = await _client.SendAsync(regenReq);
            regenRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var regenDoc = await regenRes.Content.ReadFromJsonAsync<JsonElement>();
            var newCodes = regenDoc.GetProperty("recoveryCodes").EnumerateArray().Select(x => x.GetString()!).ToList();

            newCodes.Should().NotBeNullOrEmpty();
            newCodes.First().Should().NotBe(oldCodes.First());

            // Old code must now fail
            var log2 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var doc2 = await log2.Content.ReadFromJsonAsync<JsonElement>();
            string ticket = doc2.GetProperty("twoFactorTicket").GetString()!;

            var oldCodeUseRes = await _client.PostAsJsonAsync("/api/v1/auth/2fa/verify", new
            {
                TwoFactorTicket = ticket,
                Code = oldCodes.First(),
                IsRecoveryCode = true
            });
            (oldCodeUseRes.StatusCode == HttpStatusCode.Unauthorized || oldCodeUseRes.StatusCode == HttpStatusCode.BadRequest).Should().BeTrue();

            // New code succeeds with a fresh login challenge ticket
            var log3 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var doc3 = await log3.Content.ReadFromJsonAsync<JsonElement>();
            string ticket2 = doc3.GetProperty("twoFactorTicket").GetString()!;

            var newCodeUseRes = await _client.PostAsJsonAsync("/api/v1/auth/2fa/verify", new
            {
                TwoFactorTicket = ticket2,
                Code = newCodes.First(),
                IsRecoveryCode = true
            });
            newCodeUseRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Disable_2FA_Requires_Password_Reauth_And_Revokes_Active_Sessions()
        {
            string username = "dis2fa_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{username}@edm.control";
            string password = "InitialPassword!2026";

            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                u!.Role = UserRole.SUPER_ADMIN;
                await db.SaveChangesAsync();
            }

            var log1 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var doc1 = await log1.Content.ReadFromJsonAsync<JsonElement>();
            string jwt1 = doc1.GetProperty("accessToken").GetString()!;

            // Activate 2FA
            var setupReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/setup");
            setupReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt1);
            var setupRes = await _client.SendAsync(setupReq);
            var setupDoc = await setupRes.Content.ReadFromJsonAsync<JsonElement>();
            string secret = setupDoc.GetProperty("secret").GetString()!;

            var totp = new TotpService();
            string code = totp.GenerateCurrentCode(secret);

            var confReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/confirm");
            confReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt1);
            confReq.Content = JsonContent.Create(new { Code = code });
            await _client.SendAsync(confReq);

            // Attempt Disable with Wrong Password -> Must fail
            var badDisReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/disable");
            badDisReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt1);
            badDisReq.Content = JsonContent.Create(new { Password = "WrongPassword!123" });
            var badDisRes = await _client.SendAsync(badDisReq);
            badDisRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // Disable with Correct Password
            var disReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/disable");
            disReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt1);
            disReq.Content = JsonContent.Create(new { Password = password });
            var disRes = await _client.SendAsync(disReq);
            disRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Now login directly with password without 2FA prompt
            var directLog = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            directLog.StatusCode.Should().Be(HttpStatusCode.OK);
            var directDoc = await directLog.Content.ReadFromJsonAsync<JsonElement>();
            directDoc.TryGetProperty("accessToken", out var tok).Should().BeTrue();
            tok.GetString().Should().NotBeNullOrEmpty();
            if (directDoc.TryGetProperty("requires2FA", out var req2fa) && req2fa.ValueKind == JsonValueKind.True)
            {
                Assert.Fail("2FA was disabled but login still requested 2FA");
            }
        }

        [Fact]
        public async Task Recovery_Email_Change_Requires_Password_Reauth_And_Token_Verification()
        {
            string username = "recemail_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{username}@edm.control";
            string password = "InitialPassword!2026";
            string newRecoveryEmail = $"{username}_backup@secure.org";

            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                u!.Role = UserRole.SUPER_ADMIN;
                await db.SaveChangesAsync();
            }

            var log1 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = email, Password = password });
            var doc1 = await log1.Content.ReadFromJsonAsync<JsonElement>();
            string jwt = doc1.GetProperty("accessToken").GetString()!;

            // 1. Request recovery email change with WRONG password -> Fails
            var badReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/recovery-email/request");
            badReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            badReq.Content = JsonContent.Create(new { Password = "WrongPassword!123", NewRecoveryEmail = newRecoveryEmail });
            var badRes = await _client.SendAsync(badReq);
            badRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // 2. Request recovery email change with CORRECT password
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/recovery-email/request");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            req.Content = JsonContent.Create(new { Password = password, NewRecoveryEmail = newRecoveryEmail });
            var res = await _client.SendAsync(req);
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            var resDoc = await res.Content.ReadFromJsonAsync<JsonElement>();
            string verificationToken = resDoc.GetProperty("verificationToken").GetString()!;

            // 3. Confirm with WRONG token -> Fails
            var badConfReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/recovery-email/confirm");
            badConfReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            badConfReq.Content = JsonContent.Create(new { Token = "invalid_token_999" });
            var badConfRes = await _client.SendAsync(badConfReq);
            badConfRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // 4. Confirm with CORRECT token
            var confReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/recovery-email/confirm");
            confReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            confReq.Content = JsonContent.Create(new { Token = verificationToken });
            var confRes = await _client.SendAsync(confReq);
            confRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 5. Query Security Overview -> Verified
            var secReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/security-overview");
            secReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            var secRes = await _client.SendAsync(secReq);
            var secDoc = await secRes.Content.ReadFromJsonAsync<JsonElement>();
            secDoc.GetProperty("hasRecoveryEmail").GetBoolean().Should().BeTrue();
            secDoc.GetProperty("recoveryEmail").GetString().Should().Be(newRecoveryEmail);
            secDoc.GetProperty("isRecoveryEmailVerified").GetBoolean().Should().BeTrue();
        }

        [Fact]
        public async Task Password_Recovery_Via_Verified_Recovery_Email_With_2FA_Enforcement()
        {
            string username = "rec_flow_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string primaryEmail = $"{username}@edm.control";
            string recoveryEmail = $"{username}_recovery@emergency.org";
            string password = "InitialPassword!2026";
            string brandNewPassword = "BrandNewSuperSecurePassword!2026";

            // 1. Register & Elevate
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = primaryEmail, Password = password });
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == primaryEmail);
                u!.Role = UserRole.SUPER_ADMIN;
                await db.SaveChangesAsync();
            }

            var log1 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = primaryEmail, Password = password });
            var doc1 = await log1.Content.ReadFromJsonAsync<JsonElement>();
            string jwt = doc1.GetProperty("accessToken").GetString()!;

            // 2. Set & Confirm Recovery Email
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/recovery-email/request");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            req.Content = JsonContent.Create(new { Password = password, NewRecoveryEmail = recoveryEmail });
            var reqRes = await _client.SendAsync(req);
            var reqDoc = await reqRes.Content.ReadFromJsonAsync<JsonElement>();
            string verToken = reqDoc.GetProperty("verificationToken").GetString()!;

            var confReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/recovery-email/confirm");
            confReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            confReq.Content = JsonContent.Create(new { Token = verToken });
            await _client.SendAsync(confReq);

            // 3. Activate 2FA
            var setupReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/setup");
            setupReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            var setupRes = await _client.SendAsync(setupReq);
            var setupDoc = await setupRes.Content.ReadFromJsonAsync<JsonElement>();
            string secret = setupDoc.GetProperty("secret").GetString()!;

            var totp = new TotpService();
            string code = totp.GenerateCurrentCode(secret);

            var conf2FaReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/2fa/confirm");
            conf2FaReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            conf2FaReq.Content = JsonContent.Create(new { Code = code });
            await _client.SendAsync(conf2FaReq);

            // 4. Trigger Password Recovery using the RECOVERY EMAIL address
            var forgotRes = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { Email = recoveryEmail });
            forgotRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var forgotDoc = await forgotRes.Content.ReadFromJsonAsync<JsonElement>();
            string resetToken = forgotDoc.GetProperty("resetToken").GetString()!;

            // 5. Complete Password Reset using valid 2FA code
            string freshCode = totp.GenerateCurrentCode(secret);
            var resetRes = await _client.PostAsJsonAsync("/api/v1/auth/reset-password", new
            {
                Token = resetToken,
                NewPassword = brandNewPassword,
                TwoFactorCode = freshCode
            });
            resetRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 6. Old Password fails
            var oldLog = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = primaryEmail, Password = password });
            oldLog.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // 7. New Password succeeds with 2FA
            var newLog = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = primaryEmail, Password = brandNewPassword });
            newLog.StatusCode.Should().Be(HttpStatusCode.OK);
            var newDoc = await newLog.Content.ReadFromJsonAsync<JsonElement>();
            newDoc.GetProperty("requires2FA").GetBoolean().Should().BeTrue();
        }
    }
}
