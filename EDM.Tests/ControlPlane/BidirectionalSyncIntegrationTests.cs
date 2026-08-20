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
using EDM.Services.Storage;

namespace EDM.Tests.ControlPlane
{
    public class BidirectionalSyncIntegrationTests : IClassFixture<ControlPlaneTestFactory>, IDisposable
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;
        private readonly string _tempWorkspace;

        public BidirectionalSyncIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
            _tempWorkspace = Path.Combine(Path.GetTempPath(), "EDM_Bidi_Test_" + Guid.NewGuid().ToString("N"));
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
            string username = "sync_user_" + Guid.NewGuid().ToString("N")[..8];
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
        public async Task SyncDeltas_And_DeleteByPath_Endpoints_Work_As_Expected()
        {
            var (token, userId) = await CreateAuthenticatedUserAsync();

            DateTime beforeSync = DateTime.UtcNow.AddSeconds(-2);

            // 1. Register a file
            using var regReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/storage/files");
            regReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            regReq.Content = JsonContent.Create(new
            {
                FileName = "delta_doc.txt",
                RelativePath = "Sync/delta_doc.txt",
                Category = "Documents",
                FileSizeBytes = 512,
                Sha256Hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                Version = 1
            });
            var regRes = await _client.SendAsync(regReq);
            regRes.StatusCode.Should().Be(HttpStatusCode.Created);

            // 2. Query deltas since beforeSync
            using var deltaReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/storage/sync/deltas?sinceUtc={Uri.EscapeDataString(beforeSync.ToString("O"))}");
            deltaReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var deltaRes = await _client.SendAsync(deltaReq);
            deltaRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var deltaDoc = await deltaRes.Content.ReadFromJsonAsync<JsonElement>();
            var changes = deltaDoc.GetProperty("changes").EnumerateArray().ToList();
            changes.Should().ContainSingle(c => c.GetProperty("fileName").GetString() == "delta_doc.txt");

            // 3. Delete by path (from local file watcher delete event)
            using var delReq = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/storage/files/by-path?path=Sync/delta_doc.txt");
            delReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var delRes = await _client.SendAsync(delReq);
            delRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Query deltas again -> should report IsDeleted = true
            using var deltaReq2 = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/storage/sync/deltas?sinceUtc={Uri.EscapeDataString(beforeSync.ToString("O"))}");
            deltaReq2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var deltaRes2 = await _client.SendAsync(deltaReq2);
            var deltaDoc2 = await deltaRes2.Content.ReadFromJsonAsync<JsonElement>();
            var changes2 = deltaDoc2.GetProperty("changes").EnumerateArray().ToList();
            var deletedChange = changes2.FirstOrDefault(c => c.GetProperty("fileName").GetString() == "delta_doc.txt");
            deletedChange.ValueKind.Should().NotBe(JsonValueKind.Undefined);
            deletedChange.GetProperty("isDeleted").GetBoolean().Should().BeTrue();
        }

        [Fact]
        public async Task MultiDevice_Conflict_Detection_Returns_409_When_Versions_Diverge()
        {
            var (token, userId) = await CreateAuthenticatedUserAsync();
            Guid device1 = Guid.NewGuid();
            Guid device2 = Guid.NewGuid();

            // 1. Device 1 registers initial file (v1)
            using var regReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/storage/files");
            regReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            regReq.Content = JsonContent.Create(new
            {
                FileName = "shared_notes.txt",
                RelativePath = "Work/shared_notes.txt",
                Category = "Documents",
                FileSizeBytes = 100,
                Sha256Hash = "1111111111111111111111111111111111111111111111111111111111111111",
                Version = 1,
                DeviceId = device1
            });
            var regRes = await _client.SendAsync(regReq);
            regRes.StatusCode.Should().Be(HttpStatusCode.Created);

            // 2. Device 1 updates file to v2
            using var updateReq1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/storage/files");
            updateReq1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            updateReq1.Content = JsonContent.Create(new
            {
                FileName = "shared_notes.txt",
                RelativePath = "Work/shared_notes.txt",
                Category = "Documents",
                FileSizeBytes = 200,
                Sha256Hash = "2222222222222222222222222222222222222222222222222222222222222222",
                Version = 2,
                DeviceId = device1
            });
            var updateRes1 = await _client.SendAsync(updateReq1);
            updateRes1.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. Device 2 tries to update using outdated version (v1) with different hash -> 409 Conflict!
            using var updateReq2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/storage/files");
            updateReq2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            updateReq2.Content = JsonContent.Create(new
            {
                FileName = "shared_notes.txt",
                RelativePath = "Work/shared_notes.txt",
                Category = "Documents",
                FileSizeBytes = 300,
                Sha256Hash = "3333333333333333333333333333333333333333333333333333333333333333",
                Version = 1, // Outdated version!
                DeviceId = device2
            });
            var updateRes2 = await _client.SendAsync(updateReq2);
            updateRes2.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var conflictDoc = await updateRes2.Content.ReadFromJsonAsync<JsonElement>();
            conflictDoc.GetProperty("error").GetString().Should().Be("SYNC_CONFLICT");
        }

        [Fact]
        public async Task BidirectionalSyncCoordinator_Lifecycle_And_Push_Local_Files()
        {
            var (token, userId) = await CreateAuthenticatedUserAsync();

            var settings = new EDM.Tests.Services.TestSettingsService();
            settings.SetSetting("CustomStorageRootPath", _tempWorkspace);
            settings.SetSetting("ControlPlaneApiUrl", _client.BaseAddress!.ToString());
            settings.SetSetting("EncryptedAccessToken", EDM.Services.SecureCredentialVault.EncryptSecret(token));

            var storageEngine = new LocalHddStorageEngine(settings);
            var cloudAgent = new CloudFileSyncAgent(_client, storageEngine, settings);
            var watcher = new LocalHddFileSystemWatcher(storageEngine);

            var coordinator = new BidirectionalSyncCoordinator(storageEngine, cloudAgent, watcher, settings);

            // Start coordinator
            coordinator.Start(pollingIntervalSeconds: 3);
            coordinator.IsRunning.Should().BeTrue();

            // Create local file
            string localFilePath = Path.Combine(_tempWorkspace, "local_test_file.txt");
            await File.WriteAllTextAsync(localFilePath, "Hello Bidirectional Sync Coordinator 2026!");

            // Trigger local change event
            await coordinator.HandleLocalChangeEventAsync(new LocalFileChangeEvent(
                LocalFileChangeType.Created,
                "local_test_file.txt",
                null,
                localFilePath,
                DateTime.UtcNow));

            // Verify file registered in Cloud
            using var getReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/storage/files");
            getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var getRes = await _client.SendAsync(getReq);
            var list = await getRes.Content.ReadFromJsonAsync<List<JsonElement>>();
            list.Should().ContainSingle(f => f.GetProperty("fileName").GetString() == "local_test_file.txt");

            // Stop coordinator
            coordinator.Stop();
            coordinator.IsRunning.Should().BeFalse();
            coordinator.Dispose();
        }
    }
}
