using System;
using System.Diagnostics;
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
    public class DownloadE2ETests : IAsyncLifetime
    {
        private LocalHttpTestServer _server = null!;
        private string _tempDir = null!;

        public async Task InitializeAsync()
        {
            _server = new LocalHttpTestServer();
            _tempDir = Path.Combine(Path.GetTempPath(), "EDM_E2E_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            await Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            await _server.DisposeAsync();
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch { }
        }

        [Fact]
        public async Task Download_SmallFile_Passes_Sha256()
        {
            string url = $"{_server.BaseUrl}small.bin";
            string savePath = Path.Combine(_tempDir, "small.bin");
            var service = new DownloadService();
            var pauseSource = new PauseTokenSource();

            await service.StartDownloadAsync(
                url,
                savePath,
                new Progress<DownloadProgressInfo>(),
                pauseSource,
                () => -1,
                CancellationToken.None,
                segmentCount: 4
            );

            File.Exists(savePath).Should().BeTrue();
            new FileInfo(savePath).Length.Should().Be(_server.SmallData.Length);

            string actualSha256 = ComputeSha256(savePath);
            string expectedSha256 = _server.GetExpectedSha256(_server.SmallData);
            actualSha256.Should().Be(expectedSha256);
        }

        [Fact]
        public async Task Download_1MbFile_MultiSegment_Passes_Sha256()
        {
            string url = $"{_server.BaseUrl}1mb.bin";
            string savePath = Path.Combine(_tempDir, "1mb.bin");
            var service = new DownloadService();
            var pauseSource = new PauseTokenSource();

            await service.StartDownloadAsync(
                url,
                savePath,
                new Progress<DownloadProgressInfo>(),
                pauseSource,
                () => -1,
                CancellationToken.None,
                segmentCount: 8
            );

            File.Exists(savePath).Should().BeTrue();
            new FileInfo(savePath).Length.Should().Be(_server.OneMbData.Length);

            string actualSha256 = ComputeSha256(savePath);
            string expectedSha256 = _server.GetExpectedSha256(_server.OneMbData);
            actualSha256.Should().Be(expectedSha256);
        }

        [Fact]
        public async Task Download_10MbFile_MultiSegment_Passes_Sha256()
        {
            string url = $"{_server.BaseUrl}10mb.bin";
            string savePath = Path.Combine(_tempDir, "10mb.bin");
            var service = new DownloadService();
            var pauseSource = new PauseTokenSource();

            await service.StartDownloadAsync(
                url,
                savePath,
                new Progress<DownloadProgressInfo>(),
                pauseSource,
                () => -1,
                CancellationToken.None,
                segmentCount: 8
            );

            File.Exists(savePath).Should().BeTrue();
            new FileInfo(savePath).Length.Should().Be(_server.TenMbData.Length);

            string actualSha256 = ComputeSha256(savePath);
            string expectedSha256 = _server.GetExpectedSha256(_server.TenMbData);
            actualSha256.Should().Be(expectedSha256);
        }

        [Fact]
        public async Task Download_NoRange_FallsBackToSingleThread_Passes_Sha256()
        {
            string url = $"{_server.BaseUrl}no-range.bin";
            string savePath = Path.Combine(_tempDir, "no-range.bin");
            var service = new DownloadService();
            var pauseSource = new PauseTokenSource();

            await service.StartDownloadAsync(
                url,
                savePath,
                new Progress<DownloadProgressInfo>(),
                pauseSource,
                () => -1,
                CancellationToken.None,
                segmentCount: 8
            );

            File.Exists(savePath).Should().BeTrue();
            new FileInfo(savePath).Length.Should().Be(_server.NoRangeData.Length);

            string actualSha256 = ComputeSha256(savePath);
            string expectedSha256 = _server.GetExpectedSha256(_server.NoRangeData);
            actualSha256.Should().Be(expectedSha256);
        }

        [Fact]
        public async Task Download_Redirect_FollowsAndCompletes_Passes_Sha256()
        {
            string url = $"{_server.BaseUrl}redirect.bin";
            string savePath = Path.Combine(_tempDir, "redirect_resolved.bin");
            var service = new DownloadService();
            var pauseSource = new PauseTokenSource();

            await service.StartDownloadAsync(
                url,
                savePath,
                new Progress<DownloadProgressInfo>(),
                pauseSource,
                () => -1,
                CancellationToken.None,
                segmentCount: 4
            );

            File.Exists(savePath).Should().BeTrue();
            new FileInfo(savePath).Length.Should().Be(_server.OneMbData.Length);

            string actualSha256 = ComputeSha256(savePath);
            string expectedSha256 = _server.GetExpectedSha256(_server.OneMbData);
            actualSha256.Should().Be(expectedSha256);
        }

        [Fact]
        public async Task Download_Authentication_PassesWithCredentials()
        {
            string url = $"{_server.BaseUrl}auth.bin";
            string savePath = Path.Combine(_tempDir, "auth.bin");
            var service = new DownloadService();
            var pauseSource = new PauseTokenSource();
            var creds = new DownloadCredentials("user", "pass");

            await service.StartDownloadAsync(
                url,
                savePath,
                new Progress<DownloadProgressInfo>(),
                pauseSource,
                () => -1,
                CancellationToken.None,
                segmentCount: 2,
                credentials: creds
            );

            File.Exists(savePath).Should().BeTrue();
            new FileInfo(savePath).Length.Should().Be(_server.SmallData.Length);
        }

        [Fact]
        public async Task Download_Cookie_PassesWithCookies()
        {
            string url = $"{_server.BaseUrl}cookie.bin";
            string savePath = Path.Combine(_tempDir, "cookie.bin");
            var service = new DownloadService();
            var pauseSource = new PauseTokenSource();

            await service.StartDownloadAsync(
                url,
                savePath,
                new Progress<DownloadProgressInfo>(),
                pauseSource,
                () => -1,
                CancellationToken.None,
                segmentCount: 2,
                cookies: "session_token=edm_valid_token_123"
            );

            File.Exists(savePath).Should().BeTrue();
            new FileInfo(savePath).Length.Should().Be(_server.SmallData.Length);
        }

        [Fact]
        public async Task Download_Retry_RecoversFrom503()
        {
            string url = $"{_server.BaseUrl}retry.bin";
            string savePath = Path.Combine(_tempDir, "retry.bin");
            var service = new DownloadService();
            var pauseSource = new PauseTokenSource();

            await service.StartDownloadAsync(
                url,
                savePath,
                new Progress<DownloadProgressInfo>(),
                pauseSource,
                () => -1,
                CancellationToken.None,
                segmentCount: 2
            );

            File.Exists(savePath).Should().BeTrue();
            new FileInfo(savePath).Length.Should().Be(_server.SmallData.Length);
        }

        [Fact]
        public async Task Download_SpeedLimiter_LimitsThroughputEmpirically()
        {
            string url = $"{_server.BaseUrl}range.bin";
            string savePath = Path.Combine(_tempDir, "throttled.bin");
            var service = new DownloadService();
            var pauseSource = new PauseTokenSource();

            // Set global throttle to 250 KB/s
            BandwidthThrottler.Instance.SetLimit(250);

            var sw = Stopwatch.StartNew();
            try
            {
                await service.StartDownloadAsync(
                    url,
                    savePath,
                    new Progress<DownloadProgressInfo>(),
                    pauseSource,
                    () => 250.0 * 1024,
                    CancellationToken.None,
                    segmentCount: 4
                );
            }
            finally
            {
                // Reset throttle to unlimited
                BandwidthThrottler.Instance.SetLimit(0);
            }
            sw.Stop();

            File.Exists(savePath).Should().BeTrue();
            new FileInfo(savePath).Length.Should().Be(_server.RangeData.Length);

            // 2MB at 250 KB/s takes ~ 4-8 seconds
            sw.Elapsed.TotalSeconds.Should().BeGreaterThan(2.0);
        }

        [Fact]
        public async Task Download_32Segments_StressTest_Passes_Sha256()
        {
            string url = $"{_server.BaseUrl}10mb.bin";
            string savePath = Path.Combine(_tempDir, "32seg_10mb.bin");
            var service = new DownloadService();
            var pauseSource = new PauseTokenSource();

            await service.StartDownloadAsync(
                url,
                savePath,
                new Progress<DownloadProgressInfo>(),
                pauseSource,
                () => -1,
                CancellationToken.None,
                segmentCount: 32
            );

            File.Exists(savePath).Should().BeTrue();
            new FileInfo(savePath).Length.Should().Be(_server.TenMbData.Length);

            string actualSha256 = ComputeSha256(savePath);
            string expectedSha256 = _server.GetExpectedSha256(_server.TenMbData);
            actualSha256.Should().Be(expectedSha256);
        }

        [Fact]
        public async Task Download_SimultaneousDownloads_CompleteConcurrently()
        {
            var tasks = new List<Task>();
            var paths = new List<string>();

            for (int i = 0; i < 5; i++)
            {
                int index = i;
                string url = $"{_server.BaseUrl}1mb.bin";
                string savePath = Path.Combine(_tempDir, $"concurrent_{index}.bin");
                paths.Add(savePath);

                var service = new DownloadService();
                var pauseSource = new PauseTokenSource();

                tasks.Add(service.StartDownloadAsync(
                    url,
                    savePath,
                    new Progress<DownloadProgressInfo>(),
                    pauseSource,
                    () => -1,
                    CancellationToken.None,
                    segmentCount: 4
                ));
            }

            await Task.WhenAll(tasks);

            foreach (var savePath in paths)
            {
                File.Exists(savePath).Should().BeTrue();
                new FileInfo(savePath).Length.Should().Be(_server.OneMbData.Length);
                string actualSha256 = ComputeSha256(savePath);
                string expectedSha256 = _server.GetExpectedSha256(_server.OneMbData);
                actualSha256.Should().Be(expectedSha256);
            }
        }

        [Fact]
        public async Task Download_PauseResumeStorm_SucceedsWithExactChecksum()
        {
            string url = $"{_server.BaseUrl}10mb.bin";
            string savePath = Path.Combine(_tempDir, "storm_10mb.bin");
            var service = new DownloadService();
            var pauseSource = new PauseTokenSource();

            var downloadTask = service.StartDownloadAsync(
                url,
                savePath,
                new Progress<DownloadProgressInfo>(),
                pauseSource,
                () => -1,
                CancellationToken.None,
                segmentCount: 8
            );

            // Execute rapid pause/resume cycles during active streaming
            for (int i = 0; i < 4; i++)
            {
                await Task.Delay(100);
                pauseSource.Pause();
                await Task.Delay(50);
                pauseSource.Resume();
            }

            await downloadTask;

            File.Exists(savePath).Should().BeTrue();
            new FileInfo(savePath).Length.Should().Be(_server.TenMbData.Length);
            string actualSha256 = ComputeSha256(savePath);
            string expectedSha256 = _server.GetExpectedSha256(_server.TenMbData);
            actualSha256.Should().Be(expectedSha256);
        }

        private static string ComputeSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(filePath);
            var hash = sha.ComputeHash(fs);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
