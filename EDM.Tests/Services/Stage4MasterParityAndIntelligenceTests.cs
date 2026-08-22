using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using Xunit;

namespace EDM.Tests.Services
{
    public class Stage4MasterParityAndIntelligenceTests
    {
        [Fact]
        public async Task DownloadListImportExport_JsonAndPlainText_RoundtripsSuccessfully()
        {
            var service = DownloadListImportExportService.Instance;
            string tempJson = Path.GetTempFileName();
            string tempTxt = Path.GetTempFileName();

            try
            {
                var tasks = new List<DownloadItem>
                {
                    new DownloadItem { Url = "https://example.com/file1.iso", FileName = "file1.iso", Category = "Software", Size = "1 MB" },
                    new DownloadItem { Url = "https://example.com/video2.mp4", FileName = "video2.mp4", Category = "Video", Size = "5 MB" }
                };

                // JSON Export & Import
                await service.ExportToJsonAsync(tasks, tempJson);
                var importedJson = await service.ImportFromJsonAsync(tempJson);
                Assert.Equal(2, importedJson.Count);
                Assert.Equal("https://example.com/file1.iso", importedJson[0].Url);
                Assert.Equal("Software", importedJson[0].Category);

                // PlainText Export & Import
                await service.ExportToPlainTextUrlsAsync(tasks, tempTxt);
                var importedTxt = await service.ImportFromPlainTextUrlsAsync(tempTxt);
                Assert.Equal(2, importedTxt.Count);
                Assert.Equal("https://example.com/file1.iso", importedTxt[0].Url);
            }
            finally
            {
                if (File.Exists(tempJson)) File.Delete(tempJson);
                if (File.Exists(tempTxt)) File.Delete(tempTxt);
            }
        }

        [Fact]
        public async Task DownloadListImportExport_IdmEf2Format_RoundtripsSuccessfully()
        {
            var service = DownloadListImportExportService.Instance;
            string tempEf2 = Path.GetTempFileName();

            try
            {
                var tasks = new List<DownloadItem>
                {
                    new DownloadItem
                    {
                        Url = "https://download.example.com/release.zip",
                        Cookies = "session=xyz123",
                        FileName = "release.zip",
                        SavePath = @"C:\Downloads"
                    }
                };

                await service.ExportToIdmEf2Async(tasks, tempEf2);
                string rawContent = await File.ReadAllTextAsync(tempEf2);
                Assert.Contains("<", rawContent);
                Assert.Contains("https://download.example.com/release.zip", rawContent);
                Assert.Contains("cookie: session=xyz123", rawContent);
                Assert.Contains(">", rawContent);

                var imported = await service.ImportFromIdmEf2Async(tempEf2);
                Assert.Single(imported);
                Assert.Equal("https://download.example.com/release.zip", imported[0].Url);
                Assert.Equal("session=xyz123", imported[0].Cookies);
            }
            finally
            {
                if (File.Exists(tempEf2)) File.Delete(tempEf2);
            }
        }

        [Fact]
        public void LocalizationService_Supports12Languages_AndValidatesPacks()
        {
            var loc = LocalizationService.Instance;
            var languages = loc.GetAvailableLanguages();

            Assert.True(languages.Count >= 12, $"Expected at least 12 languages, found {languages.Count}");
            Assert.Contains("en-US", languages);
            Assert.Contains("bn-BD", languages);
            Assert.Contains("es-ES", languages);
            Assert.Contains("fr-FR", languages);
            Assert.Contains("de-DE", languages);
            Assert.Contains("it-IT", languages);
            Assert.Contains("pt-BR", languages);
            Assert.Contains("ru-RU", languages);
            Assert.Contains("ja-JP", languages);
            Assert.Contains("zh-CN", languages);
            Assert.Contains("ar-SA", languages);
            Assert.Contains("hi-IN", languages);

            // Arabic RTL verification
            loc.SetLanguage("ar-SA");
            Assert.True(loc.IsCurrentRtl);

            // Bengali string verification
            loc.SetLanguage("bn-BD");
            Assert.False(loc.IsCurrentRtl);
            Assert.Equal("ডাউনলোড", loc.GetString("Btn_Download"));

            // Validation test
            var bnPack = loc.GetLanguagePack("bn-BD");
            Assert.NotNull(bnPack);
            bool isValid = loc.ValidateLanguagePack(bnPack!, out var missing);
            Assert.True(isValid);
            Assert.Empty(missing);

            // Reset to default
            loc.SetLanguage("en-US");
        }

        [Fact]
        public void SmartDownloadAnalyzer_CalculatesHealthScore_AndDetectsDuplicates()
        {
            var analyzer = SmartDownloadAnalyzer.Instance;

            // Health Score
            int healthyScore = analyzer.CalculateHealthScore(supportsRange: true, rttMs: 30, contentLength: 50 * 1024 * 1024);
            Assert.Equal(100, healthyScore);

            int degradedScore = analyzer.CalculateHealthScore(supportsRange: false, rttMs: 350, contentLength: 0);
            Assert.True(degradedScore < 50);

            // Duplicate URL Detection
            string url1 = "https://cdn.example.com/file.zip?utm_source=edm&token=123";
            string url2 = "https://cdn.example.com/file.zip?token=456";
            Assert.True(analyzer.IsDuplicateUrl(url1, url2));

            string differentUrl = "https://cdn.example.com/another-file.zip";
            Assert.False(analyzer.IsDuplicateUrl(url1, differentUrl));

            // Dynamic Segment Sizing
            int segmentsSmall = analyzer.CalculateOptimalSegments(contentLength: 1024 * 1024, rttMs: 50, supportsRange: true);
            Assert.Equal(1, segmentsSmall);

            int segmentsLargeHighRtt = analyzer.CalculateOptimalSegments(contentLength: 500 * 1024 * 1024, rttMs: 250, supportsRange: true);
            Assert.Equal(24, segmentsLargeHighRtt);
        }

        [Fact]
        public void PowerActionScheduler_MaintainsCorrectStates()
        {
            var scheduler = PowerActionScheduler.Instance;
            Assert.False(scheduler.IsCountdownActive);

            scheduler.CancelCountdown();
            Assert.False(scheduler.IsCountdownActive);
        }
    }
}
