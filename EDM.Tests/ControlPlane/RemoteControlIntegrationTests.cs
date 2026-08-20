using System;
using System.Collections.Generic;
using System.IO;
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
using EDM.Services.Remote;

namespace EDM.Tests.ControlPlane
{
    public class RemoteControlIntegrationTests : IClassFixture<ControlPlaneTestFactory>, IDisposable
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;
        private readonly string _tempWorkspace;

        public RemoteControlIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
            _tempWorkspace = Path.Combine(Path.GetTempPath(), "EDM_Remote_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempWorkspace);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempWorkspace))
            {
                try { Directory.Delete(_tempWorkspace, true); } catch { /* Ignore */ }
            }
        }

        private async Task<(string Token, Guid UserId)> CreateAuthenticatedUserAsync(string role = "SUPER_ADMIN")
        {
            string username = "remote_user_" + Guid.NewGuid().ToString("N")[..8];
            string email = $"{username}@edm.local";
            string password = "StrongPassword!2026";

            var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });
            regRes.EnsureSuccessStatusCode();

            Guid userId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                if (Enum.TryParse<UserRole>(role, out var parsedRole))
                {
                    u!.Role = parsedRole;
                }
                await db.SaveChangesAsync();
                userId = u!.Id;
            }

            var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = username, Password = password });
            var doc = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
            string token = doc.GetProperty("accessToken").GetString()!;

            return (token, userId);
        }

        [Fact]
        public async Task Device_Heartbeat_And_Presence_Tracking_Works_Correctly()
        {
            var (token, userId) = await CreateAuthenticatedUserAsync();
            Guid installId = Guid.NewGuid();

            // 1. Send device heartbeat
            using var hbReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/remote/devices/heartbeat");
            hbReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            hbReq.Content = JsonContent.Create(new
            {
                InstallationId = installId,
                OsVersion = "Windows 11 x64",
                AppVersion = "2.0.0",
                ClientType = "DesktopWindows",
                Downloads = new List<object>()
            });

            var hbRes = await _client.SendAsync(hbReq);
            hbRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var hbDoc = await hbRes.Content.ReadFromJsonAsync<JsonElement>();
            hbDoc.GetProperty("success").GetBoolean().Should().BeTrue();
            Guid deviceId = hbDoc.GetProperty("deviceId").GetGuid();

            // 2. Query user devices
            using var devReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/remote/devices");
            devReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var devRes = await _client.SendAsync(devReq);
            devRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var devDoc = await devRes.Content.ReadFromJsonAsync<JsonElement>();
            var devices = devDoc.GetProperty("devices").EnumerateArray().ToList();
            devices.Should().ContainSingle(d => d.GetProperty("id").GetGuid() == deviceId);
            var registeredDev = devices.First(d => d.GetProperty("id").GetGuid() == deviceId);
            registeredDev.GetProperty("isOnline").GetBoolean().Should().BeTrue();
            registeredDev.GetProperty("status").GetString().Should().Be("Online");
        }

        [Fact]
        public async Task Live_Download_Telemetry_Reporting_And_Query()
        {
            var (token, userId) = await CreateAuthenticatedUserAsync();
            Guid installId = Guid.NewGuid();

            // Send heartbeat with active download streams
            using var hbReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/remote/devices/heartbeat");
            hbReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            hbReq.Content = JsonContent.Create(new
            {
                InstallationId = installId,
                OsVersion = "Windows 11 x64",
                AppVersion = "2.0.0",
                ClientType = "DesktopWindows",
                Downloads = new List<object>
                {
                    new
                    {
                        DownloadId = "dl_ubuntu_001",
                        FileName = "ubuntu-24.04-desktop-amd64.iso",
                        Url = "https://releases.ubuntu.com/24.04/ubuntu-24.04-desktop-amd64.iso",
                        Category = "Software",
                        TotalBytes = 5_000_000_000L,
                        DownloadedBytes = 2_500_000_000L,
                        ProgressPercentage = 50.0,
                        SpeedBytesPerSecond = 45_000_000.0,
                        EtaSeconds = 55L,
                        Status = "Downloading",
                        ErrorMessage = (string?)null
                    }
                }
            });

            var hbRes = await _client.SendAsync(hbReq);
            hbRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Query live downloads
            using var dlReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/remote/downloads");
            dlReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var dlRes = await _client.SendAsync(dlReq);
            dlRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var dlDoc = await dlRes.Content.ReadFromJsonAsync<JsonElement>();
            var downloads = dlDoc.GetProperty("downloads").EnumerateArray().ToList();
            downloads.Should().ContainSingle(d => d.GetProperty("downloadId").GetString() == "dl_ubuntu_001");
            var item = downloads.First();
            item.GetProperty("fileName").GetString().Should().Be("ubuntu-24.04-desktop-amd64.iso");
            item.GetProperty("progressPercentage").GetDouble().Should().Be(50.0);
            item.GetProperty("speedBytesPerSecond").GetDouble().Should().Be(45_000_000.0);
            item.GetProperty("status").GetString().Should().Be("Downloading");
        }

        [Fact]
        public async Task Remote_Command_Dispatch_Acknowledgement_State_Machine_And_Audit_Log()
        {
            var (token, userId) = await CreateAuthenticatedUserAsync();
            Guid installId = Guid.NewGuid();

            // 1. Register device
            using var hbReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/remote/devices/heartbeat");
            hbReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            hbReq.Content = JsonContent.Create(new
            {
                InstallationId = installId,
                OsVersion = "Windows 11 x64",
                AppVersion = "2.0.0",
                ClientType = "DesktopWindows",
                Downloads = new List<object>()
            });
            var hbRes = await _client.SendAsync(hbReq);
            var hbDoc = await hbRes.Content.ReadFromJsonAsync<JsonElement>();
            Guid deviceId = hbDoc.GetProperty("deviceId").GetGuid();

            // 2. Dispatch PauseDownload Command from Dashboard
            using var cmdReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/remote/commands");
            cmdReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            cmdReq.Content = JsonContent.Create(new
            {
                DeviceId = deviceId,
                CommandType = "PauseDownload",
                TargetDownloadId = "dl_ubuntu_001"
            });
            var cmdRes = await _client.SendAsync(cmdReq);
            cmdRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var cmdDoc = await cmdRes.Content.ReadFromJsonAsync<JsonElement>();
            Guid commandId = cmdDoc.GetProperty("command").GetProperty("id").GetGuid();
            cmdDoc.GetProperty("command").GetProperty("status").GetString().Should().Be("Pending");

            // 3. Desktop polls for pending commands
            using var pendingReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/remote/commands/pending?installationId={installId}");
            pendingReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var pendingRes = await _client.SendAsync(pendingReq);
            var pendingDoc = await pendingRes.Content.ReadFromJsonAsync<JsonElement>();
            var pendingList = pendingDoc.GetProperty("commands").EnumerateArray().ToList();
            pendingList.Should().ContainSingle(c => c.GetProperty("id").GetGuid() == commandId);

            // 4. Desktop acknowledges: Executing
            using var ack1Req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/remote/commands/{commandId}/ack");
            ack1Req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            ack1Req.Content = JsonContent.Create(new { Status = "Executing" });
            var ack1Res = await _client.SendAsync(ack1Req);
            ack1Res.StatusCode.Should().Be(HttpStatusCode.OK);

            // 5. Desktop acknowledges: Completed
            using var ack2Req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/remote/commands/{commandId}/ack");
            ack2Req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            ack2Req.Content = JsonContent.Create(new { Status = "Completed" });
            var ack2Res = await _client.SendAsync(ack2Req);
            ack2Res.StatusCode.Should().Be(HttpStatusCode.OK);

            // 6. Dashboard checks final status
            using var checkReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/remote/commands/{commandId}");
            checkReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var checkRes = await _client.SendAsync(checkReq);
            var checkDoc = await checkRes.Content.ReadFromJsonAsync<JsonElement>();
            checkDoc.GetProperty("status").GetString().Should().Be("Completed");

            // 7. Verify Audit Log entry
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.TargetId == commandId.ToString() && a.ResultStatus == "SUCCESS");
                audit.Should().NotBeNull();
                audit!.Action.Should().Be("REMOTE_COMMAND_PauseDownload_EXECUTED");
            }
        }

        [Fact]
        public async Task DesktopRemoteControlService_E2E_Command_Handling()
        {
            var (token, userId) = await CreateAuthenticatedUserAsync();

            var settings = new EDM.Tests.Services.TestSettingsService();
            settings.SetSetting("ControlPlaneApiUrl", _client.BaseAddress!.ToString());
            settings.SetSetting("EncryptedAccessToken", EDM.Services.SecureCredentialVault.EncryptSecret(token));

            var remoteService = new DesktopRemoteControlService(_client, settings);

            bool addHandled = false;
            bool pauseHandled = false;
            bool resumeHandled = false;

            remoteService.AddDownloadHandler = (url, name) =>
            {
                addHandled = true;
                return Task.FromResult(true);
            };

            remoteService.PauseDownloadHandler = (id) =>
            {
                pauseHandled = true;
                return Task.FromResult(true);
            };

            remoteService.ResumeDownloadHandler = (id) =>
            {
                resumeHandled = true;
                return Task.FromResult(true);
            };

            // 1. Send initial heartbeat
            bool hbSuccess = await remoteService.SendHeartbeatAsync();
            hbSuccess.Should().BeTrue();

            // Get device ID
            Guid installId = Guid.Parse(settings.GetSetting("InstallationIdString")!);
            using var devReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/remote/devices");
            devReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var devRes = await _client.SendAsync(devReq);
            var devDoc = await devRes.Content.ReadFromJsonAsync<JsonElement>();
            var myDev = devDoc.GetProperty("devices").EnumerateArray().First(d => d.GetProperty("installationId").GetGuid() == installId);
            Guid deviceId = myDev.GetProperty("id").GetGuid();

            // 2. Dispatch Remote AddUrl command
            using var addCmdReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/remote/commands");
            addCmdReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            addCmdReq.Content = JsonContent.Create(new
            {
                DeviceId = deviceId,
                CommandType = "AddUrl",
                Payload = new { url = "https://example.com/movie.mp4", fileName = "movie.mp4" }
            });
            var addCmdRes = await _client.SendAsync(addCmdReq);
            addCmdRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. Dispatch Remote PauseDownload command
            using var pauseCmdReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/remote/commands");
            pauseCmdReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            pauseCmdReq.Content = JsonContent.Create(new
            {
                DeviceId = deviceId,
                CommandType = "PauseDownload",
                TargetDownloadId = "dl_123"
            });
            await _client.SendAsync(pauseCmdReq);

            // 4. Run desktop polling cycle
            await remoteService.PollAndExecuteCommandsAsync();

            addHandled.Should().BeTrue();
            pauseHandled.Should().BeTrue();

            remoteService.Dispose();
        }
    }
}
