using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public enum DownloadIntegrityStatus
    {
        NotVerified,
        Verified,
        VerificationFailed,
        VerificationUnavailable
    }

    /// <summary>
    /// Crash-safe, versioned download state persisted to disk.
    /// Supports v1/v2/v3 schema migrations.
    /// </summary>
    public class DurableDownloadState
    {
        public int SchemaVersion { get; set; } = DurableMetadataManager.CurrentSchemaVersion;

        public string DownloadId { get; set; } = Guid.NewGuid().ToString("N");
        public string Url { get; set; } = string.Empty;
        public string OriginalUrl { get; set; } = string.Empty;
        public string ResolvedUrl { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public long TotalBytes { get; set; }
        public bool ServerSupportsRanges { get; set; }
        public string? ETag { get; set; }
        public string? LastModified { get; set; }
        public string? Protocol { get; set; } = "HTTP/1.1";
        public int TotalRetries { get; set; }
        public DateTime CreatedTimeUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedTimeUtc { get; set; } = DateTime.UtcNow;
        public List<SegmentRange> Segments { get; set; } = new();

        // Checksum & Integrity Metadata
        public string? IntegrityAlgorithm { get; set; }
        public string? ExpectedChecksum { get; set; }
        public DownloadIntegrityStatus IntegrityStatus { get; set; } = DownloadIntegrityStatus.NotVerified;

        // Media Metadata
        public string? MediaId { get; set; }
        public string? MediaTitle { get; set; }
        public string? MediaContainer { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<int, string>? SegmentChecksums { get; set; }
    }

    public class DurableMetadataManager
    {
        public const int CurrentSchemaVersion = 3;
        private const int MinSupportedSchemaVersion = 1;

        private readonly object _writeLock = new();

        private static readonly JsonSerializerOptions SerializeOptions = new()
        {
            WriteIndented = true,
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode
        };

        private static readonly JsonSerializerOptions DeserializeOptions = new()
        {
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode
        };

        /// <summary>
        /// Writes a crash-safe atomic snapshot of state to disk with flush-to-disk and .bak backup.
        /// </summary>
        public async Task WriteStateAtomicAsync(string metaPath, DurableDownloadState state, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(metaPath)) throw new ArgumentNullException(nameof(metaPath));
            if (state == null) throw new ArgumentNullException(nameof(state));

            ct.ThrowIfCancellationRequested();

            DurableDownloadState snapshot = CaptureSnapshot(state);
            string json = JsonSerializer.Serialize(snapshot, SerializeOptions);

            string directory = Path.GetDirectoryName(metaPath) ?? ".";
            Directory.CreateDirectory(directory);
            string tmpPath = metaPath + ".tmp";
            string bakPath = metaPath + ".bak";

            const int maxMoveRetries = 5;
            for (int attempt = 0; attempt <= maxMoveRetries; attempt++)
            {
                try
                {
                    lock (_writeLock)
                    {
                        // 1. Write to temporary file with explicit disk flush
                        using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                        using (var sw = new StreamWriter(fs, System.Text.Encoding.UTF8))
                        {
                            sw.Write(json);
                            sw.Flush();
                            fs.Flush(flushToDisk: true);
                        }

                        // 2. Backup existing valid metadata file if it exists
                        if (File.Exists(metaPath))
                        {
                            try { File.Copy(metaPath, bakPath, overwrite: true); } catch { }
                        }

                        // 3. Atomically replace target metadata file
                        File.Move(tmpPath, metaPath, overwrite: true);
                    }
                    break;
                }
                catch (IOException) when (attempt < maxMoveRetries)
                {
                    try { await Task.Delay(20 * (attempt + 1), CancellationToken.None).ConfigureAwait(false); } catch { }
                }
            }
        }

        private static DurableDownloadState CaptureSnapshot(DurableDownloadState src)
        {
            return new DurableDownloadState
            {
                SchemaVersion = CurrentSchemaVersion,
                DownloadId = src.DownloadId,
                Url = src.Url,
                OriginalUrl = !string.IsNullOrEmpty(src.OriginalUrl) ? src.OriginalUrl : src.Url,
                ResolvedUrl = !string.IsNullOrEmpty(src.ResolvedUrl) ? src.ResolvedUrl : src.Url,
                Filename = src.Filename,
                DestinationPath = src.DestinationPath,
                TotalBytes = src.TotalBytes,
                ServerSupportsRanges = src.ServerSupportsRanges,
                ETag = src.ETag,
                LastModified = src.LastModified,
                Protocol = src.Protocol,
                TotalRetries = src.TotalRetries,
                CreatedTimeUtc = src.CreatedTimeUtc,
                LastUpdatedTimeUtc = DateTime.UtcNow,
                IntegrityAlgorithm = src.IntegrityAlgorithm,
                ExpectedChecksum = src.ExpectedChecksum,
                IntegrityStatus = src.IntegrityStatus,
                MediaId = src.MediaId,
                MediaTitle = src.MediaTitle,
                MediaContainer = src.MediaContainer,
                Segments = src.Segments.Select(s => s.Clone()).ToList(),
                SegmentChecksums = src.SegmentChecksums != null ? new Dictionary<int, string>(src.SegmentChecksums) : null
            };
        }

        /// <summary>
        /// Reads and validates the persisted download state. Automatically falls back to .bak if primary is corrupt.
        /// </summary>
        public async Task<DurableDownloadState?> ReadStateAsync(string metaPath, CancellationToken ct)
        {
            string tmpPath = metaPath + ".tmp";
            string bakPath = metaPath + ".bak";

            if (File.Exists(tmpPath))
            {
                try { File.Delete(tmpPath); } catch { }
            }

            if (File.Exists(metaPath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(metaPath, ct).ConfigureAwait(false);
                    var state = ParseAndValidate(json, metaPath);
                    if (state != null) return state;
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"[DurableMetadataManager] Primary metadata corrupt '{metaPath}': {ex.Message}. Trying backup.");
                }
            }

            // Fallback to .bak file if primary read failed or was missing
            if (File.Exists(bakPath))
            {
                try
                {
                    string bakJson = await File.ReadAllTextAsync(bakPath, ct).ConfigureAwait(false);
                    var bakState = ParseAndValidate(bakJson, bakPath);
                    if (bakState != null)
                    {
                        LoggingService.Log($"[DurableMetadataManager] Successfully recovered metadata from backup '{bakPath}'.");
                        return bakState;
                    }
                }
                catch { }
            }

            return null;
        }

        private static DurableDownloadState? ParseAndValidate(string json, string metaPath)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            DurableDownloadState? state;
            try
            {
                state = JsonSerializer.Deserialize<DurableDownloadState>(json, DeserializeOptions);
            }
            catch (JsonException ex)
            {
                LoggingService.LogWarning($"[DurableMetadataManager] Corrupt JSON at '{metaPath}': {ex.Message}");
                return null;
            }

            if (state == null) return null;

            // Schema version migration: v1/v2 upgrade automatically
            if (state.SchemaVersion < MinSupportedSchemaVersion)
            {
                LoggingService.LogWarning($"[DurableMetadataManager] Discarding outdated schema version {state.SchemaVersion} at '{metaPath}'.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(state.Url) || state.TotalBytes <= 0 || state.Segments == null || state.Segments.Count == 0)
            {
                return null;
            }

            if (!ValidateInvariants(state))
            {
                LoggingService.LogWarning($"[DurableMetadataManager] Coverage invariant check failed for '{metaPath}'.");
                return null;
            }

            return state;
        }

        public static bool ValidateInvariants(DurableDownloadState state)
        {
            if (state == null || state.TotalBytes <= 0 || state.Segments == null || state.Segments.Count == 0) return false;

            var sorted = state.Segments.OrderBy(s => s.Start).ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                var seg = sorted[i];

                if (seg.Start > seg.End) return false;
                if (seg.BytesDownloaded < 0 || seg.BytesDownloaded > seg.TotalBytes) return false;

                if (i < sorted.Count - 1)
                {
                    var next = sorted[i + 1];
                    if (seg.End >= next.Start) return false; // Overlap
                    if (seg.End + 1 != next.Start) return false; // Gap
                }
            }

            if (sorted[0].Start != 0) return false;
            if (sorted[^1].End != state.TotalBytes - 1) return false;

            return true;
        }

        public bool ReconcileAndValidate(DurableDownloadState state, string remoteETag, string remoteLastModified)
        {
            if (state == null) return false;

            // 1. Remote entity validator checks
            if (!string.IsNullOrEmpty(state.ETag) && !string.IsNullOrEmpty(remoteETag))
            {
                if (!string.Equals(state.ETag, remoteETag, StringComparison.OrdinalIgnoreCase))
                {
                    LoggingService.LogWarning($"[DurableMetadataManager] ETag changed (cached='{state.ETag}', remote='{remoteETag}'). Discarding stale resume state.");
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(state.LastModified) && !string.IsNullOrEmpty(remoteLastModified))
            {
                if (!string.Equals(state.LastModified, remoteLastModified, StringComparison.OrdinalIgnoreCase))
                {
                    LoggingService.LogWarning($"[DurableMetadataManager] Last-Modified changed (cached='{state.LastModified}', remote='{remoteLastModified}'). Discarding stale resume state.");
                    return false;
                }
            }

            // 2. Validate local segment .part files
            foreach (var seg in state.Segments)
            {
                if (string.IsNullOrEmpty(seg.TempPath) || !File.Exists(seg.TempPath))
                {
                    seg.BytesDownloaded = 0;
                    seg.State = SegmentState.Pending;
                    seg.AssignedWorkerId = null;
                    continue;
                }

                long actualLen;
                try
                {
                    actualLen = new FileInfo(seg.TempPath).Length;
                }
                catch
                {
                    seg.BytesDownloaded = 0;
                    seg.State = SegmentState.Pending;
                    seg.AssignedWorkerId = null;
                    continue;
                }

                long maxAllowed = seg.TotalBytes;

                if (actualLen > maxAllowed)
                {
                    try
                    {
                        using var fs = new FileStream(seg.TempPath, FileMode.Open, FileAccess.Write, FileShare.None);
                        fs.SetLength(maxAllowed);
                        actualLen = maxAllowed;
                    }
                    catch
                    {
                        seg.BytesDownloaded = 0;
                        seg.State = SegmentState.Pending;
                        continue;
                    }
                }

                seg.BytesDownloaded = actualLen;

                if (maxAllowed > 0 && actualLen >= maxAllowed)
                {
                    if (!string.IsNullOrEmpty(seg.Sha256Hash))
                    {
                        string? actualHash = ComputeSegmentHash(seg.TempPath);
                        if (!string.Equals(seg.Sha256Hash, actualHash, StringComparison.OrdinalIgnoreCase))
                        {
                            LoggingService.LogWarning($"[DurableMetadataManager] Segment {seg.Id} SHA-256 mismatch. Re-downloading segment {seg.Id}.");
                            seg.BytesDownloaded = 0;
                            seg.Sha256Hash = null;
                            seg.State = SegmentState.Pending;
                        }
                        else
                        {
                            seg.State = SegmentState.Completed;
                        }
                    }
                    else
                    {
                        seg.State = SegmentState.Completed;
                    }
                }
                else
                {
                    seg.State = SegmentState.Pending;
                }
            }

            foreach (var seg in state.Segments)
            {
                if (seg.State == SegmentState.Downloading || seg.State == SegmentState.Failed)
                {
                    seg.State = SegmentState.Pending;
                    seg.AssignedWorkerId = null;
                }
            }

            return true;
        }

        private static string? ComputeSegmentHash(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var sha = System.Security.Cryptography.SHA256.Create();
                var hash = sha.ComputeHash(fs);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch
            {
                return null;
            }
        }

        public void CleanOrphanTempDirectories(string downloadDir)
        {
            try
            {
                if (!Directory.Exists(downloadDir)) return;
                var dirs = Directory.GetDirectories(downloadDir, ".tmp_*");
                foreach (var dir in dirs)
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        if (DateTime.UtcNow - dirInfo.LastWriteTimeUtc > TimeSpan.FromDays(7))
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
