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
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;

namespace EDM.Tests.ControlPlane
{
    public class DashboardFileManagerAndPreviewIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public DashboardFileManagerAndPreviewIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<(string Token, Guid UserId)> CreateAuthenticatedUserAsync(string role = "SUPER_ADMIN")
        {
            string username = "fm_user_" + Guid.NewGuid().ToString("N")[..8];
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
        public async Task FileUpload_Download_And_Preview_Flow_Succeeds()
        {
            var (token, userId) = await CreateAuthenticatedUserAsync();

            // 1. Upload text file
            string textPayload = "Hello EDM File Manager 2026!\nThis is a real previewable text file.";
            byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(textPayload);

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(textBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
            content.Add(fileContent, "file", "sample_notes.txt");
            content.Add(new StringContent("Documents/Projects"), "targetFolder");
            content.Add(new StringContent("Documents"), "category");

            using var uploadReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/storage/upload");
            uploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            uploadReq.Content = content;

            var uploadRes = await _client.SendAsync(uploadReq);
            uploadRes.StatusCode.Should().Be(HttpStatusCode.Created);
            var uploadDoc = await uploadRes.Content.ReadFromJsonAsync<JsonElement>();
            Guid fileId = Guid.Parse(uploadDoc.GetProperty("file").GetProperty("id").GetString()!);

            // 2. Preview text file
            using var previewReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/storage/files/{fileId}/preview");
            previewReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var previewRes = await _client.SendAsync(previewReq);
            previewRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var previewDoc = await previewRes.Content.ReadFromJsonAsync<JsonElement>();
            previewDoc.GetProperty("previewType").GetString().Should().Be("text");
            previewDoc.GetProperty("content").GetString().Should().Contain("Hello EDM File Manager 2026!");

            // 3. Download text file
            using var dlReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/storage/files/{fileId}/download");
            dlReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var dlRes = await _client.SendAsync(dlReq);
            dlRes.StatusCode.Should().Be(HttpStatusCode.OK);
            byte[] downloadedBytes = await dlRes.Content.ReadAsByteArrayAsync();
            downloadedBytes.Should().BeEquivalentTo(textBytes);
        }

        [Fact]
        public async Task FolderNavigation_And_Search_Returns_Correct_Structure()
        {
            var (token, userId) = await CreateAuthenticatedUserAsync();

            // Register files in root, Documents/Projects, and Media
            async Task RegFile(string name, string relPath, string cat)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/storage/files");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                req.Content = JsonContent.Create(new
                {
                    FileName = name,
                    RelativePath = relPath,
                    Category = cat,
                    FileSizeBytes = 1024,
                    Sha256Hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                    Version = 1
                });
                var res = await _client.SendAsync(req);
                res.EnsureSuccessStatusCode();
            }

            await RegFile("root_doc.txt", "root_doc.txt", "General");
            await RegFile("project_plan.md", "Documents/Projects/project_plan.md", "Documents");
            await RegFile("video.mp4", "Media/video.mp4", "Media");

            // 1. Query Root folder
            using var rootReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/storage/files?folder=");
            rootReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var rootRes = await _client.SendAsync(rootReq);
            rootRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var rootDoc = await rootRes.Content.ReadFromJsonAsync<JsonElement>();

            var subFolders = rootDoc.GetProperty("subFolders").EnumerateArray().Select(x => x.GetString()).ToList();
            subFolders.Should().Contain("Documents");
            subFolders.Should().Contain("Media");

            // 2. Search by keyword
            using var searchReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/storage/files?search=project");
            searchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var searchRes = await _client.SendAsync(searchReq);
            searchRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var searchDoc = await searchRes.Content.ReadFromJsonAsync<JsonElement>();
            var searchFiles = searchDoc.EnumerateArray().ToList();
            searchFiles.Should().ContainSingle(f => f.GetProperty("fileName").GetString() == "project_plan.md");
        }

