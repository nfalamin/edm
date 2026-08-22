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
    public class WebsiteAnalyticsAndTrackingIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public WebsiteAnalyticsAndTrackingIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<string> GetSuperAdminTokenAsync()
        {
            string username = $"statadm_{Guid.NewGuid():N}".Substring(0, 16);
            string email = $"{username}@edm.test";
            string password = "StatsAdminPassword!2026";

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
        public async Task Telemetry_Beacon_Ingests_Cleanly_With_Coarse_Anonymized_IP()
        {
            string sessionId = $"sess_{Guid.NewGuid():N}";

            var eventReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/analytics/event");
            eventReq.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            eventReq.Headers.Add("CF-IPCountry", "DE");
            eventReq.Content = JsonContent.Create(new
            {
                EventType = "pageview",
                SessionId = sessionId,
                PagePath = "/pricing.html",
                PageTitle = "EDM Pricing Plans",
                Referrer = "https://google.com/search?q=fastest+download+manager"
            });

            var res = await _client.SendAsync(eventReq);
            res.StatusCode.Should().Be(HttpStatusCode.OK);

            var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
            doc.GetProperty("success").GetBoolean().Should().BeTrue();

            // Verify in database that IP was masked and UserAgent/OS parsed safely
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
            var evt = await db.WebsiteEvents.FirstOrDefaultAsync(e => e.SessionId == sessionId);
            evt.Should().NotBeNull();
            evt!.OperatingSystem.Should().Be("Windows 10/11");
            evt.Browser.Should().Be("Chrome");
            evt.DeviceCategory.Should().Be("Desktop");
            evt.CountryCode.Should().Be("DE");
            evt.Referrer.Should().Be("google.com");
            evt.ClientIpCoarse.Should().NotBeNull();
            evt.ClientIpCoarse.Should().NotContain("127.0.0.1"); // Coarse masked
        }

        [Fact]
        public async Task Website_Analytics_Summary_Computes_Aggregations_And_Conversion()
        {
            string token = await GetSuperAdminTokenAsync();
            string batchSession = $"batch_{Guid.NewGuid():N}";

            // Ingest sample pageviews
            for (int i = 0; i < 5; i++)
            {
                var evtReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/analytics/event");
                evtReq.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Edg/124.0.0.0");
                evtReq.Headers.Add("CF-IPCountry", "US");
                evtReq.Content = JsonContent.Create(new
                {
                    EventType = "pageview",
                    SessionId = $"{batchSession}_{i}",
                    PagePath = i % 2 == 0 ? "/" : "/changelog.html",
                    PageTitle = "EDM Home"
                });
                var res = await _client.SendAsync(evtReq);
                res.EnsureSuccessStatusCode();
            }

            // Fetch admin website summary
            var summaryReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/analytics/website?range=7d");
            summaryReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var summaryRes = await _client.SendAsync(summaryReq);
            summaryRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var summaryDoc = await summaryRes.Content.ReadFromJsonAsync<JsonElement>();
            summaryDoc.GetProperty("totalVisitors").GetInt32().Should().BeGreaterThanOrEqualTo(5);
            summaryDoc.GetProperty("totalPageviews").GetInt32().Should().BeGreaterThanOrEqualTo(5);
            summaryDoc.GetProperty("popularPages").GetArrayLength().Should().BeGreaterThan(0);
            summaryDoc.GetProperty("countries").GetArrayLength().Should().BeGreaterThan(0);
            summaryDoc.GetProperty("operatingSystems").GetArrayLength().Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task Download_Analytics_Overview_Calculates_Metrics_And_Increments_On_Real_Download()
        {
            string token = await GetSuperAdminTokenAsync();

            // 1. Create a release with artifact
            string version = $"2.{Random.Shared.Next(100, 999)}.0";
            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            createReq.Content = JsonContent.Create(new
            {
                Platform = 0,
                Version = version,
                Title = $"EDM {version} Analytics Test Release"
            });
            var createRes = await _client.SendAsync(createReq);
            var createDoc = await createRes.Content.ReadFromJsonAsync<JsonElement>();
            string releaseId = createDoc.GetProperty("releaseId").GetString()!;

            // Upload binary
            using var formData = new MultipartFormDataContent();
            formData.Add(new ByteArrayContent(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }), "file", "EDM-Setup.exe");
            var uploadReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/releases/{releaseId}/artifacts/upload");
            uploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            uploadReq.Content = formData;
            var uploadRes = await _client.SendAsync(uploadReq);
            var uploadDoc = await uploadRes.Content.ReadFromJsonAsync<JsonElement>();
            string artifactId = uploadDoc.GetProperty("artifactId").GetString()!;

            // 2. Fetch initial download stats
            var initialReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/analytics/downloads/overview?range=30d");
            initialReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var initialRes = await _client.SendAsync(initialReq);
            var initialDoc = await initialRes.Content.ReadFromJsonAsync<JsonElement>();
            int initialTotal = initialDoc.GetProperty("totalDownloads").GetInt32();

            // 3. Trigger live streaming download
            var dlReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/releases/artifacts/{artifactId}/download");
            dlReq.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            dlReq.Headers.Add("CF-IPCountry", "GB");
            var dlRes = await _client.SendAsync(dlReq);
            dlRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Verify download stats incremented
            var updatedReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/analytics/downloads/overview?range=30d");
            updatedReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var updatedRes = await _client.SendAsync(updatedReq);
            var updatedDoc = await updatedRes.Content.ReadFromJsonAsync<JsonElement>();
            int updatedTotal = updatedDoc.GetProperty("totalDownloads").GetInt32();

            updatedTotal.Should().Be(initialTotal + 1);
            updatedDoc.GetProperty("todayDownloads").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        }
    }
}
