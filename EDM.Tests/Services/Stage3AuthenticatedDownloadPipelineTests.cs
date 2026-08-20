using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class Stage3AuthenticatedDownloadPipelineTests
    {
        [Fact]
        public void CrossOriginRedirect_StripsAuthorizationAndCookies_OnDifferentDomain()
        {
            var originalUri = new Uri("https://auth.example.com/download/file.zip");
            var targetUri = new Uri("https://evil-mirror.com/download/file.zip");

            var request = new HttpRequestMessage(HttpMethod.Get, targetUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "secret_token_12345");
            request.Headers.TryAddWithoutValidation("Cookie", "session_id=abcdef123456");
            request.Headers.Referrer = originalUri;

            CrossOriginRedirectSecurityHandler.IsSameOrigin(originalUri, targetUri).Should().BeFalse();
            CrossOriginRedirectSecurityHandler.IsSameDomain(originalUri, targetUri).Should().BeFalse();

            CrossOriginRedirectSecurityHandler.SanitizeRequestForRedirect(request, originalUri, targetUri);

            request.Headers.Authorization.Should().BeNull();
            request.Headers.Contains("Cookie").Should().BeFalse();
            request.Headers.Referrer.Should().Be(new Uri("https://auth.example.com/"));
        }

        [Fact]
        public void CrossOriginRedirect_StripsAuthorization_OnSameDomainDifferentSubdomain()
        {
            var originalUri = new Uri("https://auth.example.com/download/file.zip");
            var targetUri = new Uri("https://cdn.example.com/download/file.zip");

            var request = new HttpRequestMessage(HttpMethod.Get, targetUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", "dXNlcjpwYXNz");
            request.Headers.TryAddWithoutValidation("Cookie", "domain_session=xyz987");

            CrossOriginRedirectSecurityHandler.IsSameOrigin(originalUri, targetUri).Should().BeFalse();
            CrossOriginRedirectSecurityHandler.IsSameDomain(originalUri, targetUri).Should().BeTrue();

            CrossOriginRedirectSecurityHandler.SanitizeRequestForRedirect(request, originalUri, targetUri);

            // Authorization must be stripped across hosts to prevent token leakage
            request.Headers.Authorization.Should().BeNull();
            // Cookie within same domain is preserved
            request.Headers.Contains("Cookie").Should().BeTrue();
        }

        [Fact]
        public void CrossOriginRedirect_StripsAllSensitiveHeaders_OnHttpsToHttpDowngrade()
        {
            var originalUri = new Uri("https://secure.example.com/file.zip");
            var targetUri = new Uri("http://insecure.example.com/file.zip");

            var request = new HttpRequestMessage(HttpMethod.Get, targetUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token123");
            request.Headers.TryAddWithoutValidation("Cookie", "session=123");

            CrossOriginRedirectSecurityHandler.SanitizeRequestForRedirect(request, originalUri, targetUri);

            request.Headers.Authorization.Should().BeNull();
            request.Headers.Contains("Cookie").Should().BeFalse();
            request.Headers.Referrer.Should().BeNull();
        }

        [Fact]
        public void SignedUrl_PreservesSignatureAndTokenQueryParameters()
        {
            var originalUri = new Uri("https://s3.amazonaws.com/bucket/file.zip?X-Amz-Signature=abcdef123&X-Amz-Expires=86400&token=my_secret_token");
            var redirectedSameParams = new Uri("https://s3-accelerate.amazonaws.com/bucket/file.zip?X-Amz-Signature=abcdef123&X-Amz-Expires=86400&token=my_secret_token");
            var redirectedMissingParams = new Uri("https://s3-accelerate.amazonaws.com/bucket/file.zip");

            CrossOriginRedirectSecurityHandler.VerifySignedUrlPreservation(originalUri, redirectedSameParams).Should().BeTrue();
            CrossOriginRedirectSecurityHandler.VerifySignedUrlPreservation(originalUri, redirectedMissingParams).Should().BeFalse();
        }

        [Fact]
        public void BrowserDownloadContext_ValidatesDangerousSchemesAndPayloadSize()
        {
            var validContext = new BrowserDownloadContext
            {
                DownloadUrl = "https://example.com/files/document.pdf",
                Cookies = "session=12345",
                UserAgent = "Mozilla/5.0",
                FileName = "document.pdf"
            };

            validContext.Validate(out string error).Should().BeTrue();
            error.Should().BeEmpty();

            var dangerousContext = new BrowserDownloadContext
            {
                DownloadUrl = "javascript:alert(document.cookie)",
                FileName = "exploit.js"
            };

            dangerousContext.Validate(out string dangError).Should().BeFalse();
            dangError.Should().Contain("Unsupported protocol scheme");

            var oversizedContext = new BrowserDownloadContext
            {
                DownloadUrl = "https://example.com/" + new string('a', 9000)
            };

            oversizedContext.Validate(out string overError).Should().BeFalse();
            overError.Should().Contain("exceeds maximum safe length");
        }

        [Fact]
        public void DownloadAuthenticationException_ProvidesStructuredDisplayStatus()
        {
            var ex401 = new DownloadAuthenticationException(
                AuthenticationErrorType.AuthenticationRequired,
                "Unauthorized",
                HttpStatusCode.Unauthorized,
                new Uri("https://example.com/file"));

            ex401.GetDisplayStatus().Should().Be("Authentication Required");

            var exExpired = new DownloadAuthenticationException(
                AuthenticationErrorType.AuthenticationExpired,
                "Expired session",
                HttpStatusCode.Forbidden,
                new Uri("https://example.com/file"));

            exExpired.GetDisplayStatus().Should().Be("Authentication Expired");

            var exForbidden = new DownloadAuthenticationException(
                AuthenticationErrorType.Forbidden,
                "Forbidden",
                HttpStatusCode.Forbidden,
                new Uri("https://example.com/file"));

            exForbidden.GetDisplayStatus().Should().Be("Access Denied (403 Forbidden)");
        }
    }
}
