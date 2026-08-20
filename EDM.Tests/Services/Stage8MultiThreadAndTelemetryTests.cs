using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using EDM.Models;
using EDM.Services;
using Xunit;

namespace EDM.Tests.Services
{
    public class Stage8MultiThreadAndTelemetryTests
    {
        [Fact]
        public void FileTypeDetector_MagicBytes_IdentifiesCorrectTypes()
        {
            // PE Executable "MZ"
            byte[] exeHeader = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00 };
            Assert.Equal(DetectedFileType.Programs, FileTypeDetector.DetectFromMagicBytes(exeHeader));

            // ZIP "PK\x03\x04"
            byte[] zipHeader = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 };
            Assert.Equal(DetectedFileType.Compressed, FileTypeDetector.DetectFromMagicBytes(zipHeader));

            // PDF "%PDF"
            byte[] pdfHeader = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 };
            Assert.Equal(DetectedFileType.Documents, FileTypeDetector.DetectFromMagicBytes(pdfHeader));

            // MP4 "ftyp" at offset 4
            byte[] mp4Header = new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70 };
            Assert.Equal(DetectedFileType.Video, FileTypeDetector.DetectFromMagicBytes(mp4Header));

            // MP3 "ID3"
            byte[] mp3Header = new byte[] { 0x49, 0x44, 0x33, 0x03, 0x00, 0x00 };
            Assert.Equal(DetectedFileType.Audio, FileTypeDetector.DetectFromMagicBytes(mp3Header));
        }

        [Fact]
        public void FileTypeDetector_MultiSignal_ResolvesPriorityCorrectly()
        {
            // Even if filename says "data.bin", if MIME is "video/mp4", it classifies as Video
            var typeFromMime = FileTypeDetector.DetectFromSignals("data.bin", contentType: "video/mp4");
            Assert.Equal(DetectedFileType.Video, typeFromMime);

            // Magic bytes override generic MIME
            byte[] exeHeader = new byte[] { 0x4D, 0x5A, 0x00, 0x00 };
            var typeFromMagic = FileTypeDetector.DetectFromSignals("setup.dat", contentType: "application/octet-stream", headerBytes: exeHeader);
            Assert.Equal(DetectedFileType.Programs, typeFromMagic);

            // Extension fallback
            var typeFromExt = FileTypeDetector.DetectFromSignals("archive.7z");
            Assert.Equal(DetectedFileType.Compressed, typeFromExt);
        }

        [Fact]
        public void DownloadCategoryRouter_IntegratesFileTypeDetector()
        {
            var router = DownloadCategoryRouter.Instance;

            // Video from MIME
            var cat1 = router.DetermineCategory("download.bin", contentType: "video/webm");
            Assert.Equal("Video", cat1.Name);

            // Compressed from magic bytes
            byte[] zipHeader = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
            var cat2 = router.DetermineCategory("unknown_payload", contentType: null, url: null, headerBytes: zipHeader);
            Assert.Equal("Compressed", cat2.Name);

            // Documents from extension
            var cat3 = router.DetermineCategory("report.pdf");
            Assert.Equal("Documents", cat3.Name);
        }

        [Fact]
        public void SegmentProgress_CalculatesOverallFromAuthoritativeBytes_NotPercentageAverage()
        {
            // Segment 1: 100 MB / 100 MB (100%)
            // Segment 2: 10 MB / 1000 MB (1%)
            // Total: 110 MB / 1100 MB = 10.0%
            // Note: If percentage average was used: (100% + 1%) / 2 = 50.5% (which is incorrect!)

            var chunkStats = new ConcurrentDictionary<int, ChunkProgressInfo>();
            chunkStats[0] = new ChunkProgressInfo(0, 100 * 1024 * 1024, 100 * 1024 * 1024, false);
            chunkStats[1] = new ChunkProgressInfo(1, 10 * 1024 * 1024, 1000 * 1024 * 1024, true);

            long totalDownloaded = chunkStats.Values.Sum(c => c.Downloaded);
            long totalBytes = 1100 * 1024 * 1024;

            var dp = new DownloadProgress(totalBytes, totalDownloaded, chunkStats);

            Assert.Equal(110 * 1024 * 1024, dp.BytesDownloaded);
            Assert.Equal(10.0, dp.Percentage, precision: 1);
        }

        [Fact]
        public void DownloadProgressInfo_PreservesAuthoritativeSegmentTelemetry()
        {
            var info = new DownloadProgressInfo
            {
                TotalBytes = 1024 * 1024 * 100, // 100 MB
                BytesReceived = 1024 * 1024 * 50, // 50 MB
                ProgressPercentage = 50.0,
                SpeedBytesPerSecond = 10 * 1024 * 1024, // 10 MB/s
                AverageSpeedBytesPerSecond = 8 * 1024 * 1024,
                PeakSpeedBytesPerSecond = 12 * 1024 * 1024,
                RemainingSeconds = 5.0,
                ActiveConnections = 4,
                ServerSupportsResume = true,
                Status = "Segmented Downloading..."
            };

            Assert.Equal(50.0, info.ProgressPercentage);
            Assert.Equal(5.0, info.RemainingSeconds);
            Assert.Equal(4, info.ActiveConnections);
            Assert.True(info.ServerSupportsResume);
        }
    }
}
