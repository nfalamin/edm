using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;
using EDM.Services.Interfaces;
using EDM.Services.Storage;

namespace EDM.Tests.ControlPlane
{
    public class CloudAndLocalHddStorageIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public CloudAndLocalHddStorageIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private class TestSettingsService : ISettingsService
        {
            private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _dict = new(StringComparer.OrdinalIgnoreCase);
            public string? GetSetting(string key) => _dict.TryGetValue(key, out var val) ? val : null;
            public void SaveSetting(string key, string value) => _dict[key] = value;
            public void SetSetting(string key, string value) => _dict[key] = value;
            public bool GetBoolSetting(string key, bool defaultValue = false) => _dict.TryGetValue(key, out var val) && bool.TryParse(val, out var b) ? b : defaultValue;
            public int GetIntSetting(string key, int defaultValue = 0) => _dict.TryGetValue(key, out var val) && int.TryParse(val, out var i) ? i : defaultValue;
            public double GetDoubleSetting(string key, double defaultValue = 0) => _dict.TryGetValue(key, out var val) && double.TryParse(val, out var d) ? d : defaultValue;
            public string GetDefaultDownloadPath() => "C:\\Downloads";
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
            public EDM.Models.ProxySettings GetProxySettings() => new();
            public void SetProxySettings(EDM.Models.ProxySettings settings, string? plainPassword = null) { }
            public List<EDM.Models.BandwidthSchedule> GetBandwidthSchedules() => new();
            public void SetBandwidthSchedules(List<EDM.Models.BandwidthSchedule> schedules) { }
            public bool GetEnableUrlSafetyCheck() => false;
            public void SetEnableUrlSafetyCheck(bool enable) { }
            public bool GetEnablePostDownloadScan() => false;
            public void SetEnablePostDownloadScan(bool enable) { }
            public string GetGoogleSafeBrowsingApiKey() => string.Empty;
            public void SetGoogleSafeBrowsingApiKey(string apiKey) { }
            public bool GetSendAnonymousCrashReports() => false;
            public void SetSendAnonymousCrashReports(bool enable) { }
        }

