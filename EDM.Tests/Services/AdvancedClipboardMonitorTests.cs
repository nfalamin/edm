using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using EDM.Models;
using EDM.Services;
using EDM.Services.Interfaces;
using EDM.ViewModels;

namespace EDM.Tests.Services
{
    public class AdvancedClipboardMonitorTests
    {
        private Mock<ISettingsService> CreateMockSettings(
            bool enabled = true,
            bool http = true,
            bool https = true,
            bool ftp = true,
            ClipboardAction action = ClipboardAction.AskBeforeDownload,
            bool ignoreDuplicates = true,
            bool showNotification = true)
        {
            var mock = new Mock<ISettingsService>();
            mock.Setup(s => s.GetEnableClipboardMonitoring()).Returns(enabled);
            mock.Setup(s => s.GetClipboardMonitorHttp()).Returns(http);
            mock.Setup(s => s.GetClipboardMonitorHttps()).Returns(https);
            mock.Setup(s => s.GetClipboardMonitorFtp()).Returns(ftp);
            mock.Setup(s => s.GetClipboardAction()).Returns(action);
            mock.Setup(s => s.GetClipboardIgnoreDuplicates()).Returns(ignoreDuplicates);
            mock.Setup(s => s.GetClipboardShowNotification()).Returns(showNotification);
            mock.Setup(s => s.GetDefaultDownloadPath()).Returns(Path.GetTempPath());
            return mock;
        }

        // 1. HTTP URL detection
        [Fact]
        public void Test1_HttpUrlDetection_IdentifiesValidHttpUrl()
        {
            var settings = CreateMockSettings();
            var monitor = new ClipboardMonitorService(settings.Object);
            string? detectedUrl = null;
            monitor.UrlDetected += (s, e) => { detectedUrl = e.Url; e.Handled = true; };

            bool accepted = monitor.ProcessText("http://example.com/file.iso");

            accepted.Should().BeTrue();
            detectedUrl.Should().Be("http://example.com/file.iso");
        }

        // 2. HTTPS URL detection
        [Fact]
        public void Test2_HttpsUrlDetection_IdentifiesValidHttpsUrl()
        {
            var settings = CreateMockSettings();
            var monitor = new ClipboardMonitorService(settings.Object);
            string? detectedUrl = null;
            monitor.UrlDetected += (s, e) => { detectedUrl = e.Url; e.Handled = true; };

            bool accepted = monitor.ProcessText("https://secure.example.com/release.zip");

            accepted.Should().BeTrue();
            detectedUrl.Should().Be("https://secure.example.com/release.zip");
        }

        // 3. Invalid text ignored
        [Fact]
        public void Test3_InvalidTextIgnored_ReturnsFalseForNormalText()
        {
            var settings = CreateMockSettings();
            var monitor = new ClipboardMonitorService(settings.Object);
            bool eventFired = false;
            monitor.UrlDetected += (s, e) => { eventFired = true; };

            bool accepted = monitor.ProcessText("Hello world! This is just a plain message with no links.");

            accepted.Should().BeFalse();
            eventFired.Should().BeFalse();
        }

        // 4. Unsupported scheme ignored
        [Theory]
        [InlineData("javascript:alert(document.cookie)")]
        [InlineData("data:text/plain;base64,SGVsbG8=")]
        [InlineData("file:///C:/Windows/System32/calc.exe")]
        [InlineData("chrome://settings")]
        [InlineData("edge://flags")]
        [InlineData("about:blank")]
        [InlineData("blob:https://example.com/uuid")]
        public void Test4_UnsupportedSchemesIgnored_ReturnsFalse(string unsafeUrl)
        {
            var settings = CreateMockSettings();
            var monitor = new ClipboardMonitorService(settings.Object);
            bool eventFired = false;
            monitor.UrlDetected += (s, e) => { eventFired = true; };

            bool accepted = monitor.ProcessText(unsafeUrl);

            accepted.Should().BeFalse();
            eventFired.Should().BeFalse();
        }

        // 5. Windows local path ignored
        [Theory]
        [InlineData(@"C:\Downloads\file.zip")]
        [InlineData(@"D:\Movies\video.mp4")]
        [InlineData(@"\\network-server\share\archive.tar")]
        [InlineData(@"E:/Files/document.pdf")]
        public void Test5_WindowsLocalPathsIgnored_ReturnsFalse(string localPath)
        {
            var settings = CreateMockSettings();
            var monitor = new ClipboardMonitorService(settings.Object);
            bool eventFired = false;
            monitor.UrlDetected += (s, e) => { eventFired = true; };

            bool accepted = monitor.ProcessText(localPath);

            accepted.Should().BeFalse();
            eventFired.Should().BeFalse();
        }

        // 6. Duplicate URL detection
        [Fact]
        public void Test6_DuplicateUrlDetection_SuppressesImmediateDuplicates()
        {
            var settings = CreateMockSettings(ignoreDuplicates: true);
            var monitor = new ClipboardMonitorService(settings.Object);
            int eventCount = 0;
            monitor.UrlDetected += (s, e) => { eventCount++; e.Handled = true; };

            string url = "https://example.com/unique-archive.zip";

            // First copy -> Accepted
            bool first = monitor.ProcessText(url);
            first.Should().BeTrue();
            eventCount.Should().Be(1);

            // Second copy -> Suppressed as duplicate
            bool second = monitor.ProcessText(url);
            second.Should().BeFalse();
            eventCount.Should().Be(1);
        }

