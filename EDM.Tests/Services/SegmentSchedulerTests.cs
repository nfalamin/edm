using System;
using System.Linq;
using FluentAssertions;
using Xunit;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class SegmentSchedulerTests : TestBase
    {
        [Fact]
        public void InitializeDefault_CreatesNonOverlappingFullCoverageRanges()
        {
            // Arrange
            long totalBytes = 100 * 1024 * 1024; // 100 MB
            var scheduler = new SegmentScheduler(totalBytes);

            // Act
            scheduler.InitializeDefault(8);

            // Assert
            scheduler.Segments.Count.Should().Be(8);
            scheduler.ValidateCoverage().Should().BeTrue();
            scheduler.GetTotalBytesDownloaded().Should().Be(0);
        }

        [Fact]
        public void GetNextWorkItem_SplitsActiveSegment_WhenIdleWorkerRequestsWork()
        {
            // Arrange
            long totalBytes = 100 * 1024 * 1024; // 100 MB
            var scheduler = new SegmentScheduler(totalBytes, minSplitThresholdBytes: 2 * 1024 * 1024);
            scheduler.InitializeDefault(1); // Single large 100MB segment

            // Worker 1 takes initial segment
            var work1 = scheduler.GetNextWorkItem("Worker_1");
            work1.Should().NotBeNull();
            work1!.Id.Should().Be(0);

            // Act - Worker 2 asks for work -> Scheduler should split active 100MB segment dynamically
            var work2 = scheduler.GetNextWorkItem("Worker_2");

            // Assert
            work2.Should().NotBeNull();
            scheduler.Segments.Count.Should().Be(2);
            scheduler.ValidateCoverage().Should().BeTrue(); // Zero byte gaps or overlaps!
        }
    }
}
