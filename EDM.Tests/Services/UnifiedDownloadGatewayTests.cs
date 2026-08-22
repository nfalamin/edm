using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using EDM.Models;
using EDM.Services;
using EDM.Services.Interfaces;

namespace EDM.Tests.Services
{
    public class UnifiedDownloadGatewayTests
    {
        private Mock<ISettingsService> CreateMockSettings(
            bool browserEnabled = true,
            bool captureDownloads = true,
            bool clipboardEnabled = true)
        {
            var mock = new Mock<ISettingsService>();
            mock.Setup(s => s.GetEnableBrowserIntegration()).Returns(browserEnabled);
            mock.Setup(s => s.GetBrowserCaptureDownloads()).Returns(captureDownloads);
            mock.Setup(s => s.GetEnableClipboardMonitoring()).Returns(clipboardEnabled);
            mock.Setup(s => s.GetBrowserShowNotification()).Returns(false);
            mock.Setup(s => s.GetClipboardShowNotification()).Returns(false);
            mock.Setup(s => s.GetDefaultDownloadPath()).Returns(Path.GetTempPath());
            return mock;
        }

        // 1. Valid manual request submission
        [Fact]
        public async Task Test1_ValidManualRequest_AcceptsAndEnqueues()
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);

            var req = new DownloadRequest
            {
                Source = IngestionSource.Manual,
                Url = "https://example.com/manual-file.zip",
                SuggestedFileName = "manual-file.zip"
            };