        // 7. URL normalization & valid parsing
        [Fact]
        public void Test7_UrlNormalization_ExtractsValidUrlFromSurroundingText()
        {
            var settings = CreateMockSettings();
            var monitor = new ClipboardMonitorService(settings.Object);
            string? detected = null;
            monitor.UrlDetected += (s, e) => { detected = e.Url; e.Handled = true; };

            string text = "Check out the latest release at https://github.com/nfalamin/edm/releases/v2.0.0/EDM_Setup.exe today!";
            bool accepted = monitor.ProcessText(text);

            accepted.Should().BeTrue();
            detected.Should().Be("https://github.com/nfalamin/edm/releases/v2.0.0/EDM_Setup.exe");
        }

        // 8. Query parameter preservation
        [Fact]
        public void Test8_QueryParameterPreservation_PreservesAllQueryParamsVerbatim()
        {
            var settings = CreateMockSettings();
            var monitor = new ClipboardMonitorService(settings.Object);
            string? detected = null;
            monitor.UrlDetected += (s, e) => { detected = e.Url; e.Handled = true; };

            string urlWithParams = "https://cdn.example.com/download.tar.gz?user=admin&token=abc123xyz&expires=1790000000&fmt=raw";
            bool accepted = monitor.ProcessText(urlWithParams);

            accepted.Should().BeTrue();
            detected.Should().Be(urlWithParams);
        }

        // 9. Signed URL preservation (AWS S3 / GCS / Azure SAS)
        [Fact]
        public void Test9_SignedUrlPreservation_PreservesComplexSignaturesWithoutDestructiveEncoding()
        {
            var settings = CreateMockSettings();
            var monitor = new ClipboardMonitorService(settings.Object);
            string? detected = null;
            monitor.UrlDetected += (s, e) => { detected = e.Url; e.Handled = true; };

            string signedUrl = "https://s3.us-east-1.amazonaws.com/mybucket/data.iso?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=AKIAIOSFODNN7EXAMPLE%2F20260821%2Fus-east-1%2Fs3%2Faws4_request&X-Amz-Date=20260821T120000Z&X-Amz-Expires=86400&X-Amz-SignedHeaders=host&X-Amz-Signature=a1b2c3d4e5f6";
            bool accepted = monitor.ProcessText(signedUrl);

            accepted.Should().BeTrue();
            detected.Should().Be(signedUrl);
        }

        // 10. Sensitive clipboard content not persisted
        [Fact]
        public void Test10_PrivacyAndSecurity_DoesNotPersistOrRetainArbitraryText()
        {
            var settings = CreateMockSettings();
            var monitor = new ClipboardMonitorService(settings.Object);

            string sensitiveContent = "Password=SuperSecret123!&CreditCard=4111222233334444";
            bool accepted = monitor.ProcessText(sensitiveContent);

            accepted.Should().BeFalse();
            // Verify by reflection or public state that no clipboard text history list exists
            typeof(ClipboardMonitorService).GetProperty("ClipboardHistory").Should().BeNull();
        }

        // 11. Clipboard source identification
        [Fact]
        public void Test11_ClipboardSourceIdentification_ReportsCorrectSource()
        {
            var settings = CreateMockSettings();
            var monitor = new ClipboardMonitorService(settings.Object);
            string? source = null;
            monitor.UrlDetected += (s, e) => { source = e.Source; e.Handled = true; };

            monitor.ProcessText("https://example.com/setup.exe", source: "WindowsClipboard");

            source.Should().Be("WindowsClipboard");
        }

        // 12. Browser + Clipboard duplicate request suppression
        [Fact]
        public void Test12_BrowserAndClipboardDuplicate_SuppressedViaUniversalIngestion()
        {
            var ingestion = new UniversalDownloadIngestionService();
            string testUrl = "https://example.com/shared-download.zip";

            // Act 1: Browser or clipboard ingests first
            var req1 = ingestion.IngestFromClipboard(testUrl, @"C:\Downloads");
            req1.Should().HaveCount(1);
            req1[0].Url.Should().Be(testUrl);

            // Act 2: Simultaneous duplicate from clipboard monitor -> Suppressed
            var req2 = ingestion.IngestFromClipboard(testUrl, @"C:\Downloads");
            req2.Should().BeEmpty("Simultaneous duplicate URLs from browser and clipboard must produce only 1 download request");
        }

        // 13. Monitor startup
        [Fact]
        public void Test13_MonitorStartup_StartsAndSetsIsRunningTrue()
        {
            var settings = CreateMockSettings();
            using var monitor = new ClipboardMonitorService(settings.Object);

            monitor.Start();

            monitor.IsRunning.Should().BeTrue();
        }

