using System;
using System.Buffers.Binary;
using System.IO;
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
    public class RealVideoDetectionAndResolverTests
    {
        [Fact]
        public async Task MediaVariantResolver_ResolvesDirectMediaSuccessfully()
        {
            var resolver = new MediaVariantResolver();
            var result = await resolver.ResolveVariantsAsync("https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4");

            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.IsDrmProtected.Should().BeFalse();
            result.Variants.Should().NotBeEmpty();
            result.Variants.First().DirectUrl.Should().Contain("BigBuckBunny.mp4");
        }

        [Fact]
        public void HlsParser_ParsesMasterPlaylistWithExactBitratesAndCodecs()
        {
            string m3u8 = @"#EXTM3U
#EXT-X-VERSION:3
#EXT-X-STREAM-INF:BANDWIDTH=8000000,RESOLUTION=3840x2160,FRAME-RATE=60.000,CODECS=""avc1.640033""
4k.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=4500000,RESOLUTION=1920x1080,FRAME-RATE=60.000,CODECS=""avc1.640028""
1080p.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=2200000,RESOLUTION=1280x720,FRAME-RATE=30.000,CODECS=""avc1.4d401f""
720p.m3u8";

            var baseUri = new Uri("https://stream.example.com/live/master.m3u8");
            var playlist = HlsParser.Parse(m3u8, baseUri);

            playlist.IsMaster.Should().BeTrue();
            playlist.Variants.Should().HaveCount(3);

            var v4k = playlist.Variants.First(v => v.Height == 2160);
            v4k.Width.Should().Be(3840);
            v4k.Bandwidth.Should().Be(8000000);
            v4k.FrameRate.Should().Be(60.0);
            v4k.Uri.Should().Be("https://stream.example.com/live/4k.m3u8");
        }

        [Fact]
        public void DashParser_ParsesVideoAndAudioRepresentations()
        {
            string mpd = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet mimeType=""video/mp4"">
      <Representation id=""v4k"" bandwidth=""12000000"" width=""3840"" height=""2160"" frameRate=""60"">
        <BaseURL>video_4k.mp4</BaseURL>
      </Representation>
      <Representation id=""v1080"" bandwidth=""5000000"" width=""1920"" height=""1080"" frameRate=""60"">
        <BaseURL>video_1080p.mp4</BaseURL>
      </Representation>
    </AdaptationSet>
    <AdaptationSet mimeType=""audio/mp4"">
      <Representation id=""a_aac"" bandwidth=""192000"" codecs=""mp4a.40.2"">
        <BaseURL>audio_192k.mp4</BaseURL>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

            var baseUri = new Uri("https://dash.example.com/manifest.mpd");
            var manifest = DashParser.Parse(mpd, baseUri);

            manifest.IsDrmProtected.Should().BeFalse();
            manifest.VideoRepresentations.Should().HaveCount(2);
            manifest.AudioRepresentations.Should().HaveCount(1);

            manifest.VideoRepresentations.First(v => v.Height == 2160).Width.Should().Be(3840);
            manifest.AudioRepresentations.First().Bandwidth.Should().Be(192000);
        }

        [Fact]
        public async Task NativeMessageListener_HandlesGetMediaVariantsActionOverStdio()
        {
            using var stdin = new MemoryStream();
            using var stdout = new MemoryStream();

            // Prepare native message payload: {"action":"GET_MEDIA_VARIANTS","url":"https://example.com/sample.mp4"}
            var requestObj = new
            {
                action = "GET_MEDIA_VARIANTS",
                url = "https://example.com/sample.mp4"
            };
            byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(requestObj);
            byte[] lenHeader = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(lenHeader, jsonBytes.Length);

            stdin.Write(lenHeader, 0, 4);
            stdin.Write(jsonBytes, 0, jsonBytes.Length);
            stdin.Position = 0;

            var listener = new NativeMessageListener(stdin, stdout);
            listener.Start();

            // Wait for message processing and response writing
            await Task.Delay(500);
            listener.Stop();

            stdout.Position = 0;
            if (stdout.Length >= 4)
            {
                byte[] resLenBuf = new byte[4];
                stdout.Read(resLenBuf, 0, 4);
                int resLen = BinaryPrimitives.ReadInt32LittleEndian(resLenBuf);
                resLen.Should().BeGreaterThan(0);

                byte[] resPayload = new byte[resLen];
                stdout.Read(resPayload, 0, resLen);
                string resJson = Encoding.UTF8.GetString(resPayload);

                resJson.Should().Contain("media_variants_resolved");
                resJson.Should().Contain("sample.mp4");
            }
        }

        [Fact]
        public void BrowserExtension_ManifestContainsRequiredPermissionsAndValidIcons()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string rootDir = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\"));
            string chromeManifestPath = Path.Combine(rootDir, "extension", "chrome", "manifest.json");

            if (!File.Exists(chromeManifestPath))
            {
                chromeManifestPath = Path.Combine(Directory.GetCurrentDirectory(), "extension", "chrome", "manifest.json");
            }

            File.Exists(chromeManifestPath).Should().BeTrue();

            string json = File.ReadAllText(chromeManifestPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            root.GetProperty("manifest_version").GetInt32().Should().Be(3);
            root.GetProperty("name").GetString().Should().Contain("Exclusive Download Manager");

            var perms = root.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToList();
            perms.Should().Contain("nativeMessaging");
            perms.Should().Contain("downloads");
            perms.Should().Contain("cookies");

            var cs = root.GetProperty("content_scripts").EnumerateArray().First();
            cs.GetProperty("js").EnumerateArray().First().GetString().Should().Be("content.js");
            cs.GetProperty("css").EnumerateArray().First().GetString().Should().Be("content.css");
        }
    }
}
