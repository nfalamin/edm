using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using EDM.Models;
using EDM.Services;
using EDM.Services.Interfaces;

namespace EDM.Tests.Services
{
    public class DownloadServiceTests : TestBase
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
        public async Task StartDownloadAsync_WithCookies_AttachesCookieHeaderToHttpRequest()
        {
            // Arrange
            string? capturedCookieHeader = null;
            var testData = Encoding.UTF8.GetBytes("Authenticated payload");

            var handler = new FakeHttpMessageHandler(req =>
            {
                if (req.Headers.Contains("Cookie"))
                {
                    capturedCookieHeader = string.Join("; ", req.Headers.GetValues("Cookie"));
                }

                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(testData)
                };
                resp.Content.Headers.ContentLength = testData.Length;
                return resp;
            });

            var httpClient = new HttpClient(handler);
            var mockNetworkService = CreateMock<INetworkService>();
            var mockSettingsService = CreateMock<ISettingsService>();

            var downloadService = new DownloadService(httpClient, mockNetworkService.Object, mockSettingsService.Object);
            var tempPath = Path.Combine(Path.GetTempPath(), "EDM_CookieTest_" + Guid.NewGuid() + ".txt");

            var progressReporter = new Progress<DownloadProgressInfo>(_ => { });
            var pauseToken = new PauseTokenSource();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var suppliedCookies = "sessionid=xyz123auth; member_type=premium";

            try
            {
                // Act
                await downloadService.StartDownloadAsync(
                    url: "http://example.com/protected.txt",
                    savePath: tempPath,
                    progressReporter: progressReporter,
                    pauseToken: pauseToken,
                    speedLimitProvider: () => 0,
                    cancellationToken: cts.Token,
                    cookies: suppliedCookies);

                // Assert
                capturedCookieHeader.Should().NotBeNull();
                capturedCookieHeader.Should().Be(suppliedCookies);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [Fact]
        public async Task StartDownloadAsync_SuccessfulDownload_CompletesAndReportsFinished()
        {
            // Arrange
            var testData = Encoding.UTF8.GetBytes("Hello World from EDM unit test!");
            var handler = new FakeHttpMessageHandler(req =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(testData)
                };
                resp.Content.Headers.ContentLength = testData.Length;
                return resp;
            });

            var httpClient = new HttpClient(handler);
            var mockNetworkService = CreateMock<INetworkService>();
            mockNetworkService.Setup(n => n.IsMeteredNetwork()).Returns(false);

            var mockSettingsService = CreateMock<ISettingsService>();
            mockSettingsService.Setup(s => s.GetActiveBandwidthLimitKbps()).Returns(0);

            var downloadService = new DownloadService(httpClient, mockNetworkService.Object, mockSettingsService.Object);

            var tempPath = Path.Combine(Path.GetTempPath(), "EDM_Test_" + Guid.NewGuid() + ".txt");
            DownloadProgressInfo? lastProgress = null;
            var progressReporter = new Progress<DownloadProgressInfo>(info =>
            {
                lastProgress = info;
            });

