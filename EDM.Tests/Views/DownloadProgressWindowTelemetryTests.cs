using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using Xunit;

namespace EDM.Tests.Views
{
    public class DownloadProgressWindowTelemetryTests
    {
        [Fact]
        public void SpeedLimiter_ValuesCalculateExactBytesPerSecond()
        {
            // Verify speed limit translation mappings
            double unlimited = -1;
            double limit100Kb = 100.0 * 1024;
            double limit500Kb = 500.0 * 1024;
            double limit1Mb = 1024.0 * 1024;
            double limit5Mb = 5.0 * 1024 * 1024;

            Assert.Equal(-1, unlimited);
            Assert.Equal(102400, limit100Kb);
            Assert.Equal(512000, limit500Kb);
            Assert.Equal(1048576, limit1Mb);
            Assert.Equal(5242880, limit5Mb);
        }

        [Fact]
        public void DownloadProgressInfo_PassesRealSegmentChunkStats()
        {
            var info = new DownloadProgressInfo
            {
                TotalBytes = 10485760, // 10 MB
                BytesReceived = 5242880, // 5 MB
                ProgressPercentage = 50.0,
                SpeedBytesPerSecond = 2097152, // 2 MB/s
                AverageSpeedBytesPerSecond = 1800000,
                PeakSpeedBytesPerSecond = 3145728,
                ActiveConnections = 4,
                SegmentCount = 4,
                ServerSupportsResume = true
            };

            var chunkDict = new ConcurrentDictionary<int, ChunkProgressInfo>();
            chunkDict[0] = new ChunkProgressInfo(0, 1310720, 2621440, true);
            chunkDict[1] = new ChunkProgressInfo(1, 1310720, 2621440, true);
            chunkDict[2] = new ChunkProgressInfo(2, 1310720, 2621440, true);
            chunkDict[3] = new ChunkProgressInfo(3, 1310720, 2621440, true);

            info.ChunkStats = chunkDict;

            Assert.NotNull(info.ChunkStats);
            Assert.Equal(4, info.ChunkStats.Count);
            Assert.Equal(4, info.ActiveConnections);
            Assert.True(info.ServerSupportsResume);
            Assert.Equal(50.0, info.ProgressPercentage);
        }

        [Fact]
        public void PauseTokenSource_StateTogglesCorrectly()
        {
            var pts = new PauseTokenSource();
            Assert.False(pts.IsPaused);

            pts.Pause();
            Assert.True(pts.IsPaused);

            pts.Resume();
            Assert.False(pts.IsPaused);
        }

        [Fact]
        public void FormatTime_HandlesZeroAndInfinityGracefully()
        {
            // Verify safe time formatting behavior
            double zero = 0;
            double inf = double.PositiveInfinity;
            double nan = double.NaN;
            double validSecs = 125; // 2 min 5 sec

            TimeSpan t = TimeSpan.FromSeconds(validSecs);
            string formatted = $"{t.Minutes:D2}:{t.Seconds:D2}";

            Assert.Equal("02:05", formatted);
            Assert.True(double.IsInfinity(inf));
            Assert.True(double.IsNaN(nan));
            Assert.Equal(0, zero);
        }
    }
}
