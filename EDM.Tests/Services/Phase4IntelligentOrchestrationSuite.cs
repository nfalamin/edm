using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class Phase4IntelligentOrchestrationSuite
    {
        #region 1. Global Resource Manager & Utility Scoring Tests

        [Fact]
        public void GlobalResourceManager_EnforcesGlobalConnectionLimit_WithPriorityFairness()
        {
            var manager = new GlobalResourceManager(globalMaxConnections: 32);

            // Download 1 (High priority) requests 24
            var (d1Conns, _) = manager.AcquireLease("dl_1", "hostA.com", requestedConnections: 24, priority: DownloadPriority.High);
            d1Conns.Should().Be(24);

            // Download 2 (Normal priority) requests 16
            // Download 3 (Low priority) requests 8
            var (d2Conns, _) = manager.AcquireLease("dl_2", "hostB.com", requestedConnections: 16, priority: DownloadPriority.Normal);
            var (d3Conns, _) = manager.AcquireLease("dl_3", "hostC.com", requestedConnections: 8, priority: DownloadPriority.Low);

            // Global limit must NOT be exceeded
            manager.TotalAllocatedConnections.Should().BeLessOrEqualTo(32, "Manager must never over-allocate global connections");

            // Fairness: Low priority download must receive at least 1 connection (no complete starvation)
            var leases = manager.GetActiveLeasesSnapshot();
            leases.First(l => l.DownloadId == "dl_3").AllocatedConnections.Should().BeGreaterThan(0, "Low priority must not be starved");

            // High priority must receive greater or equal share than low priority
            leases.First(l => l.DownloadId == "dl_1").AllocatedConnections
                .Should().BeGreaterThan(leases.First(l => l.DownloadId == "dl_3").AllocatedConnections);
        }

        [Fact]
        public void GlobalResourceManager_AppliesCompletionAwareBoost_ToNearCompleteDownloads()
        {
            var leaseNormal = new GlobalDownloadResourceLease
            {
                DownloadId = "dl_large",
                Priority = DownloadPriority.Normal,
                RemainingBytes = 500 * 1024 * 1024,
                TotalBytes = 1000 * 1024 * 1024
            };

            var leaseNearComplete = new GlobalDownloadResourceLease
            {
                DownloadId = "dl_finishing",
                Priority = DownloadPriority.Normal,
                RemainingBytes = 2 * 1024 * 1024, // 2 MB remaining
                TotalBytes = 1000 * 1024 * 1024
            };

            leaseNearComplete.CalculateUtilityScore().Should().BeGreaterThan(leaseNormal.CalculateUtilityScore(),
                "Near-complete downloads must receive utility score boost to finalize quickly");
        }

        #endregion

        #region 2. Token Bucket Bandwidth Limiter Tests

        [Fact]
        public async Task TokenBucketBandwidthLimiter_AllowsInstantBursts_AndThrottlesWhenExhausted()
        {
            // 1 MB/s limit with 256 KB burst capacity
            var limiter = new TokenBucketBandwidthLimiter(bytesPerSecond: 1024 * 1024, maxBurstBytes: 256 * 1024);

            // First 128 KB should consume immediately from burst bucket (< 20ms)
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await limiter.ThrottleAsync(128 * 1024);
            sw.Stop();
            sw.ElapsedMilliseconds.Should().BeLessThan(50, "Initial write must be served from burst bucket");

            // Large write (512 KB) exceeds remaining tokens, must introduce measured asynchronous delay
            sw.Restart();
            await limiter.ThrottleAsync(512 * 1024);
            sw.Stop();
            sw.ElapsedMilliseconds.Should().BeGreaterThan(100, "Throttler must delay when tokens are exhausted");
        }

        #endregion

        #region 3. Host Circuit Breaker Tests

        [Fact]
        public void HostCircuitBreaker_TripsOnConsecutiveFailures_AndEnforcesBackoff()
        {
            var breaker = new HostCircuitBreakerManager(failureThreshold: 3, baseOpenDuration: TimeSpan.FromSeconds(5));
            string testHost = "flaky-server.net";

            breaker.GetHostState(testHost).Should().Be(CircuitState.Closed);
            breaker.CanExecute(testHost, out _).Should().BeTrue();

            // Record 3 consecutive server failures
            breaker.RecordFailure(testHost, HttpStatusCode.ServiceUnavailable);
            breaker.RecordFailure(testHost, HttpStatusCode.ServiceUnavailable);
            breaker.RecordFailure(testHost, HttpStatusCode.ServiceUnavailable);

            // Circuit must trip to Open
            breaker.GetHostState(testHost).Should().Be(CircuitState.Open);
            bool canExecute = breaker.CanExecute(testHost, out var waitDelay);
            canExecute.Should().BeFalse("Tripped circuit must reject rapid retry storm execution");
            waitDelay.TotalSeconds.Should().BeGreaterThan(0);

            // Record success on recovery
            breaker.RecordSuccess(testHost);
            breaker.RecordSuccess(testHost);
            breaker.GetHostState(testHost).Should().Be(CircuitState.Closed, "Successful probes must reset circuit to Closed");
        }

        #endregion

        #region 4. Network Quality Profiler Tests

        [Fact]
        public void NetworkQualityProfiler_AccuratelyClassifiesSpeedTiers()
        {
            var profilerFast = new NetworkQualityProfiler();
            profilerFast.RecordSample(throughputBps: 50 * 1024 * 1024, rttMs: 25);
            profilerFast.CurrentTier.Should().Be(NetworkSpeedTier.Fast);
            profilerFast.RecommendedGlobalConnections.Should().Be(32);

            var profilerVeryFast = new NetworkQualityProfiler();
            profilerVeryFast.RecordSample(throughputBps: 150 * 1024 * 1024, rttMs: 15);
            profilerVeryFast.CurrentTier.Should().Be(NetworkSpeedTier.VeryFast);
            profilerVeryFast.RecommendedGlobalConnections.Should().Be(64);
        }

        #endregion

        #region 5. Download Queue Scheduler & Priority Aging Tests

        [Fact]
        public void DownloadQueueScheduler_SchedulesHighestPriority_AndAppliesAgingFairness()
        {
            var scheduler = new DownloadQueueScheduler(maxActiveDownloads: 2);

            var itemLow = new QueuedDownloadItem
            {
                DownloadId = "dl_low",
                Priority = DownloadPriority.Low,
                EnqueuedTimeUtc = DateTime.UtcNow.AddMinutes(-10) // 10 minutes aging (score 10 + 20 = 30)
            };

            var itemNormal = new QueuedDownloadItem
            {
                DownloadId = "dl_normal",
                Priority = DownloadPriority.Normal,
                EnqueuedTimeUtc = DateTime.UtcNow // score 20
            };

            var itemHigh = new QueuedDownloadItem
            {
                DownloadId = "dl_high",
                Priority = DownloadPriority.High,
                EnqueuedTimeUtc = DateTime.UtcNow // score 50
            };

            scheduler.Enqueue(itemLow);
            scheduler.Enqueue(itemNormal);
            scheduler.Enqueue(itemHigh);

            // First to start must be High priority (score 50)
            var first = scheduler.TryGetNextDownloadToStart();
            first.Should().NotBeNull();
            first!.DownloadId.Should().Be("dl_high");
            scheduler.MarkStarted(first.DownloadId);

            // Second to start should be Low priority due to 10-minute aging score boost (score 30 > 20)
            var second = scheduler.TryGetNextDownloadToStart();
            second.Should().NotBeNull();
            second!.DownloadId.Should().Be("dl_low", "10-minute aged low priority item must be scheduled before freshly enqueued normal item");
            scheduler.MarkStarted(second.DownloadId);

            // Max active capacity (2) reached -> next should return null
            var third = scheduler.TryGetNextDownloadToStart();
            third.Should().BeNull("Scheduler must respect MaxActiveDownloads capacity constraint");
        }

        #endregion
    }
}
