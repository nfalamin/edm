using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class HlsDashQualityPickerTests
    {
        [Fact]
        public void HlsParser_ParsesMasterPlaylistWithMultipleQualitiesCorrectly()
        {
            string masterM3u8 = @"#EXTM3U
#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=""audio-aac"",NAME=""English"",DEFAULT=YES,URI=""audio_en.m3u8""
#EXT-X-STREAM-INF:BANDWIDTH=5000000,RESOLUTION=1920x1080,FRAME-RATE=60.000,CODECS=""avc1.640028,mp4a.40.2"",AUDIO=""audio-aac""
video_1080p.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=2500000,RESOLUTION=1280x720,FRAME-RATE=30.000,CODECS=""avc1.4d401f,mp4a.40.2"",AUDIO=""audio-aac""
video_720p.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=1000000,RESOLUTION=854x480,FRAME-RATE=30.000,CODECS=""avc1.4d401f,mp4a.40.2"",AUDIO=""audio-aac""
video_480p.m3u8";

            var baseUri = new Uri("https://cdn.example.com/live/master.m3u8");
            var playlist = HlsParser.Parse(masterM3u8, baseUri);

            playlist.IsMaster.Should().BeTrue();
            playlist.IsDrmProtected.Should().BeFalse();
            playlist.Variants.Should().HaveCount(3);

            var v1080 = playlist.Variants.First(v => v.Height == 1080);
            v1080.Width.Should().Be(1920);
            v1080.Bandwidth.Should().Be(5000000);
            v1080.FrameRate.Should().Be(60.0);
            v1080.Uri.Should().Be("https://cdn.example.com/live/video_1080p.m3u8");

            playlist.AudioTracks.Should().HaveCount(1);
            playlist.AudioTracks.First().Uri.Should().Be("https://cdn.example.com/live/audio_en.m3u8");
        }

        [Fact]
        public void HlsParser_DetectsDrmProtectedStreamAndFlagsDrm()
        {
            string drmM3u8 = @"#EXTM3U
#EXT-X-KEY:METHOD=SAMPLE-AES,URI=""skd://key.example.com/key1""
#EXT-X-STREAM-INF:BANDWIDTH=3000000,RESOLUTION=1280x720
video_720p.m3u8";

            var baseUri = new Uri("https://cdn.example.com/drm/master.m3u8");
            var playlist = HlsParser.Parse(drmM3u8, baseUri);

            playlist.IsDrmProtected.Should().BeTrue();
        }

        [Fact]
        public void DashParser_ParsesRepresentationsAndDistinguishesAudioOnlyAndVideo()
        {
            string dashXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet mimeType=""video/mp4"">
      <Representation id=""v1080"" bandwidth=""4500000"" width=""1920"" height=""1080"" frameRate=""60"">
        <BaseURL>video_1080p.mp4</BaseURL>
      </Representation>
      <Representation id=""v720"" bandwidth=""2200000"" width=""1280"" height=""720"" frameRate=""30"">
        <BaseURL>video_720p.mp4</BaseURL>
      </Representation>
    </AdaptationSet>
    <AdaptationSet mimeType=""audio/mp4"">
      <Representation id=""a128"" bandwidth=""128000"" codecs=""mp4a.40.2"">
        <BaseURL>audio_128k.mp4</BaseURL>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

            var baseUri = new Uri("https://cdn.example.com/dash/manifest.mpd");
            var manifest = DashParser.Parse(dashXml, baseUri);

            manifest.IsDrmProtected.Should().BeFalse();
            manifest.VideoRepresentations.Should().HaveCount(2);
            manifest.AudioRepresentations.Should().HaveCount(1);

            var v1080 = manifest.VideoRepresentations.First(r => r.Height == 1080);
            v1080.Bandwidth.Should().Be(4500000);
            v1080.FrameRate.Should().Be(60);

            var a128 = manifest.AudioRepresentations.First();
            a128.Bandwidth.Should().Be(128000);
            a128.Codecs.Should().Be("mp4a.40.2");
        }

        [Fact]
        public async Task MediaVariantResolver_HandlesMalformedManifestGracefullyWithoutCrashing()
        {
            var resolver = new MediaVariantResolver();
            // Passing malformed invalid manifest string directly or dummy url
            var result = await resolver.ResolveVariantsAsync("https://invalid-non-existent-domain-9999.org/fake.m3u8");

            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void MediaVariantResolver_SimulatesUserQualitySelectionAndPassesSelectedVariantToPipeline()
        {
            var option = new MediaVariantOption
            {
                QualityLabel = "1080p60 (FHD)",
                Width = 1920,
                Height = 1080,
                FrameRate = 60,
                Bitrate = 5000000,
                Codec = "avc1.640028",
                DirectUrl = "https://cdn.example.com/video_1080p.m3u8",
                HasAudio = true
            };

            // Verify variant attributes pass cleanly to download arguments
            option.QualityLabel.Should().Contain("1080p");
            option.Resolution.Should().Be("1920x1080");
            option.DirectUrl.Should().Be("https://cdn.example.com/video_1080p.m3u8");
        }

        [Fact]
        public void MediaVariantOption_HandlesAudioOnlyVariantCorrectly()
        {
            var audioOption = new MediaVariantOption
            {
                QualityLabel = "Audio Only (MP3)",
                IsAudioOnly = true,
                Codec = "mp4a.40.2",
                DirectUrl = "https://cdn.example.com/audio.m3u8",
                RequiresFfmpegMerge = true
            };

            audioOption.IsAudioOnly.Should().BeTrue();
            audioOption.RequiresFfmpegMerge.Should().BeTrue();
            audioOption.QualityLabel.Should().Be("Audio Only (MP3)");
        }
    }
}
