using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class MediaAndSiteGrabberTests
    {
        [Fact]
        public void MediaDownloadService_CategorizesMediaTypesCorrectly()
        {
            MediaDownloadService.CategorizeMime("video/mp4", "http://example.com/video.mp4").Should().Be(MediaType.Video);
            MediaDownloadService.CategorizeMime("audio/mpeg", "http://example.com/song.mp3").Should().Be(MediaType.Audio);
            MediaDownloadService.CategorizeMime("image/png", "http://example.com/photo.png").Should().Be(MediaType.Image);
            MediaDownloadService.CategorizeMime("application/vnd.apple.mpegurl", "http://example.com/playlist.m3u8").Should().Be(MediaType.Manifest);
            MediaDownloadService.CategorizeMime("text/vtt", "http://example.com/subtitles.vtt").Should().Be(MediaType.Subtitle);
        }

        [Fact]
        public void MediaDownloadService_RejectsDrmStreams()
        {
            var service = new MediaDownloadService();
            bool result = service.TryRegisterMedia("https://example.com/stream.mpd?widevine=true", "application/dash+xml", "https://example.com");
            result.Should().BeFalse();
            service.GetDetectedMedia().Should().BeEmpty();
        }

        [Fact]
        public void SiteGrabberService_NormalizeUrl_StripsFragmentsAndTrackingQueryParameters()
        {
            string raw = "https://example.com/page.html?utm_source=google&utm_medium=cpc&id=123#section2";
            string normalized = SiteGrabberService.NormalizeUrl(raw);

            normalized.Should().NotContain("utm_source");
            normalized.Should().NotContain("utm_medium");
            normalized.Should().NotContain("#section2");
            normalized.Should().Contain("id=123");
        }

        [Fact]
        public void SiteGrabberService_GrabberScanOptions_HasDefaultMaxPagesCap()
        {
            var options = new GrabberScanOptions();
            options.MaxPagesScanned.Should().Be(500);
            options.MaxDepth.Should().Be(2);
        }
    }
}
