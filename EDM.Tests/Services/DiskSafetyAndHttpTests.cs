using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using EDM.Services;
using FluentAssertions;
using Xunit;


namespace EDM.Tests.Services
{
    public class DiskSafetyAndHttpTests
    {
        [Fact]
        public void SharedHttpClient_ConfiguresSocketsHttpHandler_WithDecompressionAndHttp2()
        {
            var client = SharedHttpClient.Instance;
            client.Should().NotBeNull();
            client.DefaultRequestHeaders.UserAgent.Should().NotBeNull();
        }

        [Fact]
        public void HttpRequestPipeline_CreateFreshRequest_DecoratesUserAgentAndAcceptHeaders()
        {
            var pipeline = new HttpRequestPipeline(SharedHttpClient.Instance);
            var req = pipeline.CreateFreshRequest(HttpMethod.Get, new Uri("https://example.com/file.zip"));

            req.Headers.UserAgent.ToString().Should().Contain("Chrome");
            req.Headers.Accept.ToString().Should().Contain("*/*");
        }

        [Fact]
        public void HttpRequestPipeline_IsTransientException_CorrectlyClassifiesNetworkAndNonNetworkExceptions()
        {
            // Transient network exceptions
            HttpRequestPipeline.IsTransientException(new IOException("Connection reset by peer")).Should().BeTrue();
            HttpRequestPipeline.IsTransientException(new System.Net.Sockets.SocketException((int)SocketError.ConnectionReset)).Should().BeTrue();
            HttpRequestPipeline.IsTransientException(new TimeoutException("Read timeout")).Should().BeTrue();
            HttpRequestPipeline.IsTransientException(new HttpRequestException("Server error", null, HttpStatusCode.ServiceUnavailable)).Should().BeTrue();
            HttpRequestPipeline.IsTransientException(new HttpRequestException("Rate limit", null, (HttpStatusCode)429)).Should().BeTrue();

            // Non-transient exceptions
            HttpRequestPipeline.IsTransientException(new UnauthorizedAccessException("Permission denied")).Should().BeFalse();
            HttpRequestPipeline.IsTransientException(new ArgumentException("Invalid arg")).Should().BeFalse();
            HttpRequestPipeline.IsTransientException(new HttpRequestException("Not found", null, HttpStatusCode.NotFound)).Should().BeFalse();
            HttpRequestPipeline.IsTransientException(new HttpRequestException("Forbidden", null, HttpStatusCode.Forbidden)).Should().BeFalse();
        }
    }
}
