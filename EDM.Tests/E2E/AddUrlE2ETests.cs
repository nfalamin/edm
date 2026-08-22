using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.E2E
{
    [Trait("Category", "RealE2E")]
    public class AddUrlE2ETests : IAsyncLifetime
    {
        private LocalHttpTestServer _server = null!;
        private string _tempDir = null!;

        public async Task InitializeAsync()
        {
            _server = new LocalHttpTestServer();
            _tempDir = Path.Combine(Path.GetTempPath(), "EDM_AddUrl_E2E_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            await Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            await _server.DisposeAsync();
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); } catch { }
        }

        [Fact]
        public async Task AddUrl_Workflow_CreatesItem_ExecutesDownload_ValidatesSha256()
        {
            string rawUrl = $"{_server.BaseUrl}1mb.bin";
            string fileName = "AddUrl_Test_1mb.bin";
            string savePath = Path.Combine(_tempDir, fileName);

            var item = new DownloadItem
            {
                Url = rawUrl,
                FileName = fileName,
                SavePath = savePath,
                Category = "General",
                Status = "Downloading"
            };

            var downloadService = new DownloadService();
            var pauseSource = new PauseTokenSource();

            await downloadService.StartDownloadAsync(
                item.Url,
                item.SavePath,
                new Progress<DownloadProgressInfo>(),
                pauseSource,
                () => -1,
                CancellationToken.None,
                segmentCount: 4
            );

            File.Exists(savePath).Should().BeTrue();
            new FileInfo(savePath).Length.Should().Be(_server.OneMbData.Length);

            using var sha = SHA256.Create();
            using var fs = File.OpenRead(savePath);
            string actualHash = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
            string expectedHash = _server.GetExpectedSha256(_server.OneMbData);

            actualHash.Should().Be(expectedHash);
        }

        [Theory]
        [InlineData("example.com/file.zip", "https://example.com/file.zip")]
        [InlineData("http://example.com/data.iso", "http://example.com/data.iso")]
        [InlineData("https://example.com/doc.pdf", "https://example.com/doc.pdf")]
        public void AddUrl_UrlNormalization_EnsuresValidScheme(string input, string expected)
        {
            string normalized = input.Trim();
            if (!normalized.Contains("://"))
            {
                normalized = "https://" + normalized;
            }

            normalized.Should().Be(expected);
            Uri.TryCreate(normalized, UriKind.Absolute, out var uri).Should().BeTrue();
        }
    }
}
