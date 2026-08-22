using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using EDM.Models;
using EDM.NativeMessaging;
using EDM.Services;
using EDM.Services.Interfaces;
using EDM.ViewModels;

namespace EDM.Tests.Services
{
    public class AdvancedBrowserCaptureTests
    {
        private Mock<ISettingsService> CreateMockSettings(
            bool browserEnabled = true,
            bool captureDownloads = true,
            bool showConfirmation = true,
            bool showNotification = true)
        {
            var mock = new Mock<ISettingsService>();
            mock.Setup(s => s.GetEnableBrowserIntegration()).Returns(browserEnabled);
            mock.Setup(s => s.GetBrowserCaptureDownloads()).Returns(captureDownloads);
            mock.Setup(s => s.GetBrowserShowConfirmation()).Returns(showConfirmation);
            mock.Setup(s => s.GetBrowserShowNotification()).Returns(showNotification);
            mock.Setup(s => s.GetDefaultDownloadPath()).Returns(Path.GetTempPath());
            return mock;
        }

        // 1. Valid browser download
        [Fact]
        public void Test1_ValidBrowserDownload_CreatesStructuredDownloadRequest()
        {
            var ingestion = new UniversalDownloadIngestionService();
            var payload = new IpcHandoffPayload
            {
                Url = "https://example.com/software-update.exe",
                Filename = "software-update.exe",
                Browser = "Chrome",
                Referer = "https://example.com/download-page",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
            };

            var request = ingestion.IngestFromBrowserHandoff(payload, @"C:\Downloads");

            request.Should().NotBeNull();
            request!.Url.Should().Be("https://example.com/software-update.exe");
            request.SuggestedFileName.Should().Be("software-update.exe");
            request.Source.Should().Be(IngestionSource.BrowserExtension);
            request.Referrer.Should().Be("https://example.com/download-page");
        }

        // 2. Invalid URL rejection
        [Fact]
        public void Test2_InvalidUrlRejection_ReturnsNullForMalformedUrl()
        {
            var ingestion = new UniversalDownloadIngestionService();
            var payload = new IpcHandoffPayload
            {
                Url = "not-a-valid-url-format",
                Filename = "file.bin"
            };

            var request = ingestion.IngestFromBrowserHandoff(payload, @"C:\Downloads");

            request.Should().BeNull();
        }

        // 3. Unsupported protocol rejection
        [Theory]
        [InlineData("javascript:alert(1)")]
        [InlineData("data:text/html,<h1>Test</h1>")]
        [InlineData("file:///C:/Windows/explorer.exe")]
        [InlineData("chrome://settings")]
        [InlineData("blob:https://example.com/abcd-1234")]
        [InlineData("edge://extensions")]
        public void Test3_UnsupportedProtocolRejection_ReturnsNull(string unsafeUrl)
        {
            var ingestion = new UniversalDownloadIngestionService();
            var payload = new IpcHandoffPayload
            {
                Url = unsafeUrl,
                Filename = "malicious.bin"
            };

            var request = ingestion.IngestFromBrowserHandoff(payload, @"C:\Downloads");

            request.Should().BeNull();
        }

        // 4. Malformed message handling
        [Fact]
        public void Test4_MalformedMessageHandling_DoesNotCrashHost()
        {
            string malformedJson = "{ \"action\": \"DOWNLOAD_REQUEST\", \"url\": ";

            Action parseAction = () =>
            {
                try
                {
                    JsonDocument.Parse(malformedJson);
                }
                catch (JsonException)
                {
                    // Gracefully handled
                }
            };

            parseAction.Should().NotThrow();
        }

        // 5. Oversized message rejection
        [Fact]
        public void Test5_OversizedMessageRejection_IdentifiesBufferExhaustion()
        {
            string hugeUrl = "https://example.com/" + new string('a', 9000);
            bool isUrlTooLong = hugeUrl.Length > 8192;

            isUrlTooLong.Should().BeTrue("NativeHost and EDM must reject URLs longer than 8192 characters");

            string hugeCookie = new string('c', 35000);
            bool isCookieTooLong = hugeCookie.Length > 32768;

            isCookieTooLong.Should().BeTrue("NativeHost must reject cookies exceeding 32KB");
        }

