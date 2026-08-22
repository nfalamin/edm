using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class Stage4ReleaseAndPerformanceLabTests : TestBase
    {
        [Fact]
        public void ReleaseLifecycleManager_HandlesVersionMigrationAndDowngradeProtection()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"edm_release_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Test 1: Clean Install / Initial Migration
                var result1 = ReleaseLifecycleManager.CheckAndExecuteMigrations(tempDir);
                result1.Success.Should().BeTrue();
                result1.TargetVersion.Should().Be("2.0.0.0");

                // Test 2: Downgrade Protection
                // Inject fake future version 3.0.0.0 into version.json
                string versionFile = Path.Combine(tempDir, "version.json");
                File.WriteAllText(versionFile, "{\"version\": \"3.0.0.0\", \"schemaVersion\": 3}");

                var result2 = ReleaseLifecycleManager.CheckAndExecuteMigrations(tempDir);
                result2.Success.Should().BeFalse("Downgrade must be rejected when higher version data exists");
                result2.Message.Should().Contain("Downgrade rejected");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void AuthenticodeVerifier_ReportsUnsignedAccuratelyWithoutCrashing()
        {
            string currentExe = typeof(ReleaseLifecycleManager).Assembly.Location;
            var sig = AuthenticodeVerifier.VerifyFile(currentExe);

            // Assembly in build output is unsigned development build
            sig.Should().NotBeNull();
            sig.StatusMessage.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void PerformanceLabBenchmarkService_ExecutesAllTiersAndExportsValidJson()
        {
            string jsonOut = Path.Combine(Path.GetTempPath(), $"edm_benchmarks_{Guid.NewGuid():N}.json");

            try
            {
                var report = PerformanceLabBenchmarkService.RunFullBenchmarkSuite();

                report.BandwidthTierBenchmarks.Should().HaveCount(6, "Must cover 10M, 50M, 100M, 500M, 1G, 10G tiers");
                report.ConnectionScalingBenchmarks.Should().HaveCount(6, "Must cover 1, 2, 4, 8, 16, 32 connections");
                report.QueueConcurrencyBenchmarks.Should().HaveCount(5, "Must cover 1, 5, 10, 50, 100 queues");

                foreach (var m in report.BandwidthTierBenchmarks)
                {
                    m.PassesRegressionThreshold.Should().BeTrue();
                    m.SegmentUtilizationPercent.Should().BeGreaterThan(95.0);
                }

                PerformanceLabBenchmarkService.ExportToJson(report, jsonOut);
                File.Exists(jsonOut).Should().BeTrue();

                string jsonContent = File.ReadAllText(jsonOut);
                using var doc = JsonDocument.Parse(jsonContent);
                doc.RootElement.GetProperty("BandwidthTierBenchmarks").GetArrayLength().Should().Be(6);
            }
            finally
            {
                if (File.Exists(jsonOut)) File.Delete(jsonOut);
            }
        }
    }
}
