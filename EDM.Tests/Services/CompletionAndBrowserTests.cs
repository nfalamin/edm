using System;
using System.IO;
using EDM.Models;
using EDM.NativeMessaging;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class CompletionAndBrowserTests
    {
        [Theory]
        [InlineData(PostQueueAction.None)]
        [InlineData(PostQueueAction.PlaySound)]
        [InlineData(PostQueueAction.ShowNotification)]
        [InlineData(PostQueueAction.OpenFile)]
        [InlineData(PostQueueAction.OpenFolder)]
        [InlineData(PostQueueAction.ExecuteApp)]
        [InlineData(PostQueueAction.Shutdown)]
        [InlineData(PostQueueAction.Sleep)]
        [InlineData(PostQueueAction.Hibernate)]
        [InlineData(PostQueueAction.Restart)]
        public void PostQueueActionEnum_ContainsAllConfigurableActions(PostQueueAction action)
        {
            Enum.IsDefined(typeof(PostQueueAction), action).Should().BeTrue();
        }

        [Fact]
        public void NativeMessageListener_DiagnosticMode_CanBeEnabledAndDisabled()
        {
            NativeMessageListener.DiagnosticModeEnabled = true;
            NativeMessageListener.DiagnosticModeEnabled.Should().BeTrue();

            NativeMessageListener.DiagnosticModeEnabled = false;
            NativeMessageListener.DiagnosticModeEnabled.Should().BeFalse();
        }

        [Fact]
        public void NativeMessageListener_ScrubPayloadForLogs_RedactsSensitiveData()
        {
            string json = @"{ ""url"": ""https://example.com/file.zip"", ""cookies"": ""session_token=secret123"", ""authorization"": ""Bearer token_456"" }";
            string scrubbed = NativeMessageListener.ScrubPayloadForLogs(json);

            scrubbed.Should().NotContain("secret123");
            scrubbed.Should().NotContain("token_456");
            scrubbed.Should().Contain("[REDACTED]");
        }
    }
}
