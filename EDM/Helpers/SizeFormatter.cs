using System;
using System.Globalization;

namespace EDM.Helpers
{
    /// <summary>
    /// Unified, resilient byte size parser and formatter.
    /// Safely handles nulls, empty strings, "-1", raw bytes, and unit suffixes (B, KB, MB, GB, TB).
    /// Guarantees that unknown sizes never render as negative numbers (e.g. "-1 B").
    /// </summary>
    public static class SizeFormatter
    {
        public const long UnknownSize = -1L;

        /// <summary>
        /// Formats byte count into a clean, human-readable string.
        /// Returns unknownPlaceholder (default "Unknown") when bytes <= 0.
        /// </summary>
        public static string FormatBytes(long bytes, string unknownPlaceholder = "Unknown")
        {
            if (bytes < 0) return unknownPlaceholder;
            if (bytes == 0) return "0 B";

            double dBytes = bytes;
            if (dBytes >= 1024L * 1024 * 1024 * 1024)
                return $"{dBytes / (1024.0 * 1024.0 * 1024.0 * 1024.0):F2} TB";
            if (dBytes >= 1024L * 1024 * 1024)
                return $"{dBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
            if (dBytes >= 1024L * 1024)
                return $"{dBytes / (1024.0 * 1024.0):F1} MB";
            if (dBytes >= 1024L)
                return $"{dBytes / 1024.0:F0} KB";

            return $"{bytes} B";
        }

        /// <summary>
        /// Safely parses an arbitrary size string (e.g., "1024", "10 KB", "1.5 GB", "1024 B", "-1", null) into Int64 bytes.
        /// Returns UnknownSize (-1) for invalid, negative, or unknown size strings.
        /// </summary>
        public static long ParseToBytes(string? sizeStr)
        {
            if (string.IsNullOrWhiteSpace(sizeStr)) return UnknownSize;

            string s = sizeStr.Trim();

            // Explicit unknown indicators
            if (s.Equals("-1", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("-1 B", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Calculating...", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("--", StringComparison.OrdinalIgnoreCase))
            {
                return UnknownSize;
            }

            try
            {
                if (s.EndsWith("TB", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseDouble(s[..^2], out double tb) && tb >= 0)
                        return (long)(tb * 1024.0 * 1024.0 * 1024.0 * 1024.0);
                }
                else if (s.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseDouble(s[..^2], out double gb) && gb >= 0)
                        return (long)(gb * 1024.0 * 1024.0 * 1024.0);
                }
                else if (s.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseDouble(s[..^2], out double mb) && mb >= 0)
                        return (long)(mb * 1024.0 * 1024.0);
                }
                else if (s.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseDouble(s[..^2], out double kb) && kb >= 0)
                        return (long)(kb * 1024.0);
                }
                else if (s.EndsWith("B", StringComparison.OrdinalIgnoreCase) && !s.EndsWith("KB", StringComparison.OrdinalIgnoreCase) && !s.EndsWith("MB", StringComparison.OrdinalIgnoreCase) && !s.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseDouble(s[..^1], out double b) && b >= 0)
                        return (long)b;
                }
                else if (long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out long rawBytes))
                {
                    return rawBytes >= 0 ? rawBytes : UnknownSize;
                }
                else if (TryParseDouble(s, out double rawDouble))
                {
                    return rawDouble >= 0 ? (long)rawDouble : UnknownSize;
                }
            }
            catch
            {
                return UnknownSize;
            }

            return UnknownSize;
        }

        /// <summary>
        /// Normalizes a size string: converts raw numbers or unit strings into standard formatted representation (e.g. "15.2 MB" or "Unknown").
        /// </summary>
        public static string NormalizeSizeString(string? sizeStr, string unknownPlaceholder = "Unknown")
        {
            long bytes = ParseToBytes(sizeStr);
            return bytes > 0 ? FormatBytes(bytes) : unknownPlaceholder;
        }

        private static bool TryParseDouble(string input, out double result)
        {
            input = input.Trim();
            // Try invariant culture first, then current culture
            return double.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ||
                   double.TryParse(input, NumberStyles.Any, CultureInfo.CurrentCulture, out result);
        }
    }
}
