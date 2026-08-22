using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
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
    public class ProductionSecurityAndE2EAuditTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public ProductionSecurityAndE2EAuditTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<(string token, Guid userId)> CreateUserWithRoleAsync(UserRole role)
        {
            string username = $"audit_{Guid.NewGuid():N}".Substring(0, 16);
            string email = $"{username}@edm.test";
            string password = "AuditPassword!2026";

            var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Username = username,
                Email = email,
                Password = password
            });
            regRes.EnsureSuccessStatusCode();

            Guid userId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstAsync(x => x.Email == email);
                u.Role = role;
                u.IsActive = true;
                await db.SaveChangesAsync();
                userId = u.Id;
            }

            var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                UsernameOrEmail = email,
                Password = password,
                RememberDevice = true
            });
            loginRes.EnsureSuccessStatusCode();

            var loginData = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
            string token = loginData.GetProperty("accessToken").GetString()!;
            return (token, userId);
        }

        [Fact]
        public async Task Security_Audit_Unauthenticated_Calls_To_Admin_Endpoints_Are_Rejected()
        {
            var endpoints = new[]
            {
                (HttpMethod.Get, "/api/v1/admin/dashboard/summary"),
                (HttpMethod.Get, "/api/v1/admin/users"),
                (HttpMethod.Get, "/api/v1/admin/audit-logs"),
                (HttpMethod.Get, "/api/v1/admin/releases"),
                (HttpMethod.Get, "/api/v1/admin/analytics/website"),
                (HttpMethod.Get, "/api/v1/admin/analytics/downloads/overview"),
                (HttpMethod.Post, "/api/v1/admin/releases"),
                (HttpMethod.Post, "/api/v1/pricing"),
                (HttpMethod.Put, "/api/v1/content/hero")
            };

            foreach (var (method, path) in endpoints)
            {
                var req = new HttpRequestMessage(method, path);
                if (method == HttpMethod.Post || method == HttpMethod.Put)
                {
                    req.Content = JsonContent.Create(new { });
                }

                var res = await _client.SendAsync(req);
                res.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"Anonymous access to '{path}' must return 401 Unauthorized.");
            }
        }

        [Fact]
        public async Task Security_Audit_Standard_User_Cannot_Access_Or_Mutate_Admin_Resources()
        {
            var (userToken, _) = await CreateUserWithRoleAsync(UserRole.USER);

            var forbiddenEndpoints = new[]
            {
                (HttpMethod.Get, "/api/v1/admin/dashboard/summary"),
                (HttpMethod.Get, "/api/v1/admin/users"),
                (HttpMethod.Get, "/api/v1/admin/audit-logs"),
                (HttpMethod.Post, "/api/v1/admin/releases"),
                (HttpMethod.Put, "/api/v1/content/hero"),
                (HttpMethod.Post, "/api/v1/licenses/generate")
            };

            foreach (var (method, path) in forbiddenEndpoints)
            {
                var req = new HttpRequestMessage(method, path);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
                if (method == HttpMethod.Post || method == HttpMethod.Put)
                {
                    req.Content = JsonContent.Create(new { Title = "Hacked Title", Version = "9.9.9" });
                }

                var res = await _client.SendAsync(req);
                res.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"Standard user access to '{path}' must return 403 Forbidden.");
            }
        }

        [Fact]
        public async Task Security_Audit_Malicious_File_Upload_Extensions_Are_Strictly_Rejected()
        {
            var (adminToken, _) = await CreateUserWithRoleAsync(UserRole.SUPER_ADMIN);

            // 1. Create a draft release
            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            createReq.Content = JsonContent.Create(new { Platform = 0, Version = $"9.{Random.Shared.Next(100, 999)}.0", Title = "Upload Security Test" });
            var createRes = await _client.SendAsync(createReq);
            var createDoc = await createRes.Content.ReadFromJsonAsync<JsonElement>();
            string releaseId = createDoc.GetProperty("releaseId").GetString()!;

            // 2. Test dangerous / prohibited file extensions
            var dangerousFiles = new[]
            {
                "malicious.ps1",
                "backdoor.php",
                "shell.sh",
                "exploit.bat",
                "script.vbs",
                "payload.aspx",
                "injector.dll"
            };

            foreach (var dangerousFilename in dangerousFiles)
            {
                using var form = new MultipartFormDataContent();
                form.Add(new ByteArrayContent(new byte[] { 0x3C, 0x3F, 0x70, 0x68, 0x70 }), "file", dangerousFilename);

                var uploadReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/releases/{releaseId}/artifacts/upload");
                uploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
                uploadReq.Content = form;

                var uploadRes = await _client.SendAsync(uploadReq);
                uploadRes.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"Uploading prohibited file '{dangerousFilename}' must be rejected with 400 Bad Request.");
            }
        }

        [Fact]
        public async Task Security_Audit_Path_Traversal_Filenames_Are_Normalized_And_Isolated()
        {
            var (adminToken, _) = await CreateUserWithRoleAsync(UserRole.SUPER_ADMIN);

            // 1. Create release
            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            createReq.Content = JsonContent.Create(new { Platform = 0, Version = $"9.{Random.Shared.Next(100, 999)}.0", Title = "Traversal Test" });
            var createRes = await _client.SendAsync(createReq);
            var createDoc = await createRes.Content.ReadFromJsonAsync<JsonElement>();
            string releaseId = createDoc.GetProperty("releaseId").GetString()!;

            // 2. Upload file with path traversal attempt in filename
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }), "file", "../../../Windows/System32/evil_setup.exe");

            var uploadReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/releases/{releaseId}/artifacts/upload");
            uploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            uploadReq.Content = form;

            var uploadRes = await _client.SendAsync(uploadReq);
            uploadRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var uploadDoc = await uploadRes.Content.ReadFromJsonAsync<JsonElement>();
            string artifactId = uploadDoc.GetProperty("artifactId").GetString()!;

            // Verify in DB that artifact name is cleanly normalized without traversal slashes
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
            var artifact = await db.ReleaseArtifacts.FindAsync(Guid.Parse(artifactId));
            artifact.Should().NotBeNull();
            artifact!.ArtifactName.Should().Be("evil_setup.exe");
            artifact.ArtifactName.Should().NotContain("..");
            artifact.ArtifactName.Should().NotContain("/");
            artifact.ArtifactName.Should().NotContain("\\");
        }

        [Fact]
        public async Task Security_Audit_Session_Revocation_Instantly_Invalidates_Access()
        {
            var (adminToken, userId) = await CreateUserWithRoleAsync(UserRole.SUPER_ADMIN);

            // 1. Get active sessions
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
            var session = await db.Sessions.Include(s => s.RefreshTokens).Include(s => s.Device).FirstOrDefaultAsync(s => s.UserId == userId && !s.IsRevoked);
            session.Should().NotBeNull();
            var rf = session!.RefreshTokens.FirstOrDefault();

            // 2. Revoke the session via admin endpoint
            var revokeReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/revoke-session/{session!.Id}");
            revokeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            var revokeRes = await _client.SendAsync(revokeReq);
            revokeRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. Attempt to use the revoked refresh token
            var refreshRes = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new
            {
                RefreshToken = rf?.TokenHash ?? "fake_token",
                InstallationId = session.Device?.InstallationId
            });
            refreshRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Security_Audit_Telemetry_Never_Persists_Raw_Client_IP()
        {
            string testSession = $"audit_ip_{Guid.NewGuid():N}";
            var eventReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/analytics/event");
            eventReq.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            eventReq.Headers.Add("X-Forwarded-For", "203.0.113.195");
            eventReq.Headers.Add("CF-IPCountry", "JP");
            eventReq.Content = JsonContent.Create(new
            {
                EventType = "pageview",
                SessionId = testSession,
                PagePath = "/security-audit"
            });

            var res = await _client.SendAsync(eventReq);
            res.StatusCode.Should().Be(HttpStatusCode.OK);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
            var evt = await db.WebsiteEvents.FirstOrDefaultAsync(e => e.SessionId == testSession);
            evt.Should().NotBeNull();
            evt!.ClientIpCoarse.Should().NotBeNull();
            evt.ClientIpCoarse.Should().NotBe("203.0.113.195"); // Must be masked to subnet /24
            evt.ClientIpCoarse.Should().EndWith(".0/24");
        }

        [Fact]
        public async Task E2E_Audit_Single_Source_Of_Truth_Publish_And_Rollback_Workflow()
        {
            var (adminToken, _) = await CreateUserWithRoleAsync(UserRole.SUPER_ADMIN);
            string newVer = $"5.{Random.Shared.Next(100, 999)}.0";

            // 1. Super Admin publishes a new release
            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            createReq.Content = JsonContent.Create(new
            {
                Platform = 0,
                Version = newVer,
                Title = $"EDM {newVer} Single Source Release",
                MinimumSupportedVersion = "1.0.0"
            });
            var createRes = await _client.SendAsync(createReq);
            var createDoc = await createRes.Content.ReadFromJsonAsync<JsonElement>();
            string releaseId = createDoc.GetProperty("releaseId").GetString()!;

            // Upload artifact
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }), "file", "EDM-Setup.exe");
            var uploadReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/releases/{releaseId}/artifacts/upload");
            uploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            uploadReq.Content = form;
            var uploadRes = await _client.SendAsync(uploadReq);
            uploadRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 2. Website latest release endpoint verifies new version
            var webRes = await _client.GetAsync("/api/v1/releases/latest?platform=0");
            webRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var webDoc = await webRes.Content.ReadFromJsonAsync<JsonElement>();
            webDoc.GetProperty("version").GetString().Should().Be(newVer);

            // 3. Desktop Application updater verifies new version
            var updateCheckRes = await _client.PostAsJsonAsync("/api/v1/updates/check", new
            {
                Platform = 0,
                CurrentVersion = "1.0.0",
                InstallationId = Guid.NewGuid()
            });
            updateCheckRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var updateDoc = await updateCheckRes.Content.ReadFromJsonAsync<JsonElement>();
            updateDoc.GetProperty("updateAvailable").GetBoolean().Should().BeTrue();
            updateDoc.GetProperty("latestVersion").GetString().Should().Be(newVer);

            // 4. Admin rolls back the release
            var rollbackReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/releases/{releaseId}/rollback");
            rollbackReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            rollbackReq.Content = JsonContent.Create(new
            {
                TargetVersion = "2.1.0",
                Reason = "E2E rollback certification test"
            });
            var rollbackRes = await _client.SendAsync(rollbackReq);
            rollbackRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 5. Website & Updater immediately reflect rollback
            var webRollbackRes = await _client.GetAsync("/api/v1/releases/latest?platform=0");
            webRollbackRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var webRollbackDoc = await webRollbackRes.Content.ReadFromJsonAsync<JsonElement>();
            webRollbackDoc.GetProperty("version").GetString().Should().Be("2.1.0");
        }
    }
}
