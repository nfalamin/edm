using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class Stage3FinalHardeningTests
    {
        [Theory]
        [InlineData("Host")]
        [InlineData("Connection")]
        [InlineData("Content-Length")]
        [InlineData("Transfer-Encoding")]
        [InlineData("Upgrade")]
        [InlineData("Keep-Alive")]
        [InlineData("Proxy-Connection")]
        [InlineData("Proxy-Authorization")]
        [InlineData("TE")]
        [InlineData("Trailer")]
        [InlineData("Sec-WebSocket-Key")]
        public void HeaderSanitization_RejectsForbiddenProtocolHeaders(string forbiddenHeader)
        {
            HttpHeaderSecuritySanitizer.IsForbiddenHeader(forbiddenHeader).Should().BeTrue();

            using var req = new HttpRequestMessage(HttpMethod.Get, "https://example.com/file");
            bool applied = HttpHeaderSecuritySanitizer.TryApplySafeHeader(req, forbiddenHeader, "malicious_value");
            applied.Should().BeFalse();
        }

        [Fact]
        public void HeaderSanitization_StripsCrlfInjectionCharacters()
        {
            string maliciousInput = "Mozilla/5.0\r\nInjected-Header: evil\nAnother: malicious";
            string clean = HttpHeaderSecuritySanitizer.SanitizeHeaderValue(maliciousInput);

            clean.Should().NotContain("\r");
            clean.Should().NotContain("\n");
            clean.Should().Be("Mozilla/5.0Injected-Header: evilAnother: malicious");

            using var req = new HttpRequestMessage(HttpMethod.Get, "https://example.com/file");
            bool applied = HttpHeaderSecuritySanitizer.TryApplySafeHeader(req, "X-Custom-Header", maliciousInput);
            applied.Should().BeTrue();
            req.Headers.GetValues("X-Custom-Header").Should().ContainSingle(v => !v.Contains("\r") && !v.Contains("\n"));
        }

        [Fact]
        public void LoggingSanitization_RedactsBearerTokensCookiesAndPasswords()
        {
            string logWithBearer = "Received request with Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.secretpayload.sig";
            string cleanBearer = LoggingService.SanitizeLogMessage(logWithBearer);
            cleanBearer.Should().Contain("Bearer [REDACTED]");
            cleanBearer.Should().NotContain("secretpayload");

            string logWithBasic = "Authorization: Basic dXNlcjpwYXNzd29yZDEyMw==";
            string cleanBasic = LoggingService.SanitizeLogMessage(logWithBasic);
            cleanBasic.Should().Contain("Basic [REDACTED]");
            cleanBasic.Should().NotContain("dXNlcjpwYXNzd29yZDEyMw==");

            string logWithCookie = "Cookie: session_id=abcdef123456789; user_token=secret99";
            string cleanCookie = LoggingService.SanitizeLogMessage(logWithCookie);
            cleanCookie.Should().NotContain("abcdef123456789");
            cleanCookie.Should().NotContain("secret99");

            string logWithJson = "{\"cookies\":\"auth_cookie=secret_123\",\"authorization\":\"Bearer secret_token\"}";
            string cleanJson = LoggingService.SanitizeLogMessage(logWithJson);
            cleanJson.Should().NotContain("secret_123");
            cleanJson.Should().NotContain("secret_token");
        }

        [Fact]
        public void DpapiEncryptedProperties_StoreSecretsSecurelyAndIgnorePlaintextInJson()
        {
            var item = new DownloadItem
            {
                FileName = "test.zip",
                AuthHeader = "Bearer super_secret_access_token_12345",
                Cookies = "session_cookie=abcdef987654",
                PostData = "api_key=secret_post_payload"
            };

            // DPAPI encrypted strings should be populated and not equal to plaintext
            item.EncryptedAuthHeader.Should().NotBeNullOrEmpty();
            item.EncryptedAuthHeader.Should().NotBe("Bearer super_secret_access_token_12345");

            item.EncryptedCookies.Should().NotBeNullOrEmpty();
            item.EncryptedCookies.Should().NotBe("session_cookie=abcdef987654");

            item.EncryptedPostData.Should().NotBeNullOrEmpty();
            item.EncryptedPostData.Should().NotBe("api_key=secret_post_payload");

            // JSON serialization must not contain plaintext secrets
            string json = JsonSerializer.Serialize(item);
            json.Should().NotContain("super_secret_access_token_12345");
            json.Should().NotContain("session_cookie=abcdef987654");
            json.Should().NotContain("secret_post_payload");

            // Encrypted properties must be serialized
            json.Should().Contain("EncryptedAuthHeader");
            json.Should().Contain("EncryptedCookies");
            json.Should().Contain("EncryptedPostData");

            // Round-trip deserialization should restore plaintext via DPAPI
            var restored = JsonSerializer.Deserialize<DownloadItem>(json);
            restored.Should().NotBeNull();
            restored!.AuthHeader.Should().Be("Bearer super_secret_access_token_12345");
            restored.Cookies.Should().Be("session_cookie=abcdef987654");
            restored.PostData.Should().Be("api_key=secret_post_payload");
        }

        [Fact]
        public void SameDomain_CorrectlyHandlesCcTldAndStandardDomains()
        {
            var uri1 = new Uri("https://auth.company.co.uk/login");
            var uri2 = new Uri("https://cdn.company.co.uk/download");
            var uri3 = new Uri("https://evil-company.co.uk/download");

            CrossOriginRedirectSecurityHandler.IsSameDomain(uri1, uri2).Should().BeTrue();
            CrossOriginRedirectSecurityHandler.IsSameDomain(uri1, uri3).Should().BeFalse();

            var com1 = new Uri("https://download.microsoft.com/file");
            var com2 = new Uri("https://azuredownload.microsoft.com/file");
            var com3 = new Uri("https://microsoft.evil.com/file");

            CrossOriginRedirectSecurityHandler.IsSameDomain(com1, com2).Should().BeTrue();
            CrossOriginRedirectSecurityHandler.IsSameDomain(com1, com3).Should().BeFalse();
        }

        [Fact]
        public void SignedUrlPreservation_DetectsMissingS3AndAzureParameters()
        {
            var s3SignedUri = new Uri("https://s3.amazonaws.com/files/iso.zip?X-Amz-Signature=abc12345&X-Amz-Expires=3600&X-Amz-Credential=cred");
            var validRedirect = new Uri("https://s3.us-west-2.amazonaws.com/files/iso.zip?X-Amz-Signature=abc12345&X-Amz-Expires=3600&X-Amz-Credential=cred");
            var strippedRedirect = new Uri("https://s3.us-west-2.amazonaws.com/files/iso.zip");

            CrossOriginRedirectSecurityHandler.VerifySignedUrlPreservation(s3SignedUri, validRedirect).Should().BeTrue();
            CrossOriginRedirectSecurityHandler.VerifySignedUrlPreservation(s3SignedUri, strippedRedirect).Should().BeFalse();

            var azureSignedUri = new Uri("https://blob.core.windows.net/container/file.zip?sig=sig123&sp=r&se=2026-12-31&sv=2020-08-04");
            var azureValid = new Uri("https://secondary.blob.core.windows.net/container/file.zip?sig=sig123&sp=r&se=2026-12-31&sv=2020-08-04");
            var azureMissingSig = new Uri("https://secondary.blob.core.windows.net/container/file.zip?sp=r&se=2026-12-31");

            CrossOriginRedirectSecurityHandler.VerifySignedUrlPreservation(azureSignedUri, azureValid).Should().BeTrue();
            CrossOriginRedirectSecurityHandler.VerifySignedUrlPreservation(azureSignedUri, azureMissingSig).Should().BeFalse();
        }

        [Fact]
        public void AuthenticationFailure_DoesNotBlockSubsequentQueueDownloads()
        {
            var scheduler = DownloadQueueScheduler.Instance;
            scheduler.Clear();

            string idA = Guid.NewGuid().ToString("N");
            string idB = Guid.NewGuid().ToString("N");
            string idC = Guid.NewGuid().ToString("N");

            var now = DateTime.UtcNow;
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = idA, Url = "https://auth.example.com/fileA.zip", EnqueuedTimeUtc = now.AddMinutes(-3) });
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = idB, Url = "https://public.example.com/fileB.zip", EnqueuedTimeUtc = now.AddMinutes(-2) });
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = idC, Url = "https://public.example.com/fileC.zip", EnqueuedTimeUtc = now.AddMinutes(-1) });

            // Start Download A
            var next1 = scheduler.TryGetNextDownloadToStart();
            next1.Should().NotBeNull();
            next1!.DownloadId.Should().Be(idA);
            scheduler.MarkStarted(idA);

            // Download A fails with 401 Authentication Required
            scheduler.MarkFailed(idA);

            // Scheduler should immediately allow Download B to start
            var next2 = scheduler.TryGetNextDownloadToStart();
            next2.Should().NotBeNull();
            next2!.DownloadId.Should().Be(idB);
            scheduler.MarkStarted(idB);

            // Download B completes
            scheduler.MarkCompleted(idB);

            // Scheduler should immediately allow Download C to start
            var next3 = scheduler.TryGetNextDownloadToStart();
            next3.Should().NotBeNull();
            next3!.DownloadId.Should().Be(idC);
        }

        [Fact]
        public void TransientExceptionClassification_Excludes401And403FromRetry()
        {
            var ex401 = new HttpRequestException("401 Unauthorized", null, HttpStatusCode.Unauthorized);
            var ex403 = new HttpRequestException("403 Forbidden", null, HttpStatusCode.Forbidden);
            var ex404 = new HttpRequestException("404 Not Found", null, HttpStatusCode.NotFound);
            var ex429 = new HttpRequestException("429 Too Many Requests", null, (HttpStatusCode)429);
            var ex503 = new HttpRequestException("503 Service Unavailable", null, HttpStatusCode.ServiceUnavailable);

            HttpRequestPipeline.IsTransientException(ex401).Should().BeFalse("401 requires credentials and must not enter retry loops");
            HttpRequestPipeline.IsTransientException(ex403).Should().BeFalse("403 requires fresh permission and must not enter retry loops");
            HttpRequestPipeline.IsTransientException(ex404).Should().BeFalse("404 is a permanent client error");
            HttpRequestPipeline.IsTransientException(ex429).Should().BeTrue("429 is a rate limit and should retry with backoff");
            HttpRequestPipeline.IsTransientException(ex503).Should().BeTrue("503 is a server error and should retry with backoff");
        }
    }
}
