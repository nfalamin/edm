using System;
using FluentAssertions;
using Xunit;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class AdaptiveConnectionControllerTests : TestBase
    {
        [Fact]
        public void SmallFileOptimization_LimitsConnectionCount()
        {
            // Arrange
            var controller = new AdaptiveConnectionController(initialConnections: 16);
            long smallFileSize = 2 * 1024 * 1024; // 2 MB

            // Act
            int connections = controller.EvaluateConnectionCount(smallFileSize, isMeteredNetwork: false);

            // Assert
            connections.Should().BeLessOrEqualTo(4);
        }

        [Fact]
        public void BacksOffConnections_WhenServerErrorsRecorded()
        {
            // Arrange
            var controller = new AdaptiveConnectionController(initialConnections: 8);

            // Record high telemetry with errors
            controller.RecordTelemetry(10 * 1024 * 1024, 50.0, errorCount: 0);
            controller.RecordTelemetry(10 * 1024 * 1024, 50.0, errorCount: 1);
            controller.RecordTelemetry(10 * 1024 * 1024, 50.0, errorCount: 2);

            // Act
            int connections = controller.EvaluateConnectionCount(100 * 1024 * 1024, isMeteredNetwork: false);

            // Assert - Should reduce connection count due to error detection
            connections.Should().BeLessThan(8);
        }

        [Fact]
        public void BenchmarkConnectionCounts_EvaluatesSimulatedRttConditions()
        {
            // Benchmark comparison harness for 4, 8, 16, 32 connections under simulated RTT/bandwidth
            int[] testConnectionCounts = new[] { 4, 8, 16, 32 };
            double simulatedRttMs = 120.0;
            double simulatedBandwidthBps = 50 * 1024 * 1024; // 50 Mbps

            foreach (int count in testConnectionCounts)
            {
                var controller = new AdaptiveConnectionController(initialConnections: count);
                controller.RecordTelemetry(simulatedBandwidthBps, simulatedRttMs, 0);

                int eval = controller.EvaluateConnectionCount(500 * 1024 * 1024, false);
                eval.Should().BeGreaterThan(0);
            }
        }
    }
}
