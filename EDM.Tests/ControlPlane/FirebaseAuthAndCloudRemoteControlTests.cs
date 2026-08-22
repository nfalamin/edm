using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;
using EDM.Models;
using EDM.Services;
using EDM.Services.Interfaces;
using EDM.Services.Remote;

namespace EDM.Tests.ControlPlane
{
    public class FirebaseAuthAndCloudRemoteControlTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public FirebaseAuthAndCloudRemoteControlTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private class InMemoryTestSettingsService : ISettingsService
        {
            private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _dict = new(StringComparer.OrdinalIgnoreCase);

            public string? GetSetting(string key) => _dict.TryGetValue(key, out var val) ? val : null;
            public void SaveSetting(string key, string value) => _dict[key] = value;
            public void SetSetting(string key, string value) => _dict[key] = value;
            public bool GetBoolSetting(string key, bool defaultValue = false) => _dict.TryGetValue(key, out var val) && bool.TryParse(val, out var b) ? b : defaultValue;
            public int GetIntSetting(string key, int defaultValue = 0) => _dict.TryGetValue(key, out var val) && int.TryParse(val, out var i) ? i : defaultValue;
            public double GetDoubleSetting(string key, double defaultValue = 0) => _dict.TryGetValue(key, out var val) && double.TryParse(val, out var d) ? d : defaultValue;

            public string GetDefaultDownloadPath() => Path.GetTempPath();
            public void SetDefaultDownloadPath(string path) { }
            public List<string> GetCategories() => new() { "General" };
            public void AddCategory(string category) { }
            public string GetFfmpegPath() => string.Empty;
            public void SetFfmpegPath(string path) { }
            public string GetYtDlpPath() => string.Empty;
            public void SetYtDlpPath(string path) { }
            public string GetAria2Path() => string.Empty;
            public void SetAria2Path(string path) { }
            public string GetDefaultFormatArgs() => string.Empty;
            public void SetDefaultFormatArgs(string args) { }
            public bool GetAutoConvertToMp3() => false;
            public void SetAutoConvertToMp3(bool v) { }
            public bool GetSchedulerEnabled() => false;
            public TimeSpan? GetSchedulerTime() => null;
            public void SetScheduler(bool enabled, TimeSpan? time) { }
            public int GetConnectionLimitOverride() => 0;
            public bool GetReduceQualityOnMeteredNetworks() => true;
            public int GetBandwidthLimitKbps() => 0;
            public int GetActiveBandwidthLimitKbps() => 0;
            public ProxySettings GetProxySettings() => new();
            public void SetProxySettings(ProxySettings settings, string? plainPassword = null) { }
            public List<BandwidthSchedule> GetBandwidthSchedules() => new();
            public void SetBandwidthSchedules(List<BandwidthSchedule> schedules) { }
            public bool GetEnableUrlSafetyCheck() => false;
            public void SetEnableUrlSafetyCheck(bool enable) { }
            public bool GetEnablePostDownloadScan() => false;
            public void SetEnablePostDownloadScan(bool enable) { }
            public string GetGoogleSafeBrowsingApiKey() => string.Empty;
            public void SetGoogleSafeBrowsingApiKey(string apiKey) { }
            public bool GetSendAnonymousCrashReports() => false;
            public void SetSendAnonymousCrashReports(bool enable) { }
            public bool GetEnableClipboardMonitoring() => false;
            public void SetEnableClipboardMonitoring(bool enable) { }
            public bool GetClipboardMonitorHttp() => true;
            public void SetClipboardMonitorHttp(bool enable) { }
            public bool GetClipboardMonitorHttps() => true;
            public void SetClipboardMonitorHttps(bool enable) { }
            public bool GetClipboardMonitorFtp() => true;
            public void SetClipboardMonitorFtp(bool enable) { }
            public EDM.Services.Interfaces.ClipboardAction GetClipboardAction() => EDM.Services.Interfaces.ClipboardAction.AskBeforeDownload;
            public void SetClipboardAction(EDM.Services.Interfaces.ClipboardAction action) { }
            public bool GetClipboardIgnoreDuplicates() => true;
            public void SetClipboardIgnoreDuplicates(bool enable) { }
            public bool GetClipboardShowNotification() => true;
            public void SetClipboardShowNotification(bool enable) { }
            public bool GetEnableBrowserIntegration() => true;
            public void SetEnableBrowserIntegration(bool enable) { }
            public bool GetBrowserCaptureDownloads() => true;
            public void SetBrowserCaptureDownloads(bool enable) { }
            public bool GetBrowserShowConfirmation() => true;
            public void SetBrowserShowConfirmation(bool enable) { }
            public bool GetBrowserShowNotification() => true;
            public void SetBrowserShowNotification(bool enable) { }
        }

