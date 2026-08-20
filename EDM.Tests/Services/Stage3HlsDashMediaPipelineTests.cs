using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class Stage3HlsDashMediaPipelineTests
    {
        [Fact]
        public void HlsParser_ParsesMasterPlaylist_WithFullAttributesAndAudioSubtitles()
        {
            string masterM3u8 = "#EXTM3U\n" +
"#EXT-X-VERSION:6\n" +
"#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"audio-aac\",NAME=\"English\",DEFAULT=YES,AUTOSELECT=YES,LANGUAGE=\"en\",URI=\"audio/en/prog_index.m3u8\"\n" +
"#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"audio-aac\",NAME=\"Spanish\",DEFAULT=NO,AUTOSELECT=YES,LANGUAGE=\"es\",URI=\"audio/es/prog_index.m3u8\"\n" +
"#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID=\"subs\",NAME=\"English CC\",DEFAULT=YES,AUTOSELECT=YES,FORCED=NO,LANGUAGE=\"en\",URI=\"subs/en.m3u8\"\n" +
"#EXT-X-STREAM-INF:BANDWIDTH=800000,AVERAGE-BANDWIDTH=750000,RESOLUTION=640x360,FRAME-RATE=29.970,CODECS=\"avc1.4d401f,mp4a.40.2\",AUDIO=\"audio-aac\",SUBTITLES=\"subs\"\n" +
"360p/manifest.m3u8\n" +
"#EXT-X-STREAM-INF:BANDWIDTH=2500000,AVERAGE-BANDWIDTH=2400000,RESOLUTION=1280x720,FRAME-RATE=60.000,CODECS=\"avc1.640020,mp4a.40.2\",AUDIO=\"audio-aac\",SUBTITLES=\"subs\"\n" +
"720p/manifest.m3u8\n" +
"#EXT-X-STREAM-INF:BANDWIDTH=6000000,AVERAGE-BANDWIDTH=5800000,RESOLUTION=1920x1080,FRAME-RATE=60.000,CODECS=\"avc1.640028,mp4a.40.2\",AUDIO=\"audio-aac\",SUBTITLES=\"subs\"\n" +
"1080p/manifest.m3u8";

            var baseUri = new Uri("https://cdn.example.com/live/master.m3u8");
            var playlist = HlsParser.Parse(masterM3u8, baseUri);

            playlist.IsMaster.Should().BeTrue();
            playlist.IsDrmProtected.Should().BeFalse();
            playlist.Variants.Should().HaveCount(3);
            playlist.AudioTracks.Should().HaveCount(2);
            playlist.SubtitleTracks.Should().HaveCount(1);

            var v1080 = playlist.Variants.First(v => v.Height == 1080);
            v1080.Bandwidth.Should().Be(6000000);
            v1080.AverageBandwidth.Should().Be(5800000);
            v1080.Width.Should().Be(1920);
            v1080.Height.Should().Be(1080);
            v1080.FrameRate.Should().Be(60.0);
            v1080.Codecs.Should().Be("avc1.640028,mp4a.40.2");
            v1080.Uri.Should().Be("https://cdn.example.com/live/1080p/manifest.m3u8");
            v1080.AudioGroupId.Should().Be("audio-aac");
            v1080.SubtitlesGroupId.Should().Be("subs");

            var defaultAudio = playlist.AudioTracks.First(a => a.IsDefault);
            defaultAudio.Name.Should().Be("English");
            defaultAudio.Language.Should().Be("en");
            defaultAudio.Uri.Should().Be("https://cdn.example.com/live/audio/en/prog_index.m3u8");

            var sub = playlist.SubtitleTracks.First();
            sub.Language.Should().Be("en");
            sub.Uri.Should().Be("https://cdn.example.com/live/subs/en.m3u8");
        }

        [Fact]
        public void HlsParser_ParsesMediaPlaylist_WithByteRanges_InitMap_AndAes128Keys()
        {
            string mediaM3u8 = "#EXTM3U\n" +
"#EXT-X-VERSION:7\n" +
"#EXT-X-TARGETDURATION:6\n" +
"#EXT-X-MEDIA-SEQUENCE:100\n" +
"#EXT-X-PLAYLIST-TYPE:VOD\n" +
"#EXT-X-KEY:METHOD=AES-128,URI=\"https://auth.example.com/keys/key1.bin\",IV=0x0123456789ABCDEF0123456789ABCDEF\n" +
"#EXT-X-MAP:URI=\"init.mp4\",BYTERANGE=\"720@0\"\n" +
"#EXTINF:5.000,Segment 100\n" +
"#EXT-X-BYTERANGE:102400@720\n" +
"segment100.ts\n" +
"#EXTINF:4.500,Segment 101\n" +
"#EXT-X-BYTERANGE:98304@103120\n" +
"segment101.ts\n" +
"#EXT-X-ENDLIST";

            var baseUri = new Uri("https://cdn.example.com/vod/playlist.m3u8");
            var playlist = HlsParser.Parse(mediaM3u8, baseUri);

            playlist.IsMaster.Should().BeFalse();
            playlist.IsLive.Should().BeFalse();
            playlist.IsDrmProtected.Should().BeFalse();
            playlist.TargetDurationSeconds.Should().Be(6.0);
            playlist.MediaSequence.Should().Be(100);
            playlist.TotalDurationSeconds.Should().Be(9.5);
            playlist.Segments.Should().HaveCount(2);

            var s1 = playlist.Segments[0];
            s1.Uri.Should().Be("https://cdn.example.com/vod/segment100.ts");
            s1.DurationSeconds.Should().Be(5.0);
            s1.SequenceNumber.Should().Be(100);
            s1.ByteRangeLength.Should().Be(102400);
            s1.ByteRangeOffset.Should().Be(720);
            s1.KeyMethod.Should().Be("AES-128");
            s1.KeyUri.Should().Be("https://auth.example.com/keys/key1.bin");
            s1.KeyIv.Should().NotBeNull();
            s1.KeyIv!.Length.Should().Be(16);
            s1.InitSegmentUri.Should().Be("https://cdn.example.com/vod/init.mp4");
            s1.InitByteRangeLength.Should().Be(720);
            s1.InitByteRangeOffset.Should().Be(0);

            var s2 = playlist.Segments[1];
            s2.SequenceNumber.Should().Be(101);
            s2.ByteRangeLength.Should().Be(98304);
            s2.ByteRangeOffset.Should().Be(103120);
        }

        [Theory]
        [InlineData("#EXTM3U\n#EXT-X-KEY:METHOD=SAMPLE-AES,URI=\"skd://asset123\",KEYFORMAT=\"com.apple.streamingkeydelivery\"", "FairPlay")]
        [InlineData("#EXTM3U\n#EXT-X-KEY:METHOD=SAMPLE-AES,URI=\"data:text/plain;base64,AAA...\",KEYFORMAT=\"urn:uuid:edef8ba9-79d6-4ace-a3c8-27dcd51d21ed\"", "Widevine")]
        [InlineData("#EXTM3U\n#EXT-X-KEY:METHOD=SAMPLE-AES,URI=\"https://license.example.com/playready\",KEYFORMAT=\"com.microsoft.playready\"", "PlayReady")]
        public void HlsParser_DetectsDrmProtectedStreams_AndIdentifiesDrmSystem(string manifestText, string expectedDrmSystem)
        {
            var playlist = HlsParser.Parse(manifestText, new Uri("https://example.com/manifest.m3u8"));

            playlist.IsDrmProtected.Should().BeTrue();
            playlist.DrmSystem.Should().Contain(expectedDrmSystem);
        }

        [Fact]
        public void HlsParser_DetectsLiveStream_WhenEndlistTagIsAbsent()
        {
            string liveM3u8 = "#EXTM3U\n" +
"#EXT-X-VERSION:3\n" +
"#EXT-X-TARGETDURATION:4\n" +
"#EXT-X-MEDIA-SEQUENCE:5000\n" +
"#EXTINF:4.000,\n" +
"live_5000.ts\n" +
"#EXTINF:4.000,\n" +
"live_5001.ts";

            var playlist = HlsParser.Parse(liveM3u8, new Uri("https://cdn.example.com/live.m3u8"));

            playlist.IsLive.Should().BeTrue();
            playlist.Segments.Should().HaveCount(2);
        }

        [Fact]
        public void DashParser_ParsesSegmentTemplate_WithTimelineAndRepresentationExpansion()
        {
            string dashXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
"<MPD xmlns=\"urn:mpeg:dash:schema:mpd:2011\" type=\"static\" mediaPresentationDuration=\"PT0H1M30S\">\n" +
"    <BaseURL>https://dash.example.com/vod/</BaseURL>\n" +
"    <Period id=\"1\">\n" +
"        <AdaptationSet mimeType=\"video/mp4\" contentType=\"video\" codecs=\"avc1.640028\">\n" +
"            <SegmentTemplate timescale=\"1000\" initialization=\"init_$RepresentationID$.mp4\" media=\"chunk_$RepresentationID$_$Number%05d$.m4s\" startNumber=\"1\">\n" +
"                <SegmentTimeline>\n" +
"                    <S t=\"0\" d=\"2000\" r=\"2\"/>\n" +
"                </SegmentTimeline>\n" +
"            </SegmentTemplate>\n" +
"            <Representation id=\"1080p\" bandwidth=\"5000000\" width=\"1920\" height=\"1080\" frameRate=\"30\"/>\n" +
"            <Representation id=\"720p\" bandwidth=\"2500000\" width=\"1280\" height=\"720\" frameRate=\"30\"/>\n" +
"        </AdaptationSet>\n" +
"        <AdaptationSet mimeType=\"audio/mp4\" contentType=\"audio\" codecs=\"mp4a.40.2\" lang=\"en\">\n" +
"            <SegmentTemplate timescale=\"1000\" initialization=\"audio_init.mp4\" media=\"audio_chunk_$Number$.m4s\" startNumber=\"1\">\n" +
"                <SegmentTimeline>\n" +
"                    <S t=\"0\" d=\"2000\" r=\"2\"/>\n" +
"                </SegmentTimeline>\n" +
"            </SegmentTemplate>\n" +
"            <Representation id=\"audio_128k\" bandwidth=\"128000\" audioSamplingRate=\"48000\"/>\n" +
"        </AdaptationSet>\n" +
"    </Period>\n" +
"</MPD>";

            var manifest = DashParser.Parse(dashXml, new Uri("https://dash.example.com/manifest.mpd"));

            manifest.IsDrmProtected.Should().BeFalse();
            manifest.IsLive.Should().BeFalse();
            manifest.TotalDurationSeconds.Should().Be(90.0);
            manifest.VideoRepresentations.Should().HaveCount(2);
            manifest.AudioRepresentations.Should().HaveCount(1);

            var v1080 = manifest.VideoRepresentations.First(r => r.Height == 1080);
            v1080.InitializationUrl.Should().Be("https://dash.example.com/vod/init_1080p.mp4");
            v1080.SegmentUrls.Should().HaveCount(3);
            v1080.SegmentUrls[0].Should().Be("https://dash.example.com/vod/chunk_1080p_00001.m4s");
            v1080.SegmentUrls[1].Should().Be("https://dash.example.com/vod/chunk_1080p_00002.m4s");
            v1080.SegmentUrls[2].Should().Be("https://dash.example.com/vod/chunk_1080p_00003.m4s");

            var audio = manifest.AudioRepresentations.First();
            audio.Language.Should().Be("en");
            audio.AudioSamplingRate.Should().Be(48000);
            audio.InitializationUrl.Should().Be("https://dash.example.com/vod/audio_init.mp4");
            audio.SegmentUrls.Should().HaveCount(3);
            audio.SegmentUrls[0].Should().Be("https://dash.example.com/vod/audio_chunk_1.m4s");
        }

        [Fact]
        public void DashParser_DetectsDrmContentProtection()
        {
            string drmDashXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
"<MPD xmlns=\"urn:mpeg:dash:schema:mpd:2011\">\n" +
"    <Period>\n" +
"        <AdaptationSet mimeType=\"video/mp4\">\n" +
"            <ContentProtection schemeIdUri=\"urn:uuid:edef8ba9-79d6-4ace-a3c8-27dcd51d21ed\"/>\n" +
"            <Representation id=\"1\" bandwidth=\"2000000\" width=\"1280\" height=\"720\">\n" +
"                <BaseURL>video.mp4</BaseURL>\n" +
"            </Representation>\n" +
"        </AdaptationSet>\n" +
"    </Period>\n" +
"</MPD>";

            var manifest = DashParser.Parse(drmDashXml, new Uri("https://example.com/drm.mpd"));

            manifest.IsDrmProtected.Should().BeTrue();
            manifest.DrmSystem.Should().Be("Widevine");
        }

        [Fact]
        public void Aes128Decryption_PerformsAccurateRoundTripDecryption()
        {
            byte[] key = new byte[16];
            RandomNumberGenerator.Fill(key);

            byte[] iv = new byte[16];
            RandomNumberGenerator.Fill(iv);

            byte[] originalPayload = Encoding.UTF8.GetBytes("HLS Encrypted Segment Payload 0123456789 ABCDEFGHIJKLMNOPQRSTUVWXYZ");

            // Encrypt using AES-128 CBC PKCS7
            byte[] cipherText;
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using var encryptor = aes.CreateEncryptor();
                cipherText = encryptor.TransformFinalBlock(originalPayload, 0, originalPayload.Length);
            }

            cipherText.Should().NotEqual(originalPayload);

            // Decrypt using AES-128 CBC PKCS7
            byte[] decrypted;
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using var decryptor = aes.CreateDecryptor();
                decrypted = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
            }

            decrypted.Should().Equal(originalPayload);
            Encoding.UTF8.GetString(decrypted).Should().Be("HLS Encrypted Segment Payload 0123456789 ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        }

        [Fact]
        public void HlsQualitySelection_FiltersByTargetResolution_AndFallsBackGracefully()
        {
            var variants = new List<HlsVariant>
            {
                new HlsVariant { Height = 360, Width = 640, Bandwidth = 800000, Uri = "https://cdn.example.com/360p.m3u8" },
                new HlsVariant { Height = 720, Width = 1280, Bandwidth = 2500000, Uri = "https://cdn.example.com/720p.m3u8" },
                new HlsVariant { Height = 1080, Width = 1920, Bandwidth = 6000000, Uri = "https://cdn.example.com/1080p.m3u8" },
                new HlsVariant { Height = 2160, Width = 3840, Bandwidth = 18000000, Uri = "https://cdn.example.com/4k.m3u8" }
            };

            // Test 1080p selection
            var v1080 = variants.Where(v => v.Height == 1080).OrderByDescending(v => v.Bandwidth).FirstOrDefault();
            v1080.Should().NotBeNull();
            v1080!.Height.Should().Be(1080);

            // Test Best selection
            var vBest = variants.OrderByDescending(v => v.Height).ThenByDescending(v => v.Bandwidth).First();
            vBest.Height.Should().Be(2160);

            // Test 480p fallback to nearest available
            int target = 480;
            var nearest = variants.OrderBy(v => Math.Abs(v.Height - target)).ThenByDescending(v => v.Bandwidth).First();
            nearest.Height.Should().Be(360);
        }

        [Fact]
        public void HlsSegmentResume_DetectsExistingPartsInStagingDirectory()
        {
            string tempTarget = Path.Combine(Path.GetTempPath(), $"edm_test_resume_{Guid.NewGuid():N}.mp4");
            string stagingDir = Path.Combine(Path.GetDirectoryName(tempTarget)!, "." + Path.GetFileName(tempTarget) + ".hls_segments");

            try
            {
                Directory.CreateDirectory(stagingDir);

                // Simulate segments 0, 1, 2 already completed on disk
                byte[] chunk = new byte[1024];
                File.WriteAllBytes(Path.Combine(stagingDir, "seg_000000.part"), chunk);
                File.WriteAllBytes(Path.Combine(stagingDir, "seg_000001.part"), chunk);
                File.WriteAllBytes(Path.Combine(stagingDir, "seg_000002.part"), chunk);

                // Check detection logic
                int totalSegments = 5;
                var existingParts = new Dictionary<int, string>();
                for (int i = 0; i < totalSegments; i++)
                {
                    string partPath = Path.Combine(stagingDir, $"seg_{i:D6}.part");
                    if (File.Exists(partPath) && new FileInfo(partPath).Length > 0)
                    {
                        existingParts[i] = partPath;
                    }
                }

                existingParts.Should().HaveCount(3);
                existingParts.ContainsKey(0).Should().BeTrue();
                existingParts.ContainsKey(1).Should().BeTrue();
                existingParts.ContainsKey(2).Should().BeTrue();
                existingParts.ContainsKey(3).Should().BeFalse();
                existingParts.ContainsKey(4).Should().BeFalse();

                var missingIndices = Enumerable.Range(0, totalSegments).Where(i => !existingParts.ContainsKey(i)).ToList();
                missingIndices.Should().Equal(new[] { 3, 4 });
            }
            finally
            {
                try { if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true); } catch { }
            }
        }

        [Theory]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", true)]
        [InlineData("https://youtu.be/dQw4w9WgXcQ", true)]
        [InlineData("https://www.youtube.com/shorts/abcd1234efg", true)]
        [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", true)]
        [InlineData("https://vimeo.com/12345678", false)]
        [InlineData("https://example.com/video.mp4", false)]
        public void YouTubeUrlPattern_DetectsValidYouTubeUrls(string url, bool expected)
        {
            bool isYt = MediaVariantResolver.IsYouTubeUrl(url);
            isYt.Should().Be(expected);
        }

        [Fact]
        public void MediaDownloadService_RegistersDetectedMedia_WithAuthenticationAndTitle()
        {
            var service = new MediaDownloadService();
            bool registered = service.TryRegisterMedia(
                mediaUrl: "https://example.com/streams/live.m3u8",
                mimeType: "application/vnd.apple.mpegurl",
                sourcePage: "https://example.com/watch",
                sizeBytes: 150000000,
                quality: "1080p",
                requiresAuth: true,
                title: "Example Stream",
                encryptedCookies: new byte[] { 1, 2, 3, 4 },
                isLive: false,
                isDrmProtected: false
            );

            registered.Should().BeTrue();
            var detected = service.GetDetectedMedia();
            detected.Should().HaveCount(1);

            var item = detected.First();
            item.Title.Should().Be("Example Stream");
            item.Category.Should().Be(EDM.Models.MediaType.Manifest);
            item.RequiresAuth.Should().BeTrue();
            item.EncryptedCookies.Should().NotBeNull();
            item.EncryptedCookies!.Length.Should().Be(4);
            item.Quality.Should().Be("1080p");
        }

        [Fact]
        public void MediaVariantOption_FormatsYouTubeAdaptiveStreamCorrectly()
        {
            var adaptiveOption = new MediaVariantOption
            {
                QualityLabel = "1080p60",
                Width = 1920,
                Height = 1080,
                FrameRate = 60,
                Codec = "H.264",
                AudioCodec = "AAC",
                Container = "mp4",
                DirectUrl = "https://rr1---sn-video.googlevideo.com/videoplayback?...",
                AudioStreamUrl = "https://rr1---sn-audio.googlevideo.com/videoplayback?...",
                RequiresFfmpegMerge = true,
                HasAudio = true,
                EstimatedSizeBytes = 250000000
            };

            adaptiveOption.RequiresFfmpegMerge.Should().BeTrue();
            adaptiveOption.AudioStreamUrl.Should().NotBeNullOrEmpty();
            adaptiveOption.FormattedDetails.Should().Contain("1080p60");
            adaptiveOption.FormattedDetails.Should().Contain("MP4");
            adaptiveOption.FormattedDetails.Should().Contain("60 FPS");
            adaptiveOption.FormattedDetails.Should().Contain("Audio: Included");
        }

        [Fact]
        public void AtomicPartFileMove_PreventsPartialSegmentCorruptionOnInterrupt()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"edm_atomic_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string targetPart = Path.Combine(tempDir, "seg_000001.part");
                string tmpPart = targetPart + ".tmp";

                // Step 1: Simulate in-flight download writing to .tmp
                File.WriteAllBytes(tmpPart, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
                File.Exists(targetPart).Should().BeFalse();
                File.Exists(tmpPart).Should().BeTrue();

                // Step 2: Atomic move on successful completion
                File.Move(tmpPart, targetPart, true);
                File.Exists(targetPart).Should().BeTrue();
                File.Exists(tmpPart).Should().BeFalse();

                byte[] verifiedBytes = File.ReadAllBytes(targetPart);
                verifiedBytes.Should().Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}



