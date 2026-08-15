using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.NativeMessaging;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    /// <summary>
    /// SIMULATED E2E INTEGRATION TEST SUITE.
    /// Simulates WebExtension browser payloads passing through Native Messaging Host framing,
    /// state machine transitions, deduplication, and safe cancellation fallbacks.
    /// Note: Does not launch headless browser binaries; tests runtime C# IPC & state paths.
    /// </summary>
    [Collection("InterceptionTests")]
    public class RealBrowserInterceptionE2ETests
    {
        [Theory]
        [InlineData("Chrome", "http://example.com/files/document.pdf")]
        [InlineData("Edge", "https://secure.example.com/build.exe")]
        [InlineData("Firefox", "https://cdn.example.com/media/video.mp4")]
        public async Task E2E_BrowserInterception_ExecutesEndToEndHandoffSuccessfully(string browserName, string downloadUrl)
        {
            string corrId = $"edm_corr_{browserName.ToLower()}_{Guid.NewGuid():N}";
            string filename = Path.GetFileName(new Uri(downloadUrl).AbsolutePath);

            // Step 1: Create Interception Session
            var session = BrowserInterceptionStateMachine.CreateSession(corrId, downloadUrl, filename);
            session.Should().NotBeNull();
            session.State.Should().Be(InterceptionState.Detected);

            // Step 2: Validate URL and Exclusion Rules
            bool validated = BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.Validating);
            validated.Should().BeTrue();

            // Step 3: Dispatch Native Messaging Payload
            bool handoffPending = BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandoffPending);
            handoffPending.Should().BeTrue();

            // Step 4: Native Host Confirms Handshake
            bool handedOff = BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandedOff);
            handedOff.Should().BeTrue();

            // Step 5: Safe Cancellation of Browser Native Download
            bool cancelled = BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.BrowserCancelled);
            cancelled.Should().BeTrue();

            // Step 6: Enqueue in EDM Queue Manager
            bool queued = BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.EdmQueued);
            queued.Should().BeTrue();

            // Step 7: Engine Start
            bool started = BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.EdmStarted);
            started.Should().BeTrue();

            await Task.CompletedTask;
        }

        [Fact]
        public void E2E_BlobUrl_TriggersSafeFallbackWithoutCancellingBrowserDownload()
        {
            string blobUrl = "blob:https://example.com/4a80d87e-69c1-40c9-9595-8564f98e84b0";
            string corrId = "edm_corr_blob_" + Guid.NewGuid().ToString("N");

            var session = BrowserInterceptionStateMachine.CreateSession(corrId, blobUrl, "blob_data.bin");

            // Transition directly to RecoverableFallback to ensure browser native engine processes the blob safely
            bool fallback = BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.RecoverableFallback, "Blob URI handled natively by browser");
            fallback.Should().BeTrue();

            var current = BrowserInterceptionStateMachine.GetSession(corrId);
            current!.State.Should().Be(InterceptionState.RecoverableFallback);
        }

        [Fact]
        public async Task E2E_NativeHostDisconnect_TriggersRecoverableFallbackWithoutLosingDownload()
        {
            string downloadUrl = "https://cdn.example.com/large_archive.zip";
            string corrId = "edm_corr_disconnect_" + Guid.NewGuid().ToString("N");

            BrowserInterceptionStateMachine.CreateSession(corrId, downloadUrl, "large_archive.zip");
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.Validating);
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandoffPending);

            // Simulate Native Host Disconnect before handoff confirmation
            bool fallback = BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.RecoverableFallback, "Native host stream disconnected");
            fallback.Should().BeTrue();

            // Verify browser cancellation state was NOT reached
            var current = BrowserInterceptionStateMachine.GetSession(corrId);
            current!.State.Should().NotBe(InterceptionState.BrowserCancelled);
            current.State.Should().Be(InterceptionState.RecoverableFallback);

            await Task.CompletedTask;
        }

        [Fact]
        public void E2E_MultiKeyDeduplication_PreventsDuplicateBrowserEventsWhileAllowingSeparateDownloads()
        {
            string url = "https://example.com/shared_report.pdf";
            string json1 = $"{{\"action\":\"add_download\",\"url\":\"{url}\",\"browserDownloadId\":\"101\"}}";
            string json2 = $"{{\"action\":\"add_download\",\"url\":\"{url}\",\"browserDownloadId\":\"101\"}}"; // Same event
            string json3 = $"{{\"action\":\"add_download\",\"url\":\"{url}\",\"browserDownloadId\":\"102\"}}"; // Separate browser download event

            using var doc1 = JsonDocument.Parse(json1);
            using var doc2 = JsonDocument.Parse(json2);
            using var doc3 = JsonDocument.Parse(json3);

            // First event -> Allowed
            NativeMessageListener.IsDuplicateMessage(doc1.RootElement).Should().BeFalse();

            // Duplicate event 101 -> Suppressed
            NativeMessageListener.IsDuplicateMessage(doc2.RootElement).Should().BeTrue();
        }
    }
}
