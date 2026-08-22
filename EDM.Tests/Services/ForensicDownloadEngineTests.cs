using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class ForensicDownloadEngineTests : TestBase
    {
        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(100, 4)]
        [InlineData(1024, 8)]
        [InlineData(1048576, 16)]
        [InlineData(10485760, 32)]
        [InlineData(1073741824, 16)] // 1 GB file
        public void SegmentScheduler_MathematicalCoverage_HasNoGapsOrOverlaps(long totalBytes, int connectionCount)
        {
            // Arrange
            var scheduler = new SegmentScheduler(totalBytes);

            // Act
            scheduler.InitializeDefault(connectionCount);

            // Assert
            scheduler.ValidateCoverage().Should().BeTrue("Segment scheduler coverage must cover [0, totalBytes - 1] with 0 gaps and 0 overlaps");

            var segments = scheduler.GetSegmentsSnapshot();
            long calculatedSum = segments.Sum(s => s.TotalBytes);
            calculatedSum.Should().Be(totalBytes, "Sum of all segment total bytes must exactly equal TotalBytes");

            segments[0].Start.Should().Be(0, "First segment must start at byte 0");
            segments.Last().End.Should().Be(totalBytes - 1, "Last segment must end at byte totalBytes - 1");

            for (int i = 0; i < segments.Count - 1; i++)
            {
                (segments[i].End + 1).Should().Be(segments[i + 1].Start, $"Segment {i} End + 1 must equal Segment {i+1} Start");
            }
        }

        [Fact]
        public void DynamicWorkStealing_SplitsSlowSegmentTailCorrectly()
        {
            // Arrange: 100 MB file split into 2 initial segments (50 MB each)
            long totalBytes = 100 * 1024 * 1024;
            var scheduler = new SegmentScheduler(totalBytes, minSplitThresholdBytes: 2 * 1024 * 1024);
            scheduler.InitializeDefault(2);

            // Fast worker 1 gets segment 0
            var seg0 = scheduler.GetNextWorkItem("worker-1");
            seg0.Should().NotBeNull();
            seg0!.Id.Should().Be(0);

            // Slow worker 2 gets segment 1
            var seg1 = scheduler.GetNextWorkItem("worker-2");
            seg1.Should().NotBeNull();
            seg1!.Id.Should().Be(1);

            // Fast worker 1 completes segment 0
            scheduler.MarkCompleted(seg0.Id);

            // Slow worker 2 has only downloaded 1 MB of its 50 MB segment
            scheduler.ReportProgress(seg1.Id, 1 * 1024 * 1024);

            // Fast worker 1 asks for more work -> should steal tail end of slow worker 2's segment
            var stolenSegment = scheduler.GetNextWorkItem("worker-1");

            // Assert
            stolenSegment.Should().NotBeNull("Work stealing must trigger when remaining bytes exceed split threshold");
            stolenSegment!.Id.Should().Be(2, "Stolen segment must receive new unique ID");
            stolenSegment.AssignedWorkerId.Should().Be("worker-1");

            // Verify global coverage is maintained after work stealing split
            scheduler.ValidateCoverage().Should().BeTrue("Segment coverage must remain valid after dynamic work stealing split");
        }
    }
}
