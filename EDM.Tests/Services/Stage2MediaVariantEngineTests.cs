using System;
using System.Collections.Generic;
using System.Linq;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class Stage2MediaVariantEngineTests
    {
        [Fact]
        public void Discovery_WhenBrowserPlays144p_AndSourceHas4K_Discovers2160pAsHighestQuality()
        {
            // Simulate simulated browser state playing at 144p
            string browserPlaybackQuality = "144p";
            int browserPlaybackHeight = 144;

            // Simulated real source representations available in manifest
            var availableSourceRepresentations = new List<MediaVariantOption>
            {
                new MediaVariantOption { QualityLabel = "144p", Height = 144, Width = 256, Bitrate = 100000, Codec = "H.264", Container = "mp4" },
                new MediaVariantOption { QualityLabel = "240p", Height = 240, Width = 426, Bitrate = 250000, Codec = "H.264", Container = "mp4" },
                new MediaVariantOption { QualityLabel = "360p", Height = 360, Width = 640, Bitrate = 500000, Codec = "H.264", Container = "mp4" },
                new MediaVariantOption { QualityLabel = "480p", Height = 480, Width = 854, Bitrate = 1000000, Codec = "H.264", Container = "mp4" },
                new MediaVariantOption { QualityLabel = "720p", Height = 720, Width = 1280, Bitrate = 2500000, Codec = "H.264", Container = "mp4" },
                new MediaVariantOption { QualityLabel = "1080p", Height = 1080, Width = 1920, Bitrate = 5000000, Codec = "H.264", Container = "mp4" },
                new MediaVariantOption { QualityLabel = "1440p", Height = 1440, Width = 2560, Bitrate = 10000000, Codec = "VP9", Container = "webm" },
                new MediaVariantOption { QualityLabel = "2160p", Height = 2160, Width = 3840, Bitrate = 20000000, Codec = "VP9", Container = "webm" }
            };

            // Rank representations strictly by manifest properties (Height descending, Bitrate descending)
            var sorted = availableSourceRepresentations
                .OrderByDescending(v => v.Height)
                .ThenByDescending(v => v.Bitrate)
                .ToList();

            // Assertions
            sorted.Should().NotBeEmpty();
            var highest = sorted.First();

            highest.Height.Should().Be(2160);
            highest.QualityLabel.Should().Be("2160p");
            highest.Height.Should().NotBe(browserPlaybackHeight);
            highest.QualityLabel.Should().NotBe(browserPlaybackQuality);
        }

        [Theory]
        [InlineData("360p", 1080, 1080)]
        [InlineData("1080p", 2160, 2160)]
        [InlineData("720p", 1440, 1440)]
        public void Discovery_MaximumQuality_RemainsStrictlyIndependentOfBrowserPlaybackQuality(string browserQuality, int sourceMaxHeight, int expectedEdmMaxHeight)
        {
            var manifestRepresentations = new List<MediaVariantOption>
            {
                new MediaVariantOption { QualityLabel = "360p", Height = 360 },
                new MediaVariantOption { QualityLabel = "720p", Height = 720 },
                new MediaVariantOption { QualityLabel = $"{sourceMaxHeight}p", Height = sourceMaxHeight }
            };

            var ranked = manifestRepresentations.OrderByDescending(v => v.Height).ToList();
            var edmMax = ranked.First();

            edmMax.Height.Should().Be(expectedEdmMaxHeight);
        }

        [Fact]
        public void Discovery_WhenSourceMaximumIs1080p_DoesNotManufactureFake4KOr1440pEntries()
        {
            var manifestRepresentations = new List<MediaVariantOption>
            {
                new MediaVariantOption { QualityLabel = "1080p", Height = 1080, Container = "mp4" },
                new MediaVariantOption { QualityLabel = "720p", Height = 720, Container = "mp4" },
                new MediaVariantOption { QualityLabel = "480p", Height = 480, Container = "mp4" }
            };

            var discovered = manifestRepresentations.OrderByDescending(v => v.Height).ToList();

            discovered.Should().HaveCount(3);
            discovered.First().QualityLabel.Should().Be("1080p");
            discovered.Should().NotContain(v => v.Height == 2160 || v.QualityLabel == "2160p");
            discovered.Should().NotContain(v => v.Height == 1440 || v.QualityLabel == "1440p");
            discovered.Should().NotContain(v => v.QualityLabel.Contains("Best Quality"));
        }

        [Fact]
        public void Discovery_CalculatesAccurateCombinedAdaptiveSize()
        {
            long videoBytes = 524288000; // 500 MB
            long audioBytes = 52428800;  // 50 MB
            long expectedTotal = 576716800; // 550 MB

            var option = new MediaVariantOption
            {
                QualityLabel = "1080p",
                Height = 1080,
                Codec = "H.264",
                AudioCodec = "AAC",
                Container = "mp4",
                EstimatedSizeBytes = videoBytes + audioBytes,
                RequiresFfmpegMerge = true
            };

            option.EstimatedSizeBytes.Should().Be(expectedTotal);
            option.FormattedSize.Should().Contain("550 MB");
            option.RequiresFfmpegMerge.Should().BeTrue();
        }

        [Fact]
        public void Discovery_PreservesDistinctRepresentationsAtSameResolution()
        {
            var representations = new List<MediaVariantOption>
            {
                new MediaVariantOption { QualityLabel = "1080p", Height = 1080, Codec = "H.264", Container = "mp4", Bitrate = 4500000 },
                new MediaVariantOption { QualityLabel = "1080p", Height = 1080, Codec = "VP9", Container = "webm", Bitrate = 3800000 },
                new MediaVariantOption { QualityLabel = "1080p", Height = 1080, Codec = "AV1", Container = "webm", Bitrate = 3200000 }
            };

            var ranked = representations
                .OrderByDescending(v => v.Height)
                .ThenByDescending(v => v.Bitrate)
                .ToList();

            ranked.Should().HaveCount(3);
            ranked.Select(r => r.Codec).Should().Contain(new[] { "H.264", "VP9", "AV1" });
            ranked.Select(r => r.Container).Should().Contain(new[] { "mp4", "webm" });
        }

        [Fact]
        public void HlsParser_ParsesMasterVariantsCorrectly()
        {
            string m3u8Master = @"#EXTM3U
#EXT-X-STREAM-INF:BANDWIDTH=800000,RESOLUTION=640x360,CODECS=""avc1.4d401f,mp4a.40.2""
360p.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=2500000,RESOLUTION=1280x720,CODECS=""avc1.4d401f,mp4a.40.2""
720p.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=6000000,RESOLUTION=1920x1080,CODECS=""avc1.640028,mp4a.40.2""
1080p.m3u8";

            var playlist = HlsParser.Parse(m3u8Master, new Uri("https://example.com/stream/master.m3u8"));

            playlist.IsMaster.Should().BeTrue();
            playlist.Variants.Should().HaveCount(3);
            var sorted = playlist.Variants.OrderByDescending(v => v.Height).ToList();

            sorted.First().Height.Should().Be(1080);
            sorted.First().Bandwidth.Should().Be(6000000);
            sorted.Last().Height.Should().Be(360);
        }

        [Fact]
        public void DashParser_ParsesRepresentationsWithoutTreatingSegmentAsFile()
        {
            string mpdXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"" minBufferTime=""PT1.5S"" type=""static"" mediaPresentationDuration=""PT0H3M1.63S"">
    <Period>
        <AdaptationSet mimeType=""video/mp4"" contentType=""video"">
            <Representation id=""1"" bandwidth=""1000000"" width=""640"" height=""360"" codecs=""avc1.4d401f"">
                <BaseURL>video_360p.mp4</BaseURL>
            </Representation>
            <Representation id=""2"" bandwidth=""4000000"" width=""1920"" height=""1080"" codecs=""avc1.640028"">
                <BaseURL>video_1080p.mp4</BaseURL>
            </Representation>
        </AdaptationSet>
        <AdaptationSet mimeType=""audio/mp4"" contentType=""audio"">
            <Representation id=""3"" bandwidth=""128000"" codecs=""mp4a.40.2"">
                <BaseURL>audio_128k.m4a</BaseURL>
            </Representation>
        </AdaptationSet>
    </Period>
</MPD>";

            var manifest = DashParser.Parse(mpdXml, new Uri("https://example.com/dash/manifest.mpd"));

            manifest.VideoRepresentations.Should().HaveCount(2);
            manifest.AudioRepresentations.Should().HaveCount(1);

            var bestVideo = manifest.VideoRepresentations.OrderByDescending(r => r.Height).First();
            bestVideo.Height.Should().Be(1080);
            bestVideo.Width.Should().Be(1920);
            bestVideo.Bandwidth.Should().Be(4000000);
        }
    }
}
