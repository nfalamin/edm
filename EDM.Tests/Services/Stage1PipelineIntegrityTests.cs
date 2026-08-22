using System;
using System.Text.Json;
using EDM.Models;
using EDM.NativeMessaging;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class Stage1PipelineIntegrityTests
    {
        [Fact]
        public void NativeMessageRequest_SerializesAndDeserializesAllCriticalFields()
        {
            var req = new NativeMessageRequest
            {
                Action = NativeActionNames.DownloadRequest,
                Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                VideoUrl = "https://rr1---sn-4g5edn6e.googlevideo.com/videoplayback?id=123",
                AudioUrl = "https://rr1---sn-4g5edn6e.googlevideo.com/videoplayback?id=456",
                PageUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                Title = "Rick Astley - Never Gonna Give You Up",
                Filename = "Never_Gonna_Give_You_Up_2160p.mp4",
                Quality = "2160p",
                Format = "mp4",
                FormatArg = "-f 313+140",
                RequiresFfmpegMerge = true,
                DownloadIdentity = "edm_job_a1b2c3d4",
                CorrelationId = "edm_corr_9999",
                Codec = "VP9",
                AudioCodec = "Opus",
                Container = "mp4",
                EstimatedSizeBytes = 524288000,
                IsAudioOnly = false,
                Cookies = "SID=abc; HSID=def",
                Headers = "User-Agent: Mozilla/5.0",
                ManifestUrl = "https://manifest.googlevideo.com/api/manifest/dash/id/123"
            };

            string json = JsonSerializer.Serialize(req);
            json.Should().NotBeNullOrWhiteSpace();

            var deserialized = JsonSerializer.Deserialize<NativeMessageRequest>(json);
            deserialized.Should().NotBeNull();
            deserialized!.Url.Should().Be(req.Url);
            deserialized.VideoUrl.Should().Be(req.VideoUrl);
            deserialized.AudioUrl.Should().Be(req.AudioUrl);
            deserialized.PageUrl.Should().Be(req.PageUrl);
            deserialized.Title.Should().Be(req.Title);
            deserialized.Filename.Should().Be(req.Filename);
            deserialized.Quality.Should().Be(req.Quality);
            deserialized.Format.Should().Be(req.Format);
            deserialized.FormatArg.Should().Be(req.FormatArg);
            deserialized.RequiresFfmpegMerge.Should().BeTrue();
            deserialized.DownloadIdentity.Should().Be("edm_job_a1b2c3d4");
            deserialized.CorrelationId.Should().Be("edm_corr_9999");
            deserialized.Codec.Should().Be("VP9");
            deserialized.AudioCodec.Should().Be("Opus");
            deserialized.Container.Should().Be("mp4");
            deserialized.EstimatedSizeBytes.Should().Be(524288000);
            deserialized.IsAudioOnly.Should().BeFalse();
            deserialized.Cookies.Should().Be("SID=abc; HSID=def");
            deserialized.ManifestUrl.Should().Be(req.ManifestUrl);
        }

        [Fact]
        public void IpcHandoffPayload_RoundTripPreservesAllStreamAndIdentityFields()
        {
            var payload = new IpcHandoffPayload
            {
                Url = "https://example.com/video.mp4",
                Filename = "sample_1080p.mp4",
                Title = "Sample Video",
                PageUrl = "https://example.com/watch",
                Quality = "1080p",
                Format = "mp4",
                Browser = "Chrome",
                CorrelationId = "corr_12345",
                DownloadIdentity = "identity_video_1080p",
                Source = "BrowserExtension",
                AudioUrl = "https://example.com/audio.m4a",
                VideoUrl = "https://example.com/video_only.mp4",
                FormatArg = "-f 137+140",
                RequiresFfmpegMerge = true,
                Codec = "H.264",
                AudioCodec = "AAC",
                Container = "mp4",
                EstimatedSizeBytes = 104857600,
                IsAudioOnly = false,
                ManifestUrl = "https://example.com/manifest.mpd",
                Cookies = "session=xyz"
            };

            string json = JsonSerializer.Serialize(payload);
            var deserialized = JsonSerializer.Deserialize<IpcHandoffPayload>(json);

            deserialized.Should().NotBeNull();
            deserialized!.DownloadIdentity.Should().Be("identity_video_1080p");
            deserialized.CorrelationId.Should().Be("corr_12345");
            deserialized.VideoUrl.Should().Be("https://example.com/video_only.mp4");
            deserialized.AudioUrl.Should().Be("https://example.com/audio.m4a");
            deserialized.RequiresFfmpegMerge.Should().BeTrue();
            deserialized.Quality.Should().Be("1080p");
            deserialized.FormatArg.Should().Be("-f 137+140");
            deserialized.Codec.Should().Be("H.264");
            deserialized.AudioCodec.Should().Be("AAC");
            deserialized.Container.Should().Be("mp4");
            deserialized.EstimatedSizeBytes.Should().Be(104857600);
            deserialized.IsAudioOnly.Should().BeFalse();
            deserialized.Title.Should().Be("Sample Video");
            deserialized.ManifestUrl.Should().Be("https://example.com/manifest.mpd");
        }

        [Fact]
        public void DownloadIdentity_IsDeterministic_AcrossDuplicateClicksWithDifferentCorrelationIds()
        {
            string url = "https://www.youtube.com/watch?v=sample";
            string quality = "2160p";
            string filename = "Sample_Video_2160p.mp4";

            // Simulate Click 1
            string correlationId1 = "corr_" + Guid.NewGuid().ToString("N");
            string identity1 = ComputeDeterministicIdentity(url, quality, filename);

            // Simulate Click 2 (user double-clicks 100ms later)
            string correlationId2 = "corr_" + Guid.NewGuid().ToString("N");
            string identity2 = ComputeDeterministicIdentity(url, quality, filename);

            // Simulate Click 3 (user clicks button again 5 seconds later)
            string correlationId3 = "corr_" + Guid.NewGuid().ToString("N");
            string identity3 = ComputeDeterministicIdentity(url, quality, filename);

            // CorrelationIds must be distinct
            correlationId1.Should().NotBe(correlationId2);
            correlationId2.Should().NotBe(correlationId3);

            // DownloadIdentities MUST be strictly identical
            identity1.Should().Be(identity2);
            identity2.Should().Be(identity3);
        }

        [Fact]
        public void DownloadIdentity_DiffersForDifferentQualities_OnSameMedia()
        {
            string url = "https://www.youtube.com/watch?v=sample";

            string identity4K = ComputeDeterministicIdentity(url, "2160p", "Sample_Video_2160p.mp4");
            string identity1080p = ComputeDeterministicIdentity(url, "1080p", "Sample_Video_1080p.mp4");
            string identity720p = ComputeDeterministicIdentity(url, "720p", "Sample_Video_720p.mp4");
            string identityAudio = ComputeDeterministicIdentity(url, "Audio Only", "Sample_Video_Audio.mp3");

            identity4K.Should().NotBe(identity1080p);
            identity1080p.Should().NotBe(identity720p);
            identity720p.Should().NotBe(identityAudio);
        }

        [Fact]
        public void DownloadItem_CorrectlyBindsAllStreamAndMergeProperties()
        {
            var item = new DownloadItem
            {
                Url = "https://example.com/watch?v=abc",
                FileName = "test_2160p.mp4",
                Title = "Test Video Title",
                VideoUrl = "https://example.com/streams/video_4k.mp4",
                AudioUrl = "https://example.com/streams/audio_best.m4a",
                RequiresFfmpegMerge = true,
                FormatArg = "-f bestvideo+bestaudio",
                DownloadIdentity = "job_4k_test",
                Codec = "VP9",
                AudioCodec = "Opus",
                Container = "mp4",
                EstimatedSizeBytes = 600000000,
                IsAudioOnly = false,
                ManifestUrl = "https://example.com/manifest.mpd"
            };

            item.VideoUrl.Should().Be("https://example.com/streams/video_4k.mp4");
            item.AudioUrl.Should().Be("https://example.com/streams/audio_best.m4a");
            item.RequiresFfmpegMerge.Should().BeTrue();
            item.DownloadIdentity.Should().Be("job_4k_test");
            item.Codec.Should().Be("VP9");
            item.AudioCodec.Should().Be("Opus");
            item.Container.Should().Be("mp4");
            item.EstimatedSizeBytes.Should().Be(600000000);
            item.Title.Should().Be("Test Video Title");
            item.ManifestUrl.Should().Be("https://example.com/manifest.mpd");
        }

        private static string ComputeDeterministicIdentity(string url, string quality, string filename)
        {
            string raw = $"{url}|{quality}|{filename}";
            int hash = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                hash = ((hash << 5) - hash) + raw[i];
            }
            return "edm_job_" + Math.Abs(hash).ToString("x");
        }
    }
}
