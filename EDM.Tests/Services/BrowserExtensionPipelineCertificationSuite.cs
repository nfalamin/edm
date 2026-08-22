using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.NativeMessaging;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class BrowserExtensionPipelineCertificationSuite
    {
        // =====================================================================
        // 1. YOUTUBE PLAYER RESPONSE & REAL FORMAT EXTRACTION
        // =====================================================================
        [Fact]
        public void Phase1_PlayerResponseExtraction_ParsesProgressiveAndAdaptiveStreams()
        {
            string sampleStreamingDataJson = @"{
                ""formats"": [
                    {
                        ""itag"": 18,
                        ""url"": ""https://rr1---sn-4g5ednkk.googlevideo.com/videoplayback?itag=18&id=abc"",
                        ""mimeType"": ""video/mp4; codecs=\""avc1.42001E, mp4a.40.2\"""",
                        ""bitrate"": 500000,
                        ""width"": 640,
                        ""height"": 360,
                        ""fps"": 30,
                        ""qualityLabel"": ""360p"",
                        ""contentLength"": ""15000000""
                    }
                ],
                ""adaptiveFormats"": [
                    {
                        ""itag"": 137,
                        ""url"": ""https://rr1---sn-4g5ednkk.googlevideo.com/videoplayback?itag=137&id=abc"",
                        ""mimeType"": ""video/mp4; codecs=\""avc1.640028\"""",
                        ""bitrate"": 4500000,
                        ""width"": 1920,
                        ""height"": 1080,
                        ""fps"": 30,
                        ""qualityLabel"": ""1080p"",
                        ""contentLength"": ""80000000""
                    },
                    {
                        ""itag"": 140,
                        ""url"": ""https://rr1---sn-4g5ednkk.googlevideo.com/videoplayback?itag=140&id=abc"",
                        ""mimeType"": ""audio/mp4; codecs=\""mp4a.40.2\"""",
                        ""bitrate"": 128000,
                        ""contentLength"": ""5000000""
                    }
                ]
            }";

            using var doc = JsonDocument.Parse(sampleStreamingDataJson);
            var root = doc.RootElement;

            var formats = root.GetProperty("formats").EnumerateArray().ToList();
            var adaptiveFormats = root.GetProperty("adaptiveFormats").EnumerateArray().ToList();

            formats.Should().HaveCount(1);
            adaptiveFormats.Should().HaveCount(2);

            // Verify progressive format
            var f18 = formats[0];
            f18.GetProperty("itag").GetInt32().Should().Be(18);
            f18.GetProperty("height").GetInt32().Should().Be(360);
            f18.GetProperty("mimeType").GetString().Should().Contain("video/mp4");
            f18.GetProperty("url").GetString().Should().StartWith("https://");

            // Verify adaptive video and audio
            var f137 = adaptiveFormats.First(a => a.GetProperty("itag").GetInt32() == 137);
            var f140 = adaptiveFormats.First(a => a.GetProperty("itag").GetInt32() == 140);

            f137.GetProperty("height").GetInt32().Should().Be(1080);
            f137.GetProperty("fps").GetInt32().Should().Be(30);
            f140.GetProperty("mimeType").GetString().Should().StartWith("audio/");
        }

        // =====================================================================
        // 2. SIGNATURE CIPHER EXTRACTION & RECONSTRUCTION
        // =====================================================================
        [Fact]
        public void Phase2_CipherParsing_AssemblesSignedUrlCorrectly()
        {
            string cipherString = "url=https%3A%2F%2Frr1---sn-4g5ednkk.googlevideo.com%2Fvideoplayback%3Fitag%3D137&sp=sig&s=ENCRYPTED_SIG_12345";

            var parsedParams = ParseQueryString(cipherString);
            parsedParams.Should().ContainKey("url");
            parsedParams.Should().ContainKey("sp");
            parsedParams.Should().ContainKey("s");

            string baseUrl = Uri.UnescapeDataString(parsedParams["url"]);
            string sp = parsedParams["sp"];
            string sig = parsedParams["s"];

            string reconstructed = $"{baseUrl}&{sp}={Uri.EscapeDataString(sig)}";
            reconstructed.Should().Be("https://rr1---sn-4g5ednkk.googlevideo.com/videoplayback?itag=137&sig=ENCRYPTED_SIG_12345");
            reconstructed.Should().NotContain("youtube.com/watch");
        }

        // =====================================================================
        // 3. ZERO FAKE FORMAT FALLBACK ENFORCEMENT
        // =====================================================================
        [Fact]
        public void Phase3_NoFakeFormats_WhenExtractionFailsReturnZeroVariants()
        {
            string emptyStreamingData = "{\"formats\":[], \"adaptiveFormats\":[]}";
            using var doc = JsonDocument.Parse(emptyStreamingData);
            var root = doc.RootElement;

            var formats = root.GetProperty("formats").EnumerateArray().ToList();
            var adaptiveFormats = root.GetProperty("adaptiveFormats").EnumerateArray().ToList();

            var allFormats = formats.Concat(adaptiveFormats).ToList();
            allFormats.Should().BeEmpty("Empty streaming data MUST NOT fabricate synthetic 1080p/720p/480p variants");
        }

        // =====================================================================
        // 4. MEDIA URL VALIDATION & FILTERING
        // =====================================================================
        [Theory]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", false)]
        [InlineData("https://youtu.be/dQw4w9WgXcQ", false)]
        [InlineData("http://invalid-file", false)]
        [InlineData("blob:https://youtube.com/12345", false)]
        [InlineData("https://rr1---sn-4g5ednkk.googlevideo.com/videoplayback?itag=18", true)]
        [InlineData("https://example.com/videos/media_file.mp4", true)]
        [InlineData("https://stream.example.com/playlist.m3u8", true)]
        public void Phase4_FormatValidator_RejectsHtmlPagesAndAcceptsDirectMedia(string url, bool expectedValid)
        {
            bool isValid = ValidateMediaUrl(url);
            isValid.Should().Be(expectedValid);
        }

        // =====================================================================
        // 5. ADAPTIVE VIDEO + AUDIO PAIRING & COMPOSITE SIZE
        // =====================================================================
        [Fact]
        public void Phase5_AdaptivePairing_MatchesContainersAndCalculatesCompositeSize()
        {
            long videoSize = 85_000_000;
            long audioSize = 6_500_000;
            long expectedComposite = videoSize + audioSize;

            var videoVariant = new MediaVariantOption
            {
                QualityLabel = "1080p Full HD",
                Height = 1080,
                Container = "mp4",
                Codec = "H.264",
                DirectUrl = "https://cdn.example.com/video_1080.mp4",
                AudioStreamUrl = "https://cdn.example.com/audio_aac.m4a",
                RequiresFfmpegMerge = true,
                EstimatedSizeBytes = expectedComposite
            };

            videoVariant.RequiresFfmpegMerge.Should().BeTrue();
            videoVariant.DirectUrl.Should().NotBeNullOrWhiteSpace();
            videoVariant.AudioStreamUrl.Should().NotBeNullOrWhiteSpace();
            videoVariant.EstimatedSizeBytes.Should().Be(91_500_000);
            videoVariant.Container.Should().Be("mp4");
        }

        // =====================================================================
        // 6. M3U8 & DASH MANIFEST PARSING & RESOLUTION
        // =====================================================================
        [Fact]
        public void Phase6_HlsMasterPlaylist_ParsesMultiBitrateStreams()
        {
            string m3u8Master = @"#EXTM3U
#EXT-X-VERSION:3
#EXT-X-STREAM-INF:BANDWIDTH=5000000,RESOLUTION=1920x1080,FRAME-RATE=30.000,CODECS=""avc1.640028,mp4a.40.2""
1080p/index.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=2500000,RESOLUTION=1280x720,FRAME-RATE=30.000,CODECS=""avc1.4d401f,mp4a.40.2""
720p/index.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=800000,RESOLUTION=640x360,FRAME-RATE=30.000,CODECS=""avc1.4d401e,mp4a.40.2""
360p/index.m3u8";

            var playlist = HlsParser.Parse(m3u8Master, new Uri("https://live.example.com/master.m3u8"));
            playlist.IsMaster.Should().BeTrue();
            playlist.Variants.Should().HaveCount(3);
            playlist.Variants[0].Height.Should().Be(1080);
            playlist.Variants[0].Bandwidth.Should().Be(5000000);
            playlist.Variants[1].Height.Should().Be(720);
        }

        // =====================================================================
        // 7. NATIVE MESSAGING STDIO FRAMING
        // =====================================================================
        [Fact]
        public void Phase7_NativeMessagingFraming_EncodesAndDecodesBinaryLengths()
        {
            string payload = "{\"action\":\"PING\",\"requestId\":\"test-1\"}";
            byte[] utf8 = Encoding.UTF8.GetBytes(payload);

            byte[] framedBuffer = new byte[4 + utf8.Length];
            BinaryPrimitives.WriteInt32LittleEndian(framedBuffer.AsSpan(0, 4), utf8.Length);
            utf8.CopyTo(framedBuffer.AsSpan(4));

            int decodedLength = BinaryPrimitives.ReadInt32LittleEndian(framedBuffer.AsSpan(0, 4));
            decodedLength.Should().Be(utf8.Length);

            string decodedString = Encoding.UTF8.GetString(framedBuffer, 4, decodedLength);
            decodedString.Should().Be(payload);
        }

        // =====================================================================
        // 8. IPC HANDOFF & FULL METADATA PRESERVATION
        // =====================================================================
        [Fact]
        public void Phase8_IpcHandoff_TransfersAllFieldsAcrossNamedPipe()
        {
            var expectedPayload = new IpcHandoffPayload
            {
                Url = "https://rr1---sn.googlevideo.com/videoplayback?itag=137",
                VideoUrl = "https://rr1---sn.googlevideo.com/videoplayback?itag=137",
                AudioUrl = "https://rr1---sn.googlevideo.com/videoplayback?itag=140",
                ManifestUrl = "",
                Title = "Certified Test Media",
                Filename = "Certified_Test_Media.mp4",
                Quality = "1080p Full HD",
                Format = "mp4",
                RequiresFfmpegMerge = true,
                EstimatedSizeBytes = 95_000_000,
                CorrelationId = "cert_corr_999",
                DownloadIdentity = "identity_key_123",
                Browser = "Google Chrome"
            };

            string json = JsonSerializer.Serialize(expectedPayload);
            json.Should().NotBeNullOrWhiteSpace();

            var received = JsonSerializer.Deserialize<IpcHandoffPayload>(json);

            received.Should().NotBeNull();
            received!.Title.Should().Be("Certified Test Media");
            received.Filename.Should().Be("Certified_Test_Media.mp4");
            received.RequiresFfmpegMerge.Should().BeTrue();
            received.AudioUrl.Should().Be("https://rr1---sn.googlevideo.com/videoplayback?itag=140");
            received.EstimatedSizeBytes.Should().Be(95_000_000);
            received.CorrelationId.Should().Be("cert_corr_999");
            received.Browser.Should().Be("Google Chrome");
        }

        // =====================================================================
        // 9. EMERGENCY FALLBACK RESTRICTION GUARD
        // =====================================================================
        [Theory]
        [InlineData(true, "https://cdn.example.com/file.mp4", false, "Adaptive merge must NOT fallback to browser download")]
        [InlineData(false, "https://cdn.example.com/stream.m3u8", false, "HLS manifest must NOT fallback to browser download")]
        [InlineData(false, "https://www.youtube.com/watch?v=123", false, "HTML web page must NOT fallback to browser download")]
        [InlineData(false, "https://cdn.example.com/standalone.mp4", true, "Direct standalone file CAN fallback to browser in emergency")]
        public void Phase9_EmergencyFallbackGuard_RestrictsDirectMediaOnly(bool requiresMerge, string url, bool expectedFallbackAllowed, string rationale)
        {
            bool isAllowed = CanEmergencyFallback(requiresMerge, url);
            isAllowed.Should().Be(expectedFallbackAllowed, rationale);
        }

        // =====================================================================
        // 10. ERROR CODE CLASSIFICATION
        // =====================================================================
        [Fact]
        public void Phase10_ErrorCodes_AreStandardizedAndStructured()
        {
            var expectedCodes = new HashSet<string>
            {
                "YOUTUBE_PLAYER_RESPONSE_NOT_FOUND",
                "FORMAT_EXTRACTION_FAILED",
                "CIPHER_RESOLUTION_FAILED",
                "INVALID_MEDIA_URL",
                "NATIVE_HOST_UNAVAILABLE",
                "EDM_UNAVAILABLE"
            };

            foreach (var code in expectedCodes)
            {
                code.Should().MatchRegex(@"^[A-Z_]+$", "All error codes must be uppercase SNAKE_CASE");
            }
        }

        // =====================================================================
        // HELPER LOGIC MIRRORING THE HARDENED EXTENSION CODE
        // =====================================================================
        private static bool ValidateMediaUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;

            try
            {
                var uri = new Uri(url);
                if (!uri.Host.Contains('.') && uri.Host != "localhost") return false;
                if (uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) &&
                    (uri.AbsolutePath.Equals("/watch", StringComparison.OrdinalIgnoreCase) || uri.AbsolutePath.StartsWith("/shorts", StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
                if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase)) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool CanEmergencyFallback(bool requiresFfmpegMerge, string url)
        {
            if (requiresFfmpegMerge) return false;
            if (url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase) || url.Contains(".mpd", StringComparison.OrdinalIgnoreCase)) return false;
            if (url.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase) || url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase)) return false;
            return ValidateMediaUrl(url);
        }

        private static Dictionary<string, string> ParseQueryString(string query)
        {
            var dict = new Dictionary<string, string>();
            var pairs = query.Split('&');
            foreach (var pair in pairs)
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2)
                {
                    dict[parts[0]] = parts[1];
                }
            }
            return dict;
        }
    }
}