            var pauseToken = new PauseTokenSource();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            try
            {
                // Act
                await downloadService.StartDownloadAsync(
                    url: "http://example.com/testfile.txt",
                    savePath: tempPath,
                    progressReporter: progressReporter,
                    pauseToken: pauseToken,
                    speedLimitProvider: () => 0,
                    cancellationToken: cts.Token);

                // Give progress reporter time to flush
                await Task.Delay(100);

                // Assert
                File.Exists(tempPath).Should().BeTrue();
                File.ReadAllBytes(tempPath).Should().Equal(testData);
                lastProgress.Should().NotBeNull();
                lastProgress!.Status.Should().Be("Finished");
                lastProgress.IsCompleted.Should().BeTrue();
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [Fact]
        public async Task StartDownloadAsync_CancellationMidDownload_ReportsCanceledStatus()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(req =>
            {
                throw new OperationCanceledException("Cancellation requested");
            });

            var httpClient = new HttpClient(handler);
            var mockNetworkService = CreateMock<INetworkService>();
            var mockSettingsService = CreateMock<ISettingsService>();

            var downloadService = new DownloadService(httpClient, mockNetworkService.Object, mockSettingsService.Object);
            var tempPath = Path.Combine(Path.GetTempPath(), "EDM_CancelTest_" + Guid.NewGuid() + ".tmp");

            DownloadProgressInfo? lastProgress = null;
            var progressReporter = new Progress<DownloadProgressInfo>(info =>
            {
                lastProgress = info;
            });

            var pauseToken = new PauseTokenSource();
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // pre-canceled

            try
            {
                // Act
                await downloadService.StartDownloadAsync(
                    url: "http://example.com/cancel.bin",
                    savePath: tempPath,
                    progressReporter: progressReporter,
                    pauseToken: pauseToken,
                    speedLimitProvider: () => 0,
                    cancellationToken: cts.Token);

                await Task.Delay(50);

                // Assert
                lastProgress.Should().NotBeNull();
                lastProgress!.Status.Should().Be("Canceled");
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [Fact]
        public async Task StartDownloadAsync_NetworkFailure_SurfacesErrorStatus()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(req =>
            {
                throw new HttpRequestMessageException("Simulated network failure");
            });

            var httpClient = new HttpClient(handler);
            var mockNetworkService = CreateMock<INetworkService>();
            var mockSettingsService = CreateMock<ISettingsService>();

            var downloadService = new DownloadService(httpClient, mockNetworkService.Object, mockSettingsService.Object);
            var tempPath = Path.Combine(Path.GetTempPath(), "EDM_ErrTest_" + Guid.NewGuid() + ".tmp");

            DownloadProgressInfo? lastProgress = null;
            var progressReporter = new Progress<DownloadProgressInfo>(info =>
            {
                lastProgress = info;
            });

            var pauseToken = new PauseTokenSource();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            try
            {
                // Act
                await downloadService.StartDownloadAsync(
                    url: "http://example.com/networkerror.bin",
                    savePath: tempPath,
                    progressReporter: progressReporter,
                    pauseToken: pauseToken,
                    speedLimitProvider: () => 0,
                    cancellationToken: cts.Token);

                await Task.Delay(50);

                // Assert
                lastProgress.Should().NotBeNull();
                lastProgress!.Status.Should().Be("Error");
                lastProgress.ErrorMessage.Should().NotBeNullOrEmpty();
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [Fact]
        public async Task StartDownloadAsync_DiskWriteFailure_SurfacesErrorStatus()
        {
            // Arrange - invalid drive path to trigger IOException on write
            var testData = Encoding.UTF8.GetBytes("Test data");
            var handler = new FakeHttpMessageHandler(req =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(testData)
                };
                resp.Content.Headers.ContentLength = testData.Length;
                return resp;
            });

            var httpClient = new HttpClient(handler);
            var mockNetworkService = CreateMock<INetworkService>();
            var mockSettingsService = CreateMock<ISettingsService>();

            var downloadService = new DownloadService(httpClient, mockNetworkService.Object, mockSettingsService.Object);
            // Invalid non-existent path on Windows
            var invalidPath = @"Z:\NonExistentDriveDirectoryPath_EDM\invalid.file";

            DownloadProgressInfo? lastProgress = null;
            var progressReporter = new Progress<DownloadProgressInfo>(info =>
            {
                lastProgress = info;
            });

            var pauseToken = new PauseTokenSource();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            // Act
            await downloadService.StartDownloadAsync(
                url: "http://example.com/diskerror.bin",
                savePath: invalidPath,
                progressReporter: progressReporter,
                pauseToken: pauseToken,
                speedLimitProvider: () => 0,
                cancellationToken: cts.Token);

            await Task.Delay(50);

            // Assert
            lastProgress.Should().NotBeNull();
            lastProgress!.Status.Should().Be("Error");
            lastProgress.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        private class HttpRequestMessageException : HttpRequestException
        {
            public HttpRequestMessageException(string message) : base(message) { }
        }
    }
}
