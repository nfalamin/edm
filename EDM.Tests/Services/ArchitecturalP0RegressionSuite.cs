using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EDM.Models;
using EDM.NativeMessaging;
using EDM.Services;
using EDM.Services.History;
using Xunit;

namespace EDM.Tests.Services
{
    [Collection("LocalizationTestCollection")]
    public class ArchitecturalP0RegressionSuite : IDisposable
    {
        private readonly string _tempDbPath;
        private readonly HistoryService _historyService;
        private readonly DownloadMetricsService _metricsService;

        public ArchitecturalP0RegressionSuite()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"edm_p0_test_{Guid.NewGuid():N}.db");
            _historyService = new HistoryService(_tempDbPath);
            _metricsService = new DownloadMetricsService(_historyService);
        }

        public void Dispose()
        {
            _historyService.Dispose();
            try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
        }

        #region 1. DATABASE SOURCE OF TRUTH & METRICS SEMANTICS

        [Fact]
        public async Task Test01_FreshDatabase_AllMetricsAreZero()
        {
            var snapshot = await _metricsService.RefreshMetricsAsync();

            Assert.Equal(0, snapshot.TotalDownloadsCount);
            Assert.Equal(0, snapshot.ActiveDownloadsCount);
            Assert.Equal(0, snapshot.CompletedDownloadsCount);
            Assert.Equal(0L, snapshot.TotalDownloadedBytes);
            Assert.Equal("0 B", snapshot.TotalSizeDownloadedFormatted);
        }

        [Fact]
        public async Task Test02_CompletedDownload_UpdatesHistoricalMetricsCorrectly()
        {
            var items = new List<DownloadItem>
            {
                new DownloadItem
                {
                    FileName = "Ubuntu-24.04.iso",
                    Url = "https://releases.ubuntu.com/24.04/ubuntu-24.04-desktop-amd64.iso",
                    SavePath = @"C:\Downloads\ubuntu.iso",
                    Size = "4.2 GB",
                    TotalBytes = 4509715660L,
                    DownloadedBytes = 4509715660L,
                    Status = "Completed",
                    Progress = 100.0
                }
            };

            await _historyService.SaveHistoryAsync(items);
            var snapshot = await _metricsService.RefreshMetricsAsync();

            Assert.Equal(1, snapshot.TotalDownloadsCount);
            Assert.Equal(1, snapshot.CompletedDownloadsCount);
            Assert.Equal(4509715660L, snapshot.TotalDownloadedBytes);
            Assert.Contains("4.2", snapshot.TotalSizeDownloadedFormatted);
        }

        [Fact]
        public async Task Test03_FailedDownload_CountsAsRecord_TransferredBytesContribute_NotCompleted()
        {
            var items = new List<DownloadItem>
            {
                new DownloadItem
                {
                    FileName = "LargeArchive.zip",
                    Url = "https://example.org/large.zip",
                    SavePath = @"C:\Downloads\large.zip",
                    Size = "100 MB",
                    TotalBytes = 104857600L,
                    DownloadedBytes = 28311552L, // 27 MB transferred before server error
                    Status = "Error",
                    Progress = 27.0
                }
            };

            await _historyService.SaveHistoryAsync(items);
            var snapshot = await _metricsService.RefreshMetricsAsync();

            Assert.Equal(1, snapshot.TotalDownloadsCount);
            Assert.Equal(0, snapshot.CompletedDownloadsCount);
            Assert.Equal(28311552L, snapshot.TotalDownloadedBytes); // Real transferred bytes recorded
        }

        [Fact]
        public async Task Test04_PausedDownload_PreservesTransferredBytes()
        {
            var items = new List<DownloadItem>
            {
                new DownloadItem
                {
                    FileName = "GamePatch.exe",
                    Url = "https://patch.game.net/patch.exe",
                    SavePath = @"C:\Downloads\patch.exe",
                    Size = "500 MB",
                    TotalBytes = 524288000L,
                    DownloadedBytes = 188743680L, // 180 MB
                    Status = "Paused",
                    Progress = 36.0
                }
            };

            await _historyService.SaveHistoryAsync(items);
            var snapshot = await _metricsService.RefreshMetricsAsync();

            Assert.Equal(1, snapshot.TotalDownloadsCount);
            Assert.Equal(0, snapshot.CompletedDownloadsCount);
            Assert.Equal(188743680L, snapshot.TotalDownloadedBytes);
        }

        [Fact]
        public async Task Test05_ResumedDownload_NeverDoubleCountsBytes()
        {
            // Session 1: Downloaded 40 MB of 100 MB, then paused
            var item = new DownloadItem
            {
                FileName = "Kernel_Sources.tar.gz",
                Url = "https://kernel.org/source.tar.gz",
                SavePath = @"C:\Downloads\source.tar.gz",
                Size = "100 MB",
                TotalBytes = 104857600L,
                DownloadedBytes = 41943040L, // 40 MB
                Status = "Paused",
                Progress = 40.0
            };
            await _historyService.SaveHistoryAsync(new List<DownloadItem> { item });

            var snap1 = await _metricsService.RefreshMetricsAsync();
            Assert.Equal(41943040L, snap1.TotalDownloadedBytes);

            // Session 2: Resumed and completed (total 100 MB, NOT 40 + 100 = 140 MB)
            item.DownloadedBytes = 104857600L;
            item.Status = "Completed";
            item.Progress = 100.0;
            await _historyService.SaveHistoryAsync(new List<DownloadItem> { item });

            var snap2 = await _metricsService.RefreshMetricsAsync();
            Assert.Equal(1, snap2.TotalDownloadsCount);
            Assert.Equal(1, snap2.CompletedDownloadsCount);
            Assert.Equal(104857600L, snap2.TotalDownloadedBytes); // Exact 100 MB, no double count
        }

        [Fact]
        public async Task Test06_DeleteUiItemWithoutDatabaseDelete_MetricsRemainAuthoritative()
        {
            var item = new DownloadItem
            {
                FileName = "Document.pdf",
                Url = "https://docs.net/manual.pdf",
                SavePath = @"C:\Downloads\manual.pdf",
                Size = "10 MB",
                TotalBytes = 10485760L,
                DownloadedBytes = 10485760L,
                Status = "Completed",
                Progress = 100.0
            };
            await _historyService.SaveHistoryAsync(new List<DownloadItem> { item });

            // UI removes the row from its view collection
            var uiCollection = new List<DownloadItem> { item };
            uiCollection.Clear();

            // Database remains source of truth
            var snapshot = await _metricsService.RefreshMetricsAsync();
            Assert.Equal(1, snapshot.TotalDownloadsCount);
            Assert.Equal(1, snapshot.CompletedDownloadsCount);
            Assert.Equal(10485760L, snapshot.TotalDownloadedBytes);
        }

        [Fact]
        public async Task Test07_DeleteHistoryRecordFromDatabase_UpdatesMetricsImmediately()
        {
            var item = new DownloadItem
            {
                FileName = "TemporaryFile.tmp",
                Url = "https://cdn.net/temp.tmp",
                SavePath = @"C:\Downloads\temp.tmp",
                Size = "50 MB",
                TotalBytes = 52428800L,
                DownloadedBytes = 52428800L,
                Status = "Completed",
                Progress = 100.0
            };
            await _historyService.SaveHistoryAsync(new List<DownloadItem> { item });

            // Verify added
            var snap1 = await _metricsService.RefreshMetricsAsync();
            Assert.Equal(1, snap1.TotalDownloadsCount);

            // Delete from database
            bool deleted = await _historyService.DeleteHistoryItemAsync(item.Url, item.SavePath);
            Assert.True(deleted);

            // Verify metrics updated
            var snap2 = await _metricsService.RefreshMetricsAsync();
            Assert.Equal(0, snap2.TotalDownloadsCount);
            Assert.Equal(0L, snap2.TotalDownloadedBytes);
            Assert.Equal("0 B", snap2.TotalSizeDownloadedFormatted);
        }

        [Fact]
        public async Task Test08_RestartApplication_RestoresAuthoritativeMetricsFromDatabase()
        {
            // Simulate session 1 writing downloads
            await _historyService.SaveHistoryAsync(new List<DownloadItem>
            {
                new DownloadItem { FileName = "AppSetup.msi", Url = "https://app.com/setup.msi", SavePath = @"C:\Setup.msi", Size = "20 MB", TotalBytes = 20971520L, DownloadedBytes = 20971520L, Status = "Completed" },
                new DownloadItem { FileName = "Music.mp3", Url = "https://audio.com/track.mp3", SavePath = @"C:\track.mp3", Size = "5 MB", TotalBytes = 5242880L, DownloadedBytes = 2621440L, Status = "Paused", Progress = 50.0 }
            });

            // Simulate application restart with new service instance pointing to same DB
            using var restartedHistory = new HistoryService(_tempDbPath);
            var restartedMetrics = new DownloadMetricsService(restartedHistory);

            var snapshot = await restartedMetrics.RefreshMetricsAsync();
            Assert.Equal(2, snapshot.TotalDownloadsCount);
            Assert.Equal(1, snapshot.CompletedDownloadsCount);
            Assert.Equal(20971520L + 2621440L, snapshot.TotalDownloadedBytes);
        }

        [Fact]
        public async Task Test09_UnknownTotalSize_NeverProducesNegativeSize()
        {
            var item = new DownloadItem
            {
                FileName = "LiveStream.ts",
                Url = "https://stream.net/live.ts",
                SavePath = @"C:\live.ts",
                Size = "Unknown",
                TotalBytes = -1L,
                DownloadedBytes = 1048576L,
                Status = "Downloading"
            };
            await _historyService.SaveHistoryAsync(new List<DownloadItem> { item });

            var snapshot = await _metricsService.RefreshMetricsAsync();
            Assert.True(snapshot.TotalDownloadedBytes >= 0);
            Assert.DoesNotContain("-", snapshot.TotalSizeDownloadedFormatted);
        }

        #endregion

        #region 2. DYNAMIC SEGMENT SCHEDULER & REBALANCING

        [Fact]
        public void Test10_SegmentScheduler_InitializesCoverageWithoutGapsOrOverlaps()
        {
            long totalBytes = 100 * 1024 * 1024; // 100 MB
            var scheduler = new SegmentScheduler(totalBytes, 2 * 1024 * 1024);
            scheduler.InitializeDefault(8);

            Assert.Equal(8, scheduler.Segments.Count);
            Assert.True(scheduler.ValidateCoverage());
            Assert.Equal(0, scheduler.Segments[0].Start);
            Assert.Equal(totalBytes - 1, scheduler.Segments.Last().End);
        }

        [Fact]
        public void Test11_SegmentScheduler_DynamicSplit_PreservesExactTotalCoverage()
        {
            long totalBytes = 50 * 1024 * 1024; // 50 MB
            var scheduler = new SegmentScheduler(totalBytes, 2 * 1024 * 1024);
            scheduler.InitializeDefault(2);

            // Worker 1 takes segment 0
            var w1 = scheduler.GetNextWorkItem("worker-1");
            Assert.NotNull(w1);
            Assert.Equal(0, w1!.Id);

            // Worker 2 takes segment 1
            var w2 = scheduler.GetNextWorkItem("worker-2");
            Assert.NotNull(w2);
            Assert.Equal(1, w2!.Id);

            // Worker 1 finishes segment 0
            scheduler.ReportProgress(0, w1.TotalBytes);
            scheduler.MarkCompleted(0);

            // Worker 1 is now idle -> steals remaining half from slow segment 1
            var stolenWork = scheduler.GetNextWorkItem("worker-1");
            Assert.NotNull(stolenWork);
            Assert.True(stolenWork!.TotalBytes > 0);

            // Verify invariant: mathematical coverage is preserved with zero gaps or overlaps
            Assert.True(scheduler.ValidateCoverage());
            Assert.Equal(3, scheduler.Segments.Count);
        }

        [Fact]
        public void Test12_SegmentScheduler_CompletionState_RequiresAllSegmentsCompleted()
        {
            long totalBytes = 10 * 1024 * 1024;
            var scheduler = new SegmentScheduler(totalBytes);
            scheduler.InitializeDefault(2);

            var s0 = scheduler.Segments[0];
            var s1 = scheduler.Segments[1];

            scheduler.MarkCompleted(s0.Id);
            Assert.False(scheduler.IsFullyCompleted());

            scheduler.MarkCompleted(s1.Id);
            Assert.True(scheduler.IsFullyCompleted());
        }

        #endregion

        #region 3. BROWSER MESSAGE PROTOCOL & NATIVE CONTRACTS

        [Theory]
        [InlineData("download_url")]
        [InlineData("START_DOWNLOAD")]
        [InlineData("START_EDM_DOWNLOAD")]
        [InlineData("intercept")]
        public void Test13_NativeMessageRequest_NormalizesAllDownloadActionVariants(string rawAction)
        {
            var req = new NativeMessageRequest
            {
                Action = rawAction,
                Url = "https://cdn.example.org/installer.exe",
                RequestId = "req_12345"
            };

            Assert.Equal(NativeActionNames.StartDownload, req.GetEffectiveAction());
        }

        [Theory]
        [InlineData("get_media_streams")]
        [InlineData("GET_MEDIA_STREAMS")]
        public void Test14_NativeMessageRequest_NormalizesMediaStreamActionVariants(string rawAction)
        {
            var req = new NativeMessageRequest
            {
                Action = rawAction,
                Url = "https://youtube.com/watch?v=sample123",
                RequestId = "req_67890"
            };

            Assert.Equal(NativeActionNames.GetMediaStreams, req.GetEffectiveAction());
        }

        [Fact]
        public void Test15_NativeMessageResponse_CorrelatesRequestIdAndSuccessStatus()
        {
            var response = new NativeMessageResponse
            {
                Success = true,
                Action = NativeActionNames.StartDownload,
                RequestId = "correlation_token_99",
                Status = "handed_off"
            };

            Assert.True(response.Success);
            Assert.Equal("correlation_token_99", response.RequestId);
            Assert.Equal("handed_off", response.Status);
        }

        #endregion
    }
}
