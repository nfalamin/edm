using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using EDM.Services;
using EDM.Services.Interfaces;

namespace EDM.Tests
{
    public class MultiPartDownloaderTests : TestBase
    {
        [Theory]
        [InlineData(1000, 4, 0, 999)]
        [InlineData(10, 3, 0, 9)]
        [InlineData(1, 1, 0, 0)]
        [InlineData(1024, 8, 0, 1023)]
        public void CalculateRanges_CoversWholeFile_NoGapsOrOverlaps(long totalBytes, int chunkCount, long expectedStart, long expectedEnd)
        {
            var ranges = MultiPartDownloader.CalculateRanges(totalBytes, chunkCount).ToArray();
            Assert.Equal(chunkCount, ranges.Length);
            // check start and end
            Assert.Equal(expectedStart, ranges.First().Start);
            Assert.Equal(expectedEnd, ranges.Last().End);
            // check coverage and non-overlap
            long covered = 0;
            for (int i = 0; i < ranges.Length; i++)
            {
                var r = ranges[i];
                Assert.True(r.Start <= r.End);
                covered += (r.End - r.Start + 1);
                if (i > 0)
                {
                    Assert.Equal(ranges[i - 1].End + 1, r.Start);
                }
            }
            Assert.Equal(totalBytes, covered);
        }

        [Fact]
        public async Task MergeFiles_ProducesByteIdenticalFile()
        {
            // Arrange
            var part1 = Encoding.UTF8.GetBytes("Part 1 Data: Hello ");
            var part2 = Encoding.UTF8.GetBytes("Part 2 Data: World ");
            var part3 = Encoding.UTF8.GetBytes("Part 3 Data: End of file.");

            var expectedData = part1.Concat(part2).Concat(part3).ToArray();
            var expectedChecksum = Convert.ToHexString(SHA256.HashData(expectedData));

            var tempDir = Path.Combine(Path.GetTempPath(), "EDM_MergeTest_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            var p1File = Path.Combine(tempDir, "chunk_0.part");
            var p2File = Path.Combine(tempDir, "chunk_1.part");
            var p3File = Path.Combine(tempDir, "chunk_2.part");
            var mergedFile = Path.Combine(tempDir, "merged_output.bin");

            await File.WriteAllBytesAsync(p1File, part1);
            await File.WriteAllBytesAsync(p2File, part2);
            await File.WriteAllBytesAsync(p3File, part3);

            try
            {
                // Act - call public merge helper on MultiPartDownloader
                var downloader = new MultiPartDownloader();
                var chunkFiles = new[] { p1File, p2File, p3File };
                await downloader.MergeFilesAsync(chunkFiles, mergedFile, CancellationToken.None);

                // Assert
                File.Exists(mergedFile).Should().BeTrue();
                var actualData = await File.ReadAllBytesAsync(mergedFile);
                actualData.Should().Equal(expectedData);

                var actualChecksum = Convert.ToHexString(SHA256.HashData(actualData));
                actualChecksum.Should().Be(expectedChecksum);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task AdaptiveChunkSizer_AdaptsToNetworkType()
        {
            // Arrange
            var mockSettings = CreateMock<ISettingsService>();
            mockSettings.Setup(s => s.GetActiveBandwidthLimitKbps()).Returns(0);

            var mockFastNetwork = CreateMock<INetworkService>();
            mockFastNetwork.Setup(n => n.GetCurrentNetworkType()).Returns(NetworkType.Ethernet);

            var mockSlowNetwork = CreateMock<INetworkService>();
            mockSlowNetwork.Setup(n => n.GetCurrentNetworkType()).Returns(NetworkType.Cellular);

            var fastSizer = new AdaptiveChunkSizer(mockSettings.Object, mockFastNetwork.Object);
            var slowSizer = new AdaptiveChunkSizer(mockSettings.Object, mockSlowNetwork.Object);

            long fileSize = 100L * 1024 * 1024; // 100 MB

            // Act
            long fastChunk = await fastSizer.DetermineChunkSizeAsync("http://127.0.0.1/file.bin", fileSize, 4, CancellationToken.None);
            long slowChunk = await slowSizer.DetermineChunkSizeAsync("http://127.0.0.1/file.bin", fileSize, 4, CancellationToken.None);

            // Assert - Ethernet chunk size should be larger than Cellular chunk size
            fastChunk.Should().BeGreaterThan(slowChunk);
            fastChunk.Should().BeGreaterThan(0);
            slowChunk.Should().BeGreaterThan(0);
        }
    }
}
