using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class SiteGrabberTests : TestBase
    {
        [Fact]
        public void NormalizeUrl_StripsFragmentsAndTrackingParams()
        {
            // Arrange
            string rawUrl = "https://example.com/page.html?utm_source=twitter&utm_medium=cpc&id=123#section2";

            // Act
            string normalized = SiteGrabberService.NormalizeUrl(rawUrl);

            // Assert
            normalized.Should().Be("https://example.com/page.html?id=123");
        }

        [Fact]
        public async Task SiteGrabberProject_SaveAndLoad_PersistsCorrectly()
        {
            // Arrange
            string tempFile = Path.Combine(Path.GetTempPath(), "grabber_project_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var project = new SiteGrabberProject
                {
                    Name = "Test Media Project",
                    TargetUrl = "https://example.com/gallery",
                    Options = new GrabberScanOptions { MaxDepth = 3, SameDomainOnly = true },
                    DiscoveredItems = new System.Collections.Generic.List<SiteGrabberItemResult>
                    {
                        new SiteGrabberItemResult { Url = "https://example.com/video.mp4", Extension = ".mp4", FileSizeBytes = 1048576 }
                    }
                };

                // Act
                await project.SaveToFileAsync(tempFile);
                var loaded = await SiteGrabberProject.LoadFromFileAsync(tempFile);

                // Assert
                loaded.Should().NotBeNull();
                loaded!.Name.Should().Be("Test Media Project");
                loaded.Options.MaxDepth.Should().Be(3);
                loaded.DiscoveredItems.Should().HaveCount(1);
                loaded.DiscoveredItems[0].Url.Should().Be("https://example.com/video.mp4");
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
