using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class SiteGrabberServiceTests : TestBase
    {
        private class RouteHttpMessageHandler : HttpMessageHandler
        {
            private readonly Dictionary<string, string> _routes;

            public RouteHttpMessageHandler(Dictionary<string, string> routes)
            {
                _routes = routes;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var url = request.RequestUri?.ToString() ?? string.Empty;
                if (_routes.TryGetValue(url, out var html))
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
                    };
                    return Task.FromResult(response);
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }

        [Fact]
        public async Task ScanPageAsync_ExtractsAndFiltersUrlsWithDesiredExtensions()
        {
            // Arrange
            var htmlFixture = @"
<!DOCTYPE html>
<html>
<body>
    <a href='/downloads/setup.exe'>Setup</a>
    <a href='https://cdn.example.com/video.mp4'>Video</a>
    <a href='music/song.mp3'>Audio</a>
    <a href='/about-us.html'>About Us</a>
    <img src='images/banner.jpg' />
    <video src='https://cdn.example.com/movie.webm'></video>
    <source src='media/audio.flac' />
</body>
</html>";

            var routes = new Dictionary<string, string> { { "https://example.com/downloads/index.html", htmlFixture } };
            var handler = new RouteHttpMessageHandler(routes);
            var httpClient = new HttpClient(handler);
            var siteGrabber = new SiteGrabberService(httpClient);

            var pageUrl = "https://example.com/downloads/index.html";

            // Act - SameDomainOnly = false so external cdn.example.com links pass
            var options = new GrabberScanOptions { MaxDepth = 1, SameDomainOnly = false };
            var results = await siteGrabber.ScanSiteAsync(pageUrl, options, progress: null, CancellationToken.None);

            // Assert
            results.Should().NotBeNull();
            results.Should().HaveCount(6);

            var urls = results.ConvertAll(r => r.Url);
            urls.Should().Contain("https://example.com/downloads/setup.exe");
            urls.Should().Contain("https://cdn.example.com/video.mp4");
            urls.Should().Contain("https://example.com/downloads/music/song.mp3");
            urls.Should().Contain("https://example.com/downloads/images/banner.jpg");
            urls.Should().Contain("https://cdn.example.com/movie.webm");
            urls.Should().Contain("https://example.com/downloads/media/audio.flac");

            urls.Should().NotContain("https://example.com/about-us.html");
        }

        [Fact]
        public async Task SameDomainOnly_RestrictsExternalDomainLinks()
        {
            // Arrange
            var html = @"
<html>
<body>
    <a href='https://example.com/file1.pdf'>Internal PDF</a>
    <a href='https://external.com/file2.pdf'>External PDF</a>
</body>
</html>";

            var routes = new Dictionary<string, string> { { "https://example.com/page.html", html } };
            var handler = new RouteHttpMessageHandler(routes);
            var siteGrabber = new SiteGrabberService(new HttpClient(handler));

            var options = new GrabberScanOptions { MaxDepth = 1, SameDomainOnly = true };

            // Act
            var results = await siteGrabber.ScanSiteAsync("https://example.com/page.html", options, progress: null, CancellationToken.None);

            // Assert
            results.Should().HaveCount(1);
            results[0].Url.Should().Be("https://example.com/file1.pdf");
        }

        [Fact]
        public async Task IncludeExtensions_FiltersByExtension()
        {
            // Arrange
            var html = @"
<html>
<body>
    <a href='/doc.pdf'>Document</a>
    <a href='/video.mp4'>Video</a>
    <a href='/app.exe'>Installer</a>
</body>
</html>";

            var routes = new Dictionary<string, string> { { "https://example.com/index.html", html } };
            var handler = new RouteHttpMessageHandler(routes);
            var siteGrabber = new SiteGrabberService(new HttpClient(handler));

            var options = new GrabberScanOptions
            {
                MaxDepth = 1,
                IncludeExtensions = new List<string> { ".pdf" }
            };

            // Act
            var results = await siteGrabber.ScanSiteAsync("https://example.com/index.html", options, progress: null, CancellationToken.None);

            // Assert
            results.Should().HaveCount(1);
            results[0].Url.Should().Be("https://example.com/doc.pdf");
            results[0].Extension.Should().Be(".pdf");
        }

        [Fact]
        public async Task CrawlDepth_FollowsLinksUpToMaxDepth()
        {
            // Arrange
            var page1Html = @"<html><body><a href='page2.html'>Page 2</a><a href='file1.pdf'>File 1</a></body></html>";
            var page2Html = @"<html><body><a href='file2.pdf'>File 2</a></body></html>";

            var routes = new Dictionary<string, string>
            {
                { "https://example.com/page1.html", page1Html },
                { "https://example.com/page2.html", page2Html }
            };

            var handler = new RouteHttpMessageHandler(routes);
            var siteGrabber = new SiteGrabberService(new HttpClient(handler));

            var optionsDepth1 = new GrabberScanOptions { MaxDepth = 1, SameDomainOnly = true };
            var optionsDepth2 = new GrabberScanOptions { MaxDepth = 2, SameDomainOnly = true };

            // Act
            var results1 = await siteGrabber.ScanSiteAsync("https://example.com/page1.html", optionsDepth1, progress: null, CancellationToken.None);
            var results2 = await siteGrabber.ScanSiteAsync("https://example.com/page1.html", optionsDepth2, progress: null, CancellationToken.None);

            // Assert
            results1.Should().HaveCount(1); // File 1 only
            results2.Should().HaveCount(2); // File 1 and File 2
        }

        [Fact]
        public async Task ScanPageAsync_WithNullOrEmptyUrl_ThrowsArgumentNullException()
        {
            // Arrange
            var siteGrabber = new SiteGrabberService();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => siteGrabber.ScanPageAsync(""));
        }
    }
}
