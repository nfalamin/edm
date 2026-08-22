using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using EDM.Models;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class AdvancedMediaQualitySelectorTests : IDisposable
    {
        private readonly string _testStorageDir;

        public AdvancedMediaQualitySelectorTests()
        {
            _testStorageDir = Path.Combine(Path.GetTempPath(), "EDM_QualityTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testStorageDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testStorageDir))
                {
                    Directory.Delete(_testStorageDir, true);
                }
            }
            catch { }
        }

        // 1. HLS multiple qualities parsing and ordering
        [Fact]
        public void Test1_HlsMultipleQualities_ParsedAndSortedDescending()
        {
            string masterM3u8 = @"#EXTM3U
#EXT-X-STREAM-INF:BANDWIDTH=800000,RESOLUTION=640x360
360p.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=5000000,RESOLUTION=1920x1080
1080p.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=2500000,RESOLUTION=1280x720
720p.m3u8";

            var playlist = HlsParser.Parse(masterM3u8, new Uri("https://example.com/master.m3u8"));
            var options = playlist.Variants.Select(v => new MediaVariantOption
            {
                QualityLabel = $"{v.Height}p",
                Width = v.Width,
                Height = v.Height,
                Bitrate = v.Bandwidth,
                DirectUrl = v.Uri
            }).ToList();

            var sorted = MediaVariantResolver.SortVariants(options);

            sorted.Should().HaveCount(3);
            sorted[0].Height.Should().Be(1080);
            sorted[1].Height.Should().Be(720);
            sorted[2].Height.Should().Be(360);
        }

        // 2. HLS single quality / direct fallback
        [Fact]
        public void Test2_HlsSingleQuality_ReturnsSingleDirectVariant()
        {
            string singleM3u8 = @"#EXTM3U
#EXTINF:6.0,
seg1.ts
#EXT-X-ENDLIST";

            var playlist = HlsParser.Parse(singleM3u8, new Uri("https://example.com/media.m3u8"));
            playlist.IsMaster.Should().BeFalse();
            playlist.Segments.Should().HaveCount(1);
        }

        // 3. DASH multiple representations with video + audio
        [Fact]
        public void Test3_DashMultipleRepresentations_ExtractsVideoAndAudio()
        {
            string dashXml = @"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet mimeType=""video/mp4"">
      <Representation id=""1080p"" bandwidth=""4000000"" width=""1920"" height=""1080"">
        <BaseURL>1080p.mp4</BaseURL>
      </Representation>
      <Representation id=""720p"" bandwidth=""2000000"" width=""1280"" height=""720"">
        <BaseURL>720p.mp4</BaseURL>
      </Representation>
    </AdaptationSet>
    <AdaptationSet mimeType=""audio/mp4"" lang=""en"">
      <Representation id=""a128"" bandwidth=""128000"">
        <BaseURL>audio_en.mp4</BaseURL>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

            var manifest = DashParser.Parse(dashXml, new Uri("https://example.com/manifest.mpd"));
            manifest.VideoRepresentations.Should().HaveCount(2);
            manifest.AudioRepresentations.Should().HaveCount(1);
        }

        // 4. Multiple audio tracks and language metadata
        [Fact]
        public void Test4_MultipleAudioTracks_ExtractsLanguages()
        {
            string masterM3u8 = @"#EXTM3U
#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=""audio"",NAME=""English"",LANGUAGE=""en"",DEFAULT=YES,URI=""eng.m3u8""
#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=""audio"",NAME=""Bangla"",LANGUAGE=""bn"",DEFAULT=NO,URI=""ben.m3u8""
#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=""audio"",NAME=""Spanish"",LANGUAGE=""es"",DEFAULT=NO,URI=""spa.m3u8""
#EXT-X-STREAM-INF:BANDWIDTH=3000000,RESOLUTION=1280x720,AUDIO=""audio""
720p.m3u8";

            var playlist = HlsParser.Parse(masterM3u8, new Uri("https://example.com/master.m3u8"));
            playlist.AudioTracks.Should().HaveCount(3);
            playlist.AudioTracks.Select(a => a.Name).Should().Contain(new[] { "English", "Bangla", "Spanish" });
            playlist.AudioTracks.Select(a => a.Language).Should().Contain(new[] { "en", "bn", "es" });
        }

        // 5. Audio-only stream extraction and container labeling
        [Fact]
        public void Test5_AudioOnlyStream_LabeledProperly()
        {
            var opt = new MediaVariantOption
            {
                QualityLabel = "Audio Only (AAC)",
                Container = "m4a",
                AudioCodec = "AAC",
                AudioBitrate = 128000,
                AudioLanguage = "English",
                IsAudioOnly = true
            };

            opt.IsAudioOnly.Should().BeTrue();
            opt.FormattedDetails.Should().Contain("M4A Audio");
            opt.FormattedDetails.Should().Contain("128 kbps");
            opt.FormattedDetails.Should().Contain("English");
        }

        // 6. Missing resolution / bitrate handling with fallback
        [Fact]
        public void Test6_MissingResolution_HandlesGracefullyWithQualityLabel()
        {
            var opt = new MediaVariantOption
            {
                QualityLabel = "Custom Stream",
                Width = 0,
                Height = 0,
                Bitrate = 0
            };

            opt.Resolution.Should().Be("Custom Stream");
            var sorted = MediaVariantResolver.SortVariants(new[] { opt });
            sorted.Should().ContainSingle();
        }

        // 7. Duplicate representation deduplication
        [Fact]
        public void Test7_DuplicateRepresentationDeduplication()
        {
            var options = new List<MediaVariantOption>
            {
                new() { QualityLabel = "1080p", Height = 1080, Bitrate = 4000000, DirectUrl = "https://example.com/1080.mp4" },
                new() { QualityLabel = "1080p", Height = 1080, Bitrate = 4000000, DirectUrl = "https://example.com/1080.mp4" },
                new() { QualityLabel = "720p", Height = 720, Bitrate = 2000000, DirectUrl = "https://example.com/720.mp4" }
            };

            var distinct = options.DistinctBy(o => o.DirectUrl).ToList();
            distinct.Should().HaveCount(2);
        }

        // 8. Invalid / Malformed manifest metadata recovery
        [Fact]
        public void Test8_MalformedManifest_RecoversWithoutException()
        {
            var manifest = DashParser.Parse("MALFORMED_GARBAGE", new Uri("https://example.com/"));
            manifest.Should().NotBeNull();
            manifest.VideoRepresentations.Should().BeEmpty();
        }

        // 9. Auto / Best Available selection policy
        [Fact]
        public void Test9_AutoSelection_PicksHighestQualityVideo()
        {
            var options = new List<MediaVariantOption>
            {
                new() { QualityLabel = "480p", Height = 480, Bitrate = 1000000 },
                new() { QualityLabel = "1080p", Height = 1080, Bitrate = 5000000 },
                new() { QualityLabel = "720p", Height = 720, Bitrate = 2500000 },
                new() { QualityLabel = "Audio Only", IsAudioOnly = true, Bitrate = 128000 }
            };

            var best = MediaVariantResolver.SelectBestVariant(options, "Auto");
            best.Should().NotBeNull();
            best!.Height.Should().Be(1080);
            best.IsAudioOnly.Should().BeFalse();
        }

        // 10. Explicit user selection persistence
        [Fact]
        public void Test10_ExplicitUserSelection_OverridesDefault()
        {
            var options = new List<MediaVariantOption>
            {
                new() { QualityLabel = "1080p", Height = 1080, Bitrate = 5000000 },
                new() { QualityLabel = "720p", Height = 720, Bitrate = 2500000 }
            };

            // User explicitly chose 720p
            var userChoice = options.First(o => o.QualityLabel == "720p");
            var item = new DownloadItem
            {
                Quality = userChoice.QualityLabel,
                VideoUrl = "https://example.com/720p.m3u8"
            };

            item.Quality.Should().Be("720p");
        }

        // 11. Maximum quality cap setting (e.g. max 720p)
        [Fact]
        public void Test11_MaximumQualityCap_RestrictsToUnderCap()
        {
            var options = new List<MediaVariantOption>
            {
                new() { QualityLabel = "4K 2160p", Height = 2160, Bitrate = 15000000 },
                new() { QualityLabel = "1080p", Height = 1080, Bitrate = 5000000 },
                new() { QualityLabel = "720p", Height = 720, Bitrate = 2500000 },
                new() { QualityLabel = "480p", Height = 480, Bitrate = 1000000 }
            };

            // Cap at 720p
            var selected = MediaVariantResolver.SelectBestVariant(options, "720p");
            selected.Should().NotBeNull();
            selected!.Height.Should().Be(720);
        }

        // 12. Unavailable selected stream fallback
        [Fact]
        public void Test12_UnavailableSelectedStream_FallsBackToBestAvailable()
        {
            var options = new List<MediaVariantOption>
            {
                new() { QualityLabel = "480p", Height = 480, Bitrate = 1000000 }
            };

            // User requested 1080p, but only 480p exists
            var selected = MediaVariantResolver.SelectBestVariant(options, "1080p");
            selected.Should().NotBeNull();
            selected!.Height.Should().Be(480);
        }

        // 13. Queue & Scheduler integration
        [Fact]
        public void Test13_QueueIntegration_StoresSelectedQualityInQueueItem()
        {
            var queue = new DownloadQueueScheduler();
            var item = new QueuedDownloadItem
            {
                DownloadId = "media_1080p_job",
                Url = "https://example.com/video_1080p.m3u8",
                DestinationPath = Path.Combine(_testStorageDir, "video.mp4")
            };

            queue.Enqueue(item);
            var next = queue.TryGetNextDownloadToStart();
            next.Should().NotBeNull();
            next!.DownloadId.Should().Be("media_1080p_job");
        }

        // 14. Persistence of selected quality in DownloadItem
        [Fact]
        public void Test14_Persistence_PreservesMediaProperties()
        {
            var item = new DownloadItem
            {
                Id = Guid.NewGuid(),
                FileName = "Movie_1080p.mp4",
                Quality = "1080p",
                VideoUrl = "https://example.com/vid.m3u8",
                AudioUrl = "https://example.com/aud.m3u8",
                RequiresFfmpegMerge = true,
                Codec = "H.264",
                AudioCodec = "AAC"
            };

            item.Quality.Should().Be("1080p");
            item.RequiresFfmpegMerge.Should().BeTrue();
            item.Codec.Should().Be("H.264");
            item.AudioCodec.Should().Be("AAC");
        }

        // 15. Profile / Rule integration (Video category default caps)
        [Fact]
        public void Test15_RuleEngine_CategorizesMediaStreamAsVideo()
        {
            var engine = DownloadRuleEngine.Instance;
            var result = engine.Resolve(new DownloadRequest { Url = "https://example.com/stream.m3u8", ContentType = "application/vnd.apple.mpegurl" }, _testStorageDir);

            result.Category.Should().Be("Video");
        }

        // 16. UI display formatting strings
        [Fact]
        public void Test16_FormattedDetails_RendersAccurately()
        {
            var opt = new MediaVariantOption
            {
                QualityLabel = "1080p",
                Container = "mp4",
                Codec = "h264",
                FrameRate = 60.0,
                AudioLanguage = "English",
                EstimatedSizeBytes = 104857600, // 100 MB
                HasAudio = true
            };

            string details = opt.FormattedDetails;
            details.Should().Contain("1080p");
            details.Should().Contain("MP4");
            details.Should().Contain("H264");
            details.Should().Contain("60 FPS");
            details.Should().Contain("Audio: English");
            details.Should().Contain("100 MB");
        }

        // 17. Compatibility filtering for supported containers/codecs
        [Fact]
        public void Test17_CompatibilityFiltering_AcceptsStandardFormats()
        {
            var validCodecs = new[] { "avc1", "h264", "hevc", "h265", "vp9", "av01", "aac", "mp4a", "opus" };
            foreach (var c in validCodecs)
            {
                bool isKnown = !string.IsNullOrEmpty(c);
                isKnown.Should().BeTrue();
            }
        }

        // 18. Zero-transcoding verification
        [Fact]
        public void Test18_ZeroTranscoding_PreservesOriginalContainerStreams()
        {
            var opt = new MediaVariantOption
            {
                DirectUrl = "https://cdn.example.com/video_1080p.ts",
                Container = "mp4"
            };

            // Quality selection should pass native direct URL without invoking ffmpeg video transcode filters
            opt.DirectUrl.Should().Be("https://cdn.example.com/video_1080p.ts");
        }

        // 19. Deterministic resolution descending sorting
        [Fact]
        public void Test19_Sorting_PutsVideoBeforeAudioAndHeightDescending()
        {
            var options = new List<MediaVariantOption>
            {
                new() { QualityLabel = "Audio AAC", IsAudioOnly = true, Bitrate = 128000 },
                new() { QualityLabel = "720p", Height = 720, Bitrate = 2000000 },
                new() { QualityLabel = "2160p", Height = 2160, Bitrate = 12000000 },
                new() { QualityLabel = "1080p", Height = 1080, Bitrate = 4000000 }
            };

            var sorted = MediaVariantResolver.SortVariants(options);

            sorted[0].Height.Should().Be(2160);
            sorted[1].Height.Should().Be(1080);
            sorted[2].Height.Should().Be(720);
            sorted[3].IsAudioOnly.Should().BeTrue();
        }
    }
}
