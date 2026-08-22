using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class A4MetadataPersistenceUnitTests
    {
        [Fact]
        public async Task WriteStateAtomicAsync_CreatesMetadataAndCleansTmp()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"a4_unit_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            string metaPath = Path.Combine(tempDir, "metadata.json");

            try
            {
                var manager = new DurableMetadataManager();
                var state = new DurableDownloadState
                {
                    Url = "http://127.0.0.1/test.bin",
                    TotalBytes = 10_000_000,
                    Segments = new List<SegmentRange>
                    {
                        new SegmentRange { Id = 0, Start = 0, End = 9_999_999, BytesDownloaded = 1_000_000, State = SegmentState.Downloading }
                    }
                };

                await manager.WriteStateAtomicAsync(metaPath, state, CancellationToken.None).ConfigureAwait(false);

                File.Exists(metaPath).Should().BeTrue("metadata.json must be created atomically");
                File.Exists(metaPath + ".tmp").Should().BeFalse(".tmp file must be renamed into metadata.json");

                var restored = await manager.ReadStateAsync(metaPath, CancellationToken.None).ConfigureAwait(false);
                restored.Should().NotBeNull();
                restored!.Url.Should().Be("http://127.0.0.1/test.bin");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task ReadStateAsync_CleansOrphanTmpFile()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"a4_tmp_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            string metaPath = Path.Combine(tempDir, "metadata.json");
            string tmpPath = metaPath + ".tmp";

            try
            {
                var manager = new DurableMetadataManager();
                var state = new DurableDownloadState
                {
                    Url = "http://127.0.0.1/test.bin",
                    TotalBytes = 10_000_000,
                    Segments = new List<SegmentRange>
                    {
                        new SegmentRange { Id = 0, Start = 0, End = 9_999_999, BytesDownloaded = 500_000, State = SegmentState.Pending }
                    }
                };

                await manager.WriteStateAtomicAsync(metaPath, state, CancellationToken.None).ConfigureAwait(false);

                // Create an orphan .tmp file simulating a crash mid-write
                await File.WriteAllTextAsync(tmpPath, "{ \"partial\": true }").ConfigureAwait(false);
                File.Exists(tmpPath).Should().BeTrue();

                var restored = await manager.ReadStateAsync(metaPath, CancellationToken.None).ConfigureAwait(false);
                restored.Should().NotBeNull();
                File.Exists(tmpPath).Should().BeFalse("ReadStateAsync must clean orphan .tmp file left by crash");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void ValidateInvariants_DetectsOverlapAndGap()
        {
            var validState = new DurableDownloadState
            {
                TotalBytes = 10_000_000,
                Segments = new List<SegmentRange>
                {
                    new SegmentRange { Id = 0, Start = 0, End = 4_999_999, BytesDownloaded = 0 },
                    new SegmentRange { Id = 1, Start = 5_000_000, End = 9_999_999, BytesDownloaded = 0 }
                }
            };
            DurableMetadataManager.ValidateInvariants(validState).Should().BeTrue("Valid contiguous segments must pass invariant validation");

            var overlapState = new DurableDownloadState
            {
                TotalBytes = 10_000_000,
                Segments = new List<SegmentRange>
                {
                    new SegmentRange { Id = 0, Start = 0, End = 6_000_000, BytesDownloaded = 0 },
                    new SegmentRange { Id = 1, Start = 5_000_000, End = 9_999_999, BytesDownloaded = 0 }
                }
            };
            DurableMetadataManager.ValidateInvariants(overlapState).Should().BeFalse("Overlapping segments must fail invariant validation");

            var gapState = new DurableDownloadState
            {
                TotalBytes = 10_000_000,
                Segments = new List<SegmentRange>
                {
                    new SegmentRange { Id = 0, Start = 0, End = 3_000_000, BytesDownloaded = 0 },
                    new SegmentRange { Id = 1, Start = 5_000_000, End = 9_999_999, BytesDownloaded = 0 }
                }
            };
            DurableMetadataManager.ValidateInvariants(gapState).Should().BeFalse("Gaps in coverage must fail invariant validation");
        }

        [Fact]
        public void ReconcileAndValidate_RejectsETagMismatch()
        {
            var manager = new DurableMetadataManager();
            var state = new DurableDownloadState
            {
                ETag = "\"v1\"",
                TotalBytes = 10_000_000,
                Segments = new List<SegmentRange>
                {
                    new SegmentRange { Id = 0, Start = 0, End = 9_999_999, BytesDownloaded = 1_000_000 }
                }
            };

            bool matchSame = manager.ReconcileAndValidate(state, "\"v1\"", null!);
            matchSame.Should().BeTrue("Matching ETag must approve resume");

            bool matchDiff = manager.ReconcileAndValidate(state, "\"v2-changed\"", null!);
            matchDiff.Should().BeFalse("ETag mismatch must reject resume state to prevent content merge corruption");
        }

        [Fact]
        public async Task ReconcileAndValidate_TruncatesOversizedPartFile()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"a4_part_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            string partPath = Path.Combine(tempDir, "segment_0.part");

            try
            {
                // Create an oversized 6 MB .part file for a 5 MB max segment
                byte[] data = new byte[6 * 1024 * 1024];
                await File.WriteAllBytesAsync(partPath, data).ConfigureAwait(false);

                var manager = new DurableMetadataManager();
                var state = new DurableDownloadState
                {
                    TotalBytes = 10_000_000,
                    Segments = new List<SegmentRange>
                    {
                        new SegmentRange { Id = 0, Start = 0, End = 4_999_999, BytesDownloaded = 6_000_000, TempPath = partPath, State = SegmentState.Downloading }
                    }
                };

                bool ok = manager.ReconcileAndValidate(state, null!, null!);
                ok.Should().BeTrue();

                long len = new FileInfo(partPath).Length;
                len.Should().Be(5_000_000, "ReconcileAndValidate must truncate oversized .part file to max segment bounds");
                state.Segments[0].State.Should().Be(SegmentState.Completed);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }
    }
}