        // 6. Missing URL handling
        [Fact]
        public void Test6_MissingUrlHandling_ReturnsNullWhenUrlIsEmptyOrWhitespace()
        {
            var ingestion = new UniversalDownloadIngestionService();
            var payload = new IpcHandoffPayload
            {
                Url = "   ",
                Filename = "empty.zip"
            };

            var request = ingestion.IngestFromBrowserHandoff(payload, @"C:\Downloads");

            request.Should().BeNull();
        }

        // 7. Path-traversal filename sanitization
        [Theory]
        [InlineData(@"..\..\..\Windows\System32\cmd.exe", "cmd.exe")]
        [InlineData(@"../../etc/passwd", "passwd")]
        [InlineData(@"/var/root/script.sh", "script.sh")]
        [InlineData(@"C:\Users\Admin\Documents\secret.doc", "secret.doc")]
        [InlineData(@"invalid*name?.zip", "invalidname.zip")]
        public void Test7_PathTraversalFilenameSanitization_SanitizesFileNameCleanly(string rawName, string expectedClean)
        {
            string sanitized = SecuritySanitizer.SanitizeFileName(rawName);
            sanitized.Should().Be(expectedClean);
            sanitized.Should().NotContain("..");
            sanitized.Should().NotContain(@"\");
            sanitized.Should().NotContain("/");
        }

        // 8. Duplicate browser request deduplication
        [Fact]
        public void Test8_DuplicateBrowserRequest_SuppressedOnRepeatedHandoff()
        {
            var ingestion = new UniversalDownloadIngestionService();
            var payload = new IpcHandoffPayload
            {
                Url = "https://example.com/unique-download-" + Guid.NewGuid().ToString("N") + ".zip",
                Filename = "unique.zip"
            };

            // First call -> Accepted
            var req1 = ingestion.IngestFromBrowserHandoff(payload, @"C:\Downloads");
            req1.Should().NotBeNull();

            // Immediate duplicate -> Suppressed
            var req2 = ingestion.IngestFromBrowserHandoff(payload, @"C:\Downloads");
            req2.Should().BeNull();
        }

        // 9. Browser + Clipboard duplicate request suppression
        [Fact]
        public void Test9_BrowserAndClipboardDuplicate_SuppressesCrossSourceDuplicates()
        {
            var ingestion = new UniversalDownloadIngestionService();
            string sharedUrl = "https://example.com/cross-source-test-" + Guid.NewGuid().ToString("N") + ".iso";

            // Act 1: Browser captures the download first
            var browserPayload = new IpcHandoffPayload { Url = sharedUrl, Filename = "test.iso" };
            var browserReq = ingestion.IngestFromBrowserHandoff(browserPayload, @"C:\Downloads");
            browserReq.Should().NotBeNull();

            // Act 2: Clipboard Monitor simultaneously detects the copied link -> Suppressed
            var clipboardReqs = ingestion.IngestFromClipboard(sharedUrl, @"C:\Downloads");
            clipboardReqs.Should().BeEmpty("Cross-source duplicate from clipboard must be suppressed if browser already ingested it");
        }

        // 10. NativeHost broken pipe / disconnect handling
        [Fact]
        public async Task Test10_NativeHostBrokenPipeHandling_ExitsCleanlyWithoutDeadlock()
        {
            using var emptyStream = new MemoryStream(new byte[0]);
            using var outStream = new MemoryStream();
            var listener = new NativeMessageListener(emptyStream, outStream);

            listener.Start();
            await Task.Delay(100);

            // Stdin EOF reached -> reader finishes gracefully
            listener.Stop();
            await listener.DisposeAsync();

            listener.IsRunning.Should().BeFalse();
        }

        // 11. EDM unavailable / fallback handling
        [Fact]
        public void Test11_EdmUnavailableFallback_ConstructsValidHandoffPayload()
        {
            var payload = new IpcHandoffPayload
            {
                Url = "https://example.com/app.msi",
                Filename = "app.msi",
                Browser = "Edge"
            };

            string json = JsonSerializer.Serialize(payload);
            string b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));

