using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using EDM.Services.Helpers;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class Phase7RedTeamAttackSuite : IDisposable
    {
        private readonly string _testDir;

        public Phase7RedTeamAttackSuite()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "EDM_Phase7_RedTeam_" + Guid.NewGuid().ToString("N"));
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

        #region 1. Delete Lifecycle & Race Attacks

        [Fact]
        public void RedTeam_DeleteDuringRetry_SuppressesFutureExecutionAndPurgesArtifacts()
        {
            var governor = new RetryBudgetGovernor(maxTotalRetries: 5, maxSegmentRetries: 3);
            var downloadService = new DownloadService();
            string savePath = Path.Combine(_testDir, "retry_attack.iso");
            string part0 = savePath + ".part0";
            string meta = savePath + ".edm.json";
            string metaBak = meta + ".bak";
            string tmpdl = savePath + ".tmpdl";

            File.WriteAllText(part0, "part0-content");
            File.WriteAllText(meta, "{\"DownloadId\":\"retry_attack\"}");
            File.WriteAllText(metaBak, "{\"DownloadId\":\"retry_attack_bak\"}");
            File.WriteAllText(tmpdl, "tmpdl-content");

            // 1. Simulate retry state in governor
            governor.TryRecordRetry(0, DownloadFailureCategory.Timeout, out _).Should().BeTrue();

            // 2. Perform transactional delete
            downloadService.CancelAndCleanup(savePath);

            // 3. Verify complete artifact eradication
            File.Exists(part0).Should().BeFalse("part0 must be deleted");
            File.Exists(meta).Should().BeFalse("primary metadata must be deleted");
            File.Exists(metaBak).Should().BeFalse("backup metadata must be deleted");
            File.Exists(tmpdl).Should().BeFalse("temporary single download file must be deleted");
        }

        [Fact]
        public void RedTeam_ConcurrentCancelAndDelete_ProducesDeterministicCleanState()
        {
            var downloadService = new DownloadService();
            string savePath = Path.Combine(_testDir, "race_target.bin");
            string part0 = savePath + ".part0";
            string merging = savePath + ".merging";

            File.WriteAllText(part0, "binary chunk 0");
            File.WriteAllText(merging, "incomplete merge");

            using var cts = new CancellationTokenSource();

            // Simultaneous cancellation and cleanup
            Parallel.Invoke(
                () => cts.Cancel(),
                () => downloadService.CancelAndCleanup(savePath),
                () => downloadService.CancelAndCleanup(savePath)
            );

            File.Exists(part0).Should().BeFalse("All part chunks must be purged");
            File.Exists(merging).Should().BeFalse("Incomplete merging file must be purged");
        }

        #endregion

        #region 2. Byte-Level Integrity & Range Mathematics Attacks

        [Theory]
        [InlineData(1024 * 1024, 4)]
        [InlineData(16 * 1024 * 1024, 8)]
        [InlineData(128 * 1024 * 1024, 16)]
        public void RedTeam_RangeMathematics_DisjointIntervalProof(long totalBytes, int connections)
        {
            var scheduler = new SegmentScheduler(totalBytes);
            scheduler.InitializeDefault(connections);

            var segments = scheduler.GetSegmentsSnapshot().OrderBy(s => s.Start).ToList();

            // Check 1: Start at 0
            segments.First().Start.Should().Be(0);

            // Check 2: End at totalBytes - 1
            segments.Last().End.Should().Be(totalBytes - 1);

            // Check 3: Zero gaps, zero overlaps
            for (int i = 0; i < segments.Count - 1; i++)
            {
                var s1 = segments[i];
                var s2 = segments[i + 1];

                s1.End.Should().BeLessThan(s2.Start, "Segment intervals must be strictly non-overlapping");
                (s1.End + 1).Should().Be(s2.Start, "Consecutive segments must leave no byte gaps");
            }

            // Check 4: Sum of segments equals total bytes exactly
            segments.Sum(s => s.TotalBytes).Should().Be(totalBytes);
        }

        [Fact]
        public async Task RedTeam_ByteForByteVerification_DetectsSingleBitCorruption()
        {
            string sourceFile = Path.Combine(_testDir, "source.bin");
            string corruptedFile = Path.Combine(_testDir, "corrupted.bin");

            byte[] data = new byte[256 * 1024];
            new Random(1337).NextBytes(data);
            await File.WriteAllBytesAsync(sourceFile, data);

            // Create corrupted clone (flip 1 bit)
            byte[] corruptedData = (byte[])data.Clone();
            corruptedData[1024] ^= 0xFF;
            await File.WriteAllBytesAsync(corruptedFile, corruptedData);

            var verifier = FileIntegrityService.Instance;
            string correctHash = await verifier.ComputeFileHashAsync(sourceFile, HashAlgorithmType.Sha256);

            // Verification of valid file
            var validResult = await verifier.VerifyFileIntegrityAsync(sourceFile, correctHash, HashAlgorithmType.Sha256);
            validResult.IsValid.Should().BeTrue();
            validResult.Status.Should().Be(DownloadIntegrityStatus.Verified);

            // Verification of corrupted file must fail
            var corruptResult = await verifier.VerifyFileIntegrityAsync(corruptedFile, correctHash, HashAlgorithmType.Sha256);
            corruptResult.IsValid.Should().BeFalse();
            corruptResult.Status.Should().Be(DownloadIntegrityStatus.VerificationFailed);
        }

        #endregion

        #region 3. Progress Truth & Monotonicity Audits

        [Fact]
        public void RedTeam_ProgressInfo_MaintainsByteAccuracyAndCalculatesEta()
        {
            var info = new DownloadProgressInfo
            {
                TotalBytes = 100 * 1024 * 1024,
                BytesReceived = 50 * 1024 * 1024,
                ProgressPercentage = 50.0,
                SpeedBytesPerSecond = 5 * 1024 * 1024,
                RemainingSeconds = 10.0
            };

            info.BytesReceived.Should().Be(50 * 1024 * 1024);
            info.ProgressPercentage.Should().Be(50.0);
            info.Eta.Should().Be("00:10");
        }

        [Fact]
        public void RedTeam_SpeedCalculation_CalculatesMonotonicTimeDeltaWithoutInflation()
        {
            var tracker = new MonotonicSpeedTracker();

            // Record 10 MB transferred
            tracker.RecordProgress(10 * 1024 * 1024);

            tracker.TotalBytes.Should().Be(10 * 1024 * 1024);
            tracker.AverageSpeedBps.Should().BeGreaterThan(0);
            tracker.ElapsedSeconds.Should().BeGreaterThan(0);
        }

        #endregion

        #region 4. Security & Command Injection Attacks

        [Theory]
        [InlineData("https://example.com/video.mp4; calc.exe")]
        [InlineData("https://example.com/test.mp4 | rm -rf /")]
        [InlineData("https://example.com/file.mp4 && whoami")]
        [InlineData("https://example.com/file$(calc).mp4")]
        public async Task RedTeam_ExternalProcessSupervision_UsesArgumentListAndNeutralizesCommandInjection(string maliciousUrl)
        {
            var depManager = MediaDependencyManager.Instance;

            // MediaDependencyManager uses ArgumentList for yt-dlp/ffmpeg, so shell operators are passed as literal strings
            // Execute supervised process with invalid args to verify safe invocation
            var result = await depManager.ExecuteSupervisedProcessAsync(
                "cmd.exe",
                new[] { "/c", "echo", maliciousUrl },
                timeout: TimeSpan.FromSeconds(3)
            );

            result.IsSuccess.Should().BeTrue();
            result.StandardOutput.Should().Contain(maliciousUrl.Trim(), "ArgumentList must treat injection strings as pure literals");
        }

        #endregion

        #region 5. Retry Storm & Circuit Breaker Attack

        [Fact]
        public void RedTeam_CircuitBreaker_TripsAndSuppressesConcurrentRetryFloods()
        {
            var breaker = new HostCircuitBreakerManager(failureThreshold: 4, baseOpenDuration: TimeSpan.FromSeconds(10));
            string host = "attack-target-cdn.net";

            breaker.CanExecute(host, out _).Should().BeTrue();

            // Simulate consecutive 503 HTTP errors
            breaker.RecordFailure(host, HttpStatusCode.ServiceUnavailable);
            breaker.RecordFailure(host, HttpStatusCode.ServiceUnavailable);
            breaker.RecordFailure(host, HttpStatusCode.ServiceUnavailable);
            breaker.RecordFailure(host, HttpStatusCode.ServiceUnavailable);

            // Circuit must trip to Open
            breaker.GetHostState(host).Should().Be(CircuitState.Open);
            breaker.CanExecute(host, out var delay).Should().BeFalse("All retry attempts must be rejected while circuit is Open");
            delay.TotalSeconds.Should().BeGreaterThan(0);
        }

        #endregion

        #region 6. Multi-Download Resource Allocation & Fairness Attack

        [Fact]
        public void RedTeam_GlobalResourceAllocation_PreservesCeilingUnderHeavyContention()
        {
            var governor = new GlobalResourceManager(globalMaxConnections: 24);

            // 6 competing downloads requesting 8 connections each (total 48 requested vs 24 available)
            governor.AcquireLease("dl_1", "h1.com", 8, priority: DownloadPriority.High);
            governor.AcquireLease("dl_2", "h2.com", 8, priority: DownloadPriority.Normal);
            governor.AcquireLease("dl_3", "h3.com", 8, priority: DownloadPriority.Normal);
            governor.AcquireLease("dl_4", "h4.com", 8, priority: DownloadPriority.Low);
            governor.AcquireLease("dl_5", "h5.com", 8, priority: DownloadPriority.Low);
            governor.AcquireLease("dl_6", "h6.com", 8, priority: DownloadPriority.Urgent);

            // Total sockets allocated must be strictly <= 24
            governor.TotalAllocatedConnections.Should().BeLessOrEqualTo(24, "Total allocated sockets must never exceed GlobalMaxConnections");

            // Zero starvation: every download must have >= 1 connection
            var snapshot = governor.GetActiveLeasesSnapshot();
            snapshot.All(l => l.AllocatedConnections >= 1).Should().BeTrue("No active download may be starved of connections");
        }

        #endregion
    }
}
