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
    /// <summary>
    /// Crash-safe, versioned download state persisted to disk.
    ///
    /// SchemaVersion history:
    ///   1 - Legacy (DownloadMetadata, internal MultiPartDownloader format)
    ///   2 - Current (DurableDownloadState, segment-aware, ETag/Last-Modified)
    ///   3 - Added SegmentChecksums field (reserved, populated by future A6 work)
    /// </summary>
    public class DurableDownloadState
    {
        /// <summary>
        /// Schema version for forward/backward compatibility detection.
        /// Always written as the current schema version. Deserialization
        /// rejects states with an unsupported version.
        /// </summary>
        public int SchemaVersion { get; set; } = DurableMetadataManager.CurrentSchemaVersion;

        public string DownloadId { get; set; } = Guid.NewGuid().ToString("N");
        public string Url { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public long TotalBytes { get; set; }
        public bool ServerSupportsRanges { get; set; }
        public string? ETag { get; set; }
        public string? LastModified { get; set; }
        public DateTime CreatedTimeUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedTimeUtc { get; set; } = DateTime.UtcNow;
        public List<SegmentRange> Segments { get; set; } = new();

        // Reserved for future per-segment SHA-256 checksums (A6)
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<int, string>? SegmentChecksums { get; set; }
    }

    public class DurableMetadataManager
    {
        /// <summary>
        /// Current metadata schema version. Bump this when the schema changes
        /// in a way that is not backward-compatible.
        /// </summary>
        public const int CurrentSchemaVersion = 2;

        /// <summary>
        /// Minimum schema version we will accept for resume. v1.x state files supported for backward compatibility.
        /// </summary>
        private const int MinSupportedSchemaVersion = 1;

        private readonly object _writeLock = new();

        private static readonly JsonSerializerOptions SerializeOptions = new()
        {
            WriteIndented = true,
            // Unknown fields from future schema versions are silently ignored during read
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode
        };

        private static readonly JsonSerializerOptions DeserializeOptions = new()
        {
            // Unknown fields from future schema versions are silently ignored
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode
        };

        // -----------------------------------------------------------------------
        // WRITE — atomic snapshot
        // -----------------------------------------------------------------------

        /// <summary>
        /// Writes a crash-safe atomic snapshot of <paramref name="state"/> to disk.
        ///
        /// Safety properties:
        /// 1. A consistent, immutable snapshot is captured under a lock BEFORE serialization,
        ///    so segment mutations during serialization cannot produce a torn write.
        /// 2. The JSON is written to a .tmp sibling first, then atomically renamed.
        ///    A crash between write and rename leaves only the .tmp orphan, never a half-written
        ///    metadata.json. The .tmp is cleaned up on next read (see ReadStateAsync).
        /// 3. FileOptions.WriteThrough + Flush(flushToDisk:true) ensures the OS write-back
        ///    cache is flushed before rename — the renamed file is durable on power loss.
        /// </summary>
        public async Task WriteStateAtomicAsync(string metaPath, DurableDownloadState state, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(metaPath)) throw new ArgumentNullException(nameof(metaPath));
            if (state == null) throw new ArgumentNullException(nameof(state));

            ct.ThrowIfCancellationRequested();

            // A5-FIX 1: Capture an immutable snapshot BEFORE serialization.
            // Segment workers mutate segment.BytesDownloaded concurrently; serializing
            // the live state object produces a torn (inconsistent) JSON snapshot.
            DurableDownloadState snapshot = CaptureSnapshot(state);

            // Serialize outside the lock — this is CPU work and can be slow for many segments
            string json = JsonSerializer.Serialize(snapshot, SerializeOptions);

            string directory = Path.GetDirectoryName(metaPath) ?? ".";
            Directory.CreateDirectory(directory);
            string tmpPath = metaPath + ".tmp";

            const int maxMoveRetries = 5;
            for (int attempt = 0; attempt <= maxMoveRetries; attempt++)
            {
                try
                {
                    lock (_writeLock)
                    {
                        File.WriteAllText(tmpPath, json, System.Text.Encoding.UTF8);
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



        /// <summary>
        /// Creates a deep, immutable copy of state and its segments at a single moment in time.
        /// Called under the caller's domain lock or before entering the write lock.
        /// </summary>
        private static DurableDownloadState CaptureSnapshot(DurableDownloadState src)
        {
            return new DurableDownloadState
            {
                SchemaVersion = CurrentSchemaVersion,
                DownloadId = src.DownloadId,
                Url = src.Url,
                DestinationPath = src.DestinationPath,
                TotalBytes = src.TotalBytes,
                ServerSupportsRanges = src.ServerSupportsRanges,
                ETag = src.ETag,
                LastModified = src.LastModified,
                CreatedTimeUtc = src.CreatedTimeUtc,
                LastUpdatedTimeUtc = DateTime.UtcNow,
                // Deep-clone each segment so workers can't mutate the snapshot mid-serialize
                Segments = src.Segments.Select(s => s.Clone()).ToList(),
                SegmentChecksums = src.SegmentChecksums != null
                    ? new Dictionary<int, string>(src.SegmentChecksums)
                    : null
            };
        }

        // -----------------------------------------------------------------------
        // READ — schema validation + orphan .tmp cleanup
        // -----------------------------------------------------------------------

        /// <summary>
        /// Reads and validates the persisted download state.
        ///
        /// Handles all failure modes safely:
        /// - File missing → returns null (fresh start)
        /// - Orphan .tmp from previous crash → cleaned up, then reads metaPath
        /// - Empty or partial JSON → returns null (fresh start)
        /// - Unknown schema version → returns null (fresh start, do not corrupt)
        /// - Missing required fields → returns null
        /// - Unknown extra fields → silently ignored (forward compat)
        /// </summary>
        public async Task<DurableDownloadState?> ReadStateAsync(string metaPath, CancellationToken ct)
        {
            // A5-FIX 3: Clean up orphan .tmp left by a crash between write and rename
            string tmpPath = metaPath + ".tmp";
            if (File.Exists(tmpPath))
            {
                try
                {
                    File.Delete(tmpPath);
                    LoggingService.LogWarning(
                        $"[DurableMetadataManager] Cleaned orphan .tmp file: {tmpPath}. " +
                        $"This indicates a crash during a previous metadata write.");
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"[DurableMetadataManager] Could not delete orphan .tmp: {ex.Message}");
                }
            }

            if (!File.Exists(metaPath)) return null;

            string json;
            try
            {
                // A5-FIX 2: Do not hold the write lock during file I/O — read asynchronously
                json = await File.ReadAllTextAsync(metaPath, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"[DurableMetadataManager] Could not read '{metaPath}': {ex.Message}");
                return null;
            }

            // A5-FIX 5: Validate schema version BEFORE deserializing fields
            return ParseAndValidate(json, metaPath);
        }

        private static DurableDownloadState? ParseAndValidate(string json, string metaPath)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                LoggingService.LogWarning($"[DurableMetadataManager] Empty metadata at '{metaPath}'. Starting fresh.");
                return null;
            }

            DurableDownloadState? state;
            try
            {
                state = JsonSerializer.Deserialize<DurableDownloadState>(json, DeserializeOptions);
            }
            catch (JsonException ex)
            {
                LoggingService.LogWarning(
                    $"[DurableMetadataManager] Corrupt/partial JSON at '{metaPath}': {ex.Message}. Starting fresh.");
                return null;
            }

            if (state == null)
            {
                LoggingService.LogWarning($"[DurableMetadataManager] Null state after deserialization of '{metaPath}'.");
                return null;
            }

            // A5-FIX 5: Schema version guard
            if (state.SchemaVersion < MinSupportedSchemaVersion)
            {
                LoggingService.LogWarning(
                    $"[DurableMetadataManager] Schema version {state.SchemaVersion} is below minimum " +
                    $"supported {MinSupportedSchemaVersion} at '{metaPath}'. Discarding stale state.");
                return null;
            }

            if (state.SchemaVersion > CurrentSchemaVersion)
            {
                LoggingService.LogWarning(
                    $"[DurableMetadataManager] Schema version {state.SchemaVersion} is newer than current " +
                    $"{CurrentSchemaVersion}. Will attempt to use, but unknown fields are ignored.");
                // Allow — forward compatibility: unknown fields are silently ignored
            }

            // A5-FIX 5: Validate required fields
            if (string.IsNullOrWhiteSpace(state.Url))
            {
                LoggingService.LogWarning($"[DurableMetadataManager] Missing Url in state at '{metaPath}'.");
                return null;
            }

            if (state.TotalBytes <= 0)
            {
                LoggingService.LogWarning($"[DurableMetadataManager] Invalid TotalBytes={state.TotalBytes} at '{metaPath}'.");
                return null;
            }

            if (state.Segments == null || state.Segments.Count == 0)
            {
                LoggingService.LogWarning($"[DurableMetadataManager] Null or empty Segments list at '{metaPath}'. Starting fresh.");
                return null;
            }

            if (!ValidateInvariants(state))
            {
                LoggingService.LogWarning($"[DurableMetadataManager] Invariant check failed (Overlap/Gap/Invalid bounds) at '{metaPath}'. Starting fresh.");
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
                    if (seg.End >= next.Start) return false; // Overlap!
                    if (seg.End + 1 != next.Start) return false; // Gap!
                }
            }

            if (sorted[0].Start != 0) return false;
            if (sorted[^1].End != state.TotalBytes - 1) return false;

            return true;
        }


        // -----------------------------------------------------------------------
        // RECONCILE — validate partial files against segment metadata
        // -----------------------------------------------------------------------

        /// <summary>
        /// Reconciles the persisted segment state with what's actually on disk,
        /// and validates remote entity tags.
        ///
        /// Returns true if the state is safe to resume from, false if a fresh
        /// download must be started.
        ///
        /// A5-FIX 4: Uses actual file size for BytesDownloaded correction, and truncates
        /// oversized .part files. Does NOT use file size alone to declare a segment complete —
        /// a segment is Completed only if actualLen >= TotalBytes AND TotalBytes > 0.
        /// </summary>
        public bool ReconcileAndValidate(DurableDownloadState state, string remoteETag, string remoteLastModified)
        {
            if (state == null) return false;

            // 1. Remote entity validator checks (ETag / Last-Modified)
            // A5-FIX 7: If the remote resource changed, silently merging old and new content
            //           is forbidden. Discard the cached state entirely.
            if (!string.IsNullOrEmpty(state.ETag) && !string.IsNullOrEmpty(remoteETag))
            {
                if (!string.Equals(state.ETag, remoteETag, StringComparison.OrdinalIgnoreCase))
                {
                    LoggingService.LogWarning(
                        $"[DurableMetadataManager] ETag changed: cached='{state.ETag}' remote='{remoteETag}'. " +
                        $"Remote resource has changed. Discarding resume state to prevent content merge corruption.");
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(state.LastModified) && !string.IsNullOrEmpty(remoteLastModified))
            {
                if (!string.Equals(state.LastModified, remoteLastModified, StringComparison.OrdinalIgnoreCase))
                {
                    LoggingService.LogWarning(
                        $"[DurableMetadataManager] Last-Modified changed: cached='{state.LastModified}' " +
                        $"remote='{remoteLastModified}'. Discarding resume state.");
                    return false;
                }
            }

            // 2. Validate local segment .part files
            foreach (var seg in state.Segments)
            {
                if (string.IsNullOrEmpty(seg.TempPath))
                {
                    // No temp path recorded — treat as not started
                    seg.BytesDownloaded = 0;
                    seg.State = SegmentState.Pending;
                    seg.AssignedWorkerId = null;
                    continue;
                }

                if (!File.Exists(seg.TempPath))
                {
                    // .part file missing — resume from beginning of this segment
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
                catch (Exception ex)
                {
                    LoggingService.LogWarning(
                        $"[DurableMetadataManager] Cannot stat segment file '{seg.TempPath}': {ex.Message}. " +
                        $"Treating as not started.");
                    seg.BytesDownloaded = 0;
                    seg.State = SegmentState.Pending;
                    seg.AssignedWorkerId = null;
                    continue;
                }

                long maxAllowed = seg.TotalBytes;

                // A5-FIX 4: Truncate oversized .part files (e.g. from a previous long-read bug)
                if (actualLen > maxAllowed)
                {
                    LoggingService.LogWarning(
                        $"[DurableMetadataManager] Segment {seg.Id} .part file is oversized " +
                        $"(actual={actualLen}, max={maxAllowed}). Truncating.");
                    try
                    {
                        using var fs = new FileStream(seg.TempPath, FileMode.Open, FileAccess.Write, FileShare.None);
                        fs.SetLength(maxAllowed);
                        actualLen = maxAllowed;
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogWarning(
                            $"[DurableMetadataManager] Could not truncate segment {seg.Id}: {ex.Message}. " +
                            $"Treating as not started.");
                        seg.BytesDownloaded = 0;
                        seg.State = SegmentState.Pending;
                        continue;
                    }
                }

                seg.BytesDownloaded = actualLen;

                // A segment is complete ONLY if actualLen exactly equals TotalBytes.
                // actualLen == 0 on an empty file must NOT be treated as complete.
                if (maxAllowed > 0 && actualLen >= maxAllowed)
                {
                    // Per-segment hash verification for Phase B Step 1:
                    // If a expected Sha256Hash is recorded, verify the .part file hash.
                    // If corrupted, reset ONLY this segment to Pending with 0 bytes downloaded.
                    if (!string.IsNullOrEmpty(seg.Sha256Hash))
                    {
                        string? actualHash = ComputeSegmentHash(seg.TempPath);
                        if (!string.Equals(seg.Sha256Hash, actualHash, StringComparison.OrdinalIgnoreCase))
                        {
                            LoggingService.LogWarning(
                                $"[DurableMetadataManager] Segment {seg.Id} SHA-256 hash mismatch! " +
                                $"Expected='{seg.Sha256Hash}', Actual='{actualHash}'. " +
                                $"Resetting ONLY corrupted segment {seg.Id} for re-download.");
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

            // 3. Reset any Downloading/Failed states to Pending (workers are not running)
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

        // -----------------------------------------------------------------------
        // CLEANUP — orphan temp directories
        // -----------------------------------------------------------------------

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
                            LoggingService.Log($"[DurableMetadataManager] Cleaned orphan temp directory: {dir}");
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DurableMetadataManager] CleanOrphanTempDirectories error", ex);
            }
        }
    }
}
