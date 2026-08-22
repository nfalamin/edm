using System;
using System.Text.Json;
using System.Threading.Tasks;
using EDM.NativeMessaging;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    [CollectionDefinition("InterceptionTests", DisableParallelization = true)]
    public class InterceptionTestsCollection : ICollectionFixture<object> { }

    [Collection("InterceptionTests")]
    public class BrowserInterceptionHarnessTests
    {
        [Fact]
        public void StateMachine_ExecutesLegalTransitionsSuccessfully()
        {
            string corrId = "edm_corr_harness_001_" + Guid.NewGuid().ToString("N");
            var session = BrowserInterceptionStateMachine.CreateSession(corrId, "https://example.com/video.mp4", "video.mp4");

            session.Should().NotBeNull();
            session.State.Should().Be(InterceptionState.Detected);

            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.Validating).Should().BeTrue();
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandoffPending).Should().BeTrue();
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandedOff).Should().BeTrue();
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.BrowserCancelled).Should().BeTrue();
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.EdmQueued).Should().BeTrue();
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.EdmStarted).Should().BeTrue();

            var current = BrowserInterceptionStateMachine.GetSession(corrId);
            current.Should().NotBeNull();
            current!.State.Should().Be(InterceptionState.EdmStarted);
        }

        [Fact]
        public void StateMachine_RejectsIllegalStateTransitions()
        {
            string corrId = "edm_corr_harness_002_" + Guid.NewGuid().ToString("N");
            BrowserInterceptionStateMachine.CreateSession(corrId, "https://example.com/file.zip", "file.zip");

            // Jumping from Detected directly to EdmStarted without handoff is illegal
            bool success = BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.EdmStarted);
            success.Should().BeFalse();
        }

        [Fact]
        public void NativeMessageListener_ScrubsSensitiveCredentialsFromPayloadLogs()
        {
            string rawPayload = "{\"url\":\"https://example.com/file.zip\",\"cookies\":\"session=xyz123; auth=token_abc\",\"password\":\"SecretPass123\"}";

            string scrubbed = NativeMessageListener.ScrubPayloadForLogs(rawPayload);

            scrubbed.Should().NotContain("session=xyz123");
            scrubbed.Should().NotContain("SecretPass123");
            scrubbed.Should().Contain("[REDACTED]");
        }

        [Fact]
        public void NativeMessageListener_SuppressesDuplicateDownloadMessagesWithinWindow()
        {
            NativeMessageListener.ResetDeduplicationCacheForTesting();
            string json = $"{{\"action\":\"add_download\",\"url\":\"https://example.com/unique_stream_{Guid.NewGuid():N}.mp4\"}}";
            using var doc = JsonDocument.Parse(json);

            // First call registers message
            bool isDup1 = NativeMessageListener.IsDuplicateMessage(doc.RootElement);
            isDup1.Should().BeFalse();

            // Immediate second call detects duplicate
            bool isDup2 = NativeMessageListener.IsDuplicateMessage(doc.RootElement);
            isDup2.Should().BeTrue();
        }
    }
}
