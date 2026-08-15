using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class Stage4CrashConsistencyTortureTests : TestBase
    {
        private static byte[] GeneratePredictablePayload(int size)
        {
            var data = new byte[size];
            for (int i = 0; i < size; i++)
            {
                data[i] = (byte)((i * 37 + 13) % 256);
            }
            return data;
        }

        private static string ComputeSha256Hex(byte[] data)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(data);
            return Convert.ToHexString(hash);
        }

        [Fact]
        public void PersistentSegmentJournal_WritesAndReplaysCRCValidatedRecords()
        {
            string tempDest = Path.Combine(Path.GetTempPath(), $"edm_journal_test_{Guid.NewGuid():N}.bin");
            var engine = new DownloadJournalEngine(tempDest);

            try
            {
                engine.AppendRecord(JournalRecordType.Init, totalSize: 10 * 1024 * 1024, etag: "\"v1.0\"", lastMod: "Wed, 21 Oct 2025 07:28:00 GMT");
                engine.AppendRecord(JournalRecordType.SegmentAssigned, segmentId: 0, start: 0, end: 2 * 1024 * 1024 - 1);
                engine.AppendRecord(JournalRecordType.SegmentProgress, segmentId: 0, bytesDownloaded: 1024 * 1024);
                engine.AppendRecord(JournalRecordType.SegmentCompleted, segmentId: 0, bytesDownloaded: 2 * 1024 * 1024);

                var records = engine.ReadAllValidRecords();
                records.Should().HaveCount(4);
                records[0].RecordType.Should().Be(JournalRecordType.Init);
                records[0].ETag.Should().Be("\"v1.0\"");
                records[3].RecordType.Should().Be(JournalRecordType.SegmentCompleted);
            }
            finally
            {
                engine.CleanState();
            }
        }

        [Fact]
        public void ServerChanges_ETagOrFileSize_DetectsStaleStateAndRequiresRestart()
        {
            string tempDest = Path.Combine(Path.GetTempPath(), $"edm_stale_test_{Guid.NewGuid():N}.bin");
            var engine = new DownloadJournalEngine(tempDest);

            try
            {
                engine.AppendRecord(JournalRecordType.Init, totalSize: 50 * 1024 * 1024, etag: "\"original-etag\"", lastMod: "Wed, 21 Oct 2025 07:28:00 GMT");

                // Test 1: Matching metadata -> Can resume
                var result1 = engine.ValidateResumeCondition("\"original-etag\"", "Wed, 21 Oct 2025 07:28:00 GMT", 50 * 1024 * 1024, true, out _);
                result1.Should().Be(ResumeValidationResult.ValidCanResume);

                // Test 2: Server changed ETag -> Must restart
                var result2 = engine.ValidateResumeCondition("\"new-different-etag\"", "Wed, 21 Oct 2025 07:28:00 GMT", 50 * 1024 * 1024, true, out _);
                result2.Should().Be(ResumeValidationResult.ServerChangedMustRestart);

                // Test 3: Server changed file size -> Must restart
                var result3 = engine.ValidateResumeCondition("\"original-etag\"", "Wed, 21 Oct 2025 07:28:00 GMT", 60 * 1024 * 1024, true, out _);
                result3.Should().Be(ResumeValidationResult.ServerChangedMustRestart);

                // Test 4: Server dropped Range support -> Must restart
                var result4 = engine.ValidateResumeCondition("\"original-etag\"", "Wed, 21 Oct 2025 07:28:00 GMT", 50 * 1024 * 1024, false, out _);
                result4.Should().Be(ResumeValidationResult.ServerChangedMustRestart);
            }
            finally
            {
                engine.CleanState();
            }
        }

        [Fact]
        public void SelectiveRangeRepair_PreservesValidSegmentsAndRepairsDamagedOnly()
        {
            string tempDest = Path.Combine(Path.GetTempPath(), $"edm_repair_test_{Guid.NewGuid():N}.bin");
            var engine = new DownloadJournalEngine(tempDest);

            try
            {
                engine.AppendRecord(JournalRecordType.Init, totalSize: 20 * 1024 * 1024, etag: "\"stable-etag\"");
                engine.AppendRecord(JournalRecordType.SegmentCompleted, segmentId: 0, bytesDownloaded: 5 * 1024 * 1024);
                engine.AppendRecord(JournalRecordType.SegmentCompleted, segmentId: 1, bytesDownloaded: 5 * 1024 * 1024);
                engine.AppendRecord(JournalRecordType.SegmentCorrupted, segmentId: 2, bytesDownloaded: 0); // Seg 2 corrupted
                engine.AppendRecord(JournalRecordType.SegmentCompleted, segmentId: 3, bytesDownloaded: 5 * 1024 * 1024);

                var result = engine.ValidateResumeCondition("\"stable-etag\"", null!, 20 * 1024 * 1024, true, out var damaged);
                result.Should().Be(ResumeValidationResult.CorruptedSegmentsNeedRepair);
                damaged.Should().ContainSingle().Which.Should().Be(2);
            }
            finally
            {
                engine.CleanState();
            }
        }

        [Fact]
        public void AtomicFinalization_NoPartialFileExposure_LeavesCleanFilesystem()
        {
            string tempDest = Path.Combine(Path.GetTempPath(), $"edm_atomic_test_{Guid.NewGuid():N}.bin");
            var engine = new DownloadJournalEngine(tempDest);

            try
            {
                byte[] expectedPayload = GeneratePredictablePayload(4 * 1024 * 1024); // 4MB
                string expectedSha256 = ComputeSha256Hex(expectedPayload);

                // Write temporary .part file
                File.WriteAllBytes(engine.PartFilePath, expectedPayload);
                engine.AppendRecord(JournalRecordType.Init, totalSize: expectedPayload.Length);

                // Act - Atomic finalize
                bool success = engine.AtomicallyFinalizeFile(tempDest);
                success.Should().BeTrue();

                // Assert
                File.Exists(tempDest).Should().BeTrue("Final file must exist at destination");
                File.Exists(engine.PartFilePath).Should().BeFalse("Part file must be cleanly moved/removed");
                File.Exists(engine.JournalPath).Should().BeFalse("Journal must be cleaned up on completion");

                byte[] actualPayload = File.ReadAllBytes(tempDest);
                ComputeSha256Hex(actualPayload).Should().Be(expectedSha256, "Final file must be byte-for-byte identical to source payload");
            }
            finally
            {
                if (File.Exists(tempDest)) File.Delete(tempDest);
                engine.CleanState();
            }
        }

        [Fact]
        public void TortureHarness_RandomCrashPointSimulations_1000Cycles_GuaranteesIntegrity()
        {
            var rand = new Random(42);
            int cycles = 1000;
            int payloadSize = 256 * 1024; // 256 KB fixture
            byte[] masterPayload = GeneratePredictablePayload(payloadSize);
            string masterHash = ComputeSha256Hex(masterPayload);

            for (int i = 0; i < cycles; i++)
            {
                string tempDest = Path.Combine(Path.GetTempPath(), $"edm_torture_{i}_{Guid.NewGuid():N}.bin");
                var engine = new DownloadJournalEngine(tempDest);

                try
                {
                    engine.AppendRecord(JournalRecordType.Init, totalSize: payloadSize, etag: "\"etag-v1\"");

                    // Simulate 4 segments (64KB each)
                    int segmentCount = 4;
                    int segmentSize = payloadSize / segmentCount;
                    byte[] inProgressFile = new byte[payloadSize];

                    int crashPoint = rand.Next(0, 5); // 0 to 4 segments completed before simulated crash

                    for (int s = 0; s < crashPoint; s++)
                    {
                        Buffer.BlockCopy(masterPayload, s * segmentSize, inProgressFile, s * segmentSize, segmentSize);
                        engine.AppendRecord(JournalRecordType.SegmentCompleted, segmentId: s, bytesDownloaded: segmentSize);
                    }

                    // Flush simulated partial file
                    File.WriteAllBytes(engine.PartFilePath, inProgressFile);

                    // SIMULATE PROCESS TERMINATION & RECOVERY
                    var resumeResult = engine.ValidateResumeCondition("\"etag-v1\"", null!, payloadSize, true, out _);
                    resumeResult.Should().Be(ResumeValidationResult.ValidCanResume);

                    // Complete remaining segments after recovery
                    for (int s = crashPoint; s < segmentCount; s++)
                    {
                        Buffer.BlockCopy(masterPayload, s * segmentSize, inProgressFile, s * segmentSize, segmentSize);
                        engine.AppendRecord(JournalRecordType.SegmentCompleted, segmentId: s, bytesDownloaded: segmentSize);
                    }

                    File.WriteAllBytes(engine.PartFilePath, inProgressFile);
                    bool finalOk = engine.AtomicallyFinalizeFile(tempDest);
                    finalOk.Should().BeTrue();

                    byte[] finalizedData = File.ReadAllBytes(tempDest);
                    ComputeSha256Hex(finalizedData).Should().Be(masterHash, $"Iteration {i} must produce identical cryptographic hash");
                }
                finally
                {
                    if (File.Exists(tempDest)) File.Delete(tempDest);
                    engine.CleanState();
                }
            }
        }
    }
}
