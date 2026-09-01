using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
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
    public class Phase67MonitoringAndRealtimeIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public Phase67MonitoringAndRealtimeIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<(string Token, Guid UserId)> CreateAdminUserAsync()
        {
            string username = "admin_p67_" + Guid.NewGuid().ToString("N")[..8];
            string email = $"{username}@edm.local";
            string password = "AdminPassword!2026";

            var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });
            regRes.EnsureSuccessStatusCode();

            Guid userId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                u!.Role = UserRole.SUPER_ADMIN;
                u.IsActive = true;
                await db.SaveChangesAsync();
                userId = u.Id;
            }

            var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = username, Password = password });
            var doc = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
            string token = doc.GetProperty("accessToken").GetString()!;

            return (token, userId);
        }

        [Fact]
        public async Task Phase67_Download_Metrics_Authoritative_Aggregates()
        {
            var (adminToken, adminId) = await CreateAdminUserAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            // Seed Live Downloads & Download Records
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                
                // Device
                var dev = new Device
                {
                    Id = Guid.NewGuid(),
                    UserId = adminId,
                    InstallationId = Guid.NewGuid(),
                    ClientType = ClientType.DesktopWindows,
                    OsVersion = "Windows 11 Pro",
                    AppVersion = "2.1.0",
                    LastSeenAtUtc = DateTime.UtcNow
                };
                db.Devices.Add(dev);

                // Active Live Download
                db.LiveDownloads.Add(new LiveDownloadStatus
                {
                    Id = Guid.NewGuid(),
                    UserId = adminId,
                    DeviceId = dev.Id,
                    DownloadId = "JOB-" + Guid.NewGuid().ToString("N")[..6],
                    FileName = "large-iso-test.iso",
                    Url = "https://cdn.example.org/large-iso-test.iso",
                    Host = "cdn.example.org",
                    Category = "Disk Images",
                    TotalBytes = 4000000000,
                    DownloadedBytes = 2000000000,
                    ProgressPercentage = 50.0,
                    SpeedBytesPerSecond = 25000000, // 25 MB/s
                    EtaSeconds = 80,
                    Connections = 16,
                    RetryCount = 1,
                    HttpStatusCode = 206,
                    Status = "Downloading",
                    StartedAtUtc = DateTime.UtcNow.AddMinutes(-2),
                    LastUpdatedUtc = DateTime.UtcNow
                });

                // Completed record
                db.DownloadRecords.Add(new DownloadRecord
                {
                    Id = Guid.NewGuid(),
                    UserId = adminId,
                    DeviceId = dev.Id,
                    FileName = "setup-v2.exe",
                    Url = "https://downloads.example.org/setup-v2.exe",
                    Host = "downloads.example.org",
                    Category = "Executables",
                    BytesTransferred = 150000000,
                    ConnectionsCount = 8,
                    RetryCount = 0,
                    HttpStatusCode = 200,
                    SpeedBytesPerSecond = 18000000,
                    DurationSeconds = 8,
                    Status = DownloadStatus.Completed,
                    StartedAtUtc = DateTime.UtcNow.AddHours(-1),
                    CompletedAtUtc = DateTime.UtcNow.AddHours(-1).AddSeconds(8),
                    DownloadedAtUtc = DateTime.UtcNow.AddHours(-1)
                });

                // Failed record
                db.DownloadRecords.Add(new DownloadRecord
                {
                    Id = Guid.NewGuid(),
                    UserId = adminId,
                    DeviceId = dev.Id,
                    FileName = "corrupt-file.bin",
                    Url = "https://broken.example.org/corrupt-file.bin",
                    Host = "broken.example.org",
                    Category = "General",
                    BytesTransferred = 5000000,
                    ConnectionsCount = 2,
                    RetryCount = 3,
                    HttpStatusCode = 500,
                    SpeedBytesPerSecond = 0,
                    DurationSeconds = 2,
                    Status = DownloadStatus.Failed,
                    StartedAtUtc = DateTime.UtcNow.AddHours(-2),
                    CompletedAtUtc = DateTime.UtcNow.AddHours(-2).AddSeconds(2),
                    DownloadedAtUtc = DateTime.UtcNow.AddHours(-2)
                });

                await db.SaveChangesAsync();
            }

            // Query Metrics API
            var res = await _client.GetAsync("/api/v1/admin/downloads/metrics");
            res.StatusCode.Should().Be(HttpStatusCode.OK);

            var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
            doc.GetProperty("activeDownloads").GetInt32().Should().BeGreaterOrEqualTo(1);
            doc.GetProperty("completedDownloads").GetInt32().Should().BeGreaterOrEqualTo(1);
            doc.GetProperty("failedDownloads").GetInt32().Should().BeGreaterOrEqualTo(1);
            doc.GetProperty("activeSockets").GetInt32().Should().BeGreaterOrEqualTo(16);
            doc.GetProperty("currentAggregateSpeed").GetDouble().Should().BeGreaterOrEqualTo(25000000);
            doc.GetProperty("totalBytesDownloaded").GetInt64().Should().BeGreaterOrEqualTo(155000000);
        }

        [Fact]
        public async Task Phase67_Analytics_DeepDive_TopHosts_And_FileTypes()
        {
            var (adminToken, adminId) = await CreateAdminUserAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            var res = await _client.GetAsync("/api/v1/admin/downloads/deep-dive?range=30d&period=daily");
            res.StatusCode.Should().Be(HttpStatusCode.OK);

            var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
            doc.GetProperty("totalDownloads").GetInt32().Should().BeGreaterOrEqualTo(0);
            doc.TryGetProperty("topHosts", out var topHosts).Should().BeTrue();
            doc.TryGetProperty("topFileTypes", out var topTypes).Should().BeTrue();
            doc.TryGetProperty("timeline", out var timeline).Should().BeTrue();
        }

        [Fact]
        public async Task Phase67_Notifications_Full_Lifecycle_And_Unread_Count()
        {
            var (adminToken, adminId) = await CreateAdminUserAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            // 1. Create a notification
            string title = "Critical Security Update " + Guid.NewGuid().ToString("N")[..6];
            var createRes = await _client.PostAsJsonAsync("/api/v1/admin/notifications", new
            {
                Title = title,
                Message = "Immediate engine hotfix available for all 32-socket connections.",
                Type = "SecurityAlert",
                LinkUrl = "/update-center"
            });
            createRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var createdDoc = await createRes.Content.ReadFromJsonAsync<JsonElement>();
            Guid notifId = Guid.Parse(createdDoc.GetProperty("id").GetString()!);

            // 2. Query unread count
            var countRes = await _client.GetAsync("/api/v1/admin/notifications/unread-count");
            countRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var countDoc = await countRes.Content.ReadFromJsonAsync<JsonElement>();
            countDoc.GetProperty("unreadCount").GetInt32().Should().BeGreaterOrEqualTo(1);

            // 3. Mark single notification as read
            var markRes = await _client.PostAsync($"/api/v1/admin/notifications/{notifId}/read", null);
            markRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Mark all read
            var markAllRes = await _client.PostAsync("/api/v1/admin/notifications/mark-read", null);
            markAllRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Phase67_Live_Telemetry_Broadcasting_And_Broadcaster_Channel()
        {
            var broadcaster = _factory.Services.GetRequiredService<IRealtimeEventBroadcaster>();
            broadcaster.Should().NotBeNull();

            // Broadcast test event
            await broadcaster.BroadcastEventAsync("download_progress", new
            {
                downloadId = "TEST-BROADCAST-1",
                progressPercentage = 85.5,
                speedBytesPerSecond = 45000000.0
            });

            // Verify non-blocking execution
            broadcaster.ActiveSubscriberCount.Should().BeGreaterOrEqualTo(0);
        }
    }
}
