using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class P2PAndAnalyticsTestSuite : IDisposable
    {
        private readonly string _testDir;

        public P2PAndAnalyticsTestSuite()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "EDM_P2PTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch { }
        }

        #region Feature 1: P2P Local LAN Sharing Tests
        [Fact]
        public void LanP2PSharing_RegistersSharedFile_ComputesSha256Accurately()
        {
            // Arrange
            using var engine = new LanP2PSharingEngine();
            var sampleFile = Path.Combine(_testDir, "shared_document.pdf");
            File.WriteAllText(sampleFile, "Sample P2P Shared Content 12345");

            // Act
            engine.RegisterSharedFile(sampleFile);
            var expectedHash = LanP2PSharingEngine.ComputeFileSha256(sampleFile);

            // Assert
            expectedHash.Should().NotBeNullOrWhiteSpace();
            expectedHash.Length.Should().Be(64); // SHA-256 hex string length
        }

        [Fact]
        public void LanP2PSharing_TracksDiscoveredPeers_Correctly()
        {
            // Arrange
            using var engine = new LanP2PSharingEngine();
            var peer = new LanPeerNode
            {
                PeerId = "peer-alpha-01",
                MachineName = "DESKTOP-LIVINGROOM",
                IpAddress = "192.168.1.120",
                Port = 45824,
                SharedFileHashes = new() { "a1b2c3d4e5f6" }
            };

            // Act
            engine.RegisterDiscoveredPeer(peer);
            var discovered = engine.GetDiscoveredPeers();

            // Assert
            discovered.Should().HaveCount(1);
            discovered[0].PeerId.Should().Be("peer-alpha-01");
            discovered[0].MachineName.Should().Be("DESKTOP-LIVINGROOM");
            discovered[0].SharedFileHashes.Should().Contain("a1b2c3d4e5f6");
        }
        #endregion

        #region Feature 2: In-App Direct Streaming & Auto-Extractor Tests
        [Fact]
        public async Task AutoExtractor_ExtractsZip_WhenPermissionGranted()
        {
            // Arrange
            var zipPath = Path.Combine(_testDir, "test_archive.zip");
            var extractDest = Path.Combine(_testDir, "unpacked_folder");

            // Create a valid zip archive
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry1 = zip.CreateEntry("file1.txt");
                using var writer = new StreamWriter(entry1.Open());
                writer.Write("Content 1");
            }

            var service = new AutoExtractorAndStreamService(isExtractionPermitted: true);

            // Act
            var result = await service.TryExtractArchiveAsync(zipPath, extractDest);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.ExtractedFileCount.Should().Be(1);
            File.Exists(Path.Combine(extractDest, "file1.txt")).Should().BeTrue();
        }

        [Fact]
        public async Task AutoExtractor_BlocksExtraction_WhenPermissionDenied()
        {
            // Arrange
            var zipPath = Path.Combine(_testDir, "denied_archive.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("doc.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("Denied Doc");
            }

            var service = new AutoExtractorAndStreamService(isExtractionPermitted: false);

            // Act
            var result = await service.TryExtractArchiveAsync(zipPath);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("permission");
        }

        [Fact]
        public void DirectStreaming_GeneratesProperLocalStreamingUrl()
        {
            // Arrange
            var service = new AutoExtractorAndStreamService();
            var videoPath = "C:\\Downloads\\sample_movie.mp4";

            // Act
            var url = service.GetStreamingUrlForPartialFile(videoPath);

            // Assert
            url.Should().StartWith("http://127.0.0.1:45825/edm-stream/play?file=");
            url.Should().Contain("sample_movie.mp4");
        }
        #endregion

        #region Feature 3: Download Analytics & Heatmap Tests
        [Fact]
        public void DownloadAnalytics_TracksDomainBandwidthAndHeatmap_Accurately()
        {
            // Arrange
            var analytics = new DownloadAnalyticsEngine();

            // Act
            analytics.RecordDownloadSample("https://cdn.github.com/archive.zip", 10_000_000, 25_000_000); // 25 MB/s
            analytics.RecordDownloadSample("https://cdn.github.com/release.tar", 5_000_000, 35_000_000);  // 35 MB/s
            analytics.RecordDownloadSample("https://archive.ubuntu.com/iso.img", 20_000_000, 10_000_000); // 10 MB/s

            var report = analytics.GenerateOverviewReport();

            // Assert
            report.TotalBytesDownloadedAllTime.Should().Be(35_000_000);
            report.TopFastestDomains.Should().HaveCount(2);

            var githubMetric = report.TopFastestDomains.First(d => d.Domain == "cdn.github.com");
            githubMetric.TotalBytesDownloaded.Should().Be(15_000_000);
            githubMetric.MaxSpeedBytesPerSec.Should().Be(35_000_000);
            githubMetric.AvgSpeedBytesPerSec.Should().Be(30_000_000);

            report.WeeklySpeedHeatmap.Should().HaveCount(7 * 24);
            report.IspReliabilityGrade.Should().Contain("A");
        }
        #endregion
    }
}
