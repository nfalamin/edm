using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class ForensicPhases8To10Tests : TestBase
    {
        // ==================== PHASE 8: HLS / DASH AUDIT ====================

        [Fact]
        public void Phase8_HlsParser_ExtractsVariantsAndDetectsDrm()
        {
            // Master playlist with 1080p, 720p, 480p variants
            string masterM3u8 = @"#EXTM3U
#EXT-X-VERSION:3
#EXT-X-STREAM-INF:BANDWIDTH=800000,RESOLUTION=640x360
360p.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=1400000,RESOLUTION=1280x720
720p.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=2800000,RESOLUTION=1920x1080
1080p.m3u8";

            var baseUri = new Uri("https://media.example.com/hls/master.m3u8?token=xyz123");
            var master = HlsParser.Parse(masterM3u8, baseUri);

            master.IsMaster.Should().BeTrue();
            master.IsDrmProtected.Should().BeFalse();
            master.Variants.Should().HaveCount(3);
            master.Variants[2].Uri.Should().Be("https://media.example.com/hls/1080p.m3u8");

            // Media playlist with segment URLs
            string mediaM3u8 = @"#EXTM3U
#EXT-X-TARGETDURATION:10
#EXTINF:10.0,
seg0.ts
#EXTINF:10.0,
seg1.ts";

            var media = HlsParser.Parse(mediaM3u8, new Uri("https://media.example.com/hls/1080p.m3u8"));
            media.IsMaster.Should().BeFalse();
            media.SegmentUrls.Should().HaveCount(2);
            media.SegmentUrls[0].Should().Be("https://media.example.com/hls/seg0.ts");

            // Encrypted/DRM playlist test
            string drmM3u8 = @"#EXTM3U
#EXT-X-KEY:METHOD=SAMPLE-AES,URI=""https://keys.example.com/key""
#EXTINF:10.0,
seg0.ts";

            var drm = HlsParser.Parse(drmM3u8, baseUri);
            drm.IsDrmProtected.Should().BeTrue("Must flag DRM protection when SAMPLE-AES key tag is present");
        }

        [Fact]
        public void Phase8_DashParser_ExtractsRepresentationsAndDetectsContentProtection()
        {
            string dashXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet mimeType=""video/mp4"">
      <ContentProtection schemeIdUri=""urn:uuid:edef8ba9-79d6-4ace-a3c8-27dcd51d21ed""/>
      <Representation id=""1080p"" bandwidth=""4000000"" width=""1920"" height=""1080"">
        <Initialization sourceURL=""init_1080p.mp4""/>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

            var manifest = DashParser.Parse(dashXml, new Uri("https://media.example.com/dash/manifest.mpd"));

            manifest.IsDrmProtected.Should().BeTrue("Must flag DRM when Widevine ContentProtection tag is present");
            manifest.VideoRepresentations.Should().HaveCount(1);
            manifest.VideoRepresentations[0].Width.Should().Be(1920);
        }

        // ==================== PHASE 9: SITE GRABBER LOCAL AUDIT ====================

        [Fact]
        public async Task Phase9_SiteGrabber_LocalHttpServer_PreventsInfiniteCrawlingAndStripsTracking()
        {
            using var listener = new HttpListener();
            int port = Random.Shared.Next(30000, 34999);
            string rootPrefix = $"http://127.0.0.1:{port}/";
            string scanUrl = $"http://127.0.0.1:{port}/crawl/page1";
            listener.Prefixes.Add(rootPrefix);
            listener.Start();

            // Local test server with cyclic links: /crawl/page1 -> /crawl/page2 -> /crawl/page1
            var serverTask = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var ctx = await listener.GetContextAsync();
                        string path = ctx.Request.Url?.AbsolutePath ?? "";
                        string html = "";

                        if (path.EndsWith("robots.txt", StringComparison.OrdinalIgnoreCase))
                        {
                            html = "User-agent: *\nDisallow: /crawl/blocked/";
                        }
                        else if (path.Contains("page2"))
                        {
                            html = @"<html><body>
                                <a href=""/crawl/page1?utm_source=twitter"">Cyclic Link Back</a>
                                <a href=""/crawl/video2.mp4"">Video 2</a>
                            </body></html>";
                        }
                        else
                        {
                            html = @"<html><body>
                                <a href=""/crawl/page2?fbclid=123#fragment"">Page 2</a>
                                <img src=""/crawl/image1.png""/>
                                <a href=""/crawl/blocked/file.zip"">Blocked File</a>
                            </body></html>";
                        }

                        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(html);
                        ctx.Response.ContentType = "text/html";
                        ctx.Response.ContentLength64 = bytes.Length;
                        await ctx.Response.OutputStream.WriteAsync(bytes);
                        ctx.Response.Close();
                    }
                    catch { break; }
                }
            });

            try
            {
                using var client = new HttpClient();
                var grabber = new SiteGrabberService(client);
                var options = new GrabberScanOptions
                {
                    MaxDepth = 3,
                    SameDomainOnly = true,
                    RespectRobotsTxt = true
                };

                // Act: Scan page1
                var items = await grabber.ScanSiteAsync(scanUrl, options);

                // Assert
                items.Should().NotBeNull();
                items.Should().Contain(i => i.Url.Contains("image1.png"));
                items.Should().Contain(i => i.Url.Contains("video2.mp4"));
                items.Should().NotContain(i => i.Url.Contains("blocked/file.zip"), "Robots.txt disallowed paths must be excluded");

                // URL normalization check: utm_source, fbclid, and #fragment must be stripped
                items.All(i => !i.Url.Contains("utm_source") && !i.Url.Contains("fbclid") && !i.Url.Contains("#")).Should().BeTrue();
            }
            finally
            {
                listener.Stop();
            }
        }

        // ==================== PHASE 10: SECURITY RED TEAM AUDIT ====================

        [Theory]
        [InlineData("../../Windows/System32/cmd.exe")]
        [InlineData("%2e%2e/%2e%2e/Windows/System32/cmd.exe")]
        [InlineData(@"\\127.0.0.1\c$\Windows\System32\cmd.exe")]
        [InlineData("CON.txt")]
        [InlineData("PRN.exe")]
        [InlineData("AUX.dll")]
        [InlineData("NUL.zip")]
        public void Phase10_SecurityRedTeam_RejectsPathTraversalAndReservedNames(string dangerousPath)
        {
            // Act
            string sanitizedName = SecuritySanitizer.SanitizeFileName(dangerousPath);
            bool isSafeDestination = SecuritySanitizer.TrySanitizeDestinationPath(Path.GetTempPath(), dangerousPath, out string safePath);

            // Assert
            sanitizedName.Should().NotStartWith("..");
            sanitizedName.Should().NotContain("/");
            sanitizedName.Should().NotContain(@"\");
            if (dangerousPath.Contains("CON") || dangerousPath.Contains("PRN") || dangerousPath.Contains("AUX") || dangerousPath.Contains("NUL"))
            {
                sanitizedName.Should().StartWith("_", "Reserved Windows device names must be prefixed");
            }

            isSafeDestination.Should().BeFalse("Path traversal outside base temp directory must be rejected");
            safePath.Should().BeEmpty();
        }

        [Theory]
        [InlineData("javascript:alert('XSS')")]
        [InlineData("file:///C:/Windows/System32/calc.exe")]
        [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
        [InlineData("powershell:Start-Process calc.exe")]
        [InlineData("cmd:/c calc.exe")]
        public void Phase10_SecurityRedTeam_RejectsDangerousUrlSchemes(string dangerousUrl)
        {
            // Act
            bool isAllowed = SecuritySanitizer.IsAllowedUrlScheme(dangerousUrl);

            // Assert
            isAllowed.Should().BeFalse($"Url scheme '{dangerousUrl}' must be rejected as an unallowed dangerous scheme");
        }
    }
}
