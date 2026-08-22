using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class HttpRequestPipelineTests : TestBase
    {
        [Fact]
        public void CreateFreshRequest_ReturnsNewRequestWithHeaders()
        {
            // Arrange
            var client = new HttpClient();
            var pipeline = new HttpRequestPipeline(client);
            var uri = new Uri("http://127.0.0.1/test.bin");

            // Act
            using var req1 = pipeline.CreateFreshRequest(HttpMethod.Get, uri, rangeStart: 0, rangeEnd: 1023);
            using var req2 = pipeline.CreateFreshRequest(HttpMethod.Get, uri, rangeStart: 1024, rangeEnd: 2047);

            // Assert
            req1.Should().NotBeSameAs(req2);
            req1.Headers.Range?.Ranges.First().From.Should().Be(0);
            req2.Headers.Range?.Ranges.First().From.Should().Be(1024);
        }

        [Fact]
        public void IsTransientException_ClassifiesCorrectly()
        {
            // Transient exceptions
            HttpRequestPipeline.IsTransientException(new HttpRequestException("Timeout", null, HttpStatusCode.ServiceUnavailable)).Should().BeTrue();
            HttpRequestPipeline.IsTransientException(new HttpRequestException("Gateway Timeout", null, HttpStatusCode.GatewayTimeout)).Should().BeTrue();

            // Permanent exceptions
            HttpRequestPipeline.IsTransientException(new HttpRequestException("Not Found", null, HttpStatusCode.NotFound)).Should().BeFalse();
            HttpRequestPipeline.IsTransientException(new HttpRequestException("Forbidden", null, HttpStatusCode.Forbidden)).Should().BeFalse();
            HttpRequestPipeline.IsTransientException(new OperationCanceledException()).Should().BeFalse();
        }
    }
}
