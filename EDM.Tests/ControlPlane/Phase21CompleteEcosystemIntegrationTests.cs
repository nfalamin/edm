using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
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
    public class Phase21CompleteEcosystemIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public Phase21CompleteEcosystemIntegrationTests(ControlPlaneTestFactory factory)
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

        private static string GenerateMockFirebaseIdToken(string uid, string email, string? name = null)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("Test_Firebase_Mock_Signing_Secret_Key_For_Jwt_Generation_2026!"));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, uid),
                new("user_id", uid),
                new(JwtRegisteredClaimNames.Email, email),
                new("email_verified", "true")
            };

            if (!string.IsNullOrEmpty(name)) claims.Add(new Claim("name", name));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = "https://securetoken.google.com/edm-download-manager",
                Audience = "edm-download-manager",
                NotBefore = DateTime.UtcNow.AddMinutes(-30),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = credentials
            };

            return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
        }

        private async Task<string> CreateAuthenticatedUserTokenAsync(string uid, string email, string name = "Fleet User")
        {
            string idToken = GenerateMockFirebaseIdToken(uid, email, name);
            var res = await _client.PostAsJsonAsync("/api/v1/auth/firebase", new { IdToken = idToken });
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
            return doc.GetProperty("accessToken").GetString()!;
        }

        [Fact]
        public async Task DeviceStorageManagement_ReportsLocalDrives_ReturnsFreeSpaceAndVolumeLabels()
        {
            string uid = $"fb_storage_{Guid.NewGuid():N}";
            string email = $"{uid}@fleet.edm.test";
            string token = await CreateAuthenticatedUserTokenAsync(uid, email, "Storage Tester");

            using var authClient = _factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            Guid installId = Guid.NewGuid();
            var heartbeat = new
            {
                InstallationId = installId,
                OsVersion = "Windows 11 Enterprise",
                AppVersion = "2.1.0",
                ClientType = "DesktopWindows",
                StorageDrives = new[]
                {
                    new
                    {
                        DriveName = "C:\\",
                        VolumeLabel = "System OS",
                        DriveFormat = "NTFS",
                        TotalSizeBytes = 512000000000L,
                        FreeSpaceBytes = 250000000000L,
                        AvailableFreeSpaceBytes = 245000000000L
                    },
                    new
                    {
                        DriveName = "D:\\",
                        VolumeLabel = "HighSpeed NVMe Downloads",
                        DriveFormat = "NTFS",
                        TotalSizeBytes = 2000000000000L,
                        FreeSpaceBytes = 1500000000000L,
                        AvailableFreeSpaceBytes = 1500000000000L
                    }
                }
            };

            var hbRes = await authClient.PostAsJsonAsync("/api/v1/remote/devices/heartbeat", heartbeat);
            hbRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var hbDoc = await hbRes.Content.ReadFromJsonAsync<JsonElement>();
            Guid deviceId = hbDoc.GetProperty("deviceId").GetGuid();

            // Query device storage
            var storageRes = await authClient.GetAsync($"/api/v1/remote/devices/{deviceId}/storage");
            storageRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var storageDoc = await storageRes.Content.ReadFromJsonAsync<JsonElement>();

            storageDoc.GetProperty("isOnline").GetBoolean().Should().BeTrue();
            var drives = storageDoc.GetProperty("drives");
            drives.GetArrayLength().Should().Be(2);

            var cDrive = drives[0];
            cDrive.GetProperty("driveName").GetString().Should().Be("C:\\");
            cDrive.GetProperty("volumeLabel").GetString().Should().Be("System OS");
            cDrive.GetProperty("totalSizeBytes").GetInt64().Should().Be(512000000000L);

            var dDrive = drives[1];
            dDrive.GetProperty("driveName").GetString().Should().Be("D:\\");
            dDrive.GetProperty("volumeLabel").GetString().Should().Be("HighSpeed NVMe Downloads");
            dDrive.GetProperty("freeSpaceBytes").GetInt64().Should().Be(1500000000000L);
        }

        [Fact]
        public async Task CloudDownloadHistory_SyncsDesktopRecords_PersistsAndAllowsSearchAndFiltering()
        {
            string uid = $"fb_history_{Guid.NewGuid():N}";
            string email = $"{uid}@fleet.edm.test";
            string token = await CreateAuthenticatedUserTokenAsync(uid, email, "History Tester");

            using var authClient = _factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            Guid installId = Guid.NewGuid();
            // Register device first
            await authClient.PostAsJsonAsync("/api/v1/remote/devices/heartbeat", new
            {
                InstallationId = installId,
                ClientType = "DesktopWindows"
            });

            // Sync 3 history records
            var syncPayload = new
            {
                InstallationId = installId,
                Records = new[]
                {
                    new
                    {
                        Url = "https://releases.ubuntu.com/noble/ubuntu-24.04-desktop-amd64.iso",
                        FileName = "ubuntu-24.04-desktop-amd64.iso",
                        Category = "OperatingSystem",
                        FileSizeBytes = 5700000000L,
                        Status = "Completed",
                        CompletedAtUtc = DateTime.UtcNow.AddMinutes(-30),
                        Sha256Hash = "a1b2c3d4e5f607182930415263748596a1b2c3d4e5f607182930415263748596"
                    },
                    new
                    {
                        Url = "https://download.visualstudio.microsoft.com/installer.exe",
                        FileName = "VisualStudioSetup.exe",
                        Category = "Software",
                        FileSizeBytes = 4000000L,
                        Status = "Completed",
                        CompletedAtUtc = DateTime.UtcNow.AddMinutes(-15),
                        Sha256Hash = (string?)null
                    },
                    new
                    {
                        Url = "https://example.com/failed_video.mp4",
                        FileName = "failed_video.mp4",
                        Category = "Video",
                        FileSizeBytes = 120000000L,
                        Status = "Failed",
                        CompletedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                        Sha256Hash = (string?)null
                    }
                }
            };

            var syncRes = await authClient.PostAsJsonAsync("/api/v1/remote/history/sync", syncPayload);
            syncRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var syncDoc = await syncRes.Content.ReadFromJsonAsync<JsonElement>();
            syncDoc.GetProperty("syncedCount").GetInt32().Should().Be(3);

            // 1. Query all history
            var allHist = await authClient.GetAsync("/api/v1/remote/history");
            allHist.StatusCode.Should().Be(HttpStatusCode.OK);
            var allDoc = await allHist.Content.ReadFromJsonAsync<JsonElement>();
            allDoc.GetProperty("total").GetInt32().Should().Be(3);

            // 2. Query with search filter
            var searchHist = await authClient.GetAsync("/api/v1/remote/history?search=ubuntu");
            searchHist.StatusCode.Should().Be(HttpStatusCode.OK);
            var searchDoc = await searchHist.Content.ReadFromJsonAsync<JsonElement>();
            var results = searchDoc.GetProperty("history");
            results.GetArrayLength().Should().Be(1);
            results[0].GetProperty("fileName").GetString().Should().Be("ubuntu-24.04-desktop-amd64.iso");

            // 3. Query with category filter
            var catHist = await authClient.GetAsync("/api/v1/remote/history?category=Video");
            catHist.StatusCode.Should().Be(HttpStatusCode.OK);
            var catDoc = await catHist.Content.ReadFromJsonAsync<JsonElement>();
            var catResults = catDoc.GetProperty("history");
            catResults.GetArrayLength().Should().Be(1);
            catResults[0].GetProperty("status").GetString().Should().Be("Failed");
        }

        [Fact]
        public async Task MultiPcFleetManagement_RoutesCommandsToTargetedDevicesIndependently()
        {
            string uid = $"fb_fleet_{Guid.NewGuid():N}";
            string email = $"{uid}@fleet.edm.test";
            string token = await CreateAuthenticatedUserTokenAsync(uid, email, "Fleet Controller");

            using var authClient = _factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 1. Register PC-1 (Office Workstation)
            Guid installId1 = Guid.NewGuid();
            var dev1Res = await authClient.PostAsJsonAsync("/api/v1/remote/devices/heartbeat", new
            {
                InstallationId = installId1,
                OsVersion = "Windows 11 Workstation",
                ClientType = "DesktopWindows"
            });
            Guid deviceId1 = (await dev1Res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deviceId").GetGuid();

            // 2. Register PC-2 (Home Laptop)
            Guid installId2 = Guid.NewGuid();
            var dev2Res = await authClient.PostAsJsonAsync("/api/v1/remote/devices/heartbeat", new
            {
                InstallationId = installId2,
                OsVersion = "Windows 11 Laptop",
                ClientType = "DesktopWindows"
            });
            Guid deviceId2 = (await dev2Res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deviceId").GetGuid();

            // 3. Send command to Laptop only (AddUrl)
            var cmdLaptopRes = await authClient.PostAsJsonAsync("/api/v1/remote/commands", new
            {
                DeviceId = deviceId2,
                CommandType = "AddUrl",
                Payload = new { url = "https://example.com/laptop_driver.zip" }
            });
            cmdLaptopRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Send command to Workstation only (QueueControl)
            var cmdWorkstationRes = await authClient.PostAsJsonAsync("/api/v1/remote/commands", new
            {
                DeviceId = deviceId1,
                CommandType = "QueueControl",
                Payload = new { action = "pause_all" }
            });
            cmdWorkstationRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 5. Verify Laptop receives only its command
            var pollLaptop = await authClient.GetAsync($"/api/v1/remote/commands/pending?installationId={installId2}");
            var docLaptop = await pollLaptop.Content.ReadFromJsonAsync<JsonElement>();
            var cmdsLaptop = docLaptop.GetProperty("commands");
            cmdsLaptop.GetArrayLength().Should().Be(1);
            cmdsLaptop[0].GetProperty("commandType").GetString().Should().Be("AddUrl");

            // 6. Verify Workstation receives only its command
            var pollWorkstation = await authClient.GetAsync($"/api/v1/remote/commands/pending?installationId={installId1}");
            var docWorkstation = await pollWorkstation.Content.ReadFromJsonAsync<JsonElement>();
            var cmdsWorkstation = docWorkstation.GetProperty("commands");
            cmdsWorkstation.GetArrayLength().Should().Be(1);
            cmdsWorkstation[0].GetProperty("commandType").GetString().Should().Be("QueueControl");
        }

        [Fact]
        public void DesktopRemoteControlService_GetLocalDrives_ReturnsSystemDrives()
        {
            var drives = DesktopRemoteControlService.GetLocalDrives();
            drives.Should().NotBeNull();
            drives.Count.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task DesktopRemoteControlService_OpenFileAndOpenFolder_ExecutesHandlers()
        {
            var settings = new SettingsService();
            var controlClient = new ControlPlaneClient(_client, settings);
            using var remoteService = new DesktopRemoteControlService(_client, settings, controlClient);

            bool openFileCalled = false;
            bool openFolderCalled = false;

            remoteService.OpenFileHandler = id =>
            {
                openFileCalled = (id == "dl-file-123");
                return Task.FromResult(true);
            };

            remoteService.OpenFolderHandler = id =>
            {
                openFolderCalled = (id == "dl-folder-123");
                return Task.FromResult(true);
            };

            var fOk = await remoteService.OpenFileHandler!("dl-file-123");
            fOk.Should().BeTrue();
            openFileCalled.Should().BeTrue();

            var foldOk = await remoteService.OpenFolderHandler!("dl-folder-123");
            foldOk.Should().BeTrue();
            openFolderCalled.Should().BeTrue();
        }

        [Fact]
        public async Task WebFileExplorer_DirectoryStructureAndSubfolders_NavigatesHierarchically()
        {
            string uid = $"fb_explorer_{Guid.NewGuid():N}";
            string email = $"{uid}@fleet.edm.test";
            string token = await CreateAuthenticatedUserTokenAsync(uid, email, "Explorer User");

            using var authClient = _factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Register 2 files in subdirectories
            var f1 = await authClient.PostAsJsonAsync("/api/v1/storage/files/register-metadata", new
            {
                FileName = "quarterly_report.pdf",
                RelativePath = "Documents/Work/quarterly_report.pdf",
                Category = "Documents",
                FileSizeBytes = 1024000L,
                Sha256Hash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
                Version = 1
            });
            f1.IsSuccessStatusCode.Should().BeTrue();

            var f2 = await authClient.PostAsJsonAsync("/api/v1/storage/files/register-metadata", new
            {
                FileName = "vacation.png",
                RelativePath = "Images/vacation.png",
                Category = "Images",
                FileSizeBytes = 2048000L,
                Sha256Hash = "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef",
                Version = 1
            });
            f2.IsSuccessStatusCode.Should().BeTrue();

            // 1. Query root folder
            var rootRes = await authClient.GetAsync("/api/v1/storage/files?folder=");
            rootRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var rootDoc = await rootRes.Content.ReadFromJsonAsync<JsonElement>();
            var rootSubs = rootDoc.GetProperty("subFolders");
            var subsList = new List<string>();
            foreach (var s in rootSubs.EnumerateArray()) subsList.Add(s.GetString()!);
            subsList.Should().Contain("Documents");
            subsList.Should().Contain("Images");

            // 2. Query Documents folder
            var docRes = await authClient.GetAsync("/api/v1/storage/files?folder=Documents");
            docRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var docDoc = await docRes.Content.ReadFromJsonAsync<JsonElement>();
            docDoc.GetProperty("currentFolder").GetString().Should().Be("Documents");
            var docSubs = docDoc.GetProperty("subFolders");
            var docSubsList = new List<string>();
            foreach (var s in docSubs.EnumerateArray()) docSubsList.Add(s.GetString()!);
            docSubsList.Should().Contain("Work");
        }

        [Fact]
        public async Task DesktopRemoteControlService_SyncCompletedDownloadHistory_CallsCloudEndpointSuccessfully()
        {
            string uid = $"fb_sync_{Guid.NewGuid():N}";
            string email = $"{uid}@fleet.edm.test";
            string token = await CreateAuthenticatedUserTokenAsync(uid, email, "Sync Tester");

            var settings = new InMemoryTestSettingsService();
            var controlClient = new ControlPlaneClient(_client, settings);
            await controlClient.LoginWithFirebaseAsync(GenerateMockFirebaseIdToken(uid, email));

            using var remoteService = new DesktopRemoteControlService(_client, settings, controlClient);

            bool synced = await remoteService.SyncCompletedDownloadHistoryAsync(
                url: "https://example.com/driver.zip",
                fileName: "driver.zip",
                category: "Compressed",
                fileSizeBytes: 85000000L,
                status: "Completed",
                sha256Hash: "abcdef123456"
            );

            synced.Should().BeTrue();

            // Verify in history
            using var authClient = _factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var histRes = await authClient.GetAsync("/api/v1/remote/history?search=driver.zip");
            histRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var histDoc = await histRes.Content.ReadFromJsonAsync<JsonElement>();
            var list = histDoc.GetProperty("history");
            list.GetArrayLength().Should().Be(1);
            list[0].GetProperty("fileName").GetString().Should().Be("driver.zip");
        }
    }
}
