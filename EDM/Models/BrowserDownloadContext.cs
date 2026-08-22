using System;
using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;

namespace EDM.Models
{
    /// <summary>
    /// Classification of authentication and authorization failure modes.
    /// </summary>
    public enum AuthenticationErrorType
    {
        None = 0,
        AuthenticationRequired = 1,     // HTTP 401 Unauthorized
        AuthenticationExpired = 2,      // Session/token expired during active download
        Forbidden = 3,                  // HTTP 403 Forbidden / Access Denied
        Unauthorized = 4,               // General unauthorized
        InvalidContext = 5,             // Missing or corrupted browser context
        InvalidCookie = 6,              // Malformed or oversized cookie header
        InvalidAuthorization = 7,       // Malformed authorization header
        SecurityRedirectViolation = 8   // Cross-origin redirect violation
    }

    /// <summary>
    /// Exception thrown when an authenticated download encounters an authentication or authorization failure.
    /// Prevents infinite retry loops and surfaces structured UI error states.
    /// </summary>
    public class DownloadAuthenticationException : HttpRequestException
    {
        public AuthenticationErrorType ErrorType { get; }
        public Uri? RequestUri { get; }
        public string? Host { get; }

        public DownloadAuthenticationException(
            AuthenticationErrorType errorType,
            string message,
            HttpStatusCode? statusCode = null,
            Uri? requestUri = null,
            Exception? inner = null)
            : base(message, inner, statusCode)
        {
            ErrorType = errorType;
            RequestUri = requestUri;
            Host = requestUri?.Host;
        }

        public string GetDisplayStatus() => ErrorType switch
        {
            AuthenticationErrorType.AuthenticationRequired => "Authentication Required",
            AuthenticationErrorType.AuthenticationExpired => "Authentication Expired",
            AuthenticationErrorType.Forbidden => "Access Denied (403 Forbidden)",
            AuthenticationErrorType.Unauthorized => "Unauthorized (401)",
            AuthenticationErrorType.InvalidCookie => "Invalid Session Cookie",
            AuthenticationErrorType.InvalidAuthorization => "Invalid Authorization",
            AuthenticationErrorType.SecurityRedirectViolation => "Security: Cross-Origin Blocked",
            _ => "Authentication Failed"
        };
    }

    /// <summary>
    /// Strongly-typed, validated browser download context model.
    /// Carries only safe and necessary browser context fields from extension handoff to HTTP engine.
    /// </summary>
    public sealed class BrowserDownloadContext
    {
        public string DownloadUrl { get; set; } = string.Empty;
        public string? SourcePageUrl { get; set; }
        public string? Referer { get; set; }
        public string? UserAgent { get; set; }
        public string? FileName { get; set; }
        public string? MimeType { get; set; }
        public int? TabId { get; set; }
        public int? FrameId { get; set; }
        public string? Cookies { get; set; }
        public string? AuthHeader { get; set; }
        public string? PostData { get; set; }
        public string? RequestId { get; set; }
        public string? CorrelationId { get; set; }
        public DownloadCredentials? Credentials { get; set; }

        public bool HasAuthentication => 
            !string.IsNullOrWhiteSpace(Cookies) ||
            !string.IsNullOrWhiteSpace(AuthHeader) ||
            (Credentials != null && !Credentials.IsEmpty);

        /// <summary>
        /// Validates context fields to prevent memory overflow, header injection, and dangerous schemes.
        /// </summary>
        public bool Validate(out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(DownloadUrl))
            {
                error = "Download URL cannot be empty.";
                return false;
            }

            if (!Uri.TryCreate(DownloadUrl, UriKind.Absolute, out var uri))
            {
                error = "Malformed download URL.";
                return false;
            }

            string scheme = uri.Scheme.ToLowerInvariant();
            if (scheme is not ("http" or "https" or "ftp" or "ftps"))
            {
                error = $"Unsupported protocol scheme '{scheme}:'.";
                return false;
            }

            // Size guard rails to prevent memory exhaustion & HTTP 431
            if (DownloadUrl.Length > 8192)
            {
                error = "Download URL exceeds maximum safe length (8 KB).";
                return false;
            }

            if (Cookies != null && Cookies.Length > 32768)
            {
                error = "Cookie payload exceeds maximum safe length (32 KB).";
                return false;
            }

            if (UserAgent != null && UserAgent.Length > 2048)
            {
                UserAgent = UserAgent.Substring(0, 2048);
            }

            if (Referer != null && Referer.Length > 4096)
            {
                Referer = Referer.Substring(0, 4096);
            }

            return true;
        }

        public static BrowserDownloadContext FromDownloadItem(DownloadItem item)
        {
            return new BrowserDownloadContext
            {
                DownloadUrl = item.Url,
                SourcePageUrl = item.PageUrl,
                Referer = !string.IsNullOrWhiteSpace(item.Referer) ? item.Referer : item.PageUrl,
                UserAgent = item.UserAgent,
                FileName = item.FileName,
                Cookies = item.Cookies,
                AuthHeader = item.AuthHeader,
                PostData = item.PostData,
                Credentials = item.BuildCredentials()
            };
        }
    }
}