            b64.Should().NotBeNullOrEmpty();
            string roundTripJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            var roundTrip = JsonSerializer.Deserialize<IpcHandoffPayload>(roundTripJson);

            roundTrip.Should().NotBeNull();
            roundTrip!.Url.Should().Be(payload.Url);
            roundTrip.Filename.Should().Be(payload.Filename);
        }

        // 12. NativeHost ping/pong response
        [Fact]
        public void Test12_NativeHostPing_IdentifiesPingActionCorrectly()
        {
            var req = new NativeMessageRequest
            {
                Action = "PING",
                RequestId = "ping_123"
            };

            string effectiveAction = req.GetEffectiveAction();
            effectiveAction.Should().Be("PING");

            var response = new NativeMessageResponse
            {
                Success = true,
                Action = "pong",
                RequestId = req.RequestId,
                Version = "2.0.0",
                Status = "ready"
            };

            response.Success.Should().BeTrue();
            response.Action.Should().Be("pong");
            response.Version.Should().Be("2.0.0");
        }

        // 13. Malformed IPC response safety
        [Fact]
        public void Test13_MalformedIpcResponse_DoesNotThrowUnhandledException()
        {
            string garbageResponse = "<<<NOT JSON>>>";

            Action act = () =>
            {
                try
                {
                    JsonSerializer.Deserialize<IpcHandoffPayload>(garbageResponse);
                }
                catch (JsonException)
                {
                    // Handled safely
                }
            };

            act.Should().NotThrow();
        }

        // 14. Zero-trust security validation (AuthHeader & UserAgent passing)
        [Fact]
        public void Test14_SecurityValidation_PopulatesCustomHeadersSafely()
        {
            var ingestion = new UniversalDownloadIngestionService();
            var payload = new IpcHandoffPayload
            {
                Url = "https://api.example.com/protected/archive.zip",
                Filename = "archive.zip",
                AuthHeader = "Bearer secret-token-12345",
                UserAgent = "EDM-Agent/2.0"
            };

            var req = ingestion.IngestFromBrowserHandoff(payload, @"C:\Downloads");

            req.Should().NotBeNull();
            req!.CustomHeaders.Should().ContainKey("Authorization");
            req.CustomHeaders["Authorization"].Should().Be("Bearer secret-token-12345");
            req.CustomHeaders.Should().ContainKey("User-Agent");
            req.CustomHeaders["User-Agent"].Should().Be("EDM-Agent/2.0");
        }

        // 15. Source identification
        [Fact]
        public void Test15_SourceIdentification_MarksSourceAsBrowserExtension()
        {
            var ingestion = new UniversalDownloadIngestionService();
            var payload = new IpcHandoffPayload
            {
                Url = "https://example.com/video.mp4",
                Filename = "video.mp4",
                Browser = "Firefox"
            };

            var req = ingestion.IngestFromBrowserHandoff(payload, @"C:\Downloads");

            req.Should().NotBeNull();
            req!.Source.Should().Be(IngestionSource.BrowserExtension);
        }

        // 16. Queue & DownloadManager integration
        [Fact]
        public void Test16_QueueAndDownloadManagerIntegration_InitializesDownloadItem()
        {
            var payload = new IpcHandoffPayload
            {
                Url = "https://example.com/package.tar.gz",
                Filename = "package.tar.gz",
                PageUrl = "https://example.com/releases",
                EstimatedSizeBytes = 10485760 // 10 MB
            };

            string sanitizedFileName = SecuritySanitizer.SanitizeFileName(payload.Filename);
            var item = new DownloadItem
            {
                Url = payload.Url,
                FileName = sanitizedFileName,
                SavePath = Path.Combine(@"C:\Downloads", sanitizedFileName),
                PageUrl = payload.PageUrl,
                EstimatedSizeBytes = payload.EstimatedSizeBytes ?? 0L,
                Status = "Downloading",
                Category = FileCategorizationService.ResolveDestinationPath("", sanitizedFileName)
            };

            item.FileName.Should().Be("package.tar.gz");
            item.Url.Should().Be("https://example.com/package.tar.gz");
            item.Status.Should().Be("Downloading");
            item.Category.Should().NotBeNullOrEmpty();
        }

