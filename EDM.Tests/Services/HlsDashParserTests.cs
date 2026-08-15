using System;
using System.Linq;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class HlsDashParserTests : TestBase
    {
        [Fact]
        public void HlsParser_ParsesMasterPlaylistVariantsAndResolutions()
        {
            // Arrange
            string masterM3u8 = @"#EXTM3U
#EXT-X-VERSION:3
#EXT-X-STREAM-INF:BANDWIDTH=800000,RESOLUTION=640x360,CODECS=""avc1.4d401e,mp4a.40.2""
360p.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=1400000,RESOLUTION=1280x720,CODECS=""avc1.4d401f,mp4a.40.2""
720p.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=2800000,RESOLUTION=1920x1080,CODECS=""avc1.64002a,mp4a.40.2""
1080p.m3u8";

            var baseUri = new Uri("https://cdn.example.com/video/master.m3u8");

            // Act
            var playlist = HlsParser.Parse(masterM3u8, baseUri);

            // Assert
            playlist.IsMaster.Should().BeTrue();
            playlist.IsDrmProtected.Should().BeFalse();
            playlist.Variants.Should().HaveCount(3);

            var highest = playlist.Variants.OrderByDescending(v => v.Bandwidth).First();
            highest.Width.Should().Be(1920);
            highest.Height.Should().Be(1080);
            highest.Uri.Should().Be("https://cdn.example.com/video/1080p.m3u8");
        }

        [Fact]
        public void HlsParser_DetectsDrmProtection()
        {
            // Arrange
            string drmM3u8 = @"#EXTM3U
#EXT-X-VERSION:3
#EXT-X-KEY:METHOD=SAMPLE-AES,URI=""https://keys.example.com/key""
#EXTINF:10.0,
segment1.ts";

            var baseUri = new Uri("https://cdn.example.com/video/media.m3u8");

            // Act
            var playlist = HlsParser.Parse(drmM3u8, baseUri);

            // Assert
            playlist.IsDrmProtected.Should().BeTrue("Must detect SAMPLE-AES DRM key protection");
        }

        [Fact]
        public void DashParser_ParsesRepresentationsAndSegments()
        {
            // Arrange
            string dashXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet mimeType=""video/mp4"">
      <Representation id=""1080p"" bandwidth=""4000000"" width=""1920"" height=""1080"">
        <Initialization sourceURL=""init_1080p.mp4""/>
      </Representation>
    </AdaptationSet>
    <AdaptationSet mimeType=""audio/mp4"">
      <Representation id=""audio_eng"" bandwidth=""128000"">
        <Initialization sourceURL=""init_audio.m4s""/>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

            var baseUri = new Uri("https://cdn.example.com/dash/manifest.mpd");

            // Act
            var manifest = DashParser.Parse(dashXml, baseUri);

            // Assert
            manifest.IsDrmProtected.Should().BeFalse();
            manifest.VideoRepresentations.Should().HaveCount(1);
            manifest.AudioRepresentations.Should().HaveCount(1);

            var video = manifest.VideoRepresentations[0];
            video.Width.Should().Be(1920);
            video.Height.Should().Be(1080);
            video.SegmentUrls[0].Should().Be("https://cdn.example.com/dash/init_1080p.mp4");
        }
    }
}
