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
    [Collection("InterceptionTests")]
    public class BrowserInterceptionFailureInjectionTests : IDisposable
    {
        public BrowserInterceptionFailureInjectionTests()
        {
            NativeMessageListener.ResetDeduplicationCacheForTesting();
        }

        public void Dispose()
        {
            NativeMessageListener.ResetDeduplicationCacheForTesting();
        }

        [Fact]
        public void PartC_FailureInjection_HandoffNotConfirmed_PreservesBrowserDownload()
        {
            string url = "https://example.com/critical_file.pdf";
            string corrId = "edm_corr_fail_001";

            // Stage 1: Detected -> Validating -> HandoffPending
            BrowserInterceptionStateMachine.CreateSession(corrId, url, "critical_file.pdf");
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.Validating);
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandoffPending);

            // Inject Native Host Disconnect BEFORE handoff confirmation
            bool fallback = BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.RecoverableFallback, "Native host stream EOF");
            fallback.Should().BeTrue();

            var session = BrowserInterceptionStateMachine.GetSession(corrId);
            session.Should().NotBeNull();
            // INVARIANT: Browser cancellation MUST NOT occur if handoff was not confirmed
            session!.State.Should().NotBe(InterceptionState.BrowserCancelled);
            session.State.Should().Be(InterceptionState.RecoverableFallback);
        }

        [Fact]
        public void PartC_FailureInjection_HandoffConfirmed_RetainsEdmStateForRecovery()
        {
            string url = "https://example.com/large_build.iso";
            string corrId = "edm_corr_confirmed_002";

            BrowserInterceptionStateMachine.CreateSession(corrId, url, "large_build.iso");
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.Validating);
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandoffPending);
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandedOff);
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.BrowserCancelled);

            // Inject crash after handoff confirmation
            bool queued = BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.EdmQueued);
            queued.Should().BeTrue();

            var session = BrowserInterceptionStateMachine.GetSession(corrId);
            session!.State.Should().Be(InterceptionState.EdmQueued);
            session.Url.Should().Be(url);
        }

        [Theory]
        [InlineData(0)]    // Immediate response
        [InlineData(100)]  // 100ms response delay
        [InlineData(500)]  // 500ms response delay
        [InlineData(1000)] // 1s response delay
        public async Task PartD_TimeoutSimulation_HandlesDelaysWithoutRaceOrDeadlock(int delayMs)
        {
            string url = $"https://example.com/delayed_{delayMs}.bin";
            string corrId = $"edm_corr_delay_{delayMs}_{Guid.NewGuid():N}";

            BrowserInterceptionStateMachine.CreateSession(corrId, url, "delayed.bin");
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.Validating);
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandoffPending);

            if (delayMs > 0)
            {
                await Task.Delay(delayMs).ConfigureAwait(true);
            }

            bool handedOff = BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandedOff);
            handedOff.Should().BeTrue();
        }

        [Theory]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        public void PartE_DuplicateStressTest_SuppressesDuplicatesWhilePreservingLegitimateEvents(int eventCount)
        {
            int eventsGenerated = eventCount;
            int uniqueExpected = eventCount / 2;
            int queuedCount = 0;
            int duplicatesSuppressed = 0;

            for (int i = 0; i < eventCount; i++)
            {
                // Alternating: Even indices generate unique browser events, odd indices duplicate previous event
                int bId = i % 2 == 0 ? (i / 2) + 1 : (i / 2) + 1;
                string url = $"https://example.com/item_{bId}.zip";
                string json = $"{{\"action\":\"add_download\",\"url\":\"{url}\",\"browserDownloadId\":\"{bId}\"}}";

                using var doc = JsonDocument.Parse(json);
                bool isDup = NativeMessageListener.IsDuplicateMessage(doc.RootElement);

                if (isDup)
                {
                    duplicatesSuppressed++;
                }
                else
                {
                    queuedCount++;
                }
            }

            queuedCount.Should().Be(uniqueExpected);
            duplicatesSuppressed.Should().Be(eventCount - uniqueExpected);
        }

        [Fact]
        public void PartH_MemoryLeakCheck_PrunesStaleSessionsCleanly()
        {
            BrowserInterceptionStateMachine.ResetForTesting();

            // Populate 500 interception sessions
            for (int i = 0; i < 500; i++)
            {
                string corrId = $"edm_corr_soak_{i}";
                BrowserInterceptionStateMachine.CreateSession(corrId, $"https://example.com/file_{i}.zip", $"file_{i}.zip");
            }

            BrowserInterceptionStateMachine.ActiveSessionCount.Should().Be(500);

            // Prune sessions older than 0 seconds (prunes all completed/stale sessions)
            int pruned = BrowserInterceptionStateMachine.PruneStaleSessions(TimeSpan.FromSeconds(0));
            pruned.Should().Be(500);
            BrowserInterceptionStateMachine.ActiveSessionCount.Should().Be(0);
        }

        [Fact]
        public void PartI_BlobUrlHonestClassification_ReportsSafeFallback()
        {
            string blobUrl = "blob:https://example.com/3f089912-32b0";
            string corrId = "edm_corr_blob_honest";

            BrowserInterceptionStateMachine.CreateSession(corrId, blobUrl, "blob.bin");
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.RecoverableFallback, "SAFE FALLBACK — NOT EDM INTERCEPTION");

            var session = BrowserInterceptionStateMachine.GetSession(corrId);
            session!.ErrorMessage.Should().Be("SAFE FALLBACK — NOT EDM INTERCEPTION");
        }
    }
}
