using System;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class Phase6OrchestrationPlatformSuite
    {
        #region 1. Resource Bottleneck Classifier Tests

        [Fact]
        public void ResourceBottleneckClassifier_AccuratelyIdentifiesBottleneckTypes()
        {
            // 1. Application Throttling
            var resApp = ResourceBottleneckClassifier.Classify(
                currentThroughputBps: 2 * 1024 * 1024,
                networkEstimatedBps: 100 * 1024 * 1024,
                rttMs: 20,
                isThrottledByApp: true);
            resApp.Type.Should().Be(BottleneckType.ApplicationLimited);
            resApp.ShouldStopScalingUp.Should().BeTrue();

            // 2. CPU Saturation
            var resCpu = ResourceBottleneckClassifier.Classify(
                currentThroughputBps: 10 * 1024 * 1024,
                networkEstimatedBps: 100 * 1024 * 1024,
                rttMs: 20,
                cpuUsagePercent: 95.0);
            resCpu.Type.Should().Be(BottleneckType.CpuLimited);
            resCpu.ShouldStopScalingUp.Should().BeTrue();

            // 3. Disk Latency Saturation
            var resDisk = ResourceBottleneckClassifier.Classify(
                currentThroughputBps: 20 * 1024 * 1024,
                networkEstimatedBps: 100 * 1024 * 1024,
                rttMs: 20,
                diskWriteLatencyMs: 65.0);
            resDisk.Type.Should().Be(BottleneckType.DiskLimited);
            resDisk.ShouldStopScalingUp.Should().BeTrue();

            // 4. Server Rate-Limiting / Retry Pressure
            var resRetry = ResourceBottleneckClassifier.Classify(
                currentThroughputBps: 5 * 1024 * 1024,
                networkEstimatedBps: 100 * 1024 * 1024,
                rttMs: 20,
                retryRatePercent: 8.5);
            resRetry.Type.Should().Be(BottleneckType.ServerLimited);
            resRetry.ShouldStopScalingUp.Should().BeTrue();

            // 5. Local Network Bandwidth Saturation
            var resNet = ResourceBottleneckClassifier.Classify(
                currentThroughputBps: 90 * 1024 * 1024,
                networkEstimatedBps: 100 * 1024 * 1024,
                rttMs: 20);
            resNet.Type.Should().Be(BottleneckType.NetworkLimited);
            resNet.ShouldStopScalingUp.Should().BeTrue();
        }

        #endregion

        #region 2. Download Strategy Selector Tests

        [Fact]
        public void DownloadStrategySelector_RoutesSmallFilesToSingleStream()
        {
            var result = DownloadStrategySelector.SelectStrategy(
                url: "https://example.com/icon.png",
                totalBytes: 250 * 1024, // 250 KB (< 1 MB)
                serverSupportsRanges: true);

            result.Strategy.Should().Be(DownloadStrategyType.SingleStream);
            result.RecommendedInitialConnections.Should().Be(1);
            result.ShouldPerformFullProbe.Should().BeFalse("Small files must avoid heavy probe overhead");
        }

        [Fact]
        public void DownloadStrategySelector_RoutesStreamingMediaToMediaStream()
        {
            var result = DownloadStrategySelector.SelectStrategy(
                url: "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                totalBytes: null,
                serverSupportsRanges: true,
                isMediaStreaming: true);

            result.Strategy.Should().Be(DownloadStrategyType.MediaStream);
            result.Rationale.Should().Contain("YouTube");
        }

        [Fact]
        public void DownloadStrategySelector_RoutesNonRangeServersToSingleStream()
        {
            var result = DownloadStrategySelector.SelectStrategy(
                url: "https://example.com/stream.bin",
                totalBytes: 50 * 1024 * 1024, // 50 MB
                serverSupportsRanges: false);

            result.Strategy.Should().Be(DownloadStrategyType.SingleStream);
            result.RecommendedInitialConnections.Should().Be(1);
        }

        [Fact]
        public void DownloadStrategySelector_RoutesLargeRangeSupportedFilesToAdaptiveMultipart()
        {
            var result = DownloadStrategySelector.SelectStrategy(
                url: "https://example.com/large_image.iso",
                totalBytes: 2L * 1024 * 1024 * 1024, // 2 GB
                serverSupportsRanges: true);

            result.Strategy.Should().Be(DownloadStrategyType.AdaptiveMultipart);
            result.RecommendedInitialConnections.Should().Be(8, "Files > 500 MB should start with 8 connections");
        }

        #endregion

        #region 3. Self-Diagnostic Explainer Tests

        [Fact]
        public void DownloadDiagnosticExplainer_GeneratesClearSpeedDiagnosis()
        {
            var bottleneck = new BottleneckAnalysisResult
            {
                Type = BottleneckType.ServerLimited,
                Reason = "Remote server is rate-limiting connections."
            };

            string explanation = DownloadDiagnosticExplainer.ExplainSpeed(
                currentSpeedBps: 25 * 1024 * 1024,
                peakSpeedBps: 30 * 1024 * 1024,
                bottleneck: bottleneck,
                activeConnections: 8,
                retryCount: 2);

            explanation.Should().Contain("25.00 MB/s");
            explanation.Should().Contain("8 active connections");
            explanation.Should().Contain("2 network retries");
            explanation.Should().Contain("Remote server is limiting transfer throughput");
        }

        [Fact]
        public void DownloadDiagnosticExplainer_GeneratesExplainableScalingDecisions()
        {
            string up = DownloadDiagnosticExplainer.ExplainScalingDecision(4, 8, 45.0, "Throughput increased significantly.");
            up.Should().Contain("Scaled connections UP (4 → 8) due to +45.0% throughput gain");

            string down = DownloadDiagnosticExplainer.ExplainScalingDecision(16, 12, -10.0, "Stepped down due to 429 backoff.");
            down.Should().Contain("Scaled connections DOWN (16 → 12)");
        }

        #endregion
    }
}