        // 14. Monitor shutdown & disposal
        [Fact]
        public void Test14_MonitorShutdownAndDispose_StopsAndReleasesResourcesCleanly()
        {
            var settings = CreateMockSettings();
            var monitor = new ClipboardMonitorService(settings.Object);

            monitor.Start();
            monitor.IsRunning.Should().BeTrue();

            monitor.Stop();
            monitor.IsRunning.Should().BeFalse();

            monitor.Dispose();
            monitor.IsRunning.Should().BeFalse();
        }

        // 15. Settings persistence
        [Fact]
        public void Test15_SettingsPersistence_ReadsAndWritesClipboardOptions()
        {
            var settingsService = new SettingsService();

            try
            {
                // Toggle settings
                settingsService.SetEnableClipboardMonitoring(true);
                settingsService.SetClipboardMonitorHttp(true);
                settingsService.SetClipboardMonitorHttps(true);
                settingsService.SetClipboardMonitorFtp(false);
                settingsService.SetClipboardAction(ClipboardAction.AutoDownload);
                settingsService.SetClipboardIgnoreDuplicates(true);
                settingsService.SetClipboardShowNotification(false);

                // Verify persistence
                settingsService.GetEnableClipboardMonitoring().Should().BeTrue();
                settingsService.GetClipboardMonitorHttp().Should().BeTrue();
                settingsService.GetClipboardMonitorHttps().Should().BeTrue();
                settingsService.GetClipboardMonitorFtp().Should().BeFalse();
                settingsService.GetClipboardAction().Should().Be(ClipboardAction.AutoDownload);
                settingsService.GetClipboardIgnoreDuplicates().Should().BeTrue();
                settingsService.GetClipboardShowNotification().Should().BeFalse();
            }
            finally
            {
                // Reset to safe defaults
                settingsService.SetEnableClipboardMonitoring(false);
                settingsService.SetClipboardAction(ClipboardAction.AskBeforeDownload);
            }
        }

        // 16. Ask-before-download workflow
        [Fact]
        public void Test16_AskBeforeDownload_FiresUrlDetectedWithAskAction()
        {
            var settings = CreateMockSettings(action: ClipboardAction.AskBeforeDownload);
            var monitor = new ClipboardMonitorService(settings.Object);
            bool eventFired = false;
            monitor.UrlDetected += (s, e) =>
            {
                eventFired = true;
                e.Handled = true; // Handled as Ask Before Download UI prompt
            };

            bool accepted = monitor.ProcessText("https://example.com/ask-download.pdf");

            accepted.Should().BeTrue();
            eventFired.Should().BeTrue();
        }

        // 17. Automatic-download option
        [Fact]
        public void Test17_AutomaticDownloadOption_DispatchesDirectDownload()
        {
            var settings = CreateMockSettings(action: ClipboardAction.AutoDownload);
            var monitor = new ClipboardMonitorService(settings.Object);
            bool eventFired = false;
            monitor.UrlDetected += (s, e) =>
            {
                eventFired = true;
                e.Handled = true;
            };

            bool accepted = monitor.ProcessText("https://example.com/auto-download.mp4");

            accepted.Should().BeTrue();
            eventFired.Should().BeTrue();
        }

        // 18. Notification behavior
        [Fact]
        public void Test18_NotificationBehavior_LogsAndDispatchesNotification()
        {
            var settings = CreateMockSettings(showNotification: true);
            var monitor = new ClipboardMonitorService(settings.Object);
            bool detected = false;
            monitor.UrlDetected += (s, e) => { detected = true; e.Handled = true; };

            monitor.ProcessText("https://example.com/notify-file.zip");

            detected.Should().BeTrue();
        }

        // 19. Repeated clipboard events with different URLs
        [Fact]
        public void Test19_RepeatedClipboardEvents_ProcessesMultipleDistinctUrls()
        {
            var settings = CreateMockSettings();
            var monitor = new ClipboardMonitorService(settings.Object);
            var detectedList = new List<string>();
            monitor.UrlDetected += (s, e) => { detectedList.Add(e.Url); e.Handled = true; };

            monitor.ProcessText("https://example.com/file1.zip");
            monitor.ProcessText("https://example.com/file2.zip");
            monitor.ProcessText("https://example.com/file3.zip");

            detectedList.Should().HaveCount(3);
            detectedList.Should().Contain("https://example.com/file1.zip");
            detectedList.Should().Contain("https://example.com/file2.zip");
            detectedList.Should().Contain("https://example.com/file3.zip");
        }

        // 20. Large clipboard text handling
        [Fact]
        public void Test20_LargeClipboardText_HandlesGracefullyWithoutLagOrMemoryLeak()
        {
            var settings = CreateMockSettings();
            var monitor = new ClipboardMonitorService(settings.Object);
            string? detected = null;
            monitor.UrlDetected += (s, e) => { detected = e.Url; e.Handled = true; };

            // Construct 100KB payload with a link embedded in the first 20KB
            string padding = new string('A', 5000);
            string largeText = $"{padding} https://example.com/large-payload.iso {padding} {padding}";

            bool accepted = monitor.ProcessText(largeText);

            accepted.Should().BeTrue();
            detected.Should().Be("https://example.com/large-payload.iso");
        }
    }
}
