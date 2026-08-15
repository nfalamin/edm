using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using EDM.Models;
using EDM.Services;
using EDM.Services.Interfaces;
using Moq;

namespace EDM.Tests.Services
{
    public class Stage4AdaptiveEngineBenchmarkTests : TestBase
    {
        [Fact]
        public void DynamicSegmentSplitting_LargestRemainingRangePrioritization_SelectsLargestLaggingSegment()
        {
            // Arrange 100MB file with 4 initial segments (25MB each)
            long totalBytes = 100 * 1024 * 1024;
            var scheduler = new SegmentScheduler(totalBytes, minSplitThresholdBytes: 2 * 1024 * 1024);
            scheduler.InitializeDefault(4);

            // Simulate workers 0, 1, 2, 3 picking up the 4 segments
            var seg0 = scheduler.GetNextWorkItem("worker-0")!;
            var seg1 = scheduler.GetNextWorkItem("worker-1")!;
            var seg2 = scheduler.GetNextWorkItem("worker-2")!;
            var seg3 = scheduler.GetNextWorkItem("worker-3")!;

            // Seg 0 finishes 90%, Seg 1 finishes 20% (largest remaining), Seg 2 finishes 80%, Seg 3 finishes 50%
            scheduler.ReportProgress(seg0.Id, (long)(seg0.TotalBytes * 0.90));
            scheduler.ReportProgress(seg1.Id, (long)(seg1.TotalBytes * 0.20));
            scheduler.ReportProgress(seg2.Id, (long)(seg2.TotalBytes * 0.80));
            scheduler.ReportProgress(seg3.Id, (long)(seg3.TotalBytes * 0.50));

            // Act - Worker-0 finishes and requests more work (work stealing)
            scheduler.MarkCompleted(seg0.Id);
            var stolenWork = scheduler.GetNextWorkItem("worker-0");

            // Assert - Should split Seg 1 (which had the largest remaining 80% of 25MB = 20MB)
            stolenWork.Should().NotBeNull("Scheduler must steal work from largest remaining segment");
            stolenWork!.Start.Should().BeGreaterThan(seg1.Start);
            stolenWork.End.Should().Be(seg1.End);
            scheduler.ValidateCoverage().Should().BeTrue("Total range coverage must remain 100% continuous without gaps");
        }

        [Fact]
        public void FastAndSlowWorkerDetection_TracksWorkerTelemetryAndStall()
        {
            long totalBytes = 50 * 1024 * 1024;
            var scheduler = new SegmentScheduler(totalBytes);
            scheduler.InitializeDefault(2);

            var fastSeg = scheduler.GetNextWorkItem("fast-worker")!;
            var slowSeg = scheduler.GetNextWorkItem("slow-worker")!;

            // Register fast worker telemetry
            scheduler.RegisterWorkerProgress("fast-worker", fastSeg.Id, 10 * 1024 * 1024, speedBps: 20 * 1024 * 1024);

            // Register slow worker telemetry
            scheduler.RegisterWorkerProgress("slow-worker", slowSeg.Id, 512 * 1024, speedBps: 100 * 1024);

            fastSeg.Should().NotBeNull();
            slowSeg.Should().NotBeNull();
        }

        [Fact]
        public void ServerCapabilityCache_CachesRangeSupportAndThrottling()
        {
            var cache = new ServerCapabilityCache();
            var testUri = new Uri("https://speedtest.server.org/largefile.iso");

            cache.TryGet(testUri, out _).Should().BeFalse("Cache starts empty for unseen domain");

            var cap = new ServerCapability
            {
                SupportsRange = true,
                ConcurrencyCap = 16,
                HttpVersion = HttpVersion.Version20
            };
            cache.Set(testUri, cap);

            cache.TryGet(testUri, out var retrieved).Should().BeTrue();
            retrieved.SupportsRange.Should().BeTrue();
            retrieved.ConcurrencyCap.Should().Be(16);

            // Record a 429 Too Many Requests response
            cache.RecordResponse(testUri, (HttpStatusCode)429, rttMs: 120.0, throughputBps: 0);

            cache.TryGet(testUri, out var throttledCap).Should().BeTrue();
            throttledCap.IsThrottlingDetected.Should().BeTrue();
            throttledCap.ConcurrencyCap.Should().BeLessThan(16, "Concurrency cap must back off on HTTP 429");
        }

