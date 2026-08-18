using System;
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
    public class RbacAndPermissionSecurityIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public RbacAndPermissionSecurityIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<(Guid UserId, string Token)> CreateUserWithRoleAsync(string usernamePrefix, UserRole role)
        {
            string username = $"{usernamePrefix}_{Guid.NewGuid():N}".Substring(0, 16);
            string email = $"{username}@edm.test";
            string password = "StrongTestPassword!2026";

            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });

            Guid userId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstAsync(x => x.Email == email);
                u.Role = role;
                u.IsActive = true;
                userId = u.Id;
                await db.SaveChangesAsync();
            }

            var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                UsernameOrEmail = email,
                Password = password
            });

            var doc = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
            string token = doc.GetProperty("accessToken").GetString()!;

            return (userId, token);
        }

        [Fact]
        public async Task SuperAdmin_Possesses_Wildcard_Access_To_All_Endpoints()
        {
            var (_, superToken) = await CreateUserWithRoleAsync("super_rbac", UserRole.SUPER_ADMIN);

            // 1. Can access releases (releases.read)
            var relReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/releases");
            relReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superToken);
            var relRes = await _client.SendAsync(relReq);
            relRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 2. Can access license generation (licenses.manage)
            Guid planId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var plan = await db.Plans.FirstAsync();
                planId = plan.Id;
            }

            var licReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/licenses/generate");
            licReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superToken);
            licReq.Content = JsonContent.Create(new { PlanId = planId, MaxActivations = 5 });
            var licRes = await _client.SendAsync(licReq);
            licRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. Can access diagnostics (system.health.read)
            var diagReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/health/diagnostics");
            diagReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superToken);
            var diagRes = await _client.SendAsync(diagReq);
            diagRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Direct_API_Attacker_Without_Permission_Is_Strictly_Rejected_With_403()
        {
            // Regular User attempting to create a release
            var (_, userToken) = await CreateUserWithRoleAsync("user_attacker", UserRole.USER);

            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            createReq.Content = JsonContent.Create(new
            {
                Platform = ClientType.DesktopWindows,
                Version = "9.9.9",
                Title = "Malicious Release",
                MinimumSupportedVersion = "1.0.0",
                ReleaseNotes = "Exploit attempt",
                IsMandatory = false,
                Severity = ReleaseSeverity.Standard
            });

            var createRes = await _client.SendAsync(createReq);
            createRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            var errDoc = await createRes.Content.ReadFromJsonAsync<JsonElement>();
            errDoc.GetProperty("error").GetString().Should().Be("FORBIDDEN_INSUFFICIENT_PERMISSIONS");
            errDoc.GetProperty("requiredPermission").GetString().Should().Be(Permissions.ReleasesCreate);

            // Verify Audit Log captured the unauthorized access attempt
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var log = await db.AuditLogs
                    .OrderByDescending(a => a.TimestampUtc)
                    .FirstOrDefaultAsync(a => a.Action == "UNAUTHORIZED_ACCESS_DENIED" && a.TargetId == Permissions.ReleasesCreate);

                log.Should().NotBeNull();
                log!.ResultStatus.Should().Be("DENIED");
            }
        }

        [Fact]
        public async Task Support_Role_Cannot_Create_Release_Or_Rollback_Without_Permission()
        {
            var (_, supportToken) = await CreateUserWithRoleAsync("support_agent", UserRole.SUPPORT);

            // Support can read users
            var usersReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/users");
            usersReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supportToken);
            var usersRes = await _client.SendAsync(usersReq);
            usersRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Support CANNOT create releases
            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supportToken);
            createReq.Content = JsonContent.Create(new
            {
                Platform = ClientType.DesktopWindows,
                Version = "8.8.8",
                Title = "Support Attempt",
                MinimumSupportedVersion = "1.0.0",
                Severity = ReleaseSeverity.Standard
            });

            var createRes = await _client.SendAsync(createReq);
            createRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task User_Permission_Override_Dynamically_Grants_And_Revokes_Access()
        {
            var (userId, supportToken) = await CreateUserWithRoleAsync("override_tester", UserRole.SUPPORT);
            var (_, superToken) = await CreateUserWithRoleAsync("super_granter", UserRole.SUPER_ADMIN);

            // 1. Initial attempt to update website content fails with 403
            var putReq1 = new HttpRequestMessage(HttpMethod.Put, "/api/v1/content/hero");
            putReq1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supportToken);
            putReq1.Content = JsonContent.Create(new { Title = "New Hero Title", ContentJson = "{}" });
            var putRes1 = await _client.SendAsync(putReq1);
            putRes1.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // 2. Super Admin grants 'website.manage' override to the support user
            var grantReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{userId}/permissions/grant");
            grantReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superToken);
            grantReq.Content = JsonContent.Create(new { PermissionCode = Permissions.WebsiteManage });
            var grantRes = await _client.SendAsync(grantReq);
            grantRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. User now SUCCEEDS at updating website content!
            var putReq2 = new HttpRequestMessage(HttpMethod.Put, "/api/v1/content/hero");
            putReq2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supportToken);
            putReq2.Content = JsonContent.Create(new { Title = "Override Hero Title", ContentJson = "{}" });
            var putRes2 = await _client.SendAsync(putReq2);
            putRes2.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Super Admin revokes 'website.manage' override
            var revokeReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{userId}/permissions/revoke");
            revokeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superToken);
            revokeReq.Content = JsonContent.Create(new { PermissionCode = Permissions.WebsiteManage });
            var revokeRes = await _client.SendAsync(revokeReq);
            revokeRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 5. User is once again REJECTED with 403 Forbidden
            var putReq3 = new HttpRequestMessage(HttpMethod.Put, "/api/v1/content/hero");
            putReq3.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supportToken);
            putReq3.Content = JsonContent.Create(new { Title = "Blocked Hero Title", ContentJson = "{}" });
            var putRes3 = await _client.SendAsync(putReq3);
            putRes3.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Unauthenticated_Calls_To_Protected_Endpoints_Return_401()
        {
            var res = await _client.GetAsync("/api/v1/admin/releases");
            res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var licRes = await _client.GetAsync("/api/v1/licenses");
            licRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
