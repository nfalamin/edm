using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Authentication;
using FluentAssertions;
using Xunit;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class Stage4HttpProtocolHardeningTests : TestBase
    {
        [Fact]
        public void DNSFailure_TriggersAbortDecision()
        {
            var ex = new SocketException((int)SocketError.HostNotFound);
            var decision = HttpRetryDecisionEngine.EvaluateException(ex, 1);

            decision.Action.Should().Be(RetryAction.Abort);
            decision.Reason.Should().Contain("DNS");
        }

        [Fact]
        public void ConnectionReset_TriggersRetryWithExponentialBackoff()
        {
            var ex = new SocketException((int)SocketError.ConnectionReset);
            var decision = HttpRetryDecisionEngine.EvaluateException(ex, 2);

            decision.Action.Should().Be(RetryAction.Retry);
            decision.BackoffDelay.Should().BeGreaterThan(TimeSpan.FromMilliseconds(400));
        }

        [Fact]
        public void TLSHandshakeFailure_TriggersFailFastDecision()
        {
            var ex = new AuthenticationException("SSL Handshake Failed");
            var decision = HttpRetryDecisionEngine.EvaluateException(ex, 1);

            decision.Action.Should().Be(RetryAction.FailFast);
            decision.Reason.Should().Contain("TLS");
        }

        [Fact]
        public void HTTP200_OnRangeRequest_TriggersFallbackDecision()
        {
            using var response = new HttpResponseMessage(HttpStatusCode.OK);
            var decision = HttpRetryDecisionEngine.EvaluateResponse(response, 1, isRangeRequest: true, 0, 1000, 1001, null, null);

            decision.Action.Should().Be(RetryAction.Fallback);
            decision.Reason.Should().Contain("Single-stream fallback");
        }

        [Fact]
        public void HTTP416_RequestedRangeNotSatisfiable_TriggersFallbackDecision()
        {
            using var response = new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable);
            var decision = HttpRetryDecisionEngine.EvaluateResponse(response, 1, isRangeRequest: true, 0, 1000, 1001, null, null);

            decision.Action.Should().Be(RetryAction.Fallback);
        }

        [Fact]
        public void HTTP429_WithRetryAfterSeconds_ExtractsAccurateDelay()
        {
            using var response = new HttpResponseMessage((HttpStatusCode)429);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(12));

            var decision = HttpRetryDecisionEngine.EvaluateResponse(response, 1, false, null, null, null, null, null);

            decision.Action.Should().Be(RetryAction.RetryAfter);
            decision.BackoffDelay.Should().Be(TimeSpan.FromSeconds(12));
        }

        [Fact]
        public void HTTP401_403_404_TriggersImmediateFailFast()
        {
            foreach (var code in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.Conflict })
            {
                using var response = new HttpResponseMessage(code);
                var decision = HttpRetryDecisionEngine.EvaluateResponse(response, 1, false, null, null, null, null, null);
                decision.Action.Should().Be(RetryAction.FailFast);
            }
        }

        [Fact]
        public void ETagDrift_MidDownload_TriggersRevalidate()
        {
            using var response = new HttpResponseMessage(HttpStatusCode.PartialContent);
            response.Content = new ByteArrayContent(new byte[100]);
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 99, 1000);
            response.Headers.ETag = new EntityTagHeaderValue("\"new-modified-version\"");

            var decision = HttpRetryDecisionEngine.EvaluateResponse(
                response, 1, true, 0, 99, 1000, knownEtag: "\"initial-version\"", knownLastModified: null);

            decision.Action.Should().Be(RetryAction.Revalidate);
            decision.Reason.Should().Contain("ETag changed");
        }

        [Fact]
        public void CircularRedirectLoop_IsDetectedAndBlocked()
        {
            var originUri = new Uri("https://example.com/file.iso");
            var redirectUri1 = new Uri("https://example.com/step1");
            var redirectUri2 = new Uri("https://example.com/step2");

            var visited = new HashSet<string>();

            bool step1 = HttpRetryDecisionEngine.ValidateRedirectSecurity(originUri, redirectUri1, visited, out _);
            bool step2 = HttpRetryDecisionEngine.ValidateRedirectSecurity(redirectUri1, redirectUri2, visited, out _);
            bool step3Loop = HttpRetryDecisionEngine.ValidateRedirectSecurity(redirectUri2, redirectUri1, visited, out _); // Loops back!

            step1.Should().BeTrue();
            step2.Should().BeTrue();
            step3Loop.Should().BeFalse("Circular redirect loop must be aborted");
        }

        [Fact]
        public void CrossOriginRedirect_StripsAuthorizationHeadersAcrossTrustBoundaries()
        {
            var originUri = new Uri("https://secure.bank.com/download/statement.pdf");
            var crossOriginUri = new Uri("https://cdn.thirdparty.com/statement.pdf");

            var visited = new HashSet<string>();
            bool valid = HttpRetryDecisionEngine.ValidateRedirectSecurity(originUri, crossOriginUri, visited, out bool stripAuth);

            valid.Should().BeTrue();
            stripAuth.Should().BeTrue("Authorization headers must be stripped when crossing origin trust boundaries");
        }
    }
}