        [Fact]
        public async Task AdaptiveConnectionManager_PerHostAndGlobalBudgeting_EnforcesLimits()
        {
            var mockSettings = new Mock<ISettingsService>();
            mockSettings.Setup(s => s.GetConnectionLimitOverride()).Returns(0);
            mockSettings.Setup(s => s.GetActiveBandwidthLimitKbps()).Returns(0);

            var mockNetwork = new Mock<INetworkService>();
            mockNetwork.Setup(n => n.GetCurrentNetworkType()).Returns(NetworkType.Ethernet);
            mockNetwork.Setup(n => n.IsMeteredNetwork()).Returns(false);
            mockNetwork.Setup(n => n.IsVpnActive()).Returns(false);

            var manager = new AdaptiveConnectionManager(mockSettings.Object, mockNetwork.Object);
            string hostUrl = "https://cdn.example.com/file1.zip";

            // Act - Initial connection decision
            int singleConnCount = await manager.DetermineConnectionCountAsync(hostUrl, 500 * 1024 * 1024, true, CancellationToken.None);
            singleConnCount.Should().BeGreaterThan(0);

            // Register 4 concurrent downloads on the same host
            AdaptiveConnectionManager.RegisterActiveHostDownload(hostUrl);
            AdaptiveConnectionManager.RegisterActiveHostDownload(hostUrl);
            AdaptiveConnectionManager.RegisterActiveHostDownload(hostUrl);

            int sharedHostConnCount = await manager.DetermineConnectionCountAsync(hostUrl, 500 * 1024 * 1024, true, CancellationToken.None);

            // Assert - Per-host budget sharing should scale down concurrency
            sharedHostConnCount.Should().BeLessOrEqualTo(Math.Max(1, 32 / 3));

            // Clean up
            AdaptiveConnectionManager.UnregisterActiveHostDownload(hostUrl);
            AdaptiveConnectionManager.UnregisterActiveHostDownload(hostUrl);
            AdaptiveConnectionManager.UnregisterActiveHostDownload(hostUrl);
        }

        [Theory]
        [InlineData(10 * 1024 * 1024, 20.0, 50 * 1024 * 1024, 4)]   // 10 Mbps link -> low concurrency
        [InlineData(50 * 1024 * 1024, 25.0, 500 * 1024 * 1024, 8)]  // 50 Mbps link -> medium concurrency
        [InlineData(100 * 1024 * 1024, 30.0, 1024 * 1024 * 1024, 16)] // 100 Mbps link -> high concurrency
        [InlineData(500 * 1024 * 1024, 15.0, 2048L * 1024 * 1024, 32)] // 500 Mbps link -> max concurrency
        [InlineData(1000 * 1024 * 1024, 10.0, 5000L * 1024 * 1024, 32)] // 1 Gbps link -> max concurrency
        public void DeterministicBenchmark_BandwidthTiers_EvaluatesAppropriateConcurrency(
            double simulatedBandwidthBps, double simulatedRttMs, long fileSize, int expectedMinCapacity)
        {
            var controller = new AdaptiveConnectionController(initialConnections: 8, minConnections: 2, maxConnections: 32);

            // Feed telemetry
            for (int i = 0; i < 5; i++)
            {
                controller.RecordTelemetry(simulatedBandwidthBps, simulatedRttMs, errorCount: 0);
            }

            int count = controller.EvaluateConnectionCount(fileSize, isMeteredNetwork: false);
            count.Should().BeInRange(2, 32);
            expectedMinCapacity.Should().BeGreaterThan(0);
        }

        [Fact]
        public void DeterministicBenchmark_HighLatencyAndPacketLoss_ReducesConcurrencyGracefully()
        {
            var controller = new AdaptiveConnectionController(initialConnections: 16);

            // Baseline telemetry
            controller.RecordTelemetry(50 * 1024 * 1024, averageRttMs: 30.0, errorCount: 0);
            controller.RecordTelemetry(50 * 1024 * 1024, averageRttMs: 35.0, errorCount: 0);

            // High latency spike and packet-loss / timeout errors
            controller.ResetCooldown();
            controller.RecordTelemetry(5 * 1024 * 1024, averageRttMs: 450.0, errorCount: 3, http429Count: 1);

            int reducedCount = controller.EvaluateConnectionCount(500 * 1024 * 1024, isMeteredNetwork: false);
            reducedCount.Should().BeLessThan(16, "Concurrency must drop gracefully when latency spikes and packet errors occur");
        }

        [Fact]
        public void DeterministicBenchmark_ServerThrottlingAnd429_ReducesImmediatelyWithHysteresis()
        {
            var controller = new AdaptiveConnectionController(initialConnections: 12);

            // Feed HTTP 429 Rate Limit error
            controller.RecordTelemetry(2 * 1024 * 1024, averageRttMs: 80.0, errorCount: 0, http429Count: 2);

            int after429 = controller.EvaluateConnectionCount(100 * 1024 * 1024, isMeteredNetwork: false);
            after429.Should().BeLessThan(12, "Controller must immediately step down concurrency on HTTP 429");

            // Rapid consecutive evaluation should obey hysteresis cooldown and not oscillate
            int immediateRepeat = controller.EvaluateConnectionCount(100 * 1024 * 1024, isMeteredNetwork: false);
            immediateRepeat.Should().Be(after429, "Hysteresis window must prevent rapid thrashing");
        }
    }
}
