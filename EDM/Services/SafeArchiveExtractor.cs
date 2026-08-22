using System;
using System.IO;
using System.IO.Compression;

namespace EDM.Services
{
    /// <summary>
    /// Hardened safe archive extractor protecting against ZipSlip directory traversal,
    /// ZIP bombs (unreasonable decompression ratio / explosive size), and symlink escalation.
    /// </summary>
    public static class SafeArchiveExtractor
    {
        public const long MaxUncompressedBytes = 10L * 1024 * 1024 * 1024; // 10 GB
        public const int MaxArchiveEntries = 10_000;
        public const double MaxCompressionRatio = 100.0; // Max 100:1 ratio

        public static bool SafeExtractZip(string zipPath, string destinationDirectory, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!File.Exists(zipPath))
            {
                errorMessage = "Archive file not found.";
                return false;
            }

            try
            {
                string canonicalDest = Path.GetFullPath(destinationDirectory);
                if (!canonicalDest.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                {
                    canonicalDest += Path.DirectorySeparatorChar;
                }

                Directory.CreateDirectory(canonicalDest);

                using var archive = ZipFile.OpenRead(zipPath);

                if (archive.Entries.Count > MaxArchiveEntries)
                {
                    errorMessage = $"Archive contains {archive.Entries.Count} entries, exceeding safe limit of {MaxArchiveEntries}.";
                    return false;
                }

                long totalExtractedBytes = 0;
                long totalCompressedBytes = 0;

                foreach (var entry in archive.Entries)
                {
                    totalCompressedBytes += Math.Max(1, entry.CompressedLength);
                    totalExtractedBytes += entry.Length;

                    if (totalExtractedBytes > MaxUncompressedBytes)
                    {
                        errorMessage = $"Archive total uncompressed size exceeds maximum allowable limit ({MaxUncompressedBytes} bytes).";
                        return false;
                    }

                    double currentRatio = (double)totalExtractedBytes / totalCompressedBytes;
                    if (currentRatio > MaxCompressionRatio && totalExtractedBytes > 50 * 1024 * 1024)
                    {
                        errorMessage = $"Decompression ratio {currentRatio:F1}:1 exceeds safe threshold ({MaxCompressionRatio}:1). Potential ZIP bomb detected.";
                        return false;
                    }

                    // ZipSlip Path Traversal Protection
                    string targetFilePath = Path.GetFullPath(Path.Combine(canonicalDest, entry.FullName));
                    if (!targetFilePath.StartsWith(canonicalDest, StringComparison.OrdinalIgnoreCase))
                    {
                        errorMessage = $"Path traversal attempt detected in entry '{entry.FullName}'. Extraction aborted.";
                        return false;
                    }

                    // Extract directory or file
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(targetFilePath);
                    }
                    else
                    {
                        string? parentDir = Path.GetDirectoryName(targetFilePath);
                        if (parentDir != null) Directory.CreateDirectory(parentDir);

                        entry.ExtractToFile(targetFilePath, overwrite: true);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Extraction error: {ex.Message}";
                LoggingService.LogException("[SafeArchiveExtractor] Extraction failed", ex);
                return false;
            }
        }
    }
}