        [Fact]
        public async Task Rename_Move_SoftDelete_And_Restore_Lifecycle_Works()
        {
            var (token, userId) = await CreateAuthenticatedUserAsync();

            // 1. Register File
            using var regReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/storage/files");
            regReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            regReq.Content = JsonContent.Create(new
            {
                FileName = "original_doc.txt",
                RelativePath = "Documents/original_doc.txt",
                Category = "Documents",
                FileSizeBytes = 2048,
                Sha256Hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                Version = 1
            });
            var regRes = await _client.SendAsync(regReq);
            var regDoc = await regRes.Content.ReadFromJsonAsync<JsonElement>();
            Guid fileId = Guid.Parse(regDoc.GetProperty("file").GetProperty("id").GetString()!);

            // 2. Rename File
            using var renameReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/storage/files/{fileId}/rename");
            renameReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            renameReq.Content = JsonContent.Create(new { NewFileName = "renamed_doc.txt" });
            var renameRes = await _client.SendAsync(renameReq);
            renameRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var renameDoc = await renameRes.Content.ReadFromJsonAsync<JsonElement>();
            renameDoc.GetProperty("file").GetProperty("fileName").GetString().Should().Be("renamed_doc.txt");
            renameDoc.GetProperty("file").GetProperty("relativePath").GetString().Should().Be("Documents/renamed_doc.txt");

            // 3. Move File
            using var moveReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/storage/files/{fileId}/move");
            moveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            moveReq.Content = JsonContent.Create(new { TargetFolder = "Archive/2026" });
            var moveRes = await _client.SendAsync(moveReq);
            moveRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var moveDoc = await moveRes.Content.ReadFromJsonAsync<JsonElement>();
            moveDoc.GetProperty("file").GetProperty("relativePath").GetString().Should().Be("Archive/2026/renamed_doc.txt");

            // 4. Soft Delete (Move to Trash)
            using var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/storage/files/{fileId}");
            delReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var delRes = await _client.SendAsync(delReq);
            delRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Active list should not contain file
            using var listReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/storage/files");
            listReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var listRes = await _client.SendAsync(listReq);
            var listDoc = await listRes.Content.ReadFromJsonAsync<JsonElement>();
            listDoc.EnumerateArray().Any(x => x.GetProperty("id").GetString() == fileId.ToString()).Should().BeFalse();

            // 5. Restore from Trash
            using var restoreReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/storage/files/{fileId}/restore");
            restoreReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var restoreRes = await _client.SendAsync(restoreReq);
            restoreRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Active list should now contain restored file
            using var listReq2 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/storage/files");
            listReq2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var listRes2 = await _client.SendAsync(listReq2);
            var listDoc2 = await listRes2.Content.ReadFromJsonAsync<JsonElement>();
            listDoc2.EnumerateArray().Any(x => x.GetProperty("id").GetString() == fileId.ToString()).Should().BeTrue();
        }

        [Fact]
        public async Task Unauthorized_User_Cannot_Access_Other_Users_Files()
        {
            var (user1Token, user1Id) = await CreateAuthenticatedUserAsync();
            var (user2Token, user2Id) = await CreateAuthenticatedUserAsync();

            // User 1 creates a private file
            using var regReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/storage/files");
            regReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1Token);
            regReq.Content = JsonContent.Create(new
            {
                FileName = "secret_report.pdf",
                RelativePath = "Documents/secret_report.pdf",
                Category = "Documents",
                FileSizeBytes = 4096,
                Sha256Hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                Version = 1
            });
            var regRes = await _client.SendAsync(regReq);
            var regDoc = await regRes.Content.ReadFromJsonAsync<JsonElement>();
            Guid fileId = Guid.Parse(regDoc.GetProperty("file").GetProperty("id").GetString()!);

            // User 2 attempts to get User 1's file -> 404 NotFound
            using var unauthGetReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/storage/files/{fileId}");
            unauthGetReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user2Token);
            var unauthGetRes = await _client.SendAsync(unauthGetReq);
            unauthGetRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

            // User 2 attempts to rename User 1's file -> 404 NotFound
            using var unauthRenameReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/storage/files/{fileId}/rename");
            unauthRenameReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user2Token);
            unauthRenameReq.Content = JsonContent.Create(new { NewFileName = "hacked.pdf" });
            var unauthRenameRes = await _client.SendAsync(unauthRenameReq);
            unauthRenameRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

            // Unauthenticated request (no token) -> 401 Unauthorized
            using var noAuthReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/storage/files/{fileId}");
            var noAuthRes = await _client.SendAsync(noAuthReq);
            noAuthRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
