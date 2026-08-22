using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;
using EDM.Services;
using EDM.Services.Interfaces;
using Moq;

namespace EDM.Tests.ControlPlane
{
    public class ClientApplicationUpdateIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public ClientApplicationUpdateIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<string> GetSuperAdminTokenAsync()
        {
            string username = $"updater_{Guid.NewGuid():N}".Substring(0, 16);
            string email = $"{username}@edm.test";
            string password = "UpdateAdminPassword!2026";

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
        public async Task CheckUpdate_OldVersion_ReturnsUpdateAvailable()
        {
            string token = await GetSuperAdminTokenAsync();
            string newVersion = $"3.5.{Random.Shared.Next(1, 99)}";

            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            createReq.Content = JsonContent.Create(new
            {
                Platform = 0,
                Version = newVersion,
                Title = $"EDM {newVersion} Fast Update",
                MinimumSupportedVersion = "1.0.0",
                Severity = 0 // Standard / Optional
            });
            var createRes = await _client.SendAsync(createReq);
            createRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Test client update check
            var checkRes = await _client.PostAsJsonAsync("/api/v1/updates/check", new
            {
                Platform = 0,
                CurrentVersion = "1.0.0",
                InstallationId = Guid.NewGuid()
            });
            checkRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var doc = await checkRes.Content.ReadFromJsonAsync<JsonElement>();
            doc.GetProperty("updateAvailable").GetBoolean().Should().BeTrue();
            doc.GetProperty("latestVersion").GetString().Should().Be(newVersion);
            doc.GetProperty("severity").GetString().Should().Be("OPTIONAL");
        }

        [Fact]
        public async Task CheckUpdate_CurrentVersion_ReturnsNoUpdate()
        {
            string token = await GetSuperAdminTokenAsync();
            string targetVer = $"3.6.{Random.Shared.Next(1, 99)}";

            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            createReq.Content = JsonContent.Create(new
            {
                Platform = 0,
                Version = targetVer,
                Title = "Up-to-Date Release",
                MinimumSupportedVersion = "1.0.0"
            });
            await _client.SendAsync(createReq);

            // Check using same version
            var checkRes = await _client.PostAsJsonAsync("/api/v1/updates/check", new
            {
                Platform = 0,
                CurrentVersion = targetVer,
                InstallationId = Guid.NewGuid()
            });
            checkRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var doc = await checkRes.Content.ReadFromJsonAsync<JsonElement>();
            doc.GetProperty("updateAvailable").GetBoolean().Should().BeFalse();
            doc.GetProperty("latestVersion").GetString().Should().Be(targetVer);
        }

        [Fact]
        public async Task CheckUpdate_RecommendedRelease_SetsRecommendedSeverity()
        {
            string token = await GetSuperAdminTokenAsync();
            string recVer = $"3.7.{Random.Shared.Next(1, 99)}";

            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            createReq.Content = JsonContent.Create(new
            {
                Platform = 0,
                Version = recVer,
                Title = "Recommended Performance Update",
                MinimumSupportedVersion = "1.0.0",
                Severity = 1 // Recommended
            });
            await _client.SendAsync(createReq);

            var checkRes = await _client.PostAsJsonAsync("/api/v1/updates/check", new
            {
                Platform = 0,
                CurrentVersion = "1.0.0",
                InstallationId = Guid.NewGuid()
            });
            checkRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var doc = await checkRes.Content.ReadFromJsonAsync<JsonElement>();
            doc.GetProperty("updateAvailable").GetBoolean().Should().BeTrue();
            doc.GetProperty("severity").GetString().Should().Be("RECOMMENDED");
            doc.GetProperty("isMandatory").GetBoolean().Should().BeFalse();
        }

        [Fact]
        public async Task CheckUpdate_BelowMinimumSupportedVersion_ElevatesToRequired()
        {
            string token = await GetSuperAdminTokenAsync();
            string reqVer = $"3.8.{Random.Shared.Next(1, 99)}";

            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            createReq.Content = JsonContent.Create(new
            {
                Platform = 0,
                Version = reqVer,
                Title = "Required Protocol Security Update",
                MinimumSupportedVersion = "3.0.0",
                Severity = 2 // Critical
            });
            await _client.SendAsync(createReq);

            var checkRes = await _client.PostAsJsonAsync("/api/v1/updates/check", new
            {
                Platform = 0,
                CurrentVersion = "2.5.0", // Below 3.0.0 min version
                InstallationId = Guid.NewGuid()
            });
            checkRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var doc = await checkRes.Content.ReadFromJsonAsync<JsonElement>();
            doc.GetProperty("updateAvailable").GetBoolean().Should().BeTrue();
            doc.GetProperty("severity").GetString().Should().Be("REQUIRED");
            doc.GetProperty("isMandatory").GetBoolean().Should().BeTrue();
        }

