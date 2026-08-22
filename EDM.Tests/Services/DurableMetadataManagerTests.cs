using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class DurableMetadataManagerTests : TestBase
    {
        [Fact]
        public async Task WriteStateAtomicAsync_WritesDurableStateFile()
        {
            // Arrange
            var manager = new DurableMetadataManager();
            string tempDir = Path.Combine(Path.GetTempPath(), "EDM_MetaTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string metaPath = Path.Combine(tempDir, "metadata.json");

            var state = new DurableDownloadState
            {
                Url = "http://127.0.0.1/test.bin",
                TotalBytes = 1048576,
                ETag = "\"abc1234\"",
                Segments = new List<SegmentRange>
                {
                    new SegmentRange { Id = 0, Start = 0, End = 524287, BytesDownloaded = 524288, State = SegmentState.Completed },
                    new SegmentRange { Id = 1, Start = 524288, End = 1048575, BytesDownloaded = 0, State = SegmentState.Pending }
                }
            };

            try
            {
                // Act
                await manager.WriteStateAtomicAsync(metaPath, state, CancellationToken.None);

                // Assert
                File.Exists(metaPath).Should().BeTrue();

                var loaded = await manager.ReadStateAsync(metaPath, CancellationToken.None);
                loaded.Should().NotBeNull();
                loaded!.TotalBytes.Should().Be(1048576);
                loaded.ETag.Should().Be("\"abc1234\"");
                loaded.Segments.Count.Should().Be(2);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void ReconcileAndValidate_ResetsState_WhenETagChanged()
        {
            // Arrange
            var manager = new DurableMetadataManager();
            var state = new DurableDownloadState
            {
                ETag = "\"old-etag-123\"",
                LastModified = "Mon, 10 Aug 2026 12:00:00 GMT"
            };

            // Act
            bool isValid = manager.ReconcileAndValidate(state, "\"new-etag-456\"", "Mon, 10 Aug 2026 12:00:00 GMT");

            // Assert
            isValid.Should().BeFalse();
        }
    }
}
