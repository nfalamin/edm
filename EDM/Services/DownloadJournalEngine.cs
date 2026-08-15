using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;

namespace EDM.Services
{
    public static class Crc32Helper
    {
        private static readonly uint[] Table = new uint[256];

        static Crc32Helper()
        {
            const uint poly = 0xedb88320;
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 8; j > 0; j--)
                {
                    if ((crc & 1) == 1)
                        crc = (crc >> 1) ^ poly;
                    else
                        crc >>= 1;
                }
                Table[i] = crc;
            }
        }

        public static uint Compute(byte[] bytes)
        {
            uint crc = 0xffffffff;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte index = (byte)((crc & 0xff) ^ bytes[i]);
                crc = (crc >> 8) ^ Table[index];
            }
            return ~crc;
        }
    }

    public enum JournalRecordType
    {
        Init = 1,
        SegmentAssigned = 2,
        SegmentProgress = 3,
        SegmentCompleted = 4,
        SegmentCorrupted = 5,
        Finalizing = 6,
        Finalized = 7,
        ServerChanged = 8
    }

    public class JournalRecord
    {
        public long SequenceNumber { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public JournalRecordType RecordType { get; set; }
        public int SegmentId { get; set; }
        public long StartOffset { get; set; }
        public long EndOffset { get; set; }
        public long BytesDownloaded { get; set; }
        public string? ChecksumHex { get; set; }
        public string? ETag { get; set; }
        public string? LastModified { get; set; }
        public long TotalFileSize { get; set; }
        public uint RecordCrc32 { get; set; }
    }

    public enum ResumeValidationResult
    {
        ValidCanResume = 1,
        ServerChangedMustRestart = 2,
        CorruptedSegmentsNeedRepair = 3,
        FullyCompletedAlready = 4,
        InvalidOrMissingState = 5
    }

    /// <summary>
    /// Next-generation crash-consistent download journal and zero-corruption recovery engine.
    /// Provides atomic WAL (Write-Ahead Logging), crash recovery checkpoints, server change detection,
    /// and selective range repair without re-downloading valid chunks.
    /// </summary>
    public class DownloadJournalEngine
    {
        private readonly string _journalPath;
        private readonly string _metaPath;
        private readonly string _partFilePath;
        private readonly object _lock = new();
        private long _currentSeq = 0;

        public string JournalPath => _journalPath;
        public string MetaPath => _metaPath;
        public string PartFilePath => _partFilePath;

        public DownloadJournalEngine(string destinationPath)
        {
            if (string.IsNullOrEmpty(destinationPath)) throw new ArgumentNullException(nameof(destinationPath));
            _partFilePath = destinationPath + ".edm.part";
            _journalPath = destinationPath + ".edm.journal";
            _metaPath = destinationPath + ".edm.meta";
        }

        public void AppendRecord(JournalRecordType type, int segmentId = 0, long start = 0, long end = 0,
            long bytesDownloaded = 0, string? checksum = null, string? etag = null, string? lastMod = null, long totalSize = 0)
        {
            lock (_lock)
            {
                _currentSeq++;
                var record = new JournalRecord
                {
                    SequenceNumber = _currentSeq,
                    TimestampUtc = DateTime.UtcNow,
                    RecordType = type,
                    SegmentId = segmentId,
                    StartOffset = start,
                    EndOffset = end,
                    BytesDownloaded = bytesDownloaded,
                    ChecksumHex = checksum,
                    ETag = etag,
                    LastModified = lastMod,
                    TotalFileSize = totalSize
                };

                // Compute CRC32 for record integrity
                string payload = $"{record.SequenceNumber}|{record.RecordType}|{record.SegmentId}|{record.StartOffset}|{record.EndOffset}|{record.BytesDownloaded}|{record.ETag}|{record.LastModified}|{record.TotalFileSize}";
                byte[] bytes = Encoding.UTF8.GetBytes(payload);
                record.RecordCrc32 = Crc32Helper.Compute(bytes);

                string line = JsonSerializer.Serialize(record) + Environment.NewLine;
                using var fs = new FileStream(_journalPath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
                using var sw = new StreamWriter(fs, Encoding.UTF8);
                sw.Write(line);
                sw.Flush();
                fs.Flush(flushToDisk: true);
            }
        }

        public List<JournalRecord> ReadAllValidRecords()
        {
            lock (_lock)
            {
                var list = new List<JournalRecord>();
                if (!File.Exists(_journalPath)) return list;

                try
                {
                    var lines = File.ReadAllLines(_journalPath, Encoding.UTF8);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        try
                        {
                            var rec = JsonSerializer.Deserialize<JournalRecord>(line);
                            if (rec == null) continue;

                            // Validate CRC32
                            string payload = $"{rec.SequenceNumber}|{rec.RecordType}|{rec.SegmentId}|{rec.StartOffset}|{rec.EndOffset}|{rec.BytesDownloaded}|{rec.ETag}|{rec.LastModified}|{rec.TotalFileSize}";
                            uint calcCrc = Crc32Helper.Compute(Encoding.UTF8.GetBytes(payload));
                            if (calcCrc == rec.RecordCrc32)
                            {
                                list.Add(rec);
                                _currentSeq = Math.Max(_currentSeq, rec.SequenceNumber);
                            }
                        }
                        catch { }
                    }
                }
                catch { }

                return list;
            }
        }

        public ResumeValidationResult ValidateResumeCondition(
            string currentEtag,
            string currentLastModified,
            long currentTotalSize,
            bool serverSupportsRange,
            out List<int> damagedSegmentIds)
        {
            damagedSegmentIds = new List<int>();

            if (!serverSupportsRange)
            {
                return ResumeValidationResult.ServerChangedMustRestart;
            }

            var records = ReadAllValidRecords();
            if (records.Count == 0)
            {
                return ResumeValidationResult.InvalidOrMissingState;
            }

            var initRecord = records.FirstOrDefault(r => r.RecordType == JournalRecordType.Init);
            if (initRecord == null)
            {
                return ResumeValidationResult.InvalidOrMissingState;
            }

            // Check if server file size changed
            if (currentTotalSize > 0 && initRecord.TotalFileSize > 0 && currentTotalSize != initRecord.TotalFileSize)
            {
                LoggingService.LogWarning($"[DownloadJournalEngine] Server file size changed ({initRecord.TotalFileSize} -> {currentTotalSize}). Restarting download.");
                return ResumeValidationResult.ServerChangedMustRestart;
            }

            // Check if ETag changed
            if (!string.IsNullOrEmpty(currentEtag) && !string.IsNullOrEmpty(initRecord.ETag) &&
                !string.Equals(currentEtag.Trim('"'), initRecord.ETag.Trim('"'), StringComparison.OrdinalIgnoreCase))
            {
                LoggingService.LogWarning($"[DownloadJournalEngine] Server ETag changed. State is stale. Restarting download.");
                return ResumeValidationResult.ServerChangedMustRestart;
            }

            // Check if Last-Modified changed
            if (!string.IsNullOrEmpty(currentLastModified) && !string.IsNullOrEmpty(initRecord.LastModified) &&
                !string.Equals(currentLastModified, initRecord.LastModified, StringComparison.OrdinalIgnoreCase))
            {
                LoggingService.LogWarning($"[DownloadJournalEngine] Server Last-Modified changed. Restarting download.");
                return ResumeValidationResult.ServerChangedMustRestart;
            }

            // Check for explicit corrupted segment entries
            var corrupted = records.Where(r => r.RecordType == JournalRecordType.SegmentCorrupted).Select(r => r.SegmentId).Distinct().ToList();
            if (corrupted.Count > 0)
            {
                damagedSegmentIds = corrupted;
                return ResumeValidationResult.CorruptedSegmentsNeedRepair;
            }

            // Check if finalizing was already completed
            if (records.Any(r => r.RecordType == JournalRecordType.Finalized))
            {
                return ResumeValidationResult.FullyCompletedAlready;
            }

            return ResumeValidationResult.ValidCanResume;
        }

        public bool AtomicallyFinalizeFile(string destinationPath)
        {
            lock (_lock)
            {
                try
                {
                    AppendRecord(JournalRecordType.Finalizing);

                    if (File.Exists(_partFilePath))
                    {
                        if (File.Exists(destinationPath))
                        {
                            File.Delete(destinationPath);
                        }
                        File.Move(_partFilePath, destinationPath, overwrite: true);
                    }

                    AppendRecord(JournalRecordType.Finalized);

                    // Clean up journal & metadata files upon successful finalization
                    try
                    {
                        if (File.Exists(_journalPath)) File.Delete(_journalPath);
                        if (File.Exists(_metaPath)) File.Delete(_metaPath);
                        string tmpMeta = _metaPath + ".tmp";
                        if (File.Exists(tmpMeta)) File.Delete(tmpMeta);
                    }
                    catch { }

                    return true;
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[DownloadJournalEngine] Atomic finalization failed", ex);
                    return false;
                }
            }
        }

        public void CleanState()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_journalPath)) File.Delete(_journalPath);
                    if (File.Exists(_metaPath)) File.Delete(_metaPath);
                    if (File.Exists(_partFilePath)) File.Delete(_partFilePath);
                }
                catch { }
            }
        }
    }
}
