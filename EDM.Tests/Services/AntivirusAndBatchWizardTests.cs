using System;
using System.IO;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class AntivirusAndBatchWizardTests
    {
        [Fact]
        public void Phase2_UrlPatternExpander_FiltersByCategoriesCorrectly()
        {
            var urls = new[]
            {
                "http://example.com/video1.mp4",
                "http://example.com/song.mp3",
                "http://example.com/document.pdf",
                "http://example.com/archive.zip",
                "http://example.com/image.png"
            };

            var videos = UrlPatternExpander.FilterByCategory(urls, FileTypeCategory.Videos);
            videos.Should().ContainSingle().Which.Should().Be("http://example.com/video1.mp4");

            var audio = UrlPatternExpander.FilterByCategory(urls, FileTypeCategory.Audio);
            audio.Should().ContainSingle().Which.Should().Be("http://example.com/song.mp3");

            var docs = UrlPatternExpander.FilterByCategory(urls, FileTypeCategory.Documents);
            docs.Should().ContainSingle().Which.Should().Be("http://example.com/document.pdf");

            var archives = UrlPatternExpander.FilterByCategory(urls, FileTypeCategory.Archives);
            archives.Should().ContainSingle().Which.Should().Be("http://example.com/archive.zip");

            var images = UrlPatternExpander.FilterByCategory(urls, FileTypeCategory.Images);
            images.Should().ContainSingle().Which.Should().Be("http://example.com/image.png");
        }

        [Fact]
        public async Task Phase3_AntivirusScannerService_ScansFileWithoutCrashing()
        {
            var scanner = new AntivirusScannerService();
            string tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "Clean test payload");

            try
            {
                bool isClean = await scanner.ScanFileAsync(tempFile);
                isClean.Should().BeTrue();
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
