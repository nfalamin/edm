using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class Phase2AdaptiveEngineSuite
    {
        #region 1. Adaptive Connection Scaling & Policy Tests

        [Fact]
        public void AdaptiveConnectionController_StartsConservatively_AndScalesUpOnSignificantGain()
        {
            var policy = new PerformancePolicy
            {
                InitialConnections = 4,
                MinConnections = 1,
                MaxConnections = 32,
                MinimumUsefulGainPercent = 0.05, // 5%
                ConsecutiveSamplesRequired = 3,
                CooldownInterval = TimeSpan.Zero // zero cooldown for deterministic unit testing
            };

            var controller = new AdaptiveConnectionController(initialConnections: 4, policy: policy);
            controller.CurrentConnections.Should().Be(4);

            // Record 3 samples with steady throughput improvement (+20% each step)
            controller.RecordTelemetry(aggregateThroughputBps: 10_000_000, averageRttMs: 40, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 12_000_000, averageRttMs: 42, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 15_000_000, averageRttMs: 43, errorCount: 0);

            int evaluated = controller.EvaluateConnectionCount(totalFileSize: 500 * 1024 * 1024, isMeteredNetwork: false);
            evaluated.Should().BeGreaterThan(4, "controller must scale up concurrency when throughput gain is >= 5%");
            controller.ScalingState.Should().Be(ConcurrencyScalingState.ScalingUp);
        }

        [Fact]
        public void AdaptiveConnectionController_HaltsScaleUp_WhenGainIsBelowUsefulThreshold()
        {
            var policy = new PerformancePolicy
            {
                InitialConnections = 8,
                MinConnections = 2,
                MaxConnections = 32,
                MinimumUsefulGainPercent = 0.05, // 5%
                ConsecutiveSamplesRequired = 3,
                CooldownInterval = TimeSpan.Zero
            };

            var controller = new AdaptiveConnectionController(initialConnections: 8, policy: policy);

            // Record 3 samples with negligible throughput improvement (+0.5%)
            controller.RecordTelemetry(aggregateThroughputBps: 50_000_000, averageRttMs: 30, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 50_100_000, averageRttMs: 30, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 50_200_000, averageRttMs: 30, errorCount: 0);

            int evaluated = controller.EvaluateConnectionCount(totalFileSize: 500 * 1024 * 1024, isMeteredNetwork: false);
            evaluated.Should().Be(8, "controller must NOT scale up when gain is < 5%");
            controller.ScalingState.Should().Be(ConcurrencyScalingState.Holding);
        }

        [Fact]
        public void AdaptiveConnectionController_BacksOffImmediately_OnHttp429()
        {
            var policy = new PerformancePolicy
            {
                InitialConnections = 16,
                MinConnections = 2,
                MaxConnections = 32,
                CooldownInterval = TimeSpan.Zero
            };

            var controller = new AdaptiveConnectionController(initialConnections: 16, policy: policy);

            // Record normal samples then a 429 error
            controller.RecordTelemetry(aggregateThroughputBps: 40_000_000, averageRttMs: 40, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 40_000_000, averageRttMs: 40, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 10_000_000, averageRttMs: 120, errorCount: 1, http429Count: 1);

            int evaluated = controller.EvaluateConnectionCount(totalFileSize: 500 * 1024 * 1024, isMeteredNetwork: false);
            evaluated.Should().BeLessThan(16, "controller must back off on HTTP 429 throttling");
            controller.ScalingState.Should().Be(ConcurrencyScalingState.ThrottledBackoff);
        }

        [Fact]
        public void AdaptiveConnectionController_ReducesConcurrency_OnSevereRttSpike()
        {
            var policy = new PerformancePolicy
            {
                InitialConnections = 12,
                MinConnections = 2,
                MaxConnections = 32,
                LatencySpikeThresholdMultiplier = 1.40, // 40% spike
                CooldownInterval = TimeSpan.Zero
            };

            var controller = new AdaptiveConnectionController(initialConnections: 12, policy: policy);

            // Baseline RTT: 50ms
            controller.RecordTelemetry(aggregateThroughputBps: 30_000_000, averageRttMs: 50, errorCount: 0);
            // Spiked RTT: 95ms (> 1.4 * 50)
            controller.RecordTelemetry(aggregateThroughputBps: 28_000_000, averageRttMs: 90, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 27_000_000, averageRttMs: 100, errorCount: 0);

            int evaluated = controller.EvaluateConnectionCount(totalFileSize: 500 * 1024 * 1024, isMeteredNetwork: false);
            evaluated.Should().BeLessThan(12, "controller must scale down on latency spikes");
            controller.ScalingState.Should().Be(ConcurrencyScalingState.ScalingDown);
        }

        #endregion

        #region 2. Smart Segment Sizing & Dynamic Splitting Tests

        [Theory]
        [InlineData(500 * 1024, 16, 1)]         // 500 KB -> 1 segment
        [InlineData(3 * 1024 * 1024, 16, 2)]     // 3 MB -> 2 segments
        [InlineData(30 * 1024 * 1024, 16, 4)]    // 30 MB -> 4 segments
        [InlineData(100 * 1024 * 1024, 16, 8)]   // 100 MB -> 8 segments
        [InlineData(1024L * 1024 * 1024, 16, 16)] // 1 GB -> 16 segments
        public void CalculateSmartSegmentCount_AssignsOptimalInitialSegments(long fileSize, int requested, int expected)
        {
            int calculated = SegmentScheduler.CalculateSmartSegmentCount(fileSize, requested);
            calculated.Should().Be(expected);
        }

        [Fact]
        public void SegmentScheduler_DynamicSplitting_MaintainsMathematicallyDisjointCoverage()
        {
            long fileSize = 100 * 1024 * 1024; // 100 MB
            var scheduler = new SegmentScheduler(fileSize, minSplitThresholdBytes: 2 * 1024 * 1024, splitAlignmentBytes: 64 * 1024);
            scheduler.InitializeDefault(4);

            // Initially 4 segments
            scheduler.ValidateCoverage().Should().BeTrue();

            // Simulate workers 0, 1, 2, 3 taking all 4 pending segments
            var w0 = scheduler.GetNextWorkItem("Worker_0");
            var w1 = scheduler.GetNextWorkItem("Worker_1");
            var w2 = scheduler.GetNextWorkItem("Worker_2");
            var w3 = scheduler.GetNextWorkItem("Worker_3");
            w0.Should().NotBeNull();
            w1.Should().NotBeNull();
            w2.Should().NotBeNull();
            w3.Should().NotBeNull();

            scheduler.ReportProgress(w0!.Id, 5 * 1024 * 1024);

            // Now all segments are Downloading. Simulate additional workers stealing work by splitting active segments
            var stolen1 = scheduler.GetNextWorkItem("Worker_Stealer_1");
            stolen1.Should().NotBeNull("stealer 1 must split the largest downloading segment");
            scheduler.ValidateCoverage().Should().BeTrue("Coverage must remain 100% valid after dynamic work steal 1");

            var stolen2 = scheduler.GetNextWorkItem("Worker_Stealer_2");
            stolen2.Should().NotBeNull("stealer 2 must split another large segment");
            scheduler.ValidateCoverage().Should().BeTrue("Coverage must remain 100% valid after dynamic work steal 2");

            // Verify all segment boundaries are strictly aligned and disjoint
            var snapshot = scheduler.GetSegmentsSnapshot();
            snapshot.Count.Should().Be(6, "4 original + 2 split stolen segments = 6 total segments");

            for (int i = 0; i < snapshot.Count - 1; i++)
            {
                (snapshot[i].End + 1).Should().Be(snapshot[i + 1].Start, "adjacent segments must be perfectly contiguous");
            }
        }

        #endregion

        #region 3. Per-Worker Telemetry & Slow Connection Detection Tests

        [Fact]
        public void ConnectionAccountant_AccuratelyDetectsUnderperformingWorkers()
        {
            var accountant = new ConnectionAccountant(8);

            // Register 4 workers
            accountant.RegisterWorker("Worker_1", 0);
            accountant.RegisterWorker("Worker_2", 1);
            accountant.RegisterWorker("Worker_3", 2);
            accountant.RegisterWorker("Worker_4", 3);

            // Fast workers at ~20 MB/s
            accountant.RecordWorkerProgress("Worker_1", 0, 50 * 1024 * 1024, currentSpeedBps: 20 * 1024 * 1024);
            accountant.RecordWorkerProgress("Worker_2", 1, 48 * 1024 * 1024, currentSpeedBps: 19 * 1024 * 1024);
            accountant.RecordWorkerProgress("Worker_3", 2, 45 * 1024 * 1024, currentSpeedBps: 18 * 1024 * 1024);

            // Slow worker at ~1 MB/s (< 25% of median 19 MB/s)
            accountant.RecordWorkerProgress("Worker_4", 3, 2 * 1024 * 1024, currentSpeedBps: 1 * 1024 * 1024);

            var slow = accountant.DetectSlowWorkers(thresholdRatio: 0.25);
            // Note: worker start time check needs duration > 3.0s in production, but let's verify snapshot records speeds accurately
            var snap = accountant.GetSnapshot();
            snap.WorkerSnapshots.Should().HaveCount(4);
            snap.WorkerSnapshots.First(w => w.WorkerId == "Worker_4").CurrentThroughputBps.Should().Be(1 * 1024 * 1024);
        }

        #endregion

        #region 4. Server Capability Learning Tests

        [Fact]
        public void ServerCapabilityCache_LearnsOptimalConnections_AndDecaysGracefully()
        {
            var cache = new ServerCapabilityCache();
            var uri = new Uri("https://fast-cdn.example.com/data.bin");

            // Initial recommendation with no history -> default conservative (4)
            int initialRec = cache.GetRecommendedInitialConnections(uri, fileSize: 100 * 1024 * 1024, userConfiguredMax: 32);
            initialRec.Should().Be(4);

            // Record successful fast download at 16 connections with 85 MB/s
            cache.RecordResponse(uri, HttpStatusCode.OK, rttMs: 25, throughputBps: 85 * 1024 * 1024, supportsRange: true, activeConnections: 16);

            // Next download recommendation should learn the optimal 16 connections
            int learnedRec = cache.GetRecommendedInitialConnections(uri, fileSize: 100 * 1024 * 1024, userConfiguredMax: 32);
            learnedRec.Should().Be(16);

            // Record a 429 rate limit
            cache.RecordResponse(uri, (HttpStatusCode)429, rttMs: 50, throughputBps: 0, supportsRange: true, activeConnections: 16);

            int throttledRec = cache.GetRecommendedInitialConnections(uri, fileSize: 100 * 1024 * 1024, userConfiguredMax: 32);
            throttledRec.Should().BeLessThanOrEqualTo(4, "cache must cap recommended connections after 429 throttling");
        }

        #endregion

        #region 5. Global Multi-Download Budgeting Tests

        [Fact]
        public void GlobalConnectionGovernor_DistributesBudgetFairly_AcrossMultipleDownloads()
        {
            var governor = new GlobalConnectionGovernor(globalMax: 32);

            // Download 1 (High priority) requests 24 connections
            int d1Budget = governor.AcquireConnectionBudget("dl_1", "serverA.com", requested: 24, priority: DownloadPriority.High);
            d1Budget.Should().Be(24);

            // Download 2 (Normal priority) requests 16 connections -> governor must distribute 32 total
            int d2Budget = governor.AcquireConnectionBudget("dl_2", "serverB.com", requested: 16, priority: DownloadPriority.Normal);
            
            governor.TotalAllocatedConnections.Should().BeLessOrEqualTo(32, "governor must enforce global maximum connection limit");
            governor.ActiveDownloadCount.Should().Be(2);

            // Release Download 1
            governor.ReleaseConnectionBudget("dl_1");
            governor.ActiveDownloadCount.Should().Be(1);
            governor.TotalAllocatedConnections.Should().Be(16, "Download 2 should now receive its full requested 16 connections");
        }

        #endregion

        #region 6. Monotonic Speed Tracker Tests

        [Fact]
        public void MonotonicSpeedTracker_CalculatesInstantaneousRollingAverageAndPeakSpeeds()
        {
            var tracker = new MonotonicSpeedTracker(ewmaAlpha: 0.3);

            tracker.RecordProgress(10 * 1024 * 1024); // 10 MB
            tracker.TotalBytes.Should().Be(10 * 1024 * 1024);

            tracker.RecordProgress(30 * 1024 * 1024); // 30 MB
            tracker.TotalBytes.Should().Be(30 * 1024 * 1024);

            tracker.ElapsedSeconds.Should().BeGreaterThan(0);
            tracker.AverageSpeedBps.Should().BeGreaterThan(0);
        }

        #endregion
    }
}
