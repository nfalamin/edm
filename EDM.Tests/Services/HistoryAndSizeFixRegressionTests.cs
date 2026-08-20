using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EDM.Helpers;
using EDM.Models;
using EDM.Services.History;
using Xunit;

namespace EDM.Tests.Services
{
    public class HistoryAndSizeFixRegressionTests : IDisposable
    {
        private readonly string _testDbPath;

        public HistoryAndSizeFixRegressionTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"edm_test_{Guid.NewGuid():N}.db");
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
                string shm = _testDbPath + "-shm";
                string wal = _testDbPath + "-wal";
                if (File.Exists(shm)) File.Delete(shm);
                if (File.Exists(wal)) File.Delete(wal);
            }
            catch { }
        }

        // ==========================================
        // PART A: SIZE PARSING & NORMALIZATION TESTS
        // ==========================================

        [Theory]
        [InlineData(null, SizeFormatter.UnknownSize)]
        [InlineData("", SizeFormatter.UnknownSize)]
        [InlineData("   ", SizeFormatter.UnknownSize)]
        [InlineData("-1", SizeFormatter.UnknownSize)]
        [InlineData("-1 B", SizeFormatter.UnknownSize)]
        [InlineData("Unknown", SizeFormatter.UnknownSize)]
        [InlineData("Calculating...", SizeFormatter.UnknownSize)]
        [InlineData("--", SizeFormatter.UnknownSize)]
        public void ParseToBytes_UnknownOrInvalidSizes_ReturnsUnknownConstant(string? input, long expected)
        {
            long parsed = SizeFormatter.ParseToBytes(input);
            Assert.Equal(expected, parsed);
        }

        [Theory]
        [InlineData("1024", 1024L)]
        [InlineData("1024 B", 1024L)]
        [InlineData("1 KB", 1024L)]
        [InlineData("10 KB", 10240L)]
        [InlineData("1 MB", 1048576L)]
        [InlineData("1.5 GB", 1610612736L)]
        public void ParseToBytes_ValidUnits_ParsesAccurately(string input, long expected)
        {
            long parsed = SizeFormatter.ParseToBytes(input);
            Assert.Equal(expected, parsed);
        }

        [Fact]
        public void FormatBytes_HandlesNegativeAndZeroWithoutProducingNegativeUI()
        {
            Assert.Equal("Unknown", SizeFormatter.FormatBytes(-1));
            Assert.Equal("0 B", SizeFormatter.FormatBytes(0));
            Assert.Equal("1 KB", SizeFormatter.FormatBytes(1024));
            Assert.Equal("1.0 MB", SizeFormatter.FormatBytes(1024 * 1024));
            Assert.Equal("1.50 GB", SizeFormatter.FormatBytes((long)(1.5 * 1024 * 1024 * 1024)));
        }

        // ==========================================
        // PART B & C: ASYNC INIT & CONCURRENCY
        // ==========================================

        [Fact]
        public async Task EnsureInitializedAsync_ConcurrentCalls_AreSafeAndExecuteOnce()
        {
            using var svc = new HistoryService(_testDbPath);

            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(svc.EnsureInitializedAsync());
            }

            await Task.WhenAll(tasks);
            int count = await svc.GetTotalCountAsync();
            Assert.Equal(0, count);
        }

        // ==========================================
        // PART D: VERIFICATION MESSAGE PARAMETER AUDIT
        // ==========================================

        [Fact]
        public async Task SaveHistoryAsync_StoresVerificationMessageAndTimestampIndependently()
        {
            using var svc = new HistoryService(_testDbPath);
            await svc.EnsureInitializedAsync();

            var expectedTime = new DateTime(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
            string expectedMsg = "SHA-256 integrity hash successfully validated against official manifest.";

            var items = new ObservableCollection<DownloadItem>
            {
                new DownloadItem
                {
                    Url = "https://example.com/release-v6.0.zip",
                    SavePath = Path.Combine(Path.GetTempPath(), "release-v6.0.zip"),
                    Size = "50 MB",
                    Status = "Completed",
                    VerificationState = VerificationState.Verified,
                    VerificationAlgorithm = "SHA256",
                    TrustedVerificationHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                    ComputedVerificationHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                    VerificationMessage = expectedMsg,
                    VerificationTimestamp = expectedTime
                }
            };

            await svc.SaveHistoryAsync(items);

            var loaded = await svc.LoadHistoryAsync();
            Assert.Single(loaded);

            var loadedItem = loaded[0];
            Assert.Equal(expectedMsg, loadedItem.VerificationMessage);
            Assert.NotNull(loadedItem.VerificationTimestamp);
            Assert.Equal(expectedTime, loadedItem.VerificationTimestamp.Value);
            Assert.Equal("50.0 MB", loadedItem.Size);
            Assert.Equal("Completed", loadedItem.Status);
            Assert.Equal(100.0, loadedItem.Progress);
        }

        // ==========================================
        // PART B: DEDUPLICATION TESTS
        // ==========================================

        [Fact]
        public async Task SaveDownloadAsync_DeduplicatesIdenticalDownloads_WithoutCreatingMultipleRows()
        {
            using var svc = new HistoryService(_testDbPath);
            await svc.EnsureInitializedAsync();

            string url = "https://example.com/video.mp4";
            string dest = Path.Combine(Path.GetTempPath(), "video.mp4");

            var item1 = new DownloadItem { Url = url, SavePath = dest, Size = "100 MB", Status = "Queued" };
            var item2 = new DownloadItem { Url = url, SavePath = dest, Size = "100 MB", Status = "Completed" };

            long id1 = await svc.SaveDownloadAsync(item1);
            long id2 = await svc.SaveDownloadAsync(item2);

            Assert.Equal(id1, id2); // Same record updated

            int total = await svc.GetTotalCountAsync();
            Assert.Equal(1, total);

            var list = await svc.LoadHistoryAsync();
            Assert.Single(list);
            Assert.Equal("Completed", list[0].Status);
            Assert.Equal(100.0, list[0].Progress);
        }

        // ==========================================
        // PART E: BATCH TRANSACTION WRITE PERFORMANCE
        // ==========================================

        [Fact]
        public async Task SaveHistoryAsync_BatchedTransaction_WritesMultipleItemsAtomically()
        {
            using var svc = new HistoryService(_testDbPath);
            await svc.EnsureInitializedAsync();

            var list = new ObservableCollection<DownloadItem>();
            for (int i = 0; i < 50; i++)
            {
                list.Add(new DownloadItem
                {
                    Url = $"https://example.com/file_{i}.bin",
                    SavePath = Path.Combine(Path.GetTempPath(), $"file_{i}.bin"),
                    Size = $"{i + 1} MB",
                    Status = i % 2 == 0 ? "Completed" : "Paused"
                });
            }

            await svc.SaveHistoryAsync(list);

            int count = await svc.GetTotalCountAsync();
            Assert.Equal(50, count);

            var loaded = await svc.LoadHistoryAsync();
            Assert.Equal(50, loaded.Count);
        }
    }
}