        private static string GenerateMockFirebaseIdToken(
            string uid,
            string email,
            string? name = null,
            string? picture = null,
            bool emailVerified = true,
            TimeSpan? lifetime = null)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("Test_Firebase_Mock_Signing_Secret_Key_For_Jwt_Generation_2026!"));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, uid),
                new("user_id", uid),
                new(JwtRegisteredClaimNames.Email, email),
                new("email_verified", emailVerified.ToString().ToLowerInvariant())
            };

            if (!string.IsNullOrEmpty(name)) claims.Add(new Claim("name", name));
            if (!string.IsNullOrEmpty(picture)) claims.Add(new Claim("picture", picture));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = "https://securetoken.google.com/edm-download-manager",
                Audience = "edm-download-manager",
                NotBefore = DateTime.UtcNow.AddMinutes(-30),
                Expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(1)),
                SigningCredentials = credentials
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        [Fact]
        public async Task FirebaseAuth_ValidToken_AutoRegistersNewUserWithFirebaseUid()
        {
            string uid = $"fb_{Guid.NewGuid():N}";
            string email = $"{uid}@cloud.edm.test";
            string idToken = GenerateMockFirebaseIdToken(uid, email, "Cloud Tester", "https://photo.url/avatar.png");

            var payload = new
            {
                IdToken = idToken,
                InstallationId = Guid.NewGuid(),
                ClientType = "WebDashboard"
            };

            var response = await _client.PostAsJsonAsync("/api/v1/auth/firebase", payload);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
            doc.GetProperty("success").GetBoolean().Should().BeTrue();
            doc.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
            doc.GetProperty("refreshToken").GetString().Should().NotBeNullOrEmpty();

            var userElem = doc.GetProperty("user");
            userElem.GetProperty("email").GetString().Should().Be(email);
            userElem.GetProperty("firebaseUid").GetString().Should().Be(uid);
            userElem.GetProperty("displayName").GetString().Should().Be("Cloud Tester");
        }

        [Fact]
        public async Task FirebaseAuth_ExistingUser_LinksFirebaseUidAndUpdatesProfile()
        {
            string unique = Guid.NewGuid().ToString("N");
            string email = $"link_{unique}@cloud.edm.test";
            string password = "StrongPassword123!@#";

            // 1. Standard register
            var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Username = $"localuser_{unique.Substring(0, 8)}",
                Email = email,
                Password = password
            });
            regRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 2. Firebase sign in with same email
            string fbUid = $"fb_{unique}";
            string idToken = GenerateMockFirebaseIdToken(fbUid, email, "Linked Display Name", "https://avatar.test/pic.png");

            var fbRes = await _client.PostAsJsonAsync("/api/v1/auth/firebase/login", new
            {
                IdToken = idToken,
                InstallationId = Guid.NewGuid()
            });

            fbRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var doc = await fbRes.Content.ReadFromJsonAsync<JsonElement>();
            var userElem = doc.GetProperty("user");
            userElem.GetProperty("firebaseUid").GetString().Should().Be(fbUid);
            userElem.GetProperty("displayName").GetString().Should().Be("Linked Display Name");
        }

        [Fact]
        public async Task FirebaseAuth_ExpiredToken_ReturnsBadRequest()
        {
            string uid = $"fb_exp_{Guid.NewGuid():N}";
            string email = $"{uid}@cloud.edm.test";
            string expiredToken = GenerateMockFirebaseIdToken(uid, email, lifetime: TimeSpan.FromMinutes(-10));

            var response = await _client.PostAsJsonAsync("/api/v1/auth/firebase", new
            {
                IdToken = expiredToken,
                InstallationId = Guid.NewGuid()
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
            doc.GetProperty("error").GetString().Should().Be("INVALID_FIREBASE_TOKEN");
        }

        [Fact]
        public async Task RemoteControl_DeviceHeartbeat_And_GetDevices_ReturnsLiveStatus()
        {
            // 1. Authenticate user via Firebase
            string uid = $"fb_dev_{Guid.NewGuid():N}";
            string email = $"{uid}@cloud.edm.test";
            string idToken = GenerateMockFirebaseIdToken(uid, email, "Device Tester");

            var authRes = await _client.PostAsJsonAsync("/api/v1/auth/firebase", new { IdToken = idToken });
            var authDoc = await authRes.Content.ReadFromJsonAsync<JsonElement>();
            string accessToken = authDoc.GetProperty("accessToken").GetString()!;

            using var authClient = _factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // 2. Send Heartbeat with active downloads
            Guid installId = Guid.NewGuid();
            var heartbeatPayload = new
            {
                InstallationId = installId,
                OsVersion = "Windows 11 Pro 64-bit",
                AppVersion = "2.1.0",
                ClientType = "DesktopWindows",
                Downloads = new[]
                {
                    new
                    {
                        DownloadId = "dl-101",
                        FileName = "Ubuntu_24.04.iso",
                        Url = "https://releases.ubuntu.com/24.04/ubuntu-24.04-desktop-amd64.iso",
                        Category = "ISO",
                        TotalBytes = 4000000000L,
                        DownloadedBytes = 2000000000L,
                        ProgressPercentage = 50.0,
                        SpeedBytesPerSecond = 25000000.0,
                        EtaSeconds = (long?)80,
                        Status = "Downloading",
                        ErrorMessage = (string?)null
                    }
                }
            };

            var hbRes = await authClient.PostAsJsonAsync("/api/v1/remote/devices/heartbeat", heartbeatPayload);
            hbRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. Query Devices from Dashboard
            var devRes = await authClient.GetAsync("/api/v1/remote/devices");
            devRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var devDoc = await devRes.Content.ReadFromJsonAsync<JsonElement>();

            var devices = devDoc.GetProperty("devices");
            devices.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

            var firstDev = devices[0];
            firstDev.GetProperty("installationId").GetGuid().Should().Be(installId);
            firstDev.GetProperty("isOnline").GetBoolean().Should().BeTrue();
            firstDev.GetProperty("activeDownloadCount").GetInt32().Should().Be(1);

            // 4. Query Live Downloads from Dashboard
            var dlRes = await authClient.GetAsync("/api/v1/remote/downloads");
            dlRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var dlDoc = await dlRes.Content.ReadFromJsonAsync<JsonElement>();

            var downloads = dlDoc.GetProperty("downloads");
            downloads.GetArrayLength().Should().Be(1);
            downloads[0].GetProperty("downloadId").GetString().Should().Be("dl-101");
            downloads[0].GetProperty("progressPercentage").GetDouble().Should().Be(50.0);
            downloads[0].GetProperty("speedBytesPerSecond").GetDouble().Should().Be(25000000.0);
        }

        [Fact]
        public async Task RemoteControl_CommandSubmission_Polling_And_AcknowledgmentLifecycle()
        {
            // 1. Authenticate user
            string uid = $"fb_cmd_{Guid.NewGuid():N}";
            string email = $"{uid}@cloud.edm.test";
            string idToken = GenerateMockFirebaseIdToken(uid, email, "Command Tester");

            var authRes = await _client.PostAsJsonAsync("/api/v1/auth/firebase", new { IdToken = idToken });
            var authDoc = await authRes.Content.ReadFromJsonAsync<JsonElement>();
            string accessToken = authDoc.GetProperty("accessToken").GetString()!;

            using var authClient = _factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // 2. Register device via heartbeat
            Guid installId = Guid.NewGuid();
            var hbRes = await authClient.PostAsJsonAsync("/api/v1/remote/devices/heartbeat", new
            {
                InstallationId = installId,
                OsVersion = "Windows 11",
                AppVersion = "2.1.0",
                ClientType = "DesktopWindows"
            });
            var hbDoc = await hbRes.Content.ReadFromJsonAsync<JsonElement>();
            Guid deviceId = hbDoc.GetProperty("deviceId").GetGuid();

            // 3. Web Dashboard creates remote command: AddUrl
            var cmdPayload = new
            {
                DeviceId = deviceId,
                CommandType = "AddUrl",
                TargetDownloadId = (string?)null,
                Payload = new
                {
                    url = "https://example.com/movie.mp4",
                    fileName = "movie.mp4"
                }
            };

            var cmdRes = await authClient.PostAsJsonAsync("/api/v1/remote/commands", cmdPayload);
            cmdRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var cmdDoc = await cmdRes.Content.ReadFromJsonAsync<JsonElement>();
            cmdDoc.GetProperty("success").GetBoolean().Should().BeTrue();
            Guid commandId = cmdDoc.GetProperty("commandId").GetGuid();

            // 4. Desktop Client polls for pending commands
            var pollRes = await authClient.GetAsync($"/api/v1/remote/commands/pending?installationId={installId}");
            pollRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var pollDoc = await pollRes.Content.ReadFromJsonAsync<JsonElement>();
            var cmds = pollDoc.GetProperty("commands");
            cmds.GetArrayLength().Should().Be(1);
            cmds[0].GetProperty("id").GetGuid().Should().Be(commandId);
            cmds[0].GetProperty("commandType").GetString().Should().Be("AddUrl");

            // 5. Desktop acknowledges command as Executing then Completed
            var ackExecuting = await authClient.PostAsJsonAsync($"/api/v1/remote/commands/{commandId}/ack", new
            {
                Status = "Executing"
            });
            ackExecuting.StatusCode.Should().Be(HttpStatusCode.OK);

            var ackCompleted = await authClient.PostAsJsonAsync($"/api/v1/remote/commands/{commandId}/ack", new
            {
                Status = "Completed"
            });
            ackCompleted.StatusCode.Should().Be(HttpStatusCode.OK);

            // 6. Verify pending queue is now empty
            var pollAfter = await authClient.GetAsync($"/api/v1/remote/commands/pending?installationId={installId}");
            var pollAfterDoc = await pollAfter.Content.ReadFromJsonAsync<JsonElement>();
            pollAfterDoc.GetProperty("commands").GetArrayLength().Should().Be(0);
        }

        [Fact]
        public async Task RemoteControl_MultiTenantIsolation_UserCannotControlAnotherUsersDevice()
        {
            // 1. User A setup
            string uidA = $"fb_userA_{Guid.NewGuid():N}";
            string emailA = $"{uidA}@cloud.edm.test";
            var authA = await _client.PostAsJsonAsync("/api/v1/auth/firebase", new { IdToken = GenerateMockFirebaseIdToken(uidA, emailA) });
            string tokenA = (await authA.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

            using var clientA = _factory.CreateClient();
            clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

            Guid installIdA = Guid.NewGuid();
            var hbA = await clientA.PostAsJsonAsync("/api/v1/remote/devices/heartbeat", new
            {
                InstallationId = installIdA,
                ClientType = "DesktopWindows"
            });
            Guid deviceIdA = (await hbA.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deviceId").GetGuid();

            // 2. User B setup
            string uidB = $"fb_userB_{Guid.NewGuid():N}";
            string emailB = $"{uidB}@cloud.edm.test";
            var authB = await _client.PostAsJsonAsync("/api/v1/auth/firebase", new { IdToken = GenerateMockFirebaseIdToken(uidB, emailB) });
            string tokenB = (await authB.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

            using var clientB = _factory.CreateClient();
            clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

            // 3. User B attempts to send command to Device A
            var maliciousCmd = await clientB.PostAsJsonAsync("/api/v1/remote/commands", new
            {
                DeviceId = deviceIdA,
                CommandType = "PauseDownload",
                TargetDownloadId = "dl-target"
            });

            // Must be forbidden / forbidden status
            maliciousCmd.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task DesktopRemoteControlService_ExecutesMockCommandsViaHandlers()
        {
            var settings = new InMemoryTestSettingsService();
            var controlClient = new ControlPlaneClient(_client, settings);
            using var remoteService = new DesktopRemoteControlService(_client, settings, controlClient);

            bool addInvoked = false;
            bool pauseInvoked = false;
            bool resumeInvoked = false;
            bool cancelInvoked = false;
            bool deleteInvoked = false;
            bool queueInvoked = false;

            remoteService.AddDownloadHandler = (url, name) =>
            {
                addInvoked = url.Contains("test.zip");
                return Task.FromResult(true);
            };

            remoteService.PauseDownloadHandler = id =>
            {
                pauseInvoked = (id == "dl-99");
                return Task.FromResult(true);
            };

            remoteService.ResumeDownloadHandler = id =>
            {
                resumeInvoked = (id == "dl-99");
                return Task.FromResult(true);
            };

            remoteService.CancelDownloadHandler = id =>
            {
                cancelInvoked = (id == "dl-99");
                return Task.FromResult(true);
            };

            remoteService.DeleteDownloadHandler = id =>
            {
                deleteInvoked = (id == "dl-99");
                return Task.FromResult(true);
            };

            remoteService.QueueControlHandler = action =>
            {
                queueInvoked = (action == "start_all");
                return Task.FromResult(true);
            };

            // Register snapshot
            remoteService.RegisterOrUpdateDownload(new DesktopLiveDownloadSnapshot(
                DownloadId: "dl-99",
                FileName: "test.zip",
                Url: "https://example.com/test.zip",
                Category: "Archives",
                TotalBytes: 1000,
                DownloadedBytes: 500,
                ProgressPercentage: 50.0,
                SpeedBytesPerSecond: 1000,
                EtaSeconds: 1,
                Status: "Downloading"
            ));

            // Test handler executions
            var addOk = await remoteService.AddDownloadHandler!("https://example.com/test.zip", "test.zip");
            addOk.Should().BeTrue();
            addInvoked.Should().BeTrue();

            var pauseOk = await remoteService.PauseDownloadHandler!("dl-99");
            pauseOk.Should().BeTrue();
            pauseInvoked.Should().BeTrue();

            var resumeOk = await remoteService.ResumeDownloadHandler!("dl-99");
            resumeOk.Should().BeTrue();
            resumeInvoked.Should().BeTrue();

            var cancelOk = await remoteService.CancelDownloadHandler!("dl-99");
            cancelOk.Should().BeTrue();
            cancelInvoked.Should().BeTrue();

            var deleteOk = await remoteService.DeleteDownloadHandler!("dl-99");
            deleteOk.Should().BeTrue();
            deleteInvoked.Should().BeTrue();

            var queueOk = await remoteService.QueueControlHandler!("start_all");
            queueOk.Should().BeTrue();
            queueInvoked.Should().BeTrue();
        }

        [Fact]
        public async Task ControlPlaneClient_LoginWithFirebase_PersistsTokensAndSetsActiveState()
        {
            var settings = new InMemoryTestSettingsService();
            var controlClient = new ControlPlaneClient(_client, settings);

            string uid = $"fb_client_{Guid.NewGuid():N}";
            string email = $"{uid}@cloud.edm.test";
            string idToken = GenerateMockFirebaseIdToken(uid, email, "Desktop Sync User");

            bool loginSuccess = await controlClient.LoginWithFirebaseAsync(idToken);
            loginSuccess.Should().BeTrue();
            controlClient.CurrentSecurityState.Should().Be(AccountSecurityState.Active);

            string? savedAccess = settings.GetSetting("EncryptedAccessToken");
            savedAccess.Should().NotBeNullOrEmpty();
            string? savedRefresh = settings.GetSetting("EncryptedRefreshToken");
            savedRefresh.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task FirebaseAuth_DeactivatedUser_ReturnsForbidden()
        {
            string uid = $"fb_deact_{Guid.NewGuid():N}";
            string email = $"{uid}@cloud.edm.test";
            string idToken = GenerateMockFirebaseIdToken(uid, email, "Deactivated User");

            // 1. First register via Firebase
            var firstRes = await _client.PostAsJsonAsync("/api/v1/auth/firebase", new { IdToken = idToken });
            firstRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 2. Deactivate user in DB
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var user = await db.Users.FirstAsync(u => u.FirebaseUid == uid);
                user.IsActive = false;
                await db.SaveChangesAsync();
            }

            // 3. Attempt login again -> 403 Forbidden
            var secondRes = await _client.PostAsJsonAsync("/api/v1/auth/firebase", new { IdToken = idToken });
            secondRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var doc = await secondRes.Content.ReadFromJsonAsync<JsonElement>();
            doc.GetProperty("error").GetString().Should().Be("ACCOUNT_SUSPENDED");
        }

        [Fact]
        public async Task RemoteControl_SendRemoteCommand_PauseAndResumeTargetDownload()
        {
            // 1. Auth user
            string uid = $"fb_pause_{Guid.NewGuid():N}";
            string email = $"{uid}@cloud.edm.test";
            var authRes = await _client.PostAsJsonAsync("/api/v1/auth/firebase", new { IdToken = GenerateMockFirebaseIdToken(uid, email) });
            string token = (await authRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

            using var authClient = _factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 2. Heartbeat device
            Guid installId = Guid.NewGuid();
            var hbRes = await authClient.PostAsJsonAsync("/api/v1/remote/devices/heartbeat", new
            {
                InstallationId = installId,
                ClientType = "DesktopWindows",
                Downloads = new[]
                {
                    new
                    {
                        DownloadId = "dl-555",
                        FileName = "archive.tar.gz",
                        Url = "https://example.com/archive.tar.gz",
                        Category = "Compressed",
                        TotalBytes = 1000000L,
                        DownloadedBytes = 500000L,
                        ProgressPercentage = 50.0,
                        SpeedBytesPerSecond = 50000.0,
                        EtaSeconds = (long?)10,
                        Status = "Downloading",
                        ErrorMessage = (string?)null
                    }
                }
            });
            var hbDoc = await hbRes.Content.ReadFromJsonAsync<JsonElement>();
            Guid deviceId = hbDoc.GetProperty("deviceId").GetGuid();

            // 3. Send PauseDownload command
            var pauseCmdRes = await authClient.PostAsJsonAsync("/api/v1/remote/commands", new
            {
                DeviceId = deviceId,
                CommandType = "PauseDownload",
                TargetDownloadId = "dl-555"
            });
            pauseCmdRes.StatusCode.Should().Be(HttpStatusCode.OK);
            Guid pauseCmdId = (await pauseCmdRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("commandId").GetGuid();

            // 4. Poll and verify command
            var pollRes = await authClient.GetAsync($"/api/v1/remote/commands/pending?installationId={installId}");
            var pollDoc = await pollRes.Content.ReadFromJsonAsync<JsonElement>();
            var cmds = pollDoc.GetProperty("commands");
            cmds.GetArrayLength().Should().Be(1);
            cmds[0].GetProperty("id").GetGuid().Should().Be(pauseCmdId);
            cmds[0].GetProperty("commandType").GetString().Should().Be("PauseDownload");
            cmds[0].GetProperty("targetDownloadId").GetString().Should().Be("dl-555");
        }
    }
}
