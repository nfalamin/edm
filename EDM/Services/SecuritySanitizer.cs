using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace EDM.Services
{
    public static class SecuritySanitizer
    {
        private static readonly string[] ReservedDeviceNames = new[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        private static readonly string[] AllowedUrlSchemes = new[] { "http", "https", "ftp", "ftps" };

        public static bool IsAllowedUrlScheme(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    return AllowedUrlSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch { }
            return false;
        }

        public static string SanitizeFileName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return "downloaded_file.bin";

            // Strip path separators and invalid chars
            string nameOnly = Path.GetFileName(rawName);
            var invalidChars = Path.GetInvalidFileNameChars();
            string cleaned = new string(nameOnly.Where(c => !invalidChars.Contains(c)).ToArray());

            if (string.IsNullOrWhiteSpace(cleaned)) return "downloaded_file.bin";

            // Reserved Windows names check
            string nameWithoutExt = Path.GetFileNameWithoutExtension(cleaned);
            if (ReservedDeviceNames.Contains(nameWithoutExt.ToUpperInvariant()))
            {
                cleaned = "_" + cleaned;
            }

            return cleaned;
        }

        public static bool TrySanitizeDestinationPath(string baseDirectory, string requestedPath, out string safeFullPath)
        {
            safeFullPath = string.Empty;
            try
            {
                string canonicalBase = Path.GetFullPath(baseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string unescaped = System.Uri.UnescapeDataString(requestedPath);
                string combined = Path.Combine(canonicalBase, unescaped);
                string canonicalTarget = Path.GetFullPath(combined);

                if (canonicalTarget.StartsWith(canonicalBase, StringComparison.OrdinalIgnoreCase))
                {
                    safeFullPath = canonicalTarget;
                    return true;
                }
            }
            catch { }
            return false;
        }

        public static ProcessStartInfo CreateSafeProcessStartInfo(string fileName, params string[] args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            return psi;
        }
    }
}
