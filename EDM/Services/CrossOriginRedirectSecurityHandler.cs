using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

namespace EDM.Services
{
    /// <summary>
    /// Implements origin-aware header forwarding and cross-origin redirect security.
    /// Strictly prevents Authorization tokens and Session Cookies from leaking to foreign domains,
    /// while preserving signed URL query parameters (AWS S3, CloudFront, GCS, Azure Blob).
    /// </summary>
    public static class CrossOriginRedirectSecurityHandler
    {
        private static readonly string[] SensitiveQueryKeys = new[]
        {
            "sig", "signature", "token", "auth", "key", "expires", "se", "st", "sp", "sv", "sr", "key-pair-id"
        };

        /// <summary>
        /// Determines if two URIs have identical Scheme, Host, and Port (Strict Same-Origin).
        /// </summary>
        public static bool IsSameOrigin(Uri? source, Uri? destination)
        {
            if (source == null || destination == null) return false;
            return string.Equals(source.Scheme, destination.Scheme, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(source.Host, destination.Host, StringComparison.OrdinalIgnoreCase) &&
                   source.Port == destination.Port;
        }

        /// <summary>
        /// Determines if two URIs belong to the same registrable domain (e.g. auth.example.com and cdn.example.com).
        /// </summary>
        public static bool IsSameDomain(Uri? source, Uri? destination)
        {
            if (source == null || destination == null) return false;
            if (string.Equals(source.Host, destination.Host, StringComparison.OrdinalIgnoreCase)) return true;

            string rootSource = GetRegistrableDomain(source.Host);
            string rootDest = GetRegistrableDomain(destination.Host);

            return !string.IsNullOrEmpty(rootSource) &&
                   string.Equals(rootSource, rootDest, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Sanitizes an outgoing redirected HttpRequestMessage according to RFC 9110 & Chromium security standards:
        /// - If redirect crosses to a different origin/domain, strip Authorization and Cookie headers.
        /// - Preserve signed URL query parameters.
        /// - Set Referer according to strict-origin-when-cross-origin policy.
        /// </summary>
        public static void SanitizeRequestForRedirect(HttpRequestMessage request, Uri originalUri, Uri targetUri)
        {
            if (request == null || originalUri == null || targetUri == null) return;

            // 1. Cross-Origin Redirect Token Stripping
            if (!IsSameOrigin(originalUri, targetUri))
            {
                // Different host / different origin -> ALWAYS strip Authorization
                request.Headers.Authorization = null;
                request.Headers.Remove("Authorization");

                // If completely different domain -> Strip Cookie header to prevent session leakage
                if (!IsSameDomain(originalUri, targetUri))
                {
                    request.Headers.Remove("Cookie");

                    // Set Referer to Origin only (strict-origin-when-cross-origin)
                    request.Headers.Referrer = new Uri(originalUri.GetLeftPart(UriPartial.Authority));
                }
            }

            // 2. HTTPS to HTTP downgrade protection
            if (string.Equals(originalUri.Scheme, "https", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(targetUri.Scheme, "http", StringComparison.OrdinalIgnoreCase))
            {
                // Insecure downgrade: strip all sensitive headers
                request.Headers.Authorization = null;
                request.Headers.Remove("Authorization");
                request.Headers.Remove("Cookie");
                request.Headers.Referrer = null;
            }
        }

        /// <summary>
        /// Extracts the top-level registrable domain (e.g. 'example.com' from 'api.sub.example.com').
        /// </summary>
        public static string GetRegistrableDomain(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return string.Empty;
            var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 1) return host;
            if (parts.Length == 2) return host;

            // Handle common 2-part ccTLDs (e.g., .co.uk, .com.au, .gov.bd)
            string lastTwo = $"{parts[^2]}.{parts[^1]}".ToLowerInvariant();
            if ((lastTwo.StartsWith("co.") || lastTwo.StartsWith("com.") || lastTwo.StartsWith("org.") ||
                 lastTwo.StartsWith("net.") || lastTwo.StartsWith("edu.") || lastTwo.StartsWith("gov.")) &&
                parts.Length >= 3)
            {
                return $"{parts[^3]}.{parts[^2]}.{parts[^1]}";
            }

            return $"{parts[^2]}.{parts[^1]}";
        }

        /// <summary>
        /// Validates that signed URL parameters (signature, expires, token) in original URI are preserved.
        /// </summary>
        public static bool VerifySignedUrlPreservation(Uri originalUri, Uri redirectedUri)
        {
            if (originalUri == null || redirectedUri == null) return false;
            if (string.IsNullOrEmpty(originalUri.Query)) return true;

            var origQueries = System.Web.HttpUtility.ParseQueryString(originalUri.Query);
            var redQueries = System.Web.HttpUtility.ParseQueryString(redirectedUri.Query);

            foreach (var key in origQueries.AllKeys)
            {
                if (key == null) continue;
                if (SensitiveQueryKeys.Any(k => key.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    if (redQueries[key] == null)
                    {
                        // Target URI is missing the signed URL token
                        return false;
                    }
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Enforces strict HTTP header allowlisting and sanitization against CRLF injection and forbidden protocol headers.
    /// </summary>
    public static class HttpHeaderSecuritySanitizer
    {
        private static readonly System.Collections.Generic.HashSet<string> ForbiddenHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Host",
            "Connection",
            "Content-Length",
            "Transfer-Encoding",
            "Upgrade",
            "Keep-Alive",
            "Proxy-Connection",
            "Proxy-Authorization",
            "TE",
            "Trailer",
            "Sec-WebSocket-Key",
            "Sec-WebSocket-Extensions",
            "Sec-WebSocket-Accept"
        };

        /// <summary>
        /// Checks if a header name is forbidden to prevent HTTP request splitting and hop-by-hop abuse.
        /// </summary>
        public static bool IsForbiddenHeader(string? headerName)
        {
            if (string.IsNullOrWhiteSpace(headerName)) return true;
            return ForbiddenHeaders.Contains(headerName.Trim());
        }

        /// <summary>
        /// Sanitizes header values by stripping dangerous CRLF characters (\r, \n) and trimming whitespace.
        /// </summary>
        public static string SanitizeHeaderValue(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\r", "").Replace("\n", "").Trim();
        }

        /// <summary>
        /// Safely applies a custom header to HttpRequestMessage, skipping forbidden headers and stripping CRLF.
        /// </summary>
        public static bool TryApplySafeHeader(HttpRequestMessage request, string name, string value)
        {
            if (request == null || IsForbiddenHeader(name)) return false;

            string cleanName = SanitizeHeaderValue(name);
            string cleanVal = SanitizeHeaderValue(value);

            if (string.IsNullOrWhiteSpace(cleanName)) return false;

            try
            {
                request.Headers.Remove(cleanName);
                return request.Headers.TryAddWithoutValidation(cleanName, cleanVal);
            }
            catch
            {
                return false;
            }
        }
    }
}
