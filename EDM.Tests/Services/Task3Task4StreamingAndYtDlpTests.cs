using System;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class Task3Task4StreamingAndYtDlpTests
    {
        [Theory]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", true)]
        [InlineData("https://youtu.be/dQw4w9WgXcQ", true)]
        [InlineData("https://vimeo.com/76979871", true)]
        [InlineData("https://www.dailymotion.com/video/x8x123", true)]
        [InlineData("https://fb.watch/xyz123/", true)]
        [InlineData("https://www.tiktok.com/@user/video/123456789", true)]
        [InlineData("https://www.twitch.tv/videos/1234567", true)]
        [InlineData("https://twitter.com/user/status/123456", true)]
        [InlineData("https://x.com/user/status/123456", true)]
        [InlineData("https://example.com/files/archive.zip", false)]
        [InlineData("https://cdn.mydomain.org/downloads/setup.exe", false)]
        [InlineData("https://server.com/images/photo.jpg", false)]
        public void IsVideoStreamingUrl_DetectsDomainsCorrectly(string url, bool expectedIsStreaming)
        {
            bool result = DownloadService.IsVideoStreamingUrl(url);
            result.Should().Be(expectedIsStreaming);
        }

        [Fact]
        public void IsVideoStreamingUrl_HandlesNullOrWhitespace()
        {
            DownloadService.IsVideoStreamingUrl(null!).Should().BeFalse();
            DownloadService.IsVideoStreamingUrl("").Should().BeFalse();
            DownloadService.IsVideoStreamingUrl("   ").Should().BeFalse();
            DownloadService.IsVideoStreamingUrl("not-a-valid-url").Should().BeFalse();
        }
    }
}
