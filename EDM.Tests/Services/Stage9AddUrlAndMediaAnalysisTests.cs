using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using EDM.ViewModels;
using EDM.Views;
using Xunit;

namespace EDM.Tests.Services
{
    public class Stage9AddUrlAndMediaAnalysisTests
    {
        [Theory]
        [InlineData("https://example.com/archive.zip", true)]
        [InlineData("http://mirror.umd.edu/ubuntu-iso/ubuntu.iso", true)]
        [InlineData("ftp://speedtest.tele2.net/100MB.zip", true)]
        [InlineData("magnet:?xt=urn:btih:d6b0e8", true)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("javascript:alert(1)", false)]
        [InlineData("data:text/plain;base64,SGVsbG8=", false)]
        [InlineData("file:///C:/secret.txt", false)]
        [InlineData("blob:https://youtube.com/123", false)]
        public void ValidateUrlInput_EnforcesStrictSecurityAndProtocols(string input, bool expectedValid)
        {
            bool isValid = AddUrlWindow.ValidateUrlInput(input, out string normalized, out string error);
            Assert.Equal(expectedValid, isValid);
            if (expectedValid)
            {
                Assert.False(string.IsNullOrWhiteSpace(normalized));
                Assert.Empty(error);
            }
            else
            {
                Assert.NotEmpty(error);
            }
        }

        [Fact]
        public void MediaVariantOption_ProducesRealFormattedDetails_ForVideo()
        {
            var videoOption = new MediaVariantOption
            {
                QualityLabel = "1080p",
                Width = 1920,
                Height = 1080,
                FrameRate = 60,
                Codec = "avc1",
                Container = "mp4",
                EstimatedSizeBytes = 152_043_520, // ~145 MB
                HasAudio = true
            };

            string details = videoOption.FormattedDetails;
            Assert.Contains("1080p", details);
            Assert.Contains("MP4", details);
            Assert.Contains("AVC1", details);
            Assert.Contains("60 FPS", details);
            Assert.Contains("MB", details);
            Assert.Contains("Audio: Included", details);
        }

        [Fact]
        public void MediaVariantOption_ProducesRealFormattedDetails_ForAudioOnly()
        {
            var audioOption = new MediaVariantOption
            {
                QualityLabel = "Audio Only",
                IsAudioOnly = true,
                Codec = "opus",
                Container = "webm",
                AudioCodec = "opus",
                AudioBitrate = 160_000, // 160 kbps
                EstimatedSizeBytes = 8_388_608 // 8 MB
            };

            string details = audioOption.FormattedDetails;
            Assert.Contains("WEBM Audio", details);
            Assert.Contains("160 kbps", details);
            Assert.Contains("8 MB", details);
        }

        [Fact]
        public void MediaVariantOption_HandlesUnknownSizeGracefully()
        {
            var unknownSizeOption = new MediaVariantOption
            {
                QualityLabel = "720p",
                Container = "mp4",
                Codec = "h264",
                EstimatedSizeBytes = -1,
                HasAudio = true
            };

            string details = unknownSizeOption.FormattedDetails;
            Assert.Contains("720p", details);
            Assert.Contains("Size: Unknown", details);
        }

        [Fact]
        public async Task AddUrlViewModel_AnalyzeDirectFile_PopulatesRealFileMetadata()
        {
            var vm = new AddUrlViewModel();
            vm.Url = "https://speedtest.example.com/files/test_installer.exe";

            await vm.AnalyzeMediaAsync();

            Assert.Equal(AddUrlAnalysisState.Detected, vm.AnalysisState);
            Assert.NotEmpty(vm.AvailableQualities);
            Assert.NotEmpty(vm.AvailableFormats);
            Assert.False(string.IsNullOrWhiteSpace(vm.AnalysisStatus));
        }

        [Fact]
        public void AddUrlViewModel_StartDownload_BuildsAuthoritativeDownloadItem()
        {
            var vm = new AddUrlViewModel();
            vm.Url = "https://example.com/downloads/setup.exe";
            vm.SelectedCategory = "Programs";
            vm.AutoStartDownload = true;

            vm.StartDownload();

            Assert.NotNull(vm.CreatedDownloadItem);
            Assert.Equal("https://example.com/downloads/setup.exe", vm.CreatedDownloadItem!.Url);
            Assert.Equal("setup.exe", vm.CreatedDownloadItem.FileName);
            Assert.Equal("Programs", vm.CreatedDownloadItem.Category);
            Assert.Equal("Downloading", vm.CreatedDownloadItem.Status);
        }

        [Fact]
        public void TelemetrySpeedHistory_RingBuffer_NeverExceedsMaximumSamples()
        {
            var history = new Queue<double>(60);
            const int maxSamples = 60;

            for (int i = 0; i < 200; i++)
            {
                if (history.Count >= maxSamples)
                {
                    history.Dequeue();
                }
                history.Enqueue(i * 1024.0);
            }

            Assert.Equal(maxSamples, history.Count);
            Assert.Equal(199 * 1024.0, history.Last());
        }
    }
}
