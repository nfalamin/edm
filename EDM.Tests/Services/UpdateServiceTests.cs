using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using EDM.Services;
using EDM.Services.Interfaces;

namespace EDM.Tests.Services
{
    public class UpdateServiceTests : TestBase
    {
        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_handler(request));
            }
        }

        [Fact]
        public async Task CheckForUpdatesAsync_LocalManifestNewerVersion_ReturnsUpdateAvailable()
        {
            // Arrange
            var mockSettings = CreateMock<ISettingsService>();
            var updateService = new UpdateService(mockSettings.Object);

            var manifestJson = @"{
                ""version"": ""2.5.0"",
                ""downloadUrl"": ""https://example.com/EDMSetup_2.5.0.exe"",
                ""sha256"": ""abcd1234efgh5678"",
                ""changelog"": ""New feature releases and stability fixes.""
            }";

            var tempManifestPath = Path.Combine(Path.GetTempPath(), "test_update_" + Guid.NewGuid() + ".json");
            await File.WriteAllTextAsync(tempManifestPath, manifestJson);

            try
            {
                // Act
                var info = await updateService.CheckForUpdatesAsync(tempManifestPath, new Version(1, 0, 0));

                // Assert
                info.Should().NotBeNull();
                info.IsUpdateAvailable.Should().BeTrue();
                info.Version.Should().Be("2.5.0");
                info.DownloadUrl.Should().Be("https://example.com/EDMSetup_2.5.0.exe");
                info.Changelog.Should().Contain("New feature");
            }
            finally
            {
                if (File.Exists(tempManifestPath)) File.Delete(tempManifestPath);
            }
        }

        [Fact]
        public async Task CheckForUpdatesAsync_OlderVersion_ReturnsNoUpdateAvailable()
        {
            // Arrange
            var mockSettings = CreateMock<ISettingsService>();
            var updateService = new UpdateService(mockSettings.Object);

            var manifestJson = @"{
                ""version"": ""1.0.0"",
                ""downloadUrl"": ""https://example.com/EDMSetup_1.0.0.exe""
            }";

            var tempManifestPath = Path.Combine(Path.GetTempPath(), "test_update_" + Guid.NewGuid() + ".json");
            await File.WriteAllTextAsync(tempManifestPath, manifestJson);

            try
            {
                // Act
                var info = await updateService.CheckForUpdatesAsync(tempManifestPath, new Version(2, 0, 0));

                // Assert
                info.Should().NotBeNull();
                info.IsUpdateAvailable.Should().BeFalse();
            }
            finally
            {
                if (File.Exists(tempManifestPath)) File.Delete(tempManifestPath);
            }
        }

        [Fact]
        public async Task DownloadAndVerifyUpdateAsync_ValidChecksum_SucceedsAndReturnsPath()
        {
            // Arrange
            var testBinaryContent = Encoding.UTF8.GetBytes("Fake EDM Installer Binary Data");
            string expectedSha256;
            using (var sha = SHA256.Create())
            {
                expectedSha256 = Convert.ToHexString(sha.ComputeHash(testBinaryContent)).ToLowerInvariant();
            }

            var handler = new FakeHttpMessageHandler(req =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(testBinaryContent)
                };
                resp.Content.Headers.ContentLength = testBinaryContent.Length;
                return resp;
            });

            var httpClient = new HttpClient(handler);
            var mockSettings = CreateMock<ISettingsService>();
            mockSettings.Setup(s => s.GetActiveBandwidthLimitKbps()).Returns(0);

            var mockNetwork = CreateMock<INetworkService>();
            mockNetwork.Setup(n => n.IsMeteredNetwork()).Returns(false);

            var downloadService = new DownloadService(httpClient, mockNetwork.Object, mockSettings.Object);
            var updateService = new UpdateService(mockSettings.Object, downloadService, httpClient);

            var updateInfo = new UpdateInfo
            {
                Version = "2.1.0",
                DownloadUrl = "http://example.com/installer.exe",
                Sha256 = expectedSha256
            };

            var progress = new Progress<DownloadProgressInfo>(_ => { });
            var pauseToken = new PauseTokenSource();

            string downloadedInstallerPath = string.Empty;

            try
            {
                // Act
                downloadedInstallerPath = await updateService.DownloadAndVerifyUpdateAsync(updateInfo, progress, pauseToken);

                // Assert
                File.Exists(downloadedInstallerPath).Should().BeTrue();
            }
            finally
            {
                if (!string.IsNullOrEmpty(downloadedInstallerPath) && File.Exists(downloadedInstallerPath))
                {
                    File.Delete(downloadedInstallerPath);
                }
            }
        }

        [Fact]
        public async Task DownloadAndVerifyUpdateAsync_MismatchedChecksum_ThrowsInvalidDataException()
        {
            // Arrange
            var testBinaryContent = Encoding.UTF8.GetBytes("Fake EDM Installer Binary Data");

            var handler = new FakeHttpMessageHandler(req =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(testBinaryContent)
                };
                resp.Content.Headers.ContentLength = testBinaryContent.Length;
                return resp;
            });

            var httpClient = new HttpClient(handler);
            var mockSettings = CreateMock<ISettingsService>();
            var mockNetwork = CreateMock<INetworkService>();

            var downloadService = new DownloadService(httpClient, mockNetwork.Object, mockSettings.Object);
            var updateService = new UpdateService(mockSettings.Object, downloadService, httpClient);

            var updateInfo = new UpdateInfo
            {
                Version = "2.1.0",
                DownloadUrl = "http://example.com/installer.exe",
                Sha256 = "invalid_hash_value_12345"
            };

            var progress = new Progress<DownloadProgressInfo>(_ => { });
            var pauseToken = new PauseTokenSource();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                updateService.DownloadAndVerifyUpdateAsync(updateInfo, progress, pauseToken));
        }
    }
}