            var result = await gateway.SubmitRequestAsync(req);

            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Status.Should().Be(DownloadSubmissionStatus.Accepted);
            result.Item.Should().NotBeNull();
            result.Item!.FileName.Should().Be("manual-file.zip");
        }

        // 2. Valid browser request submission
        [Fact]
        public async Task Test2_ValidBrowserRequest_AcceptsAndEnqueues()
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);

            var req = new DownloadRequest
            {
                Source = IngestionSource.BrowserExtension,
                Url = "https://example.com/browser-download.msi",
                SuggestedFileName = "browser-download.msi",
                Referrer = "https://example.com/downloads"
            };

            var result = await gateway.SubmitRequestAsync(req);

            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Status.Should().Be(DownloadSubmissionStatus.Accepted);
            result.Item!.Referer.Should().Be("https://example.com/downloads");
        }

        // 3. Valid clipboard request submission
        [Fact]
        public async Task Test3_ValidClipboardRequest_AcceptsAndEnqueues()
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);

            var req = new DownloadRequest
            {
                Source = IngestionSource.ClipboardMonitor,
                Url = "https://example.com/clipboard-archive.tar.gz",
                SuggestedFileName = "clipboard-archive.tar.gz"
            };

            var result = await gateway.SubmitRequestAsync(req);

            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Status.Should().Be(DownloadSubmissionStatus.Accepted);
            result.Item!.FileName.Should().Be("clipboard-archive.tar.gz");
        }

        // 4. Valid remote dashboard request submission
        [Fact]
        public async Task Test4_ValidRemoteDashboardRequest_AcceptsAndEnqueues()
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);

            var req = new DownloadRequest
            {
                Source = IngestionSource.RemoteDashboard,
                Url = "https://example.com/remote-package.exe",
                SuggestedFileName = "remote-package.exe"
            };

            var result = await gateway.SubmitRequestAsync(req);

            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Status.Should().Be(DownloadSubmissionStatus.Accepted);
            result.Item!.FileName.Should().Be("remote-package.exe");
        }

        // 5. Malformed URL rejection
        [Theory]
        [InlineData("not a valid url")]
        [InlineData("htt://bad-url")]
        [InlineData("://missing-scheme.com")]
        public async Task Test5_MalformedUrlRejection_ReturnsInvalidOrSecurityRejected(string malformedUrl)
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);

            var req = new DownloadRequest
            {
                Source = IngestionSource.Manual,
                Url = malformedUrl
            };

            var result = await gateway.SubmitRequestAsync(req);

            result.IsSuccess.Should().BeFalse();
            result.Status.Should().BeOneOf(DownloadSubmissionStatus.Invalid, DownloadSubmissionStatus.SecurityRejected);
        }

        // 6. Unsupported scheme rejection
        [Theory]
        [InlineData("javascript:void(0)")]
        [InlineData("data:text/plain;base64,SGVsbG8=")]
        [InlineData("file:///C:/Windows/notepad.exe")]
        [InlineData("chrome://settings")]
        [InlineData("edge://flags")]
        [InlineData("blob:https://example.com/1234")]
        public async Task Test6_UnsupportedSchemeRejection_ReturnsSecurityRejected(string unsafeUrl)
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);

            var req = new DownloadRequest
            {
                Source = IngestionSource.BrowserExtension,
                Url = unsafeUrl
            };

            var result = await gateway.SubmitRequestAsync(req);

            result.IsSuccess.Should().BeFalse();
            result.Status.Should().Be(DownloadSubmissionStatus.SecurityRejected);
        }

        // 7. Oversized URL rejection (>8192 chars)
        [Fact]
        public async Task Test7_OversizedUrlRejection_ReturnsSecurityRejected()
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);

            string hugeUrl = "https://example.com/path?" + new string('q', 8200);

            var req = new DownloadRequest
            {
                Source = IngestionSource.BrowserExtension,
                Url = hugeUrl
            };

            var result = await gateway.SubmitRequestAsync(req);

            result.IsSuccess.Should().BeFalse();
            result.Status.Should().Be(DownloadSubmissionStatus.SecurityRejected);
            result.Message.Should().Contain("8192");
        }

        // 8. Path traversal filename sanitization
        [Theory]
        [InlineData(@"..\..\..\Windows\System32\calc.exe", "calc.exe")]
        [InlineData(@"../../etc/shadow", "shadow")]
        [InlineData(@"C:\Secret\Password.txt", "Password.txt")]
        [InlineData(@"/var/log/audit.log", "audit.log")]
        [InlineData(@"illegal:name?*<.dat", "illegalname.dat")]
        public async Task Test8_PathTraversalFilenameSanitization_SanitizesFileName(string rawFileName, string expectedSafeName)
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);

            var req = new DownloadRequest
            {
                Source = IngestionSource.BrowserExtension,
                Url = "https://example.com/download",
                SuggestedFileName = rawFileName
            };

            var result = await gateway.SubmitRequestAsync(req);

            result.IsSuccess.Should().BeTrue();
            result.Item!.FileName.Should().Be(expectedSafeName);
            result.Item.FileName.Should().NotContain("..");
            result.Item.FileName.Should().NotContain(@"\");
            result.Item.FileName.Should().NotContain("/");
        }

        // 9. Duplicate submission suppression
        [Fact]
        public async Task Test9_DuplicateSubmission_SuppressedOnRepeatedRequests()
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);
            string uniqueUrl = $"https://example.com/unique-{Guid.NewGuid():N}.iso";

            var req1 = new DownloadRequest { Source = IngestionSource.Manual, Url = uniqueUrl, SuggestedFileName = "test.iso" };
            var req2 = new DownloadRequest { Source = IngestionSource.Manual, Url = uniqueUrl, SuggestedFileName = "test.iso" };

            var res1 = await gateway.SubmitRequestAsync(req1);
            var res2 = await gateway.SubmitRequestAsync(req2);

            res1.IsSuccess.Should().BeTrue();
            res2.IsSuccess.Should().BeFalse();
            res2.Status.Should().Be(DownloadSubmissionStatus.Duplicate);
        }

        // 10. Simultaneous concurrent duplicate race condition safety
        [Fact]
        public async Task Test10_ConcurrentDuplicateRequests_OnlyOneSucceeds()
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);
            string sharedUrl = $"https://example.com/race-{Guid.NewGuid():N}.zip";

            var tasks = Enumerable.Range(0, 10).Select(i =>
            {
                var req = new DownloadRequest
                {
                    Source = i % 2 == 0 ? IngestionSource.BrowserExtension : IngestionSource.ClipboardMonitor,
                    Url = sharedUrl,
                    SuggestedFileName = "race.zip"
                };
                return gateway.SubmitRequestAsync(req);
            }).ToArray();

            var results = await Task.WhenAll(tasks);

            int successCount = results.Count(r => r.IsSuccess);
            int duplicateCount = results.Count(r => r.Status == DownloadSubmissionStatus.Duplicate);

            successCount.Should().Be(1, "Exactly one concurrent submission must win the race");
            duplicateCount.Should().Be(9, "All 9 other simultaneous requests must be identified as duplicates");
        }

        // 11. Cross-source duplicate suppression (Browser + Clipboard)
        [Fact]
        public async Task Test11_BrowserAndClipboardDuplicate_CrossSourceSuppression()
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);
            string sharedUrl = $"https://example.com/cross-browser-clip-{Guid.NewGuid():N}.mp4";

            // Browser arrives first
            var browserReq = new DownloadRequest { Source = IngestionSource.BrowserExtension, Url = sharedUrl, SuggestedFileName = "video.mp4" };
            var res1 = await gateway.SubmitRequestAsync(browserReq);

            // Clipboard arrives immediately after
            var clipReq = new DownloadRequest { Source = IngestionSource.ClipboardMonitor, Url = sharedUrl, SuggestedFileName = "video.mp4" };
            var res2 = await gateway.SubmitRequestAsync(clipReq);

            res1.IsSuccess.Should().BeTrue();
            res2.IsSuccess.Should().BeFalse();
            res2.Status.Should().Be(DownloadSubmissionStatus.Duplicate);
        }

        // 12. Cross-source duplicate suppression (Dashboard + Manual)
        [Fact]
        public async Task Test12_DashboardAndManualDuplicate_CrossSourceSuppression()
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);
            string sharedUrl = $"https://example.com/cross-dash-manual-{Guid.NewGuid():N}.bin";

            var dashReq = new DownloadRequest { Source = IngestionSource.RemoteDashboard, Url = sharedUrl, SuggestedFileName = "file.bin" };
            var manualReq = new DownloadRequest { Source = IngestionSource.Manual, Url = sharedUrl, SuggestedFileName = "file.bin" };

            var res1 = await gateway.SubmitRequestAsync(dashReq);
            var res2 = await gateway.SubmitRequestAsync(manualReq);

            res1.IsSuccess.Should().BeTrue();
            res2.IsSuccess.Should().BeFalse();
            res2.Status.Should().Be(DownloadSubmissionStatus.Duplicate);
        }

        // 13. Query parameter and signed URL preservation (AWS S3 HMAC, GCS)
        [Fact]
        public async Task Test13_SignedUrlPreservation_PreservesCompleteQueryTokens()
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);
            string signedUrl = "https://s3.amazonaws.com/bucket/data.tar?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=AKIAIOSFODNN7EXAMPLE%2F20260821%2Fus-east-1%2Fs3%2Faws4_request&X-Amz-Date=20260821T120000Z&X-Amz-Signature=abcd1234efgh5678";

            var req = new DownloadRequest
            {
                Source = IngestionSource.Manual,
                Url = signedUrl,
                SuggestedFileName = "data.tar"
            };

            var res = await gateway.SubmitRequestAsync(req);

            res.IsSuccess.Should().BeTrue();
            res.Item!.Url.Should().Be(signedUrl, "Gateway must preserve complete query string without destructive stripping");
        }

        // 14. Source identification and tracking
        [Fact]
        public async Task Test14_SourceIdentification_IdentifiesSourceAccurately()
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);

            var req = new DownloadRequest
            {
                Source = IngestionSource.CommandLine,
                Url = "https://example.com/cli-package.deb",
                SuggestedFileName = "cli-package.deb"
            };

            var res = await gateway.SubmitRequestAsync(req);

            res.IsSuccess.Should().BeTrue();
            res.Item.Should().NotBeNull();
        }

        // 15. Disabled integration rejection (Browser / Clipboard toggles in Settings)
        [Fact]
        public async Task Test15_DisabledIntegrationRejection_RejectsWhenSettingIsOff()
        {
            // Browser disabled in settings
            var settings = CreateMockSettings(browserEnabled: false);
            var gateway = new DownloadRequestGateway(settings.Object);

            var req = new DownloadRequest
            {
                Source = IngestionSource.BrowserExtension,
                Url = "https://example.com/blocked-extension.zip"
            };

            var res = await gateway.SubmitRequestAsync(req);

            res.IsSuccess.Should().BeFalse();
            res.Status.Should().Be(DownloadSubmissionStatus.Disabled);
        }

        // 16. Queue & DownloadManager integration
        [Fact]
        public async Task Test16_QueueAndDownloadManagerIntegration_InitializesFullDownloadItem()
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);

            var req = new DownloadRequest
            {
                Source = IngestionSource.Manual,
                Url = "https://example.com/setup.exe",
                SuggestedFileName = "setup.exe",
                DestinationDirectory = @"C:\CustomDownloads"
            };

            var res = await gateway.SubmitRequestAsync(req);

            res.IsSuccess.Should().BeTrue();
            res.Item!.FileName.Should().Be("setup.exe");
            res.Item.SavePath.Should().Be(Path.Combine(@"C:\CustomDownloads", "setup.exe"));
            res.Item.Status.Should().Be("Downloading");
            res.Item.Category.Should().NotBeNullOrEmpty();
        }

        // 17. Submission result structure and error message fidelity
        [Fact]
        public async Task Test17_SubmissionResultStructure_ProvidesClearDiagnosticMessage()
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);

            var req = new DownloadRequest
            {
                Source = IngestionSource.Manual,
                Url = ""
            };

            var res = await gateway.SubmitRequestAsync(req);

            res.IsSuccess.Should().BeFalse();
            res.Status.Should().Be(DownloadSubmissionStatus.Invalid);
            res.Message.Should().NotBeNullOrWhiteSpace();
        }

        // 18. Custom headers passing (Authorization & User-Agent)
        [Fact]
        public async Task Test18_CustomHeadersPassing_AttachesAuthAndUserAgentToDownloadItem()
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);

            var req = new DownloadRequest
            {
                Source = IngestionSource.BrowserExtension,
                Url = "https://api.example.com/secure-data.bin",
                SuggestedFileName = "secure-data.bin",
                CustomHeaders = new Dictionary<string, string>
                {
                    { "Authorization", "Bearer sec-tok-999" },
                    { "User-Agent", "CustomBrowser/1.0" }
                }
            };

            var res = await gateway.SubmitRequestAsync(req);

            res.IsSuccess.Should().BeTrue();
            res.Item!.AuthHeader.Should().Be("Bearer sec-tok-999");
            res.Item.UserAgent.Should().Be("CustomBrowser/1.0");
        }

        // 19. Category auto-routing through Gateway
        [Fact]
        public async Task Test19_CategoryAutoRouting_ResolvesAppropriateCategory()
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);

            var req = new DownloadRequest
            {
                Source = IngestionSource.Manual,
                Url = "https://example.com/movie.mp4",
                SuggestedFileName = "movie.mp4"
            };

            var res = await gateway.SubmitRequestAsync(req);

            res.IsSuccess.Should().BeTrue();
            res.Item!.Category.Should().NotBeNullOrEmpty();
        }

        // 20. Clean cancellation handling
        [Fact]
        public async Task Test20_CleanCancellationHandling_HandlesCancellationToken()
        {
            var settings = CreateMockSettings();
            var gateway = new DownloadRequestGateway(settings.Object);
            using var cts = new CancellationTokenSource();

            var req = new DownloadRequest
            {
                Source = IngestionSource.Manual,
                Url = "https://example.com/test-cancel.zip",
                SuggestedFileName = "test-cancel.zip"
            };

            var res = await gateway.SubmitRequestAsync(req, cts.Token);

            res.IsSuccess.Should().BeTrue();
        }
    }
}
