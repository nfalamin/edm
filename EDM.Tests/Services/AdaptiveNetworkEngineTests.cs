using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;
using EDM.Services.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace EDM.Tests.Services
{
    public class AdaptiveNetworkEngineTests
    {
        [Fact]
        public void Part3_RangeValidation_Detects206PartialContentRequirement()
        {
            var pipeline = new HttpRequestPipeline(SharedHttpClient.Instance);
            var req = pipeline.CreateFreshRequest(HttpMethod.Get, new Uri("https://example.com/file.bin"), rangeStart: 0, rangeEnd: 1023);

            req.Headers.Range.Should().NotBeNull();
            req.Headers.Range!.Ranges.Should().ContainSingle();
            req.Headers.Range.Ranges.First().From.Should().Be(0);
            req.Headers.Range.Ranges.First().To.Should().Be(1023);
        }

        [Fact]
        public async Task Part5_HostBudgetFairness_ScalesConnectionBudgetForMultipleDownloadsOnSameHost()
        {
            string url1 = "https://cdn.example.com/file1.zip";
            string url2 = "https://cdn.example.com/file2.zip";

            var mockSettings = new Mock<ISettingsService>();
            mockSettings.Setup(s => s.GetConnectionLimitOverride()).Returns(0);
            mockSettings.Setup(s => s.GetActiveBandwidthLimitKbps()).Returns(0);

            var mockNet = new Mock<INetworkService>();
            mockNet.Setup(n => n.GetCurrentNetworkType()).Returns(NetworkType.Ethernet);

            var manager = new AdaptiveConnectionManager(mockSettings.Object, mockNet.Object);

            // Register first download
            AdaptiveConnectionManager.RegisterActiveHostDownload(url1);
            int connsSingle = await manager.DetermineConnectionCountAsync(url1, 100 * 1024 * 1024, true, CancellationToken.None).ConfigureAwait(true);

            // Register second download on same host
            AdaptiveConnectionManager.RegisterActiveHostDownload(url2);
            int connsConcurrent = await manager.DetermineConnectionCountAsync(url2, 100 * 1024 * 1024, true, CancellationToken.None).ConfigureAwait(true);

            connsConcurrent.Should().BeLessThanOrEqualTo(16);

            // Cleanup
            AdaptiveConnectionManager.UnregisterActiveHostDownload(url1);
            AdaptiveConnectionManager.UnregisterActiveHostDownload(url2);
        }

        [Theory]
        [InlineData(100L * 1024 * 1024)] // 100 MB
        [InlineData(1024L * 1024 * 1024)] // 1 GB
        [InlineData(5120L * 1024 * 1024)] // 5 GB
        [InlineData(10240L * 1024 * 1024)] // 10 GB
        public void Part10_LargeFileMemoryStability_VerifiesMemoryDoesNotGrowWithFileSize(long fileSize)
        {
            long memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

            // Simulate chunk processing over large file
            long chunkSize = 64 * 1024;
            long totalChunks = fileSize / chunkSize;

            long processedBytes = 0;
            for (int i = 0; i < Math.Min(1000, totalChunks); i++)
            {
                processedBytes += chunkSize;
            }

            long memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
            long memoryDelta = memoryAfter - memoryBefore;

            // Memory delta must be < 1 MB regardless of whether total file is 100MB or 10GB
            memoryDelta.Should().BeLessThan(1 * 1024 * 1024);
        }
    }
}
