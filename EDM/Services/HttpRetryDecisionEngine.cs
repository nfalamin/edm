using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using EDM.Models;

namespace EDM.Services
{
    public enum RetryAction
    {
        Retry = 1,
        RetryAfter = 2,
        Fallback = 3,
        FailFast = 4,
        Revalidate = 5,
        Abort = 6
    }

    public class RetryDecision
    {
        public RetryAction Action { get; set; }
        public TimeSpan BackoffDelay { get; set; } = TimeSpan.Zero;
        public string Reason { get; set; } = string.Empty;
        public bool StripCredentialsOnRedirect { get; set; }
    }

    /// <summary>
    /// Formal deterministic HTTP and Network Protocol retry and security decision engine.
    /// Handles all transient failures, status codes, server drifts, and security trust boundaries.
    /// </summary>
    public static class HttpRetryDecisionEngine
    {
        public const int MaxAllowedRetries = 5;
        public const int MaxRedirectHops = 10;
        private const double BaseBackoffMs = 400.0;
        private const double MaxBackoffMs = 15_000.0;

        /// <summary>
        /// Evaluates an HTTP response against formal deterministic policies.
        /// </summary>
        public static RetryDecision EvaluateResponse(
            HttpResponseMessage response,
            int attempt,
            bool isRangeRequest,
            long? expectedStart,
            long? expectedEnd,
            long? knownTotalSize,
            string? knownEtag,
            string? knownLastModified)
        {
            int statusCode = (int)response.StatusCode;

            // 1. HTTP 200 on a Range Request -> Fallback to single stream
            if (isRangeRequest && response.StatusCode == HttpStatusCode.OK)
            {
                return new RetryDecision
                {
                    Action = RetryAction.Fallback,
                    Reason = "Server returned 200 OK for Range request. Single-stream fallback required."
                };
            }

            // 2. HTTP 206 Partial Content Range Validations
            if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                var contentRange = response.Content.Headers.ContentRange;
                if (contentRange == null)
                {
                    return new RetryDecision
                    {
                        Action = RetryAction.Fallback,
                        Reason = "206 Partial Content missing Content-Range header."
                    };
                }

                if (expectedStart.HasValue && contentRange.HasRange && contentRange.From.HasValue &&
                    contentRange.From.Value != expectedStart.Value)
                {
                    return new RetryDecision
                    {
                        Action = RetryAction.Revalidate,
                        Reason = $"Content-Range start mismatch (expected {expectedStart}, got {contentRange.From})."
                    };
                }

                if (knownTotalSize.HasValue && contentRange.HasLength && contentRange.Length.HasValue &&
                    contentRange.Length.Value != knownTotalSize.Value)
                {
                    return new RetryDecision
                    {
                        Action = RetryAction.Revalidate,
                        Reason = $"Content-Range total length mismatch (expected {knownTotalSize}, got {contentRange.Length})."
                    };
                }

                // Check ETag drift
                string? responseEtag = response.Headers.ETag?.Tag;
                if (!string.IsNullOrEmpty(knownEtag) && !string.IsNullOrEmpty(responseEtag) &&
                    !string.Equals(knownEtag.Trim('"'), responseEtag.Trim('"'), StringComparison.OrdinalIgnoreCase))
                {
                    return new RetryDecision
                    {
                        Action = RetryAction.Revalidate,
                        Reason = "ETag changed mid-download."
                    };
                }

                return new RetryDecision { Action = RetryAction.FailFast, Reason = "206 Validated" }; // Success
            }

            // 3. HTTP 416 Range Not Satisfiable -> Fallback
            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                return new RetryDecision
                {
                    Action = RetryAction.Fallback,
                    Reason = "HTTP 416 Range Not Satisfiable."
                };
            }

            // 4. HTTP 429 (Too Many Requests) or HTTP 503 (Service Unavailable) -> RetryAfter
            if (response.StatusCode == (HttpStatusCode)429 || response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                var retryAfterDelay = ParseRetryAfterHeader(response) ?? CalculateBackoffWithJitter(attempt);
                return new RetryDecision
                {
                    Action = RetryAction.RetryAfter,
                    BackoffDelay = retryAfterDelay,
                    Reason = $"Rate limited or unavailable ({statusCode})."
                };
            }

