using System;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using EDM.Models;
using EDM.Services;
using EDM.Services.Helpers;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class HttpStatusAndFilenameTests
    {
        [Theory]
        [InlineData(HttpStatusCode.OK, HttpStatusCategory.Success)]
        [InlineData(HttpStatusCode.PartialContent, HttpStatusCategory.Success)]
        [InlineData(HttpStatusCode.Unauthorized, HttpStatusCategory.AuthRequired)]
        [InlineData(HttpStatusCode.ProxyAuthenticationRequired, HttpStatusCategory.AuthRequired)]
        [InlineData((HttpStatusCode)429, HttpStatusCategory.RateLimited)]
        [InlineData(HttpStatusCode.NotFound, HttpStatusCategory.PermanentFailure)]
        [InlineData(HttpStatusCode.Forbidden, HttpStatusCategory.AuthRequired)]

        [InlineData(HttpStatusCode.InternalServerError, HttpStatusCategory.ServerFailure)]
        [InlineData(HttpStatusCode.ServiceUnavailable, HttpStatusCategory.ServerFailure)]
        [InlineData(HttpStatusCode.RequestedRangeNotSatisfiable, HttpStatusCategory.RangeInvalid)]

        public void HttpStatusClassifier_ClassifiesStatusCodeCorrectly(HttpStatusCode input, HttpStatusCategory expected)
        {
            HttpStatusClassifier.Classify(input).Should().Be(expected);
        }

        [Fact]
        public void FileNamingHelper_SanitizeFileName_PreventsPathTraversalAndUnescapesUtf8()
        {
            // Path traversal attempt
            string pathTraversal = "../../etc/passwd";
            string sanitized = FileNamingHelper.SanitizeFileName(pathTraversal);
            sanitized.Should().Be("passwd");

            // Windows path traversal attempt
            string winTraversal = @"C:\Windows\System32\cmd.exe";
            string winSanitized = FileNamingHelper.SanitizeFileName(winTraversal);
            winSanitized.Should().Be("cmd.exe");

            // Encoded UTF-8 string
            string utf8Encoded = "%F0%9F%93%84%20my%20file.pdf";
            string utf8Sanitized = FileNamingHelper.SanitizeFileName(utf8Encoded);
            utf8Sanitized.Should().Contain("my file.pdf");
        }

        [Fact]
        public void FileNamingHelper_DetermineFileNameFromHeaders_PrioritizesContentDisposition()
        {
            var cd = new ContentDispositionHeaderValue("attachment") { FileName = "invoice_2026.pdf" };
            var uri = new Uri("https://example.com/download.php?id=123");

            string result = FileNamingHelper.DetermineFileNameFromHeaders(cd, "application/pdf", uri);
            result.Should().Be("invoice_2026.pdf");
        }

        [Fact]
        public void QueuePriorityEnum_HasFiveDistinctPriorityLevels()
        {
            ((int)QueuePriority.Lowest).Should().BeLessThan((int)QueuePriority.Low);
            ((int)QueuePriority.Low).Should().BeLessThan((int)QueuePriority.Normal);
            ((int)QueuePriority.Normal).Should().BeLessThan((int)QueuePriority.High);
            ((int)QueuePriority.High).Should().BeLessThan((int)QueuePriority.Highest);
        }
    }
}
