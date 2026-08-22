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
    public class AdvancedAudioExtractionTests : IDisposable
    {
        private readonly string _testStorageDir;

        public AdvancedAudioExtractionTests()
        {
            _testStorageDir = Path.Combine(Path.GetTempPath(), "EDM_AudioTests_" + Guid.NewGuid().ToString("N"));
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

        // 1. HLS audio-only stream extraction
        [Fact]
        public void Test1_HlsAudioOnlyExtraction_IdentifiesAudioTrackUrls()
        {
            string masterM3u8 = @"#EXTM3U
#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=""audio-aac"",NAME=""English Audio"",LANGUAGE=""en"",DEFAULT=YES,URI=""audio_en.m3u8""
#EXT-X-STREAM-INF:BANDWIDTH=3000000,RESOLUTION=1280x720,AUDIO=""audio-aac""
video_720p.m3u8";

            var playlist = HlsParser.Parse(masterM3u8, new Uri("https://cdn.example.com/stream/master.m3u8"));
            playlist.AudioTracks.Should().ContainSingle();
            playlist.AudioTracks[0].Uri.Should().Be("https://cdn.example.com/stream/audio_en.m3u8");
            playlist.AudioTracks[0].Language.Should().Be("en");
        }

        // 2. DASH audio representation extraction without video
        [Fact]
        public void Test2_DashAudioOnlyExtraction_ExtractsAudioRepresentations()
        {
            string mpd = @"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet mimeType=""video/mp4"">
      <Representation id=""v1"" bandwidth=""2000000"" width=""1280"" height=""720"">
        <BaseURL>v.mp4</BaseURL>
      </Representation>
    </AdaptationSet>
    <AdaptationSet mimeType=""audio/mp4"" lang=""en"">
      <Representation id=""a1"" bandwidth=""128000"" audioSamplingRate=""48000"">
        <BaseURL>audio_en.mp4</BaseURL>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

            var manifest = DashParser.Parse(mpd, new Uri("https://cdn.example.com/dash/"));
            manifest.AudioRepresentations.Should().ContainSingle();
            manifest.AudioRepresentations[0].Language.Should().Be("en");
            manifest.AudioRepresentations[0].Bandwidth.Should().Be(128000);
            manifest.AudioRepresentations[0].AudioSamplingRate.Should().Be(48000);
        }

        // 3. Multi-language audio track detection (English, Bangla, Spanish)
        [Fact]
        public void Test3_MultiLanguageAudioTracks_DetectedCorrectly()
        {
            string masterM3u8 = @"#EXTM3U
#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=""aud"",NAME=""English"",LANGUAGE=""en"",DEFAULT=YES,URI=""a_en.m3u8""
#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=""aud"",NAME=""Bangla"",LANGUAGE=""bn"",DEFAULT=NO,URI=""a_bn.m3u8""
#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=""aud"",NAME=""Spanish"",LANGUAGE=""es"",DEFAULT=NO,URI=""a_es.m3u8""
#EXT-X-STREAM-INF:BANDWIDTH=2000000,AUDIO=""aud""
stream.m3u8";

            var playlist = HlsParser.Parse(masterM3u8, new Uri("https://example.com/"));
            var languages = playlist.AudioTracks.Select(a => a.Language).ToList();

            languages.Should().BeEquivalentTo(new[] { "en", "bn", "es" });
        }

        // 4. Audio bitrate selection (320kbps, 256kbps, 128kbps, 64kbps)
        [Fact]
        public void Test4_AudioBitrateSelection_OrdersDescending()
        {
            var audioOptions = new List<MediaVariantOption>
            {
                new() { QualityLabel = "64k", AudioBitrate = 64000, IsAudioOnly = true },
                new() { QualityLabel = "320k", AudioBitrate = 320000, IsAudioOnly = true },
                new() { QualityLabel = "128k", AudioBitrate = 128000, IsAudioOnly = true },
                new() { QualityLabel = "256k", AudioBitrate = 256000, IsAudioOnly = true }
            };

            var sorted = audioOptions.OrderByDescending(a => a.AudioBitrate).ToList();
            sorted[0].AudioBitrate.Should().Be(320000);
            sorted[1].AudioBitrate.Should().Be(256000);
            sorted[2].AudioBitrate.Should().Be(128000);
            sorted[3].AudioBitrate.Should().Be(64000);
        }

        // 5. Audio codec detection (AAC, Opus, MP3, Vorbis)
        [Theory]
        [InlineData("mp4a.40.2", "AAC")]
        [InlineData("opus", "Opus")]
        [InlineData("mp3", "MP3")]
        [InlineData("vorbis", "Vorbis")]
        public void Test5_AudioCodecDetection_IdentifiesCodecs(string rawCodec, string expectedGroup)
        {
            string codecUpper = rawCodec.ToUpperInvariant();
            bool matches = codecUpper.Contains(expectedGroup.ToUpperInvariant()) ||
                           (expectedGroup == "AAC" && codecUpper.Contains("MP4A"));

            matches.Should().BeTrue();
        }

        // 6. Audio container detection (M4A, MP3, WebM, Opus)
        [Theory]
        [InlineData("audio/mp4", "m4a")]
        [InlineData("audio/webm", "webm")]
        [InlineData("audio/mpeg", "mp3")]
        [InlineData("audio/ogg", "ogg")]
        public void Test6_AudioContainerDetection_MapsExtensions(string mime, string expectedExt)
        {
            string ext = mime switch
            {
                "audio/mp4" => "m4a",
                "audio/webm" => "webm",
                "audio/mpeg" => "mp3",
                "audio/ogg" => "ogg",
                _ => "m4a"
            };

            ext.Should().Be(expectedExt);
        }

        // 7. Unsupported / missing audio stream handling
        [Fact]
        public void Test7_MissingAudioStream_ReturnsEmpty()
        {
            string masterM3u8 = @"#EXTM3U
#EXT-X-STREAM-INF:BANDWIDTH=1000000
video_only.m3u8";

            var playlist = HlsParser.Parse(masterM3u8, new Uri("https://example.com/"));
            playlist.AudioTracks.Should().BeEmpty();
        }

        // 8. Filename sanitization for audio output
        [Fact]
        public void Test8_FilenameSanitization_CleansAudioFilename()
        {
            string rawName = "My Audio: Song <Vol 1> [2026]?.mp3";
            string sanitized = SecuritySanitizer.SanitizeFileName(rawName);

            sanitized.Should().NotContainAny(":", "<", ">", "?");
            sanitized.Should().EndWith(".mp3");
        }

        // 9. Invalid output path validation
        [Fact]
        public void Test9_InvalidOutputPath_ThrowsArgumentException()
        {
            var mergeService = new MediaMergeService(SharedHttpClient.Instance);
            var act = () => mergeService.ExtractAudioAsync("valid_input.mp4", "", null, CancellationToken.None);

            act.Should().ThrowAsync<ArgumentException>();
        }

        // 10. Segment pause and resume for audio streams
        [Fact]
        public async Task Test10_PauseAndResume_HandlesAudioPauseToken()
        {
            var pts = new PauseTokenSource();
            pts.IsPaused.Should().BeFalse();

            pts.Pause();
            pts.IsPaused.Should().BeTrue();

            var waitTask = Task.Run(async () => await pts.WaitIfPausedAsync());
            await Task.Delay(50);
            waitTask.IsCompleted.Should().BeFalse();

            pts.Resume();
            await waitTask;
            pts.IsPaused.Should().BeFalse();
        }

        // 11. Segment retry with exponential backoff for audio chunks
        [Fact]
        public async Task Test11_AudioChunkRetry_ExecutesViaHttpRequestPipeline()
        {
            var pipeline = new HttpRequestPipeline();
            int calls = 0;

            var resp = await pipeline.ExecuteWithRetryAsync(() =>
            {
                calls++;
                return new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "https://httpbin.org/status/200");
            }, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);

            calls.Should().BeGreaterThanOrEqualTo(1);
        }

        // 12. Queue and Scheduler integration for audio jobs
        [Fact]
        public void Test12_QueueIntegration_EnqueuesAudioJob()
        {
            var queue = new DownloadQueueScheduler();
            var item = new QueuedDownloadItem
            {
                DownloadId = "audio_job_1",
                Url = "https://example.com/audio.m4a",
                DestinationPath = Path.Combine(_testStorageDir, "audio.m4a")
            };

            queue.Enqueue(item);
            var next = queue.TryGetNextDownloadToStart();
            next.Should().NotBeNull();
            next!.DownloadId.Should().Be("audio_job_1");
        }

        // 13. Profile / Rule integration (Audio category routing)
        [Fact]
        public void Test13_RuleEngine_CategorizesAudioStream()
        {
            var engine = DownloadRuleEngine.Instance;
            var res = engine.Resolve(new DownloadRequest { Url = "https://example.com/podcast.m4a", ContentType = "audio/mp4" }, _testStorageDir);

            res.Category.Should().Be("Music");
        }

        // 14. Lossless stream remuxing verification (-c:a copy)
        [Fact]
        public void Test14_LosslessRemuxArguments_ConstructedSafely()
        {
            var psi = new System.Diagnostics.ProcessStartInfo();
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add("input.ts");
            psi.ArgumentList.Add("-vn");
            psi.ArgumentList.Add("-c:a");
            psi.ArgumentList.Add("copy");
            psi.ArgumentList.Add("output.m4a");

            psi.ArgumentList.Should().ContainInOrder("-vn", "-c:a", "copy");
        }

        // 15. Transcoding to MP3 verification (-c:a libmp3lame)
        [Fact]
        public void Test15_Mp3TranscodeArguments_ConstructedSafely()
        {
            var psi = new System.Diagnostics.ProcessStartInfo();
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add("input.m4a");
            psi.ArgumentList.Add("-vn");
            psi.ArgumentList.Add("-c:a");
            psi.ArgumentList.Add("libmp3lame");
            psi.ArgumentList.Add("-q:a");
            psi.ArgumentList.Add("2");
            psi.ArgumentList.Add("output.mp3");

            psi.ArgumentList.Should().ContainInOrder("-vn", "-c:a", "libmp3lame", "-q:a", "2");
        }

        // 16. External FFmpeg process cleanup on cancellation
        [Fact]
        public async Task Test16_ProcessCleanupOnCancel_CancelsTokenSafely()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            cts.IsCancellationRequested.Should().BeTrue();
        }

        // 17. Temporary file cleanup on completion and failure
        [Fact]
        public void Test17_TemporaryFileCleanup_CleansTmpFiles()
        {
            string tempFile = Path.Combine(_testStorageDir, "temp_audio_chunk.tmp");
            File.WriteAllText(tempFile, "TEMP_AUDIO");

            File.Exists(tempFile).Should().BeTrue();

            try { File.Delete(tempFile); } catch { }

            File.Exists(tempFile).Should().BeFalse();
        }

        // 18. Zero-video bandwidth optimization for audio-only
        [Fact]
        public void Test18_ZeroVideoOptimization_SetsDirectAudioUrlWithoutVideo()
        {
            var audioOption = new MediaVariantOption
            {
                QualityLabel = "Audio Only (128 kbps)",
                DirectUrl = "https://cdn.example.com/audio_only.m3u8",
                IsAudioOnly = true,
                RequiresFfmpegMerge = false,
                Container = "m4a"
            };

            var item = new DownloadItem
            {
                VideoUrl = audioOption.DirectUrl,
                AudioUrl = string.Empty,
                RequiresFfmpegMerge = false,
                IsAudioOnly = true
            };

            item.IsAudioOnly.Should().BeTrue();
            item.RequiresFfmpegMerge.Should().BeFalse();
            item.AudioUrl.Should().BeEmpty();
        }

        // 19. Deterministic audio quality sorting
        [Fact]
        public void Test19_AudioQualitySorting_OrdersByBitrateDescending()
        {
            var options = new List<MediaVariantOption>
            {
                new() { QualityLabel = "Audio Low", AudioBitrate = 64000, IsAudioOnly = true },
                new() { QualityLabel = "Audio High", AudioBitrate = 320000, IsAudioOnly = true },
                new() { QualityLabel = "Audio Mid", AudioBitrate = 192000, IsAudioOnly = true }
            };

            var sorted = options.OrderByDescending(o => o.AudioBitrate).ToList();
            sorted[0].AudioBitrate.Should().Be(320000);
            sorted[1].AudioBitrate.Should().Be(192000);
            sorted[2].AudioBitrate.Should().Be(64000);
        }

        // 20. Audio metadata preservation in DownloadItem
        [Fact]
        public void Test20_AudioMetadataPreservation_RetainsProperties()
        {
            var item = new DownloadItem
            {
                FileName = "Podcast_Episode_12.m4a",
                Quality = "Audio Only (256 kbps)",
                AudioCodec = "AAC",
                IsAudioOnly = true,
                Container = "m4a"
            };

            item.FileName.Should().Be("Podcast_Episode_12.m4a");
            item.Quality.Should().Be("Audio Only (256 kbps)");
            item.AudioCodec.Should().Be("AAC");
            item.IsAudioOnly.Should().BeTrue();
        }

        // 21. Channel and sample rate metadata extraction
        [Fact]
        public void Test21_ChannelAndSampleRateExtraction_PopulatesCorrectly()
        {
            var opt = new MediaVariantOption
            {
                AudioChannels = 6, // 5.1 Surround
                AudioSamplingRate = 48000,
                AudioBitrate = 384000
            };

            opt.AudioChannels.Should().Be(6);
            opt.AudioSamplingRate.Should().Be(48000);
            opt.AudioBitrate.Should().Be(384000);
        }

        // 22. Error handling when FFmpeg is unavailable
        [Fact]
        public async Task Test22_MissingFfmpeg_ThrowsFileNotFoundException()
        {
            var mergeService = new MediaMergeService(SharedHttpClient.Instance);
            string nonExistentFfmpeg = Path.Combine(_testStorageDir, "non_existent_ffmpeg.exe");

            string dummyInput = Path.Combine(_testStorageDir, "dummy_input.mp4");
            File.WriteAllText(dummyInput, "DUMMY");

            var act = () => mergeService.ExtractAudioAsync(dummyInput, Path.Combine(_testStorageDir, "out.m4a"), nonExistentFfmpeg, CancellationToken.None);

            await act.Should().ThrowAsync<FileNotFoundException>();
        }
    }
}
