using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using EDM.Services.Helpers;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class Phase5FinalCertificationSuite : IDisposable
    {
        private readonly string _testDir;

        public Phase5FinalCertificationSuite()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "EDM_Phase5_Cert_" + Guid.NewGuid().ToString("N"));
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

        #region 1. Single Source of Truth & Transactional Delete Lifecycle Certification

        [Fact]
        public void DownloadService_CancelAndCleanup_PurgesAllTemporaryAndBackupFiles()
        {
            var downloadService = new DownloadService();
            string savePath = Path.Combine(_testDir, "test_file.zip");
            string part0 = savePath + ".part0";
            string part1 = savePath + ".part1";
            string meta = savePath + ".edm.json";
            string metaTmp = meta + ".tmp";
            string metaBak = meta + ".bak";
            string merging = savePath + ".merging";
            string tmpdl = savePath + ".tmpdl";

            File.WriteAllText(part0, "part0");
            File.WriteAllText(part1, "part1");
            File.WriteAllText(meta, "{}");
            File.WriteAllText(metaTmp, "{}");
            File.WriteAllText(metaBak, "{}");
            File.WriteAllText(merging, "merging");
            File.WriteAllText(tmpdl, "tmpdl");

            downloadService.CancelAndCleanup(savePath);

            File.Exists(part0).Should().BeFalse("part0 must be cleaned up");
            File.Exists(part1).Should().BeFalse("part1 must be cleaned up");
            File.Exists(meta).Should().BeFalse("metadata must be cleaned up");
            File.Exists(metaTmp).Should().BeFalse("meta tmp must be cleaned up");
            File.Exists(metaBak).Should().BeFalse("meta bak must be cleaned up");
            File.Exists(merging).Should().BeFalse("merging file must be cleaned up");
            File.Exists(tmpdl).Should().BeFalse("tmpdl file must be cleaned up");
        }

        #endregion

        #region 2. Range Mathematics & Disjoint Interval Coverage Certification

        [Theory]
        [InlineData(10_000_000, 4)]
        [InlineData(100_000_000, 8)]
        [InlineData(1_000_000_000, 16)]
        public void SegmentScheduler_MathematicalRangeInvariants_FullyCoversTotalLengthWithoutGapsOrOverlaps(long fileSize, int initialConnections)
        {
            var scheduler = new SegmentScheduler(fileSize);
            scheduler.InitializeDefault(initialConnections);

            var segments = scheduler.GetSegmentsSnapshot().OrderBy(s => s.Start).ToList();

            // 1. First segment must start at 0
            segments.First().Start.Should().Be(0);

            // 2. Last segment must end at TotalBytes - 1
            segments.Last().End.Should().Be(fileSize - 1);

            // 3. Consecutive segments must be perfectly adjacent without gaps or overlaps
            for (int i = 0; i < segments.Count - 1; i++)
            {
                var curr = segments[i];
                var next = segments[i + 1];

                curr.End.Should().BeLessThan(next.Start, "Ranges must be disjoint");
                (curr.End + 1).Should().Be(next.Start, "Consecutive ranges must not have any gaps");
            }

            // 4. Sum of segment lengths must equal TotalBytes
            long sum = segments.Sum(s => s.TotalBytes);
            sum.Should().Be(fileSize);
        }

        #endregion

        #region 3. Chaos Testing & Recovery Certification

        [Fact]
        public async Task ChaosTest_CorruptedPrimaryMetadata_AutomaticallyRecoversFromBackup()
        {
            var manager = new DurableMetadataManager();
            string metaPath = Path.Combine(_testDir, "chaos_metadata.edm.json");

            var state = new DurableDownloadState
            {
                DownloadId = "chaos_dl_1",
                Url = "https://example.com/chaos.dat",
                DestinationPath = Path.Combine(_testDir, "chaos.dat"),
                TotalBytes = 20_000_000,
                ServerSupportsRanges = true,
                Segments = new List<SegmentRange>
                {
                    new SegmentRange { Id = 0, Start = 0, End = 9_999_999, BytesDownloaded = 10_000_000, State = SegmentState.Completed },
                    new SegmentRange { Id = 1, Start = 10_000_000, End = 19_999_999, BytesDownloaded = 5_000_000, State = SegmentState.Pending }
                }
            };

            // Write version 1 and 2 to establish valid .bak
            await manager.WriteStateAtomicAsync(metaPath, state, CancellationToken.None);
            state.Segments[1].BytesDownloaded = 8_000_000;
            await manager.WriteStateAtomicAsync(metaPath, state, CancellationToken.None);

            // Simulate chaos: zero-byte corruption of primary file
            await File.WriteAllTextAsync(metaPath, "");

            var recovered = await manager.ReadStateAsync(metaPath, CancellationToken.None);
            recovered.Should().NotBeNull("Manager must recover from .bak when primary file is corrupt or empty");
            recovered!.DownloadId.Should().Be("chaos_dl_1");
            recovered.Segments[1].BytesDownloaded.Should().Be(5_000_000);
        }

        [Fact]
        public void ChaosTest_ServerFailureStorm_TripsCircuitBreakerAndSuppressesRetries()
        {
            var breaker = new HostCircuitBreakerManager(failureThreshold: 3, baseOpenDuration: TimeSpan.FromSeconds(5));
            string host = "failing-server.example.com";

            // Inundate with consecutive 503 Service Unavailable errors
            breaker.RecordFailure(host, HttpStatusCode.ServiceUnavailable);
            breaker.RecordFailure(host, HttpStatusCode.ServiceUnavailable);
            breaker.RecordFailure(host, HttpStatusCode.ServiceUnavailable);

            // Circuit must trip to Open
            breaker.GetHostState(host).Should().Be(CircuitState.Open);
            breaker.CanExecute(host, out var delay).Should().BeFalse();
            delay.TotalSeconds.Should().BeGreaterThan(0, "Host circuit breaker must enforce positive backoff delay");
        }

        #endregion

        #region 4. Security & Filename Sanitization Certification

        [Theory]
        [InlineData("../../../etc/passwd", "passwd")]
        [InlineData("..\\..\\Windows\\System32\\cmd.exe", "cmd.exe")]
        [InlineData("CON.txt", "CON_file.txt")]
        [InlineData("PRN", "PRN_file")]
        [InlineData("AUX.dat", "AUX_file.dat")]
        [InlineData("NUL.iso", "NUL_file.iso")]
        [InlineData("COM1.bin", "COM1_file.bin")]
        [InlineData("file*name?.mp4", "file_name_.mp4")]
        public void Security_FilenameSanitizer_NeutralizesPathTraversalAndReservedNames(string input, string expectedSubstring)
        {
            string sanitized = FileNamingHelper.SanitizeFileName(input);
            sanitized.Should().NotContain("../");
            sanitized.Should().NotContain("..\\");
            sanitized.Should().Be(expectedSubstring);
        }

        #endregion

        #region 5. Disk Space Governor Certification

        [Fact]
        public void DiskSpaceGovernor_PreflightCheck_ThrowsOnInsufficentDiskSpace()
        {
            string drive = Path.GetPathRoot(Directory.GetCurrentDirectory()) ?? "C:\\";
            long astronomicalSize = 500L * 1024 * 1024 * 1024 * 1024; // 500 Terabytes

            Action act = () => DiskSpaceGovernor.EnsureAvailableSpaceOrThrow(drive, astronomicalSize);
            act.Should().Throw<InsufficientDiskSpaceException>("Governor must throw InsufficientDiskSpaceException before allocating huge downloads");
        }

        #endregion

        #region 6. End-to-End Multi-Download Global Resource Governor Certification

        [Fact]
        public void GlobalResourceManager_MultiDownloadStress_MaintainsAuthoritativeCapWithoutStarvation()
        {
            var manager = new GlobalResourceManager(globalMaxConnections: 48);

            // 4 simultaneous downloads with diverse priorities
            var d1 = manager.AcquireLease("dl_urgent", "cdnA.com", requestedConnections: 24, priority: DownloadPriority.Urgent);
            var d2 = manager.AcquireLease("dl_high", "cdnB.com", requestedConnections: 16, priority: DownloadPriority.High);
            var d3 = manager.AcquireLease("dl_normal", "cdnC.com", requestedConnections: 12, priority: DownloadPriority.Normal);
            var d4 = manager.AcquireLease("dl_low", "cdnD.com", requestedConnections: 8, priority: DownloadPriority.Low);

            manager.TotalAllocatedConnections.Should().BeLessOrEqualTo(48, "Total sockets must never exceed GlobalMaxConnections");

            var snapshot = manager.GetActiveLeasesSnapshot();
            snapshot.Should().HaveCount(4);

            // All downloads must receive at least 1 connection (zero starvation)
            snapshot.All(l => l.AllocatedConnections >= 1).Should().BeTrue("No active download may be starved of connections");

            // Urgent and High must receive higher allocation than Low
            var urgent = snapshot.First(l => l.DownloadId == "dl_urgent");
            var low = snapshot.First(l => l.DownloadId == "dl_low");
            urgent.AllocatedConnections.Should().BeGreaterThan(low.AllocatedConnections);
        }

        #endregion
    }
}
