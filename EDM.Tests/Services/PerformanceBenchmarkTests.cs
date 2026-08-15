using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class PerformanceBenchmarkTests : TestBase
    {
        [Fact]
        public void ArrayPool_BufferRentAndReturn_EliminatesGarbageCollectionAllocations()
        {
            // Arrange
            long memoryBefore = GC.GetTotalMemory(true);

            // Act: Rent and return 1,000 buffers from ArrayPool
            for (int i = 0; i < 1000; i++)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(65536);
                buffer[0] = (byte)(i % 255);
                ArrayPool<byte>.Shared.Return(buffer);
            }

            long memoryAfter = GC.GetTotalMemory(false);
            long allocatedDiff = Math.Max(0, memoryAfter - memoryBefore);

            // Assert: ArrayPool allocations should add zero GC heap pressure
            allocatedDiff.Should().BeLessThan(1024 * 1024, "ArrayPool reuse prevents LOH and heap fragmentation");
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(5, 4)]
        [InlineData(10, 8)]
        [InlineData(20, 16)]
        public void BenchmarkConnectionMatrix_CalculatesSegmentDistributionCorrectly(int concurrentDownloads, int connectionsPerDownload)
        {
            // Arrange
            long totalFileLength = 100 * 1024 * 1024; // 100 MB

            // Act
            var ranges = MultiPartDownloader.CalculateRanges(totalFileLength, connectionsPerDownload);

            // Assert
            ranges.Should().HaveCount(connectionsPerDownload);
            long totalCovered = 0;
            foreach (var range in ranges)
            {
                (range.End - range.Start + 1).Should().BeGreaterThan(0);
                totalCovered += (range.End - range.Start + 1);
            }

            totalCovered.Should().Be(totalFileLength, "SUM of connection segment lengths must equal total file length exactly");
        }
    }
}
