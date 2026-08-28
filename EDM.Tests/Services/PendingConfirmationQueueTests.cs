using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using EDM.Models;
using EDM.NativeMessaging;
using EDM.Services;
using EDM.Services.Interfaces;

namespace EDM.Tests.Services
{
    public class PendingConfirmationQueueTests
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
            mock.Setup(s => s.GetDefaultDownloadPath()).Returns(System.IO.Path.GetTempPath());
            return mock;
        }

        // Test A: One browser request transitions: Pending -> User Approval -> Approved
        [Fact]
        public void TestA_SingleBrowserRequest_PendingToUserApproval()
        {
            using var queue = new PendingConfirmationQueueService();
            string url = "https://example.com/file1.zip";

            var request = queue.EnqueueRequest(url, IngestionSource.BrowserExtension, "file1.zip");

            request.Should().NotBeNull();
            request.Status.Should().Be(PendingConfirmationStatus.Pending);
            queue.PendingCount.Should().Be(1);

            // Mark as displayed
            bool displayed = queue.MarkAsDisplayed(request.PendingRequestId);
            displayed.Should().BeTrue();
            request.Status.Should().Be(PendingConfirmationStatus.Displayed);

            // User approves
            bool approved = queue.TryApprove(request.PendingRequestId, out var approvedReq);
            approved.Should().BeTrue();
            approvedReq.Should().NotBeNull();
            approvedReq!.Status.Should().Be(PendingConfirmationStatus.Approved);
            approvedReq.DecisionTimeUtc.Should().NotBeNull();
            queue.PendingCount.Should().Be(0);
        }

        // Test B: 5 simultaneous browser requests -> 5 independent pending requests, no overwrite
        [Fact]
        public async Task TestB_FiveSimultaneousBrowserRequests_AreIndependentWithoutOverwrite()
        {
            using var queue = new PendingConfirmationQueueService();
            var tasks = new List<Task<PendingDownloadRequest>>();

            for (int i = 0; i < 5; i++)
            {
                int itemIdx = i;
                tasks.Add(Task.Run(() =>
                    queue.EnqueueRequest(
                        $"https://example.com/asset_{itemIdx}.mp4",
                        IngestionSource.BrowserExtension,
                        $"asset_{itemIdx}.mp4")));
            }

            var results = await Task.WhenAll(tasks);

            results.Length.Should().Be(5);
            var distinctIds = results.Select(r => r.PendingRequestId).Distinct().ToList();
            distinctIds.Count.Should().Be(5, "Each pending request must have a unique GUID");

            queue.PendingCount.Should().Be(5);
            var allPending = queue.GetPendingRequests();
            allPending.Count.Should().Be(5);

            for (int i = 0; i < 5; i++)
            {
                string expectedName = $"asset_{i}.mp4";
                allPending.Should().Contain(r => r.SuggestedFileName == expectedName);
            }
        }

        // Test C: Browser + Clipboard + IPC simultaneously -> 3 independent pending requests
        [Fact]
        public async Task TestC_BrowserClipboardIpcSimultaneously_CreatesThreeUniqueRequests()
        {
            using var queue = new PendingConfirmationQueueService();

            var taskBrowser = Task.Run(() => queue.EnqueueRequest("https://example.com/browser.iso", IngestionSource.BrowserExtension, "browser.iso"));
            var taskClipboard = Task.Run(() => queue.EnqueueRequest("https://example.com/clipboard.pdf", IngestionSource.ClipboardMonitor, "clipboard.pdf"));
            var taskIpc = Task.Run(() => queue.EnqueueRequest("https://example.com/ipc.tar.gz", IngestionSource.NativeHost, "ipc.tar.gz"));

            var results = await Task.WhenAll(taskBrowser, taskClipboard, taskIpc);

            results.Length.Should().Be(3);
            queue.PendingCount.Should().Be(3);

            results[0].Source.Should().Be(IngestionSource.BrowserExtension);
            results[1].Source.Should().Be(IngestionSource.ClipboardMonitor);
            results[2].Source.Should().Be(IngestionSource.NativeHost);

            results.Select(r => r.PendingRequestId).Distinct().Count().Should().Be(3);
        }

        // Test D: Double-click / rapid concurrent Start Download -> Exactly one approval succeeds (atomic CAS)
        [Fact]
        public async Task TestD_DoubleApprovalAttempt_OnlyOneSucceedsAtomically()
        {
            using var queue = new PendingConfirmationQueueService();
            var req = queue.EnqueueRequest("https://example.com/software.exe", IngestionSource.BrowserExtension, "software.exe");

            int successCount = 0;
            int failureCount = 0;

            var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
            {
                if (queue.TryApprove(req.PendingRequestId, out var approved) && approved != null)
                {
                    Interlocked.Increment(ref successCount);
                }
                else
                {
                    Interlocked.Increment(ref failureCount);
                }
            })).ToArray();

            await Task.WhenAll(tasks);

            successCount.Should().Be(1, "Exactly one approval attempt must succeed");
            failureCount.Should().Be(9, "All subsequent or concurrent approval attempts must be safely rejected");
            req.Status.Should().Be(PendingConfirmationStatus.Approved);
        }

        // Test E: Approve after expiration -> Fails safely, request marked Expired
        [Fact]
        public void TestE_ApproveAfterExpiration_FailsSafely()
        {
            using var queue = new PendingConfirmationQueueService();
            // Set 1 millisecond expiration so it is immediately expired
            var req = queue.EnqueueRequest(
                "https://example.com/expired-item.zip",
                IngestionSource.BrowserExtension,
                "expired-item.zip",
                expiration: TimeSpan.FromMilliseconds(1));

            Thread.Sleep(20); // Wait for expiration

            bool approved = queue.TryApprove(req.PendingRequestId, out var approvedReq);
            approved.Should().BeFalse("Approval must be denied after expiration");
            approvedReq.Should().BeNull();

            req.Status.Should().Be(PendingConfirmationStatus.Expired);
        }

        // Test F: Reject then attempt approval -> Fails safely, remains Rejected
        [Fact]
        public void TestF_RejectThenAttemptApproval_FailsSafely()
        {
            using var queue = new PendingConfirmationQueueService();
            var req = queue.EnqueueRequest("https://example.com/doc.pdf", IngestionSource.BrowserExtension, "doc.pdf");

            bool rejected = queue.TryReject(req.PendingRequestId, "User clicked Cancel");
            rejected.Should().BeTrue();
            req.Status.Should().Be(PendingConfirmationStatus.Rejected);
            req.RejectionReason.Should().Be("User clicked Cancel");

            // Attempt approval on rejected request
            bool approved = queue.TryApprove(req.PendingRequestId, out var approvedReq);
            approved.Should().BeFalse("Cannot approve an already rejected request");
            approvedReq.Should().BeNull();
            req.Status.Should().Be(PendingConfirmationStatus.Rejected);
        }

        // Test G: Close confirmation UI / Cancel -> Request cancelled safely, no execution
        [Fact]
        public void TestG_CancelRequest_TransitionsToCancelled()
        {
            using var queue = new PendingConfirmationQueueService();
            var req = queue.EnqueueRequest("https://example.com/archive.7z", IngestionSource.BrowserExtension, "archive.7z");

            bool cancelled = queue.TryCancel(req.PendingRequestId);
            cancelled.Should().BeTrue();
            req.Status.Should().Be(PendingConfirmationStatus.Cancelled);

            bool approved = queue.TryApprove(req.PendingRequestId, out var approvedReq);
            approved.Should().BeFalse();
            approvedReq.Should().BeNull();
        }

        // Test H: Invalid URL scheme -> Security rejected before queue
        [Theory]
        [InlineData("javascript:alert('XSS')")]
        [InlineData("data:text/html,<h1>PWNED</h1>")]
        [InlineData("file:///C:/Windows/System32/calc.exe")]
        [InlineData("blob:https://example.com/1234")]
        public void TestH_InvalidUrlScheme_RejectedBySecuritySanitizer(string unsafeUrl)
        {
            bool isAllowed = SecuritySanitizer.IsAllowedUrlScheme(unsafeUrl);
            isAllowed.Should().BeFalse("Unsafe URL schemes must be rejected before queuing");
        }

        // Test I: External request while browser integration is disabled -> Gateway & Handshake reject
        [Fact]
        public async Task TestI_ExternalRequestWhenBrowserIntegrationDisabled_IsRejectedByGateway()
        {
            var mockSettings = CreateMockSettings(browserEnabled: false, captureDownloads: false);
            var gateway = new DownloadRequestGateway(mockSettings.Object);

            var req = new DownloadRequest
            {
                Source = IngestionSource.BrowserExtension,
                Url = "https://example.com/test.zip",
                SuggestedFileName = "test.zip"
            };

            var result = await gateway.SubmitRequestAsync(req);

            result.IsSuccess.Should().BeFalse();
            result.Status.Should().Be(DownloadSubmissionStatus.Disabled);
        }

        // Test J: Manual Add URL download -> Normal download behavior preserved
        [Fact]
        public async Task TestJ_ManualDownload_PreservesNormalExecution()
        {
            var mockSettings = CreateMockSettings(showConfirmation: true);
            var gateway = new DownloadRequestGateway(mockSettings.Object);

            var req = new DownloadRequest
            {
                Source = IngestionSource.Manual,
                Url = "https://example.com/manual-download.mp4",
                SuggestedFileName = "manual-download.mp4",
                SilentMode = false
            };

            var result = await gateway.SubmitRequestAsync(req);

            result.IsSuccess.Should().BeTrue();
            result.Status.Should().Be(DownloadSubmissionStatus.Accepted);
            result.Item.Should().NotBeNull();
            result.Item!.Status.Should().Be("Downloading", "Manual downloads should start downloading directly");
        }
    }
}
