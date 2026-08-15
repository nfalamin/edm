using System;
using System.Text.Json;
using EDM.NativeMessaging;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class ExtensionNativeMessagingTests : TestBase
    {
        [Fact]
        public void ScrubPayloadForLogs_RedactsCookiesAndAuthorizationTokens()
        {
            // Arrange
            string jsonInput = @"{
                ""action"": ""DOWNLOAD_REQUEST"",
                ""url"": ""https://example.com/file.mp4"",
                ""cookies"": ""session_token=abc123secret; auth=xyz"",
                ""authorization"": ""Bearer my_secret_jwt_token"",
                ""filename"": ""file.mp4""
            }";

            // Act
            string scrubbed = NativeMessageListener.ScrubPayloadForLogs(jsonInput);

            // Assert
            scrubbed.Should().NotContain("abc123secret");
            scrubbed.Should().NotContain("my_secret_jwt_token");
            scrubbed.Should().Contain("\"cookies\": \"[REDACTED]\"");
            scrubbed.Should().Contain("\"authorization\": \"[REDACTED]\"");
        }

        [Fact]
        public void IsDuplicateMessage_SuppressesDuplicateEventsWithinWindow()
        {
            // Arrange
            string json = @"{ ""url"": ""https://example.com/duplicate_test.bin"", ""filename"": ""file.bin"" }";
            using var doc1 = JsonDocument.Parse(json);
            using var doc2 = JsonDocument.Parse(json);

            // Act
            bool firstCall = NativeMessageListener.IsDuplicateMessage(doc1.RootElement);
            bool secondCall = NativeMessageListener.IsDuplicateMessage(doc2.RootElement);

            // Assert
            firstCall.Should().BeFalse("First message invocation should be accepted");
            secondCall.Should().BeTrue("Immediate duplicate invocation within window must be suppressed");
        }
    }
}
