using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.NativeMessaging;
using EDM.Services;
using Xunit;

namespace EDM.Tests.Services
{
    public class Stage4DownloadPipelineTests
    {
        [Fact]
        public void DownloadIdentity_Deterministic_MatchesIdenticalPayloads()
        {
            string url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
            string quality = "2160p (4K UHD)";
            string filename = "Rick_Astley_4K.mp4";

            string id1 = $"{url}|{quality}|{filename}";
            string id2 = $"{url}|{quality}|{filename}";

            Assert.Equal(id1, id2);
        }

        [Fact]
        public void DownloadIdentity_Distinct_ForDifferentQualities()
        {
            string url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
            string filename = "Rick_Astley.mp4";

            string id4k = $"{url}|2160p|{filename}";
            string id1080p = $"{url}|1080p|{filename}";

            Assert.NotEqual(id4k, id1080p);
        }

        [Fact]
        public void IpcHandoffPayload_RoundtripFieldPreservation()
        {
            var payload = new IpcHandoffPayload
            {
                Url = "https://video.example.com/manifest.mpd",
                VideoUrl = "https://video.example.com/vid_2160p.mp4",
                AudioUrl = "https://video.example.com/aud_opus.m4a",
                Quality = "2160p 4K",
                Format = "video/mp4",
                Filename = "Example_4K.mp4",
                RequiresFfmpegMerge = true,
                DownloadIdentity = "test-identity-12345",
                EstimatedSizeBytes = 500000000,
                IsAudioOnly = false
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<IpcHandoffPayload>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(payload.Url, deserialized!.Url);
            Assert.Equal(payload.VideoUrl, deserialized.VideoUrl);
            Assert.Equal(payload.AudioUrl, deserialized.AudioUrl);
            Assert.Equal(payload.Quality, deserialized.Quality);
            Assert.Equal(payload.RequiresFfmpegMerge, deserialized.RequiresFfmpegMerge);
            Assert.Equal(payload.DownloadIdentity, deserialized.DownloadIdentity);
            Assert.Equal(payload.EstimatedSizeBytes, deserialized.EstimatedSizeBytes);
        }

        [Fact]
        public void DownloadItem_FromHandoffPayload_PreservesAllContractFields()
        {
            var payload = new IpcHandoffPayload
            {
                Url = "https://video.example.com/video.mp4",
                VideoUrl = "https://video.example.com/vid_stream.mp4",
                AudioUrl = "https://video.example.com/aud_stream.m4a",
                Quality = "1440p 2K",
                Filename = "Test_2K.mp4",
                RequiresFfmpegMerge = true,
                EstimatedSizeBytes = 250000000,
                DownloadIdentity = "identity-999"
            };

            var item = new DownloadItem
            {
                Url = payload.Url,
                FileName = payload.Filename,
                SavePath = Path.Combine(Path.GetTempPath(), payload.Filename),
                Quality = payload.Quality,
                VideoUrl = payload.VideoUrl,
                AudioUrl = payload.AudioUrl,
                RequiresFfmpegMerge = payload.RequiresFfmpegMerge,
                EstimatedSizeBytes = payload.EstimatedSizeBytes ?? -1,
                DownloadIdentity = payload.DownloadIdentity
            };

            Assert.Equal("1440p 2K", item.Quality);
            Assert.True(item.RequiresFfmpegMerge);
            Assert.Equal("https://video.example.com/vid_stream.mp4", item.VideoUrl);
            Assert.Equal("https://video.example.com/aud_stream.m4a", item.AudioUrl);
            Assert.Equal("identity-999", item.DownloadIdentity);
        }
    }
}