        [Fact]
        public async Task LocalHddStorageEngine_StreamWriteAtomic_And_Sha256_ComputesCorrectly()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"edm_storage_test_{Guid.NewGuid():N}");
            var settings = new TestSettingsService();
            settings.SetSetting("CustomStorageRootPath", tempDir);

            var engine = new LocalHddStorageEngine(settings);

            try
            {
                // Generate 8 MB sample stream
                byte[] testBytes = new byte[8 * 1024 * 1024];
                new Random(42).NextBytes(testBytes);
                using var sourceStream = new MemoryStream(testBytes);

                var metadata = await engine.StreamWriteAtomicAsync(sourceStream, "Documents/LargeDoc.dat", testBytes.Length);

                metadata.Should().NotBeNull();
                metadata.FileSizeBytes.Should().Be(testBytes.Length);
                metadata.Sha256Hash.Should().NotBeNullOrEmpty();
                File.Exists(metadata.FullPath).Should().BeTrue();

                // Compute hash independently to verify rolling streaming hash
                string verifyHash = await engine.CalculateFileSha256Async(metadata.FullPath);
                metadata.Sha256Hash.Should().Be(verifyHash);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void LocalHddStorageEngine_DiskSpacePrecheck_Throws_On_Insufficient_Space()
        {
            var settings = new TestSettingsService();
            var engine = new LocalHddStorageEngine(settings);

            // Requesting 50 Petabytes should throw InsufficientDiskSpaceException
            long impossibleSize = 50L * 1024 * 1024 * 1024 * 1024 * 1024; // 50 PB

            Action act = () => engine.ValidateAvailableDiskSpace(impossibleSize);
            act.Should().Throw<InsufficientDiskSpaceException>();
        }

        [Fact]
        public async Task CloudStorage_MetadataRegistration_Versioning_And_ConflictResolution_Works()
        {
            string username = "storage_admin_" + Guid.NewGuid().ToString("N")[..8];
            string email = $"{username}@edm.local";
            string password = "StrongPassword!2026";

            // 1. Register & Elevate
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = password });
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
                var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
                u!.Role = UserRole.SUPER_ADMIN;
                await db.SaveChangesAsync();
            }

            // 2. Login
            var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new { UsernameOrEmail = username, Password = password });
            var loginDoc = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
            string token = loginDoc.GetProperty("accessToken").GetString()!;

            // 3. Register initial file (v1)
            using var regReq1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/storage/files");
            regReq1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            regReq1.Content = JsonContent.Create(new
            {
                FileName = "report.pdf",
                RelativePath = "Documents/report.pdf",
                Category = "Documents",
                FileSizeBytes = 1048576,
                Sha256Hash = "a1b2c3d4e5f60000000000000000000000000000000000000000000000000001",
                Version = 1
            });
            var regRes1 = await _client.SendAsync(regReq1);
            regRes1.StatusCode.Should().Be(HttpStatusCode.Created);
            var doc1 = await regRes1.Content.ReadFromJsonAsync<JsonElement>();
            Guid fileId = Guid.Parse(doc1.GetProperty("file").GetProperty("id").GetString()!);

            // 4. Same hash -> UNCHANGED
            using var regReqSame = new HttpRequestMessage(HttpMethod.Post, "/api/v1/storage/files");
            regReqSame.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            regReqSame.Content = JsonContent.Create(new
            {
                FileName = "report.pdf",
                RelativePath = "Documents/report.pdf",
                Category = "Documents",
                FileSizeBytes = 1048576,
                Sha256Hash = "a1b2c3d4e5f60000000000000000000000000000000000000000000000000001",
                Version = 1
            });
            var regResSame = await _client.SendAsync(regReqSame);
            regResSame.StatusCode.Should().Be(HttpStatusCode.OK);
            var docSame = await regResSame.Content.ReadFromJsonAsync<JsonElement>();
            docSame.GetProperty("action").GetString().Should().Be("UNCHANGED");

            // 5. Version upgrade (v2) with higher version
            using var regReqV2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/storage/files");
            regReqV2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            regReqV2.Content = JsonContent.Create(new
            {
                FileName = "report.pdf",
                RelativePath = "Documents/report.pdf",
                Category = "Documents",
                FileSizeBytes = 2097152,
                Sha256Hash = "a1b2c3d4e5f60000000000000000000000000000000000000000000000000002",
                Version = 2
            });
            var regResV2 = await _client.SendAsync(regReqV2);
            regResV2.StatusCode.Should().Be(HttpStatusCode.OK);
            var docV2 = await regResV2.Content.ReadFromJsonAsync<JsonElement>();
            docV2.GetProperty("action").GetString().Should().Be("UPDATED");

            // 6. Conflict detection: submitting differing hash with version <= current (v1 <= v2)
            using var conflictReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/storage/files");
            conflictReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            conflictReq.Content = JsonContent.Create(new
            {
                FileName = "report.pdf",
                RelativePath = "Documents/report.pdf",
                Category = "Documents",
                FileSizeBytes = 3145728,
                Sha256Hash = "a1b2c3d4e5f60000000000000000000000000000000000000000000000000003",
                Version = 1 // Lower version conflicting with cloud v2
            });
            var conflictRes = await _client.SendAsync(conflictReq);
            conflictRes.StatusCode.Should().Be(HttpStatusCode.Conflict);

            // 7. Resolve Conflict using KeepBoth
            using var resolveReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/storage/files/{fileId}/resolve-conflict");
            resolveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            resolveReq.Content = JsonContent.Create(new
            {
                Strategy = "KeepBoth",
                ResolvedHash = "a1b2c3d4e5f60000000000000000000000000000000000000000000000000003",
                ResolvedSize = 3145728
            });
            var resolveRes = await _client.SendAsync(resolveReq);
            resolveRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 8. Verify Quota
            using var quotaReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/storage/quota");
            quotaReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var quotaRes = await _client.SendAsync(quotaReq);
            quotaRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var quotaDoc = await quotaRes.Content.ReadFromJsonAsync<JsonElement>();
            quotaDoc.GetProperty("totalFiles").GetInt32().Should().BeGreaterThanOrEqualTo(2);
        }
    }
}