            // 5. Transient Server Errors (HTTP 408, 425, 500, 502, 504) -> Retry
            if (statusCode == 408 || statusCode == 425 || statusCode == 500 || statusCode == 502 || statusCode == 504)
            {
                if (attempt >= MaxAllowedRetries)
                {
                    return new RetryDecision { Action = RetryAction.Abort, Reason = $"Exceeded max retries on HTTP {statusCode}." };
                }

                return new RetryDecision
                {
                    Action = RetryAction.Retry,
                    BackoffDelay = CalculateBackoffWithJitter(attempt),
                    Reason = $"Transient server error (HTTP {statusCode})."
                };
            }

            // 6. Hard Client Errors (400, 401, 403, 404, 405, 409, 410) -> FailFast
            if (statusCode is 400 or 401 or 403 or 404 or 405 or 409 or 410)
            {
                return new RetryDecision
                {
                    Action = RetryAction.FailFast,
                    Reason = $"Non-retryable client error (HTTP {statusCode})."
                };
            }

            return new RetryDecision { Action = RetryAction.FailFast, Reason = $"HTTP {statusCode}" };
        }

        /// <summary>
        /// Evaluates an Exception against formal network retry policies.
        /// </summary>
        public static RetryDecision EvaluateException(Exception ex, int attempt)
        {
            if (ex is OperationCanceledException)
            {
                return new RetryDecision { Action = RetryAction.Abort, Reason = "User cancellation requested." };
            }

            if (ex is AuthenticationException)
            {
                return new RetryDecision { Action = RetryAction.FailFast, Reason = "TLS Handshake failed." };
            }

            if (ex is SocketException sockEx)
            {
                if (sockEx.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData)
                {
                    return new RetryDecision { Action = RetryAction.Abort, Reason = "DNS resolution failure." };
                }

                if (sockEx.SocketErrorCode is SocketError.ConnectionRefused or SocketError.ConnectionReset or
                    SocketError.TimedOut or SocketError.NetworkUnreachable)
                {
                    if (attempt >= MaxAllowedRetries)
                    {
                        return new RetryDecision { Action = RetryAction.Abort, Reason = $"Exceeded retries on socket error {sockEx.SocketErrorCode}." };
                    }
                    return new RetryDecision
                    {
                        Action = RetryAction.Retry,
                        BackoffDelay = CalculateBackoffWithJitter(attempt),
                        Reason = $"Socket transient failure ({sockEx.SocketErrorCode})."
                    };
                }
            }

            if (ex is IOException or TimeoutException)
            {
                if (attempt >= MaxAllowedRetries)
                {
                    return new RetryDecision { Action = RetryAction.Abort, Reason = "Exceeded retries on IO/Timeout exception." };
                }
                return new RetryDecision
                {
                    Action = RetryAction.Retry,
                    BackoffDelay = CalculateBackoffWithJitter(attempt),
                    Reason = "IO timeout or socket reset."
                };
            }

            return new RetryDecision { Action = RetryAction.FailFast, Reason = ex.Message };
        }

        /// <summary>
        /// Validates redirect target URI against circular redirect loops and cross-origin credential security boundaries.
        /// </summary>
        public static bool ValidateRedirectSecurity(
            Uri currentUri,
            Uri targetUri,
            HashSet<string> visitedUris,
            out bool stripAuthorization)
        {
            stripAuthorization = false;

            // Check circular loop
            string targetKey = targetUri.ToString().ToLowerInvariant();
            if (visitedUris.Contains(targetKey))
            {
                return false; // Circular redirect loop detected!
            }
            visitedUris.Add(targetKey);

            // Cross-origin boundary check: Strip Authorization and sensitive cookies if different host/domain
            if (!string.Equals(currentUri.Host, targetUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                stripAuthorization = true;
            }

            return true;
        }

        public static TimeSpan? ParseRetryAfterHeader(HttpResponseMessage response)
        {
            if (response.Headers.RetryAfter == null) return null;

            if (response.Headers.RetryAfter.Delta.HasValue)
            {
                return response.Headers.RetryAfter.Delta.Value;
            }

            if (response.Headers.RetryAfter.Date.HasValue)
            {
                var diff = response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
                return diff > TimeSpan.Zero ? diff : TimeSpan.FromSeconds(1);
            }

            return null;
        }

        public static TimeSpan CalculateBackoffWithJitter(int attempt)
        {
            double jitter = Random.Shared.NextDouble() * 200.0;
            double delayMs = BaseBackoffMs * Math.Pow(2, attempt) + jitter;
            return TimeSpan.FromMilliseconds(Math.Min(MaxBackoffMs, Math.Max(100.0, delayMs)));
        }
    }
}