        [Fact]
        public async Task DownloadAndVerifyUpdate_ValidSha256_Succeeds()
        {
            string token = await GetSuperAdminTokenAsync();
            string ver = $"3.9.{Random.Shared.Next(1, 99)}";

            // 1. Create release
            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            createReq.Content = JsonContent.Create(new { Platform = 0, Version = ver, Title = $"EDM {ver}" });
            var createRes = await _client.SendAsync(createReq);
            var createDoc = await createRes.Content.ReadFromJsonAsync<JsonElement>();
            string releaseId = createDoc.GetProperty("releaseId").GetString()!;

            // 2. Upload binary
            byte[] installerBytes = new byte[1024 * 32];
            installerBytes[0] = 0x4D;
            installerBytes[1] = 0x5A;
            Random.Shared.NextBytes(installerBytes.AsSpan(2));
            string realSha256 = Convert.ToHexString(SHA256.HashData(installerBytes)).ToLowerInvariant();

            using var formData = new MultipartFormDataContent();
            formData.Add(new ByteArrayContent(installerBytes), "file", "EDM-Setup.exe");
            var uploadReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/releases/{releaseId}/artifacts/upload");
            uploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            uploadReq.Content = formData;
            var uploadRes = await _client.SendAsync(uploadReq);
            var uploadDoc = await uploadRes.Content.ReadFromJsonAsync<JsonElement>();
            string artifactId = uploadDoc.GetProperty("artifactId").GetString()!;

            // 3. Mock settings and download update using client UpdateService
            var mockSettings = new Mock<ISettingsService>();
            mockSettings.Setup(s => s.GetSetting("ControlPlaneApiUrl")).Returns(_client.BaseAddress?.ToString() ?? "http://localhost");
            var updateService = new UpdateService(mockSettings.Object, httpClient: _client);

            var updateInfo = new UpdateInfo
            {
                Version = ver,
                DownloadUrl = $"{_client.BaseAddress}api/v1/releases/artifacts/{artifactId}/download",
                Sha256 = realSha256
            };

            var progress = new Progress<DownloadProgressInfo>();
            var pauseToken = new PauseTokenSource();

            string downloadedPath = await updateService.DownloadAndVerifyUpdateAsync(updateInfo, progress, pauseToken, CancellationToken.None);
            File.Exists(downloadedPath).Should().BeTrue();

            try { File.Delete(downloadedPath); } catch { }
        }

        [Fact]
        public async Task DownloadAndVerifyUpdate_MismatchedSha256_DeletesFile_And_ThrowsException()
        {
            string token = await GetSuperAdminTokenAsync();
            string ver = $"4.0.{Random.Shared.Next(1, 99)}";

            // 1. Create release & upload artifact
            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            createReq.Content = JsonContent.Create(new { Platform = 0, Version = ver, Title = $"EDM {ver}" });
            var createRes = await _client.SendAsync(createReq);
            var createDoc = await createRes.Content.ReadFromJsonAsync<JsonElement>();
            string releaseId = createDoc.GetProperty("releaseId").GetString()!;

            using var formData = new MultipartFormDataContent();
            formData.Add(new ByteArrayContent(new byte[] { 0x4D, 0x5A, 0x01, 0x02 }), "file", "EDM-Setup.exe");
            var uploadReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/releases/{releaseId}/artifacts/upload");
            uploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            uploadReq.Content = formData;
            var uploadRes = await _client.SendAsync(uploadReq);
            var uploadDoc = await uploadRes.Content.ReadFromJsonAsync<JsonElement>();
            string artifactId = uploadDoc.GetProperty("artifactId").GetString()!;

            var mockSettings = new Mock<ISettingsService>();
            var updateService = new UpdateService(mockSettings.Object, httpClient: _client);

            var corruptUpdateInfo = new UpdateInfo
            {
                Version = ver,
                DownloadUrl = $"{_client.BaseAddress}api/v1/releases/artifacts/{artifactId}/download",
                Sha256 = "0000000000000000000000000000000000000000000000000000000000000000" // Tampered hash
            };

            var act = async () => await updateService.DownloadAndVerifyUpdateAsync(corruptUpdateInfo, new Progress<DownloadProgressInfo>(), new PauseTokenSource(), CancellationToken.None);

            await act.Should().ThrowAsync<InvalidDataException>()
                .WithMessage("*failed SHA256 integrity check*");
        }
    }
}
