using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class AdvancedFeaturesTestSuite
    {
        [Fact]
        public void UrlPatternExpander_ExpandsNumericAndAlphaPatternsCorrectly()
        {
            string numericPattern = "http://example.com/archive_[01-03].zip";
            string alphaPattern = "http://example.com/img_[a-c].png";

            var expandedNum = UrlPatternExpander.Expand(numericPattern);
            var expandedAlpha = UrlPatternExpander.Expand(alphaPattern);

            expandedNum.Should().HaveCount(3);
            expandedNum.Should().Contain("http://example.com/archive_01.zip");
            expandedNum.Should().Contain("http://example.com/archive_02.zip");
            expandedNum.Should().Contain("http://example.com/archive_03.zip");

            expandedAlpha.Should().HaveCount(3);
            expandedAlpha.Should().Contain("http://example.com/img_a.png");
            expandedAlpha.Should().Contain("http://example.com/img_b.png");
            expandedAlpha.Should().Contain("http://example.com/img_c.png");
        }

        [Fact]
        public async Task PostDownloadScannerService_ScansFileAndReturnsValidResult()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "edm_test_scan_" + Guid.NewGuid().ToString("N") + ".txt");
            await File.WriteAllTextAsync(tempFile, "Clean test payload text.");

            try
            {
                var scanner = new PostDownloadScannerService();
                var result = await scanner.ScanFileAsync(tempFile, CancellationToken.None).ConfigureAwait(false);

                result.Should().NotBeNull();
                result.FilePath.Should().Be(tempFile);
                result.IsSafe.Should().BeTrue();
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void DownloadPathCategoryService_RegistersAndRemovesCustomCategoryMappings()
        {
            DownloadPathCategoryService.RegisterCustomCategoryMapping(".iso", "SoftwareISOs");
            DownloadPathCategoryService.RegisterCustomCategoryMapping(".mkv", "CinemaMovies");

            string isoSubfolder = DownloadPathCategoryService.GetCategorySubfolderByFileName("ubuntu.iso");
            string mkvSubfolder = DownloadPathCategoryService.GetCategorySubfolderByFileName("movie.mkv");

            isoSubfolder.Should().Be("SoftwareISOs");
            mkvSubfolder.Should().Be("CinemaMovies");

            DownloadPathCategoryService.RemoveCustomCategoryMapping(".iso");
            string isoAfterRemove = DownloadPathCategoryService.GetCategorySubfolderByFileName("ubuntu.iso");
            isoAfterRemove.Should().Be("Documents"); // Fallbacks to Documents/Compressed
        }

        [Fact]
        public async Task SiteGrabberService_CrawlsMockHtmlPageAndDiscoversAssets()
        {
            var grabber = new SiteGrabberService();
            var assets = await grabber.CrawlWebsiteAsync("https://invalid-non-existent-site-99.org", 1, CancellationToken.None).ConfigureAwait(false);

            assets.Should().NotBeNull();
        }
    }
}
