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
using EDM.ControlPlane.Api.Services;

namespace EDM.Tests.ControlPlane
{
    public class CentralizedServicesAndApiIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public CentralizedServicesAndApiIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<string> GetSuperAdminTokenAsync()
        {
            string username = $"super_{Guid.NewGuid():N}".Substring(0, 16);
            string email = $"{username}@edm.test";
            string password = "SuperSecretPassword!2026";

            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstAsync(x => x.Email == email);
                u.Role = UserRole.SUPER_ADMIN;
                u.IsActive = true;
                await db.SaveChangesAsync();
            }

            var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                UsernameOrEmail = email,
                Password = password
            });

            var doc = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
            return doc.GetProperty("accessToken").GetString()!;
        }

        [Fact]
        public async Task License_Lifecycle_Generation_Activation_Suspension_And_Revocation()
        {
            var token = await GetSuperAdminTokenAsync();
            Guid planId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var plan = await db.Plans.FirstAsync();
                planId = plan.Id;
            }

            // 1. Generate License via API
            var genReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/licenses/generate");
            genReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            genReq.Content = JsonContent.Create(new { PlanId = planId, MaxActivations = 2 });
            var genRes = await _client.SendAsync(genReq);
            genRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var genDoc = await genRes.Content.ReadFromJsonAsync<JsonElement>();
            string rawKey = genDoc.GetProperty("plaintextKey").GetString()!;
            Guid licenseId = genDoc.GetProperty("licenseId").GetGuid();

            rawKey.Should().StartWith("EDM-");

            // 2. Activate License on Device A
            var installIdA = Guid.NewGuid();
            var actRes1 = await _client.PostAsJsonAsync("/api/v1/licenses/activate", new
            {
                LicenseKey = rawKey,
                InstallationId = installIdA
            });
            actRes1.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. Suspend License
            var suspReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/licenses/{licenseId}/suspend");
            suspReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            suspReq.Content = JsonContent.Create(new { Reason = "Payment investigation" });
            var suspRes = await _client.SendAsync(suspReq);
            suspRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Activation should now fail
            var actRes2 = await _client.PostAsJsonAsync("/api/v1/licenses/activate", new
            {
                LicenseKey = rawKey,
                InstallationId = Guid.NewGuid()
            });
            actRes2.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // 5. Reactivate License
            var reactReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/licenses/{licenseId}/reactivate");
            reactReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var reactRes = await _client.SendAsync(reactReq);
            reactRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 6. Revoke License permanently
            var revReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/licenses/{licenseId}/revoke");
            revReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            revReq.Content = JsonContent.Create(new { Reason = "Fraudulent chargeback" });
            var revRes = await _client.SendAsync(revReq);
            revRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 7. Activation fails permanently
            var actRes3 = await _client.PostAsJsonAsync("/api/v1/licenses/activate", new
            {
                LicenseKey = rawKey,
                InstallationId = Guid.NewGuid()
            });
            actRes3.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Release_Management_Publish_And_Safe_Rollback_Workflow()
        {
            var token = await GetSuperAdminTokenAsync();

            // 1. Create Baseline Stable Release 2.5.0
            var baseReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            baseReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            baseReq.Content = JsonContent.Create(new
            {
                Platform = ClientType.DesktopWindows,
                Version = "2.5.0",
                Channel = "stable",
                Title = "EDM 2.5.0 Stable Base",
                MinimumSupportedVersion = "1.0.0",
                ReleaseNotes = "Base stable build",
                IsMandatory = false,
                Severity = ReleaseSeverity.Standard
            });
            var baseRes = await _client.SendAsync(baseReq);
            baseRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 2. Create Faulty Release 2.6.0
            var faultyReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            faultyReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            faultyReq.Content = JsonContent.Create(new
            {
                Platform = ClientType.DesktopWindows,
                Version = "2.6.0",
                Channel = "stable",
                Title = "EDM 2.6.0 Faulty Build",
                MinimumSupportedVersion = "1.0.0",
                ReleaseNotes = "Contains unexpected regression",
                IsMandatory = false,
                Severity = ReleaseSeverity.Critical
            });
            var faultyRes = await _client.SendAsync(faultyReq);
            faultyRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var faultyDoc = await faultyRes.Content.ReadFromJsonAsync<JsonElement>();
            Guid faultyId = faultyDoc.GetProperty("releaseId").GetGuid();

            // 3. Rollback 2.6.0 to 2.5.0
            var rollReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/releases/{faultyId}/rollback");
            rollReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            rollReq.Content = JsonContent.Create(new
            {
                TargetVersion = "2.5.0",
                Reason = "Critical stability regression in socket pool"
            });
            var rollRes = await _client.SendAsync(rollReq);
            rollRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Verify in DB
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var rolledBack = await db.Releases.FindAsync(faultyId);
                rolledBack!.IsWithdrawn.Should().BeTrue();
                rolledBack.RollbackTargetVersion.Should().Be("2.5.0");
                rolledBack.RollbackReason.Should().Contain("Critical stability");
            }
        }

        [Fact]
        public async Task Website_Content_Versioning_And_Pricing_Tiers()
        {
            var token = await GetSuperAdminTokenAsync();

            // 1. Update Content (Version increments)
            var putReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/content/faq");
            putReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            putReq.Content = JsonContent.Create(new
            {
                Title = "Frequently Asked Questions",
                ContentJson = "{\"questions\":[{\"q\":\"Is EDM free?\",\"a\":\"Yes!\"}]}",
                Locale = "en"
            });
            var putRes = await _client.SendAsync(putReq);
            putRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var putDoc = await putRes.Content.ReadFromJsonAsync<JsonElement>();
            putDoc.GetProperty("version").GetInt32().Should().BeGreaterThanOrEqualTo(1);

            // 2. Fetch public content
            var getRes = await _client.GetAsync("/api/v1/content/faq?locale=en");
            getRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. Fetch public pricing tiers
            var pricingRes = await _client.GetAsync("/api/v1/pricing");
            pricingRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var pricingDoc = await pricingRes.Content.ReadFromJsonAsync<JsonElement>();
            pricingDoc.GetArrayLength().Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task Support_Ticket_Creation_Threading_And_Status_Lifecycle()
        {
            var token = await GetSuperAdminTokenAsync();

            // 1. Customer creates ticket
            var createRes = await _client.PostAsJsonAsync("/api/v1/support/tickets", new
            {
                CustomerEmail = "user@example.org",
                CustomerName = "Alice User",
                Subject = "Connection timeout during torrent import",
                Category = TicketCategory.Technical,
                Priority = TicketPriority.High,
                Message = "When I paste magnet links, the socket times out after 10 seconds."
            });
            createRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var createDoc = await createRes.Content.ReadFromJsonAsync<JsonElement>();
            Guid ticketId = createDoc.GetProperty("ticketId").GetGuid();

            // 2. Admin retrieves ticket details and messages
            var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/support/tickets/{ticketId}");
            getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var getRes = await _client.SendAsync(getReq);
            getRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. Admin replies to ticket
            var replyReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/support/tickets/{ticketId}/reply");
            replyReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            replyReq.Content = JsonContent.Create(new
            {
                MessageContent = "Please check if your firewall allows outbound TCP connections on ports 6881-6889."
            });
            var replyRes = await _client.SendAsync(replyReq);
            replyRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Admin updates status to Resolved
            var statReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/support/tickets/{ticketId}/status");
            statReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            statReq.Content = JsonContent.Create(new { Status = TicketStatus.Resolved });
            var statRes = await _client.SendAsync(statReq);
            statRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task System_Health_Diagnostic_Probes_And_Snapshots()
        {
            var token = await GetSuperAdminTokenAsync();

            var diagReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/health/diagnostics");
            diagReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var diagRes = await _client.SendAsync(diagReq);
            diagRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var doc = await diagRes.Content.ReadFromJsonAsync<JsonElement>();
            doc.GetProperty("overallStatus").GetInt32().Should().Be((int)HealthStatus.Healthy);
            doc.GetProperty("components").GetProperty("Database").GetProperty("status").GetInt32().Should().Be((int)HealthStatus.Healthy);
        }
    }
}
