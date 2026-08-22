using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class AdvancedBenchmarkEngineTests : IDisposable
    {
        private readonly string _testStorageDir;

        public AdvancedBenchmarkEngineTests()
        {
            _testStorageDir = Path.Combine(Path.GetTempPath(), "EDM_BenchTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testStorageDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testStorageDir))
                {
                    Directory.Delete(_testStorageDir, true);
                }
            }
            catch { }
        }

        // 1. Single download benchmark execution and metrics
        [Fact]
        public async Task Test1_SingleDownloadBenchmark_ExecutesAndMeasuresThroughput()
        {
            var res = await PerformanceLabBenchmarkService.RunSingleDownloadBenchmarkAsync(1024 * 1024);

            res.ScenarioName.Should().Contain("Single Stream");
            res.TotalBytes.Should().Be(1024 * 1024);
            res.DurationMs.Should().BeGreaterThan(0);
            res.ThroughputMbps.Should().BeGreaterThan(0);
            res.CompletedJobs.Should().Be(1);
        }

        // 2. Multi-connection scaling benchmark
        [Fact]
        public async Task Test2_ConcurrentDownloadBenchmark_MeasuresAggregatedThroughput()
        {
            var res = await PerformanceLabBenchmarkService.RunConcurrentDownloadBenchmarkAsync(4, 512 * 1024);

            res.CompletedJobs.Should().Be(4);
            res.TotalBytes.Should().Be(4 * 512 * 1024);
            res.ThroughputMbps.Should().BeGreaterThan(0);
        }

        // 3. Segmented download chunk assembly benchmark
        [Fact]
        public async Task Test3_SegmentedDownloadBenchmark_MeasuresSegmentAssembly()
        {
            var res = await PerformanceLabBenchmarkService.RunSegmentedDownloadBenchmarkAsync(8, 2 * 1024 * 1024);

            res.ScenarioName.Should().Contain("Segmented");
            res.TotalBytes.Should().Be(2 * 1024 * 1024);
            res.CompletedJobs.Should().Be(1);
        }

        // 4. HLS playlist parsing and segment assembly benchmark
        [Fact]
        public async Task Test4_HlsAssemblyBenchmark_MeasuresSegmentAssemblySpeed()
        {
            var res = await PerformanceLabBenchmarkService.RunHlsAssemblyBenchmarkAsync(10, 64 * 1024);

            res.ScenarioName.Should().Contain("HLS Assembly");
            res.TotalBytes.Should().Be(10 * 64 * 1024);
            res.ThroughputMbps.Should().BeGreaterThan(0);
        }

        // 5. DASH manifest parsing and chunk assembly benchmark
        [Fact]
        public async Task Test5_DashAssemblyBenchmark_MeasuresChunkAssemblySpeed()
        {
            var res = await PerformanceLabBenchmarkService.RunDashAssemblyBenchmarkAsync(10, 64 * 1024);

            res.ScenarioName.Should().Contain("DASH Assembly");
            res.TotalBytes.Should().Be(10 * 64 * 1024);
            res.ThroughputMbps.Should().BeGreaterThan(0);
        }

        // 6. Audio-only stream extraction benchmark
        [Fact]
        public async Task Test6_AudioExtractionBenchmark_MeasuresRemuxSpeed()
        {
            var res = await PerformanceLabBenchmarkService.RunAudioExtractionBenchmarkAsync(1024 * 1024);

            res.ScenarioName.Should().Contain("Audio-Only");
            res.TotalBytes.Should().Be(1024 * 1024);
            res.ThroughputMbps.Should().BeGreaterThan(0);
        }

        // 7. Retry and backoff recovery overhead measurement
        [Fact]
        public void Test7_RetryRecoveryBenchmark_MeasuresOverhead()
        {
            var res = PerformanceLabBenchmarkService.RunRetryRecoveryBenchmark(4);

            res.RetryCount.Should().Be(4);
            res.DurationMs.Should().BeGreaterThanOrEqualTo(0);
        }

        // 8. Queue scheduling insertion and dispatch latency measurement
        [Fact]
        public void Test8_QueueSchedulerBenchmark_MeasuresQueueOperations()
        {
            var res = PerformanceLabBenchmarkService.RunQueueSchedulerBenchmark(20);

            res.ScenarioName.Should().Contain("Queue Scheduling");
            res.CompletedJobs.Should().BeGreaterThan(0);
        }

        // 9. CPU utilization measurement during benchmark runs
        [Fact]
        public void Test9_CpuMeasurement_CapturesSystemEnvironment()
        {
            int cores = Environment.ProcessorCount;
            cores.Should().BeGreaterThan(0);
        }

        // 10. Memory and GC collection diagnostics
        [Fact]
        public async Task Test10_MemoryDiagnostics_MeasuresPeakWorkingSet()
        {
            var res = await PerformanceLabBenchmarkService.RunSingleDownloadBenchmarkAsync(512 * 1024);

            res.PeakMemoryBytes.Should().BeGreaterThan(0);
        }

        // 11. Disk sequential write throughput measurement
        [Fact]
        public async Task Test11_DiskWriteBenchmark_MeasuresDiskSpeed()
        {
            var res = await PerformanceLabBenchmarkService.RunDiskWriteBenchmarkAsync(_testStorageDir, 2 * 1024 * 1024);

            res.ScenarioName.Should().Contain("Disk Sequential Write");
            res.DiskWriteMBps.Should().BeGreaterThan(0);
        }

        // 12. Markdown report generation and table formatting
        [Fact]
        public void Test12_MarkdownReportGeneration_RendersMarkdownTable()
        {
            var results = new List<BenchmarkScenarioResult>
            {
                new() { ScenarioName = "Single Stream", ThroughputMbps = 450.2, DurationMs = 25.1, TotalBytes = 10485760, AverageLatencyMs = 1.1, PeakMemoryBytes = 85000000 },
                new() { ScenarioName = "Concurrent 4x", ThroughputMbps = 890.5, DurationMs = 35.4, TotalBytes = 20971520, AverageLatencyMs = 1.4, PeakMemoryBytes = 92000000 }
            };

            string md = PerformanceLabBenchmarkService.FormatMarkdownReport(results);

            md.Should().Contain("# EDM Performance & Benchmark Lab Report");
            md.Should().Contain("| Benchmark Scenario |");
            md.Should().Contain("Single Stream");
            md.Should().Contain("450.20");
        }

        // 13. JSON report serialization and export
        [Fact]
        public void Test13_JsonReportExport_WritesValidJsonFile()
        {
            var report = PerformanceLabBenchmarkService.RunFullBenchmarkSuite();
            string jsonPath = Path.Combine(_testStorageDir, "benchmark_report.json");

            PerformanceLabBenchmarkService.ExportToJson(report, jsonPath);

            File.Exists(jsonPath).Should().BeTrue();
            string text = File.ReadAllText(jsonPath);
            text.Should().Contain("BandwidthTierBenchmarks");
        }

        // 14. Reproducibility of repeated benchmark runs
        [Fact]
        public async Task Test14_BenchmarkReproducibility_ProducesConsistentResults()
        {
            var res1 = await PerformanceLabBenchmarkService.RunSingleDownloadBenchmarkAsync(512 * 1024);
            var res2 = await PerformanceLabBenchmarkService.RunSingleDownloadBenchmarkAsync(512 * 1024);

            res1.TotalBytes.Should().Be(res2.TotalBytes);
            res1.CompletedJobs.Should().Be(res2.CompletedJobs);
        }

        // 15. Cancellation during active benchmark execution
        [Fact]
        public async Task Test15_CancellationHandling_ThrowsOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var act = () => PerformanceLabBenchmarkService.RunHlsAssemblyBenchmarkAsync(100, 1024 * 1024, cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        // 16. Test environment and OS metadata recording
        [Fact]
        public void Test16_EnvironmentMetadata_RecordedAccurately()
        {
            var report = new PerformanceLabReport();

            report.EnvironmentOS.Should().NotBeEmpty();
            report.ProcessorCount.Should().Be(Environment.ProcessorCount);
            report.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }
    }
}
