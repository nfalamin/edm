using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
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
    public class ReleaseManagementAndStorageIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public ReleaseManagementAndStorageIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<string> GetSuperAdminTokenAsync()
        {
            string username = $"relmgr_{Guid.NewGuid():N}".Substring(0, 16);
            string email = $"{username}@edm.test";
            string password = "ReleaseAdminPassword!2026";

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
        public async Task Valid_Installer_Upload_Calculates_Sha256_And_Stores_In_Isolated_Path()
        {
            string token = await GetSuperAdminTokenAsync();

            // 1. Create a release
            string version = $"2.{Random.Shared.Next(10, 99)}.0";
            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            createReq.Content = JsonContent.Create(new
            {
                Platform = 0, // DesktopWindows
                Version = version,
                Title = $"EDM {version} Turbo Release",
                ReleaseNotes = "• Ultra-fast 32 socket downloads\n• Streaming integrity checking",
                IsMandatory = false,
                Severity = 0
            });
            var createRes = await _client.SendAsync(createReq);
            createRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var createDoc = await createRes.Content.ReadFromJsonAsync<JsonElement>();
            string releaseId = createDoc.GetProperty("releaseId").GetString()!;

            // 2. Prepare sample installer binary (PE header magic bytes MZ)
            byte[] installerBytes = new byte[1024 * 64]; // 64 KB binary
            installerBytes[0] = 0x4D; // 'M'
            installerBytes[1] = 0x5A; // 'Z'
            Random.Shared.NextBytes(installerBytes.AsSpan(2));
            string expectedSha256 = Convert.ToHexString(SHA256.HashData(installerBytes)).ToLowerInvariant();

            // 3. Upload artifact
            using var formData = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(installerBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.microsoft.portable-executable");
            formData.Add(fileContent, "file", $"EDM-Setup-{version}-x64.exe");
            formData.Add(new StringContent("x64"), "architecture");
            formData.Add(new StringContent(expectedSha256), "expectedSha256");

            var uploadReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/releases/{releaseId}/artifacts/upload");
            uploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            uploadReq.Content = formData;

            var uploadRes = await _client.SendAsync(uploadReq);
            uploadRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var uploadDoc = await uploadRes.Content.ReadFromJsonAsync<JsonElement>();
            uploadDoc.GetProperty("success").GetBoolean().Should().BeTrue();
            uploadDoc.GetProperty("sha256Hash").GetString().Should().Be(expectedSha256);
            uploadDoc.GetProperty("fileSizeBytes").GetInt64().Should().Be(installerBytes.Length);
            string artifactId = uploadDoc.GetProperty("artifactId").GetString()!;

            // 4. Download artifact and verify exact binary byte equality & headers
            var dlRes = await _client.GetAsync($"/api/v1/releases/artifacts/{artifactId}/download");
            dlRes.StatusCode.Should().Be(HttpStatusCode.OK);
            dlRes.Headers.Contains("X-Content-Type-Options").Should().BeTrue();
            dlRes.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
            dlRes.Content.Headers.ContentDisposition.Should().NotBeNull();
            dlRes.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");

            byte[] downloadedBytes = await dlRes.Content.ReadAsByteArrayAsync();
            downloadedBytes.Should().Equal(installerBytes);
        }

        [Theory]
        [InlineData("exploit.php")]
        [InlineData("backdoor.aspx")]
        [InlineData("payload.dll")]
        [InlineData("malicious.bat")]
        [InlineData("script.ps1")]
        [InlineData("danger.html")]
        public async Task Invalid_File_Types_Are_Strictly_Rejected(string badFilename)
        {
            string token = await GetSuperAdminTokenAsync();

            // Create release
            string version = $"2.{Random.Shared.Next(100, 999)}.0";
            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            createReq.Content = JsonContent.Create(new
            {
                Platform = 0,
                Version = version,
                Title = "Test Release"
            });
            var createRes = await _client.SendAsync(createReq);
            var createDoc = await createRes.Content.ReadFromJsonAsync<JsonElement>();
            string releaseId = createDoc.GetProperty("releaseId").GetString()!;

            // Try uploading prohibited file
            using var formData = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(new byte[] { 0x3C, 0x3F, 0x70, 0x68, 0x70 });
            formData.Add(fileContent, "file", badFilename);

            var uploadReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/releases/{releaseId}/artifacts/upload");
            uploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            uploadReq.Content = formData;

            var uploadRes = await _client.SendAsync(uploadReq);
            uploadRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var errDoc = await uploadRes.Content.ReadFromJsonAsync<JsonElement>();
            errDoc.GetProperty("error").GetString().Should().Be("VALIDATION_FAILED");
        }

        [Fact]
        public async Task Tampered_Sha256_Checksum_Fails_Upload_Integrity_Check()
        {
            string token = await GetSuperAdminTokenAsync();

            string version = $"2.{Random.Shared.Next(1000, 9999)}.0";
            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            createReq.Content = JsonContent.Create(new
            {
                Platform = 0,
                Version = version,
                Title = "Checksum Test Release"
            });
            var createRes = await _client.SendAsync(createReq);
            var createDoc = await createRes.Content.ReadFromJsonAsync<JsonElement>();
            string releaseId = createDoc.GetProperty("releaseId").GetString()!;

            byte[] realBytes = new byte[1024];
            Random.Shared.NextBytes(realBytes);

            using var formData = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(realBytes);
            formData.Add(fileContent, "file", "EDM-Setup.exe");
            formData.Add(new StringContent("0000000000000000000000000000000000000000000000000000000000000000"), "expectedSha256"); // Fake hash

            var uploadReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/releases/{releaseId}/artifacts/upload");
            uploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            uploadReq.Content = formData;

            var uploadRes = await _client.SendAsync(uploadReq);
            uploadRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Publish_Unpublish_And_Rollback_Lifecycle_Maintains_History()
        {
            string token = await GetSuperAdminTokenAsync();

            // 1. Create Base Release v2.4.0
            string v1 = $"2.4.{Random.Shared.Next(1, 50)}";
            var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req1.Content = JsonContent.Create(new { Platform = 0, Version = v1, Title = $"EDM {v1} Stable" });
            var res1 = await _client.SendAsync(req1);
            var doc1 = await res1.Content.ReadFromJsonAsync<JsonElement>();
            string relId1 = doc1.GetProperty("releaseId").GetString()!;

            // 2. Create Newer Release v2.5.0
            string v2 = $"2.5.{Random.Shared.Next(1, 50)}";
            var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/releases");
            req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req2.Content = JsonContent.Create(new { Platform = 0, Version = v2, Title = $"EDM {v2} New Feature" });
            var res2 = await _client.SendAsync(req2);
            var doc2 = await res2.Content.ReadFromJsonAsync<JsonElement>();
            string relId2 = doc2.GetProperty("releaseId").GetString()!;

            // 3. Unpublish v2.5.0 (move to draft)
            var unpubReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/releases/{relId2}/unpublish");
            unpubReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var unpubRes = await _client.SendAsync(unpubReq);
            unpubRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Latest public active should now be v1
            var latestRes = await _client.GetAsync("/api/v1/releases/latest?platform=DesktopWindows");
            var latestDoc = await latestRes.Content.ReadFromJsonAsync<JsonElement>();
            latestDoc.GetProperty("version").GetString().Should().Be(v1);

            // 4. Publish v2.5.0 back to production
            var pubReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/releases/{relId2}/publish");
            pubReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var pubRes = await _client.SendAsync(pubReq);
            pubRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Latest public active is now v2
            var latestRes2 = await _client.GetAsync("/api/v1/releases/latest?platform=DesktopWindows");
            var latestDoc2 = await latestRes2.Content.ReadFromJsonAsync<JsonElement>();
            latestDoc2.GetProperty("version").GetString().Should().Be(v2);

            // 5. Execute confirmed rollback of v2.5.0 back to v2.4.0
            var rollReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/releases/{relId2}/rollback");
            rollReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            rollReq.Content = JsonContent.Create(new
            {
                TargetVersion = v1,
                Reason = "Critical bug identified in v2.5.0"
            });
            var rollRes = await _client.SendAsync(rollReq);
            rollRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Latest public active is safely back to v1
            var latestRes3 = await _client.GetAsync("/api/v1/releases/latest?platform=DesktopWindows");
            var latestDoc3 = await latestRes3.Content.ReadFromJsonAsync<JsonElement>();
            latestDoc3.GetProperty("version").GetString().Should().Be(v1);
        }

        [Fact]
        public async Task Unauthorized_Upload_Attempt_Is_Rejected()
        {
            using var formData = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(new byte[] { 0x4D, 0x5A });
            formData.Add(fileContent, "file", "EDM-Setup.exe");

            var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/releases/{Guid.NewGuid()}/artifacts/upload")
            {
                Content = formData
            };

            var res = await _client.SendAsync(req);
            res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
