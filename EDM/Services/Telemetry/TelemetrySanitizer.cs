using System;
using System.IO;
using System.Text.RegularExpressions;

namespace EDM.Services.Telemetry
{
    /// <summary>
    /// Privacy-Preserving Telemetry Sanitizer.
    /// Strips Personally Identifiable Information (PII), query tokens, local Windows paths,
    /// and credentials from telemetry events before they leave the client machine.
    /// </summary>
    public static class TelemetrySanitizer
    {
        private static readonly Regex UserPathRegex = new(@"[A-Za-z]:\\Users\\[^\\]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QueryParamRegex = new(@"\?[^\s]+", RegexOptions.Compiled);
        private static readonly Regex BearerTokenRegex = new(@"(bearer|token|password|auth|secret)\s*[:=]\s*[^\s,;&]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string SanitizeUrl(string? rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return string.Empty;

            try
            {
                if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
                {
                    // Retain only Scheme + Host + Path without Query or Fragment
                    return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
                }
            }
            catch { }

            // Fallback regex strip
            return QueryParamRegex.Replace(rawUrl, string.Empty);
        }

        public static string SanitizeHost(string? rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return string.Empty;
            try
            {
                if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
                {
                    return uri.Host;
                }
            }
            catch { }
            return "unknown-host";
        }

        public static string SanitizePath(string? rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return string.Empty;

            // Replace user directories like C:\Users\Username with generic <USER_HOME>
            string sanitized = UserPathRegex.Replace(rawPath, @"<USER_HOME>");

            // Replace sensitive internal paths
            sanitized = sanitized.Replace(Environment.UserName, "<USER>");
            return sanitized;
        }

        public static string SanitizeStackTrace(string? stackTrace)
        {
            if (string.IsNullOrWhiteSpace(stackTrace)) return string.Empty;

            string sanitized = SanitizePath(stackTrace);
            sanitized = BearerTokenRegex.Replace(sanitized, "$1=[REDACTED]");
            return sanitized;
        }

        public static string ExtractExtension(string? fileNameOrUrl)
        {
            if (string.IsNullOrWhiteSpace(fileNameOrUrl)) return ".bin";
            try
            {
                string ext = Path.GetExtension(SanitizeUrl(fileNameOrUrl));
                if (!string.IsNullOrEmpty(ext) && ext.Length <= 10)
                {
                    return ext.ToLowerInvariant();
                }
            }
            catch { }
            return ".bin";
        }
    }
}
