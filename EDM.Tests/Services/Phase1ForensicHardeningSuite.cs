using System;
using System.IO;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using EDM.Services.Helpers;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class Phase1ForensicHardeningSuite
    {
        #region 1. Delete Lifecycle & Temporary File Cleanup Tests

        [Fact]
        public async Task CleanTemporaryFilesAsync_PurgesAllTempFilesAndSegmentFolders()
        {
            // Arrange
            string tempBase = Path.Combine(Path.GetTempPath(), "EDM_Delete_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempBase);

            try
            {
                string targetFile = Path.Combine(tempBase, "large_video.mp4");
                string singleTemp = targetFile + ".tmpdl";
                string metaFile = targetFile + ".edm.json";
                string tempVideo = targetFile + ".temp_video";
                string tempAudio = targetFile + ".temp_audio";

                string segmentDir = Path.Combine(tempBase, $".tmp_{Path.GetFileName(targetFile)}_segments");
                Directory.CreateDirectory(segmentDir);
                string seg1 = Path.Combine(segmentDir, "segment_0.part");
                string seg2 = Path.Combine(segmentDir, "segment_1.part");

                File.WriteAllText(singleTemp, "temp single download data");
                File.WriteAllText(metaFile, "{\"Url\": \"http://example.com/test\"}");
                File.WriteAllText(tempVideo, "video data");
                File.WriteAllText(tempAudio, "audio data");
                File.WriteAllText(seg1, "seg1 data");
                File.WriteAllText(seg2, "seg2 data");

                // Act
                await DownloadLifecycleManager.CleanTemporaryFilesAsync(targetFile);

                // Assert
                File.Exists(singleTemp).Should().BeFalse("single stream temp file should be deleted");
                File.Exists(metaFile).Should().BeFalse("metadata file should be deleted");
                File.Exists(tempVideo).Should().BeFalse("temp video file should be deleted");
                File.Exists(tempAudio).Should().BeFalse("temp audio file should be deleted");
                Directory.Exists(segmentDir).Should().BeFalse("segment directory should be purged");
            }
            finally
            {
                if (Directory.Exists(tempBase))
                {
                    try { Directory.Delete(tempBase, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task DeleteDownloadAsync_PreservesFinalFile_WhenDeleteFileFromDiskIsFalse()
        {
            // Arrange
            string tempBase = Path.Combine(Path.GetTempPath(), "EDM_Delete_Preserve_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempBase);

            try
            {
                string finalPath = Path.Combine(tempBase, "downloaded_file.zip");
                File.WriteAllText(finalPath, "complete payload content");

                var item = new DownloadItem
                {
                    FileName = "downloaded_file.zip",
                    SavePath = finalPath,
                    Status = "Completed"
                };

                // Act
                await DownloadLifecycleManager.Instance.DeleteDownloadAsync(item, deleteFileFromDisk: false);

                // Assert
                File.Exists(finalPath).Should().BeTrue("final file must NOT be deleted when deleteFileFromDisk is false");
            }
            finally
            {
                if (Directory.Exists(tempBase))
                {
                    try { Directory.Delete(tempBase, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task DeleteDownloadAsync_DeletesFinalFile_WhenDeleteFileFromDiskIsTrue()
        {
            // Arrange
            string tempBase = Path.Combine(Path.GetTempPath(), "EDM_Delete_Disk_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempBase);

            try
            {
                string finalPath = Path.Combine(tempBase, "downloaded_file.zip");
                File.WriteAllText(finalPath, "complete payload content");

                var item = new DownloadItem
                {
                    FileName = "downloaded_file.zip",
                    SavePath = finalPath,
                    Status = "Completed"
                };

                // Act
                await DownloadLifecycleManager.Instance.DeleteDownloadAsync(item, deleteFileFromDisk: true);

                // Assert
                File.Exists(finalPath).Should().BeFalse("final file MUST be deleted when deleteFileFromDisk is true");
            }
            finally
            {
                if (Directory.Exists(tempBase))
                {
                    try { Directory.Delete(tempBase, true); } catch { }
                }
            }
        }

        #endregion

        #region 2. Real Connection Accounting Tests

        [Fact]
        public void ConnectionAccountant_NeverPermitsNegativeOrDriftingCounters()
        {
            // Arrange
            var accountant = new ConnectionAccountant(configuredMaxConnections: 8);
            accountant.SetRequestedConnections(8);

            // Act: request, start, and complete connections
            accountant.OnConnectionRequested();
            accountant.OnConnectionRequested();
            accountant.StartingConnections.Should().Be(2);

            accountant.OnConnectionStarted();
            accountant.ActiveConnections.Should().Be(1);
            accountant.StartingConnections.Should().Be(1);

            accountant.OnConnectionStarted();
            accountant.ActiveConnections.Should().Be(2);
            accountant.StartingConnections.Should().Be(0);

            // Complete connections
            accountant.OnConnectionCompleted();
            accountant.ActiveConnections.Should().Be(1);

            accountant.OnConnectionCompleted();
            accountant.ActiveConnections.Should().Be(0);

            // Extra decrement should NOT drop below 0
            accountant.OnConnectionCompleted();
            accountant.ActiveConnections.Should().Be(0, "ActiveConnections must never drop below 0");
        }

        [Fact]
        public void ConnectionAccountant_TracksErrorsAndSnapshotsCorrectly()
        {
            // Arrange
            var accountant = new ConnectionAccountant(16);
            accountant.SetRequestedConnections(8);

            accountant.OnConnectionRequested();
            accountant.OnConnectionStarted();
            accountant.OnConnectionFailed(new System.Net.Http.HttpRequestException("Too many requests", null, System.Net.HttpStatusCode.TooManyRequests));

            accountant.OnConnectionRequested();
            accountant.OnConnectionStarted();
            accountant.OnConnectionFailed(new System.Net.Http.HttpRequestException("Internal server error", null, System.Net.HttpStatusCode.InternalServerError));

            accountant.OnConnectionRequested();
            accountant.OnConnectionStarted();
            accountant.OnConnectionFailed(new TimeoutException("Socket timed out"));

            // Assert snapshot
            var snap = accountant.GetSnapshot(queuedSegments: 5, runningSegments: 2, completedSegments: 10);
            snap.Http429Count.Should().Be(1);
            snap.Http5xxCount.Should().Be(1);
            snap.TimeoutCount.Should().Be(1);
            snap.TotalErrors.Should().Be(3);
            snap.QueuedSegments.Should().Be(5);
            snap.RunningSegments.Should().Be(2);
            snap.CompletedSegments.Should().Be(10);
        }

        #endregion

        #region 3. Real RTT & Network Telemetry Measurement Tests

        [Fact]
        public void ConnectionAccountant_CalculatesRollingRttWithoutHardcodedConstants()
        {
            var accountant = new ConnectionAccountant(8);

            // Initial sample
            accountant.RecordNetworkMetrics(rttMs: 120.0, ttfbMs: 80.0);
            accountant.MeasuredRttMs.Should().Be(120.0);
            accountant.TimeToFirstByteMs.Should().Be(80.0);

            // Second sample with EWMA smoothing
            accountant.RecordNetworkMetrics(rttMs: 60.0, ttfbMs: 40.0);
            // EWMA: 0.3 * 60 + 0.7 * 120 = 18 + 84 = 102
            accountant.MeasuredRttMs.Should().BeApproximately(102.0, 0.01);
            // TTFB EWMA: 0.3 * 40 + 0.7 * 80 = 12 + 56 = 68
            accountant.TimeToFirstByteMs.Should().BeApproximately(68.0, 0.01);
        }

        #endregion

        #region 4. Single Authoritative Filename Resolution Tests

        [Fact]
        public void ResolveAuthoritativeFileName_FollowsStrictPrecedence()
        {
            var uri = new Uri("http://example.com/files/remote_file.zip?token=123");
            var cd = new ContentDispositionHeaderValue("attachment") { FileName = "header_file.zip" };

            // 1. Explicit user name takes top precedence
            string name1 = FileNamingHelper.ResolveAuthoritativeFileName("custom_user_name.zip", cd, "Media Title", "application/zip", uri);
            name1.Should().Be("custom_user_name.zip");

            // 2. Content-Disposition takes precedence over media title and URL
            string name2 = FileNamingHelper.ResolveAuthoritativeFileName(null, cd, "Media Title", "application/zip", uri);
            name2.Should().Be("header_file.zip");

            // 3. Media Title takes precedence over URL
            string name3 = FileNamingHelper.ResolveAuthoritativeFileName(null, null, "My Presentation Video", "video/mp4", uri);
            name3.Should().Be("My Presentation Video.mp4");

            // 4. URL path segment is used if no CD or media title
            string name4 = FileNamingHelper.ResolveAuthoritativeFileName(null, null, null, null, uri);
            name4.Should().Be("remote_file.zip");

            // 5. Fallback if URL has no filename
            var rootUri = new Uri("http://example.com/");
            string name5 = FileNamingHelper.ResolveAuthoritativeFileName(null, null, null, "video/mp4", rootUri);
            name5.Should().Be("download.mp4");
        }

        [Theory]
        [InlineData("CON.txt", "CON_file.txt")]
        [InlineData("PRN.pdf", "PRN_file.pdf")]
        [InlineData("aux.mp4", "aux_file.mp4")]
        [InlineData("NUL", "NUL_file")]
        [InlineData("com1.zip", "com1_file.zip")]
        [InlineData("LPT9.bin", "LPT9_file.bin")]
        public void SanitizeFileName_GuardsWindowsReservedDeviceNames(string input, string expected)
        {
            string sanitized = FileNamingHelper.SanitizeFileName(input);
            sanitized.Should().Be(expected);
        }

        [Fact]
        public void SanitizeFileName_StripsDirectoryTraversalAndInvalidChars()
        {
            // Traversal & invalid chars
            string raw = @"..\..\..\etc\passwd<>:\""|?*test.dat... ";
            string sanitized = FileNamingHelper.SanitizeFileName(raw);

            sanitized.Should().NotContain("..");
            sanitized.Should().NotContain("<");
            sanitized.Should().NotContain(">");
            sanitized.Should().NotContain(":");
            sanitized.Should().NotContain("\"");
            sanitized.Should().NotContain("|");
            sanitized.Should().NotContain("?");
            sanitized.Should().NotContain("*");
            sanitized.EndsWith(".").Should().BeFalse("trailing dots must be removed");
            sanitized.EndsWith(" ").Should().BeFalse("trailing spaces must be removed");
        }

        [Theory]
        [InlineData("video.mp4.mp4", "video.mp4")]
        [InlineData("archive.zip.zip", "archive.zip")]
        [InlineData("document.pdf", "document.pdf")]
        public void DeduplicateExtension_RemovesDuplicateExtensions(string input, string expected)
        {
            string deduplicated = FileNamingHelper.DeduplicateExtension(input);
            deduplicated.Should().Be(expected);
        }

        [Fact]
        public void DetermineFileNameFromHeaders_DecodesRfc5987Utf8Filenames()
        {
            var cd = new ContentDispositionHeaderValue("attachment");
            cd.Parameters.Add(new NameValueHeaderValue("filename*", "UTF-8''Special%20Report%202026.pdf"));

            string result = FileNamingHelper.DetermineFileNameFromHeaders(cd, "application/pdf", new Uri("http://example.com/file"));
            result.Should().Be("Special Report 2026.pdf");
        }

        #endregion

        #region 5. Robust Json Parsing in UrlMetadataService Tests

        [Fact]
        public void UrlMetadataService_ParsesFullJsonMetadataWithoutStringSlicing()
        {
            string sampleJson = """
            {
                "title": "Quantum Computing Explained in 10 Minutes",
                "duration": 605,
                "formats": [
                    { "ext": "mp4", "height": 1080 },
                    { "ext": "mp4", "height": 720 },
                    { "ext": "webm", "height": 2160 }
                ]
            }
            """;

            var ytDlpMock = new YtDlpService();
            var settingsMock = new SettingsService();
            var metaService = new UrlMetadataService(ytDlpMock, settingsMock);

            // Reflection invocation of private ParseMetadata
            var method = typeof(UrlMetadataService).GetMethod("ParseMetadata", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Should().NotBeNull();

            var result = method!.Invoke(metaService, new object[] { sampleJson, "https://youtube.com/watch?v=123" }) as UrlMetadataService.VideoMetadata;

            result.Should().NotBeNull();
            result!.Title.Should().Be("Quantum Computing Explained in 10 Minutes");
            result.DurationSeconds.Should().Be(605);
            result.AvailableFormats.Should().Contain("mp4");
            result.AvailableFormats.Should().Contain("webm");
            result.MaxResolution.Should().Be("2160p");
        }

        #endregion

        #region 6. Authoritative Progress Mathematics Tests

        [Theory]
        [InlineData(1000, 500, 50.0)]
        [InlineData(1000, 1000, 100.0)]
        [InlineData(1000, 0, 0.0)]
        [InlineData(0, 500, 0.0)]
        public void DownloadProgress_CalculatesExactMathematicalPercentages(long total, long downloaded, double expectedPct)
        {
            var dp = new DownloadProgress(total, downloaded);
            dp.Percentage.Should().BeApproximately(expectedPct, 0.001);
        }

        #endregion
    }
}