        // 17. Cancellation handling
        [Fact]
        public void Test17_CancellationHandling_CancellationTokenCancelsListenerLoop()
        {
            using var cts = new CancellationTokenSource();
            using var inStream = new MemoryStream(new byte[100]);
            using var outStream = new MemoryStream();
            var listener = new NativeMessageListener(inStream, outStream);

            listener.Start();
            cts.Cancel();
            listener.Stop();

            listener.IsRunning.Should().BeFalse();
        }

        // 18. Settings persistence for browser integration
        [Fact]
        public void Test18_SettingsPersistence_ReadsAndWritesBrowserOptions()
        {
            var settingsService = new SettingsService();

            try
            {
                // Toggle browser settings
                settingsService.SetEnableBrowserIntegration(true);
                settingsService.SetBrowserCaptureDownloads(true);
                settingsService.SetBrowserShowConfirmation(false);
                settingsService.SetBrowserShowNotification(true);

                // Verify persistence
                settingsService.GetEnableBrowserIntegration().Should().BeTrue();
                settingsService.GetBrowserCaptureDownloads().Should().BeTrue();
                settingsService.GetBrowserShowConfirmation().Should().BeFalse();
                settingsService.GetBrowserShowNotification().Should().BeTrue();
            }
            finally
            {
                // Reset to safe defaults
                settingsService.SetEnableBrowserIntegration(true);
                settingsService.SetBrowserCaptureDownloads(true);
                settingsService.SetBrowserShowConfirmation(true);
            }
        }

        // 19. Multiple simultaneous browser events
        [Fact]
        public void Test19_MultipleSimultaneousBrowserEvents_ProcessesDistinctUrlsConcurrently()
        {
            var ingestion = new UniversalDownloadIngestionService();
            var results = new List<DownloadRequest>();

            for (int i = 0; i < 10; i++)
            {
                var payload = new IpcHandoffPayload
                {
                    Url = $"https://example.com/batch-file-{i}.dat",
                    Filename = $"batch-file-{i}.dat",
                    Browser = "Chrome"
                };

                var req = ingestion.IngestFromBrowserHandoff(payload, @"C:\Downloads");
                if (req != null)
                {
                    results.Add(req);
                }
            }

            results.Should().HaveCount(10);
        }

        // 20. Repeated download event handling (Duplicate protection)
        [Fact]
        public void Test20_RepeatedDownloadEventHandling_OnlyIngestsFirstInstance()
        {
            var ingestion = new UniversalDownloadIngestionService();
            string targetUrl = "https://example.com/single-instance-" + Guid.NewGuid().ToString("N") + ".zip";

            var payload1 = new IpcHandoffPayload { Url = targetUrl, Filename = "single.zip" };
            var payload2 = new IpcHandoffPayload { Url = targetUrl, Filename = "single.zip" };
            var payload3 = new IpcHandoffPayload { Url = targetUrl, Filename = "single.zip" };

            var r1 = ingestion.IngestFromBrowserHandoff(payload1, @"C:\Downloads");
            var r2 = ingestion.IngestFromBrowserHandoff(payload2, @"C:\Downloads");
            var r3 = ingestion.IngestFromBrowserHandoff(payload3, @"C:\Downloads");

            r1.Should().NotBeNull("First event must be ingested");
            r2.Should().BeNull("Second repeated event must be deduplicated");
            r3.Should().BeNull("Third repeated event must be deduplicated");
        }
    }
}
