using System;
using System.Collections.Generic;
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
    public class WebsiteContentAndSyncIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public WebsiteContentAndSyncIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<string> GetSuperAdminTokenAsync()
        {
            string username = $"webadm_{Guid.NewGuid():N}".Substring(0, 16);
            string email = $"{username}@edm.test";
            string password = "WebsiteAdminPassword!2026";

            var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Username = username,
                Email = email,
                Password = password
            });
            regRes.EnsureSuccessStatusCode();

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
                Password = password,
                RememberDevice = true
            });
            loginRes.EnsureSuccessStatusCode();

            var loginData = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
            return loginData.GetProperty("accessToken").GetString()!;
        }

        [Fact]
        public async Task Release_Version_Update_Syncs_Promptly_To_Public_Endpoints()
        {
            string token = await GetSuperAdminTokenAsync();

            string newVer = $"3.0.{Random.Shared.Next(1, 99)}";

            // 1. Admin creates and publishes new version
            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            createReq.Content = JsonContent.Create(new
            {
                Platform = 0,
                Version = newVer,
                Title = $"EDM {newVer} Quantum Engine",
                ReleaseNotes = "• 32 multi-socket turbo streams\n• Instant resume recovery",
                MinimumSupportedVersion = "2.0.0",
                IsMandatory = false,
                Severity = 1,
                Artifacts = new[]
                {
                    new
                    {
                        ArtifactName = $"EDM-Setup-{newVer}.exe",
                        Architecture = "x64",
                        DownloadUrl = $"/api/v1/releases/artifacts/{Guid.NewGuid()}/download",
                        Sha256Hash = "93049cf86301342dbdaae74256d4013a1e30133aa26a38dbe08e2a6e3e32d023",
                        FileSizeBytes = 2500000
                    }
                }
            });
            var createRes = await _client.SendAsync(createReq);
            createRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 2. Public website queries latest release
            var webRes = await _client.GetAsync("/api/v1/releases/latest?platform=DesktopWindows");
            webRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var webDoc = await webRes.Content.ReadFromJsonAsync<JsonElement>();
            webDoc.GetProperty("version").GetString().Should().Be(newVer);
            webDoc.GetProperty("title").GetString().Should().Be($"EDM {newVer} Quantum Engine");

            // 3. EDM desktop app checks for updates
            var updateRes = await _client.PostAsJsonAsync("/api/v1/updates/check", new
            {
                Platform = 0,
                CurrentVersion = "2.0.0",
                InstallationId = Guid.NewGuid(),
                Channel = "stable"
            });
            updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var updateDoc = await updateRes.Content.ReadFromJsonAsync<JsonElement>();
            updateDoc.GetProperty("updateAvailable").GetBoolean().Should().BeTrue();
            updateDoc.GetProperty("latestVersion").GetString().Should().Be(newVer);
        }

        [Fact]
        public async Task Dynamic_Website_Section_Content_Updates_And_Retrieval()
        {
            string token = await GetSuperAdminTokenAsync();

            string updatedHeadline = $"The #1 Verified Download Manager ({Guid.NewGuid():N})";
            string updatedDesc = "Engineered with 32 concurrent TCP socket connections for maximum bandwidth utilization.";

            // 1. Admin updates Hero content
            var putReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/content/hero");
            putReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            putReq.Content = JsonContent.Create(new
            {
                Title = updatedHeadline,
                ContentJson = JsonSerializer.Serialize(new
                {
                    badge = "Production Build Live",
                    headline = updatedHeadline,
                    description = updatedDesc,
                    cta = "Download EDM for Windows"
                }),
                IsPublished = true
            });

            var putRes = await _client.SendAsync(putReq);
            putRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 2. Public website retrieves updated section
            var getRes = await _client.GetAsync("/api/v1/content/hero");
            getRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var getDoc = await getRes.Content.ReadFromJsonAsync<JsonElement>();
            getDoc.GetProperty("title").GetString().Should().Be(updatedHeadline);
        }

        [Fact]
        public async Task Dynamic_Pricing_Tier_Updates_Reflect_On_Public_Pricing_Endpoint()
        {
            string token = await GetSuperAdminTokenAsync();

            // Fetch a plan
            var plansReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/licenses/plans");
            plansReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var plansRes = await _client.SendAsync(plansReq);
            var plansList = await plansRes.Content.ReadFromJsonAsync<List<JsonElement>>();
            string planId = plansList![0].GetProperty("id").GetString()!;

            string tierName = $"Ultimate Pro {Guid.NewGuid():N}".Substring(0, 18);

            // 1. Admin creates / updates pricing tier
            var postReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/pricing");
            postReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            postReq.Content = JsonContent.Create(new
            {
                PlanId = Guid.Parse(planId),
                DisplayName = tierName,
                Currency = "USD",
                MonthlyPrice = 29.99m,
                YearlyPrice = 79.99m,
                FeaturesListJson = "[\"32 Concurrent Connections\",\"4K Stream Ripper\",\"Lifetime Updates\"]",
                IsActive = true,
                SortOrder = 3,
                BadgeText = "BEST VALUE"
            });

            var postRes = await _client.SendAsync(postReq);
            postRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 2. Public website queries pricing tiers
            var publicRes = await _client.GetAsync("/api/v1/pricing");
            publicRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var tiersList = await publicRes.Content.ReadFromJsonAsync<List<JsonElement>>();
            tiersList.Should().Contain(t => t.GetProperty("displayName").GetString() == tierName);
        }
    }
}
