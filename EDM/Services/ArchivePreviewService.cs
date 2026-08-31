using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace EDM.Services
{
    public class ArchiveEntryInfo
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long CompressedSizeBytes { get; set; }
        public long UncompressedSizeBytes { get; set; }
        public double CompressionRatio { get; set; }
        public DateTimeOffset LastWriteTime { get; set; }
        public bool IsDirectory { get; set; }
        public bool IsPathTraversalSuspicious { get; set; }
    }

    public class ArchivePreviewResult
    {
        public bool IsValid { get; set; }
        public string ArchivePath { get; set; } = string.Empty;
        public int TotalEntries { get; set; }
        public long TotalUncompressedBytes { get; set; }
        public long TotalCompressedBytes { get; set; }
        public List<ArchiveEntryInfo> Entries { get; set; } = new();
        public string? SecurityWarning { get; set; }
    }

    /// <summary>
    /// EDM Native Safe Archive Preview & In-Memory ZIP Inspector.
    /// Provides zero-extraction directory listing, decompression ratio calculation,
    /// and ZipSlip path traversal inspection.
    /// </summary>
    public static class ArchivePreviewService
    {
        public static ArchivePreviewResult InspectZipArchive(string zipFilePath)
        {
            var result = new ArchivePreviewResult { ArchivePath = zipFilePath };

            if (!File.Exists(zipFilePath))
            {
                result.IsValid = false;
                result.SecurityWarning = "Archive file not found.";
                return result;
            }

            try
            {
                using var archive = ZipFile.OpenRead(zipFilePath);
                result.IsValid = true;
                result.TotalEntries = archive.Entries.Count;

                foreach (var entry in archive.Entries)
                {
                    result.TotalCompressedBytes += Math.Max(1, entry.CompressedLength);
                    result.TotalUncompressedBytes += entry.Length;

                    bool suspicious = entry.FullName.Contains("..") ||
                                      entry.FullName.StartsWith("/") ||
                                      entry.FullName.StartsWith("\\");

                    double ratio = entry.CompressedLength > 0
                        ? (double)entry.Length / entry.CompressedLength
                        : 1.0;

                    result.Entries.Add(new ArchiveEntryInfo
                    {
                        Name = entry.Name,
                        FullPath = entry.FullName,
                        CompressedSizeBytes = entry.CompressedLength,
                        UncompressedSizeBytes = entry.Length,
                        CompressionRatio = ratio,
                        LastWriteTime = entry.LastWriteTime,
                        IsDirectory = string.IsNullOrEmpty(entry.Name),
                        IsPathTraversalSuspicious = suspicious
                    });

                    if (suspicious)
                    {
                        result.SecurityWarning = $"Suspicious entry detected in archive: '{entry.FullName}'";
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.SecurityWarning = $"Invalid or corrupt archive: {ex.Message}";
                return result;
            }
        }
    }
}
