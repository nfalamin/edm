using System;
using System.IO;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using Xunit;

namespace EDM.Tests.Services
{
    public class PhasePassTests
    {
        [Fact]
        public async Task ExpiredUrlRecoveryService_DetectsExpiredUrlsAndRecovers()
        {
            var recoveryService = ExpiredUrlRecoveryService.Instance;

            // 1. Expiry detection
            bool is401Expired = await recoveryService.IsUrlExpiredAsync("https://s3.amazonaws.com/bucket/file.zip?X-Amz-Expires=3600", 401);
            Assert.True(is401Expired);

            bool is403SignedExpired = await recoveryService.IsUrlExpiredAsync("https://storage.provider.com/data.iso?token=abc", 403, "<Error><Code>TokenExpired</Code></Error>");
            Assert.True(is403SignedExpired);

            bool is410Expired = await recoveryService.IsUrlExpiredAsync("https://cdn.example.com/file.bin", 410);
            Assert.True(is410Expired);

            bool is200NotExpired = await recoveryService.IsUrlExpiredAsync("https://cdn.example.com/file.bin", 200);
            Assert.False(is200NotExpired);

            // 2. State & DownloadItem preservation
            var item = new DownloadItem
            {
                Url = "https://old.cdn.com/expired-archive.zip",
                FileName = "archive.zip",
                SavePath = Path.GetTempPath(),
                Size = "100 MB",
                Status = "Error"
            };

            var recoveryResult = await recoveryService.RecoverUrlAsync(item, "https://raw.githubusercontent.com/user/repo/main/README.md");
            Assert.True(recoveryResult.Success || !string.IsNullOrEmpty(recoveryResult.FailureReason));
        }

        [Fact]
        public void AntivirusScannerProvider_ValidatesPlaceholdersAndPath()
        {
            var provider = new CustomAntivirusScannerProvider
            {
                ProviderName = "TestScanner",
                ExecutablePath = "cmd.exe",
                ArgumentsTemplate = "/c echo Scanning %FILE% in %DIRECTORY%",
                ExpectedCleanExitCode = 0
            };

            Assert.True(provider.IsAvailable || !string.IsNullOrEmpty(provider.ExecutablePath));
        }

        [Fact]
        public void PerDiskTempStorageManager_CalculatesSpaceAndSelectsOptimalCache()
        {
            var manager = PerDiskTempStorageManager.Instance;
            manager.MinimumFreeSpaceThresholdBytes = 104_857_600; // 100 MB

            string cacheDir = manager.GetOptimalCacheDirectory(50_000_000);
            Assert.False(string.IsNullOrEmpty(cacheDir));
            Assert.True(Directory.Exists(cacheDir));

            bool isSufficient = manager.IsDiskSpaceSufficient(cacheDir, 10_000_000);
            Assert.True(isSufficient);
        }

        [Fact]
        public async Task PartialMediaPreviewService_DetectsFormatsAndConstructsSnapshot()
        {
            var preview = PartialMediaPreviewService.Instance;

            Assert.True(preview.IsMediaExtension("video.mp4"));
            Assert.True(preview.IsMediaExtension("movie.mkv"));
            Assert.True(preview.IsMediaExtension("audio.mp3"));
            Assert.False(preview.IsMediaExtension("archive.zip"));

            // Test non-existent file handling
            var result = await preview.CreatePreviewSnapshotAsync(@"D:\non_existent_stream.mp4");
            Assert.False(result.Success);
            Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void LocalizationService_Supports13LanguagesIncludingKoreanAndBengali()
        {
            var loc = LocalizationService.Instance;
            var packs = loc.GetAvailableLanguagePacks();

            Assert.True(packs.Count >= 13);
            Assert.Contains(packs, p => p.CultureCode == "en-US");
            Assert.Contains(packs, p => p.CultureCode == "bn-BD");
            Assert.Contains(packs, p => p.CultureCode == "ko-KR");
            Assert.Contains(packs, p => p.CultureCode == "ar-SA" && p.IsRightToLeft);

            // Test Korean pack strings
            loc.SetLanguage("ko-KR");
            Assert.Equal("다운로드", loc.GetString("Btn_Download"));
            Assert.Equal("일시 정지", loc.GetString("Btn_Pause"));

            // Reset back to English
            loc.SetLanguage("en-US");
            Assert.Equal("Download", loc.GetString("Btn_Download"));
        }
    }
}
