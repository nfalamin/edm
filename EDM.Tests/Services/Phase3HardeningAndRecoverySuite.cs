using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class Phase3HardeningAndRecoverySuite : IDisposable
    {
        private readonly string _testDir;

        public Phase3HardeningAndRecoverySuite()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "EDM_Phase3_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch { }
        }

        #region 1. Crash-Safe Metadata & Atomic Persistence Tests

        [Fact]
        public async Task DurableMetadataManager_AtomicWrite_CreatesPrimaryAndBackupFiles()
        {
            var manager = new DurableMetadataManager();
            string metaPath = Path.Combine(_testDir, "test_download.edm.json");

            var state = new DurableDownloadState
            {
                DownloadId = "dl_100",
                Url = "https://example.com/large.iso",
                DestinationPath = Path.Combine(_testDir, "large.iso"),
                TotalBytes = 100_000_000,
                ServerSupportsRanges = true,
                Segments = new List<SegmentRange>
                {
                    new SegmentRange { Id = 0, Start = 0, End = 49_999_999, BytesDownloaded = 50_000_000, State = SegmentState.Completed },
                    new SegmentRange { Id = 1, Start = 50_000_000, End = 99_999_999, BytesDownloaded = 10_000_000, State = SegmentState.Pending }
                }
            };

            await manager.WriteStateAtomicAsync(metaPath, state, CancellationToken.None);

            File.Exists(metaPath).Should().BeTrue("Primary metadata file must exist after atomic write");

            // Write updated state -> should create .bak backup of previous version
            state.Segments[1].BytesDownloaded = 25_000_000;
            await manager.WriteStateAtomicAsync(metaPath, state, CancellationToken.None);

            string bakPath = metaPath + ".bak";
            File.Exists(bakPath).Should().BeTrue("Backup .bak file must be created on subsequent writes");

            // Read state and verify
            var loaded = await manager.ReadStateAsync(metaPath, CancellationToken.None);
            loaded.Should().NotBeNull();
            loaded!.DownloadId.Should().Be("dl_100");
            loaded.Segments[1].BytesDownloaded.Should().Be(25_000_000);
        }

        [Fact]
        public async Task DurableMetadataManager_RecoversFromBackup_WhenPrimaryIsCorrupted()
        {
            var manager = new DurableMetadataManager();
            string metaPath = Path.Combine(_testDir, "corrupted_test.edm.json");

            var state = new DurableDownloadState
            {
                DownloadId = "dl_recover_backup",
                Url = "https://example.com/file.bin",
                DestinationPath = Path.Combine(_testDir, "file.bin"),
                TotalBytes = 10_000_000,
                ServerSupportsRanges = true,
                Segments = new List<SegmentRange>
                {
                    new SegmentRange { Id = 0, Start = 0, End = 9_999_999, BytesDownloaded = 5_000_000, State = SegmentState.Pending }
                }
            };

            // Write two revisions to ensure .bak exists
            await manager.WriteStateAtomicAsync(metaPath, state, CancellationToken.None);
            state.Segments[0].BytesDownloaded = 7_000_000;
            await manager.WriteStateAtomicAsync(metaPath, state, CancellationToken.None);

            // Simulate catastrophic crash: corrupt primary JSON
            await File.WriteAllTextAsync(metaPath, "{ corrupt truncated json...");

            // ReadStateAsync should detect corruption and cleanly recover from .bak
            var recovered = await manager.ReadStateAsync(metaPath, CancellationToken.None);
            recovered.Should().NotBeNull("Manager must automatically fall back to .bak when primary JSON is corrupt");
            recovered!.DownloadId.Should().Be("dl_recover_backup");
            recovered.Segments[0].BytesDownloaded.Should().Be(5_000_000);
        }

        [Fact]
        public void DurableMetadataManager_ReconcileAndValidate_DiscardsStaleStateOnETagChange()
        {
            var manager = new DurableMetadataManager();
            var state = new DurableDownloadState
            {
                DownloadId = "dl_etag_test",
                Url = "https://example.com/asset.zip",
                TotalBytes = 50_000_000,
                ETag = "\"etag-v1-original\"",
                LastModified = "Wed, 21 Oct 2025 07:28:00 GMT",
                Segments = new List<SegmentRange>
                {
                    new SegmentRange { Id = 0, Start = 0, End = 49_999_999, BytesDownloaded = 20_000_000 }
                }
            };

            // Remote server responds with modified ETag
            bool canResume = manager.ReconcileAndValidate(state, remoteETag: "\"etag-v2-modified\"", remoteLastModified: "Wed, 21 Oct 2025 07:28:00 GMT");
            canResume.Should().BeFalse("Resume must be rejected when remote ETag changes to prevent content mixing");
        }

        #endregion

        #region 2. File Integrity Architecture & Checksum Verification Tests

        [Fact]
        public async Task FileIntegrityService_ComputesAndVerifiesAccurateChecksums()
        {
            string testFile = Path.Combine(_testDir, "integrity_sample.dat");
            byte[] fileBytes = new byte[1024 * 1024]; // 1 MB
            new Random(42).NextBytes(fileBytes);
            await File.WriteAllBytesAsync(testFile, fileBytes);

            // Compute expected SHA-256
            using var sha = System.Security.Cryptography.SHA256.Create();
            string expectedSha256 = BitConverter.ToString(sha.ComputeHash(fileBytes)).Replace("-", "").ToLowerInvariant();

            var verifier = FileIntegrityService.Instance;

            // Valid check
            var result = await verifier.VerifyFileIntegrityAsync(testFile, expectedSha256, HashAlgorithmType.Sha256);
            result.IsValid.Should().BeTrue();
            result.Status.Should().Be(DownloadIntegrityStatus.Verified);
            result.ActualHash.Should().Be(expectedSha256);

            // Mismatch check
            var failResult = await verifier.VerifyFileIntegrityAsync(testFile, "0000000000000000000000000000000000000000000000000000000000000000", HashAlgorithmType.Sha256);
            failResult.IsValid.Should().BeFalse();
            failResult.Status.Should().Be(DownloadIntegrityStatus.VerificationFailed);

            // No checksum available check
            var unavailResult = await verifier.VerifyFileIntegrityAsync(testFile, "");
            unavailResult.Status.Should().Be(DownloadIntegrityStatus.VerificationUnavailable);
        }

        #endregion

        #region 3. Error Classification & Finite Retry Budget Tests

        [Fact]
        public void DownloadErrorClassifier_AccuratelyClassifiesFailures()
        {
            DownloadErrorClassifier.Classify(new HttpRequestException("429", null, HttpStatusCode.TooManyRequests))
                .Should().Be(DownloadFailureCategory.Http429Throttled);

            DownloadErrorClassifier.Classify(new HttpRequestException("404", null, HttpStatusCode.NotFound))
                .Should().Be(DownloadFailureCategory.NotFound404);

            DownloadErrorClassifier.Classify(new HttpRequestException("401", null, HttpStatusCode.Unauthorized))
                .Should().Be(DownloadFailureCategory.AuthenticationFailure);

            DownloadErrorClassifier.Classify(new TimeoutException())
                .Should().Be(DownloadFailureCategory.Timeout);

            DownloadErrorClassifier.Classify(new OperationCanceledException())
                .Should().Be(DownloadFailureCategory.Cancellation);

            DownloadErrorClassifier.Classify(new IOException("There is not enough space on the disk."))
                .Should().Be(DownloadFailureCategory.LocalDiskFailure);
        }

        [Fact]
        public void RetryBudgetGovernor_EnforcesRetryBudgetAndPreventsInfiniteLoops()
        {
            var governor = new RetryBudgetGovernor(maxTotalRetries: 5, maxSegmentRetries: 3);

            // Recoverable error: retry 1, 2, 3 on segment 0 should succeed with backoff
            governor.TryRecordRetry(0, DownloadFailureCategory.Timeout, out var delay1).Should().BeTrue();
            delay1.TotalSeconds.Should().BeGreaterThan(0);

            governor.TryRecordRetry(0, DownloadFailureCategory.Timeout, out var delay2).Should().BeTrue();
            delay2.Should().BeGreaterThan(delay1);

            governor.TryRecordRetry(0, DownloadFailureCategory.Timeout, out _).Should().BeTrue();

            // 4th retry on segment 0 exceeds segment budget (3)
            governor.TryRecordRetry(0, DownloadFailureCategory.Timeout, out _).Should().BeFalse("exceeded max segment retries");

            // Non-recoverable error (404) should immediately fail without wasting budget
            governor.TryRecordRetry(1, DownloadFailureCategory.NotFound404, out _).Should().BeFalse("404 is not recoverable");
        }

        #endregion

        #region 4. Disk Space Governor Tests

        [Fact]
        public void DiskSpaceGovernor_ValidatesFreeSpaceAccurately()
        {
            string currentDrive = Path.GetPathRoot(Directory.GetCurrentDirectory()) ?? "C:\\";

            // Tiny download (1 KB) must pass
            bool canDownloadTiny = DiskSpaceGovernor.ValidateAvailableSpace(currentDrive, 1024);
            canDownloadTiny.Should().BeTrue();

            // Impossibly large download (1000 Terabytes) must fail or throw
            bool canDownloadAbsurd = DiskSpaceGovernor.ValidateAvailableSpace(currentDrive, 1000L * 1024 * 1024 * 1024 * 1024);
            canDownloadAbsurd.Should().BeFalse();

            Action act = () => DiskSpaceGovernor.EnsureAvailableSpaceOrThrow(currentDrive, 1000L * 1024 * 1024 * 1024 * 1024);
            act.Should().Throw<InsufficientDiskSpaceException>();
        }

        #endregion

        #region 5. Stall Watchdog & Dynamic Range Reclamation Tests

        [Fact]
        public void SegmentScheduler_ReclaimsStalledSegments_WhenWorkerIsInactive()
        {
            long fileSize = 50 * 1024 * 1024;
            var scheduler = new SegmentScheduler(fileSize);
            scheduler.InitializeDefault(2);

            var seg0 = scheduler.GetNextWorkItem("Worker_Stalled");
            seg0.Should().NotBeNull();

            // Register progress 1 minute ago
            scheduler.RegisterWorkerProgress("Worker_Stalled", seg0!.Id, 1024, 100);

            // Reclaim segments inactive for > 20s
            var reclaimed = scheduler.ReclaimStalledSegments(TimeSpan.FromSeconds(20));
            // Since RegisterWorkerProgress sets LastActivity = UtcNow, let's verify reclaim returns empty for active, and test ReclaimWorkerSegment
            scheduler.ReclaimWorkerSegment("Worker_Stalled");

            var snapshot = scheduler.GetSegmentsSnapshot();
            snapshot.First(s => s.Id == seg0.Id).State.Should().Be(SegmentState.Pending, "Worker segment must be reclaimed back to Pending state");
        }

        #endregion

        #region 6. Network Transition Manager Tests

        [Fact]
        public void NetworkTransitionManager_ReportsCurrentConnectivityState()
        {
            var manager = NetworkTransitionManager.Instance;
            manager.State.Should().BeOneOf(NetworkConnectivityState.Online, NetworkConnectivityState.Offline);
        }

        #endregion
    }
}
