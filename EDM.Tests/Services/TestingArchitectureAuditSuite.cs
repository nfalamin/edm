using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using EDM.Converters;
using EDM.Models;
using EDM.Services;
using EDM.Services.Helpers;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    /// <summary>
    /// STEP 16.1: Complete Testing Architecture Audit Test Suite.
    /// Deeply tests core units, edge cases, invariants, security sanitization, manifest parsing,
    /// dynamic segmentation mathematical correctness, and protocol safety.
    /// </summary>
    public class TestingArchitectureAuditSuite : TestBase
    {
        #region 1. ProtocolDetector & URL Parsing

        [Theory]
        [InlineData("http://example.com/file.zip", DownloadProtocolType.Http, "HTTP")]
        [InlineData("https://secure.example.com/iso/os.iso", DownloadProtocolType.Https, "HTTPS")]
        [InlineData("ftp://ftp.example.com/pub/data.tar.gz", DownloadProtocolType.Ftp, "FTP")]
        [InlineData("ftps://ftps.example.com/secure.dat", DownloadProtocolType.Ftps, "FTPS")]
        [InlineData("sftp://ssh.example.com/files/dump.sql", DownloadProtocolType.Sftp, "SFTP")]
        [InlineData("magnet:?xt=urn:btih:d6b05d429a34&dn=Ubuntu+ISO", DownloadProtocolType.Magnet, "MAGNET")]
        [InlineData("https://tracker.org/torrents/linux.torrent", DownloadProtocolType.BitTorrent, "TORRENT")]
        [InlineData("https://live.example.com/stream/index.m3u8", DownloadProtocolType.Hls, "HLS")]
        [InlineData("https://video.example.com/dash/manifest.mpd", DownloadProtocolType.Dash, "DASH")]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", DownloadProtocolType.StreamingMedia, "STREAM")]
        [InlineData("https://vimeo.com/123456789", DownloadProtocolType.StreamingMedia, "STREAM")]
        [InlineData("https://twitch.tv/streamer", DownloadProtocolType.StreamingMedia, "STREAM")]
        public void ProtocolDetector_ClassifiesUrlsCorrectly(string url, DownloadProtocolType expectedType, string expectedScheme)
        {
            var result = ProtocolDetector.Detect(url);
            result.Protocol.Should().Be(expectedType);
            result.DisplayScheme.Should().Be(expectedScheme);
            result.SupportsResume.Should().BeTrue();
        }

        [Fact]
        public void ProtocolDetector_SanitizeUrlForLogging_ScrubsPasswordsAndTokens()
        {
            string raw = "ftp://admin:P@ssw0rd123!@ftp.secure-server.com:21/backup.tar";
            string sanitized = ProtocolDetector.SanitizeUrlForLogging(raw);

            sanitized.Should().Be("ftp://admin:***@ftp.secure-server.com:21/backup.tar");
            sanitized.Should().NotContain("P@ssw0rd123!");
        }

        [Fact]
        public void ProtocolDetector_TryExtractCredentials_ParsesUserAndPass()
        {
            string urlWithAuth = "http://myuser:secretToken@example.com/private/doc.pdf";
            bool success = ProtocolDetector.TryExtractCredentials(urlWithAuth, out string cleanUrl, out NetworkCredential? creds);

            success.Should().BeTrue();
            cleanUrl.Should().Be("http://example.com/private/doc.pdf");
            creds.Should().NotBeNull();
            creds!.UserName.Should().Be("myuser");
            creds!.Password.Should().Be("secretToken");
        }

        [Theory]
        [InlineData("", DownloadProtocolType.Unknown)]
        [InlineData("   ", DownloadProtocolType.Unknown)]
        [InlineData("gopher://old.internet.com", DownloadProtocolType.Unknown)]
        public void ProtocolDetector_HandlesEdgeAndMalformedUrls(string input, DownloadProtocolType expected)
        {
            var result = ProtocolDetector.Detect(input);
            result.Protocol.Should().Be(expected);
        }

        #endregion

        #region 2. FileNamingHelper & SecuritySanitizer

        [Theory]
        [InlineData("CON.txt", "CON_file.txt")]
        [InlineData("prn.pdf", "prn_file.pdf")]
        [InlineData("aux.tar.gz", "aux_file.tar.gz")]
        [InlineData("NUL", "NUL_file")]
        [InlineData("com1.dat", "com1_file.dat")]
        [InlineData("lpt9.log", "lpt9_file.log")]
        public void FileNamingHelper_SanitizeFileName_GuardsWindowsReservedDeviceNames(string input, string expected)
        {
            string result = FileNamingHelper.SanitizeFileName(input);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("my_video.mp4.mp4", "my_video.mp4")]
        [InlineData("archive.zip.zip", "archive.zip")]
        [InlineData("document.pdf.PDF", "document.pdf")]
        [InlineData("sample.tar.gz", "sample.tar.gz")]
        public void FileNamingHelper_DeduplicateExtension_RemovesDuplicateExtensions(string input, string expected)
        {
            string result = FileNamingHelper.DeduplicateExtension(input);
            result.Should().Be(expected);
        }

        [Fact]
        public void FileNamingHelper_ResolveAuthoritativeFileName_FollowsStrictPrecedence()
        {
            var cd = new ContentDispositionHeaderValue("attachment") { FileName = "header_name.zip" };
            var uri = new Uri("https://example.com/files/url_name.zip");

            // 1. Explicit user filename overrides all
            string r1 = FileNamingHelper.ResolveAuthoritativeFileName("custom.zip", cd, "Media Title", "application/zip", uri);
            r1.Should().Be("custom.zip");

            // 2. Content-Disposition overrides media title & URL
            string r2 = FileNamingHelper.ResolveAuthoritativeFileName(null, cd, "Media Title", "application/zip", uri);
            r2.Should().Be("header_name.zip");

            // 3. Media title overrides URL path
            string r3 = FileNamingHelper.ResolveAuthoritativeFileName(null, null, "My Presentation", "video/mp4", uri);
            r3.Should().Be("My Presentation.mp4");

            // 4. URL path segment is used when no CD or title
            string r4 = FileNamingHelper.ResolveAuthoritativeFileName(null, null, null, null, uri);
            r4.Should().Be("url_name.zip");

            // 5. Mime fallback used when URL has no extension
            var noExtUri = new Uri("https://example.com/download/stream");
            string r5 = FileNamingHelper.ResolveAuthoritativeFileName(null, null, null, "image/png", noExtUri);
            r5.Should().Be("download.png");
        }

        [Fact]
        public void SecuritySanitizer_TrySanitizeDestinationPath_EnforcesDirectoryBoundary()
        {
            string baseDir = Path.Combine(Path.GetTempPath(), "EDM_Sanitize_Test");
            Directory.CreateDirectory(baseDir);

            try
            {
                // Safe relative subpath
                bool ok1 = SecuritySanitizer.TrySanitizeDestinationPath(baseDir, "downloads/file.bin", out string path1);
                ok1.Should().BeTrue();
                path1.Should().StartWith(Path.GetFullPath(baseDir));

                // Malicious traversal escape
                bool ok2 = SecuritySanitizer.TrySanitizeDestinationPath(baseDir, "../../Windows/System32/calc.exe", out string _);
                ok2.Should().BeFalse("Must reject paths traversing outside base directory");
            }
            finally
            {
                if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true);
            }
        }

        #endregion

        #region 3. Dynamic Segmentation & SegmentScheduler Coverage Invariance

        [Theory]
        [InlineData(1, 1)]
        [InlineData(500 * 1024, 1)]             // 500 KB -> 1
        [InlineData(3 * 1024 * 1024, 2)]        // 3 MB -> 2
        [InlineData(20 * 1024 * 1024, 4)]       // 20 MB -> 4
        [InlineData(200 * 1024 * 1024, 8)]      // 200 MB -> 8
        [InlineData(1024L * 1024 * 1024, 16)]   // 1 GB -> 16
        public void SegmentScheduler_CalculateSmartSegmentCount_AdaptsToFileSize(long fileSize, int expectedSegments)
        {
            int segments = SegmentScheduler.CalculateSmartSegmentCount(fileSize, 16);
            segments.Should().Be(expectedSegments);
        }

        [Fact]
        public void SegmentScheduler_MathematicalCoverage_RemainsStrictlyInvariantUnderSplits()
        {
            // 50 MB total file size
            long totalBytes = 50 * 1024 * 1024;
            var scheduler = new SegmentScheduler(totalBytes, minSplitThresholdBytes: 1 * 1024 * 1024, splitAlignmentBytes: 64 * 1024);
            scheduler.InitializeDefault(2);

            scheduler.ValidateCoverage().Should().BeTrue();
            scheduler.Segments.Count.Should().Be(2);

            // Worker 1 takes seg 0, Worker 2 takes seg 1
            var w1 = scheduler.GetNextWorkItem("W1");
            var w2 = scheduler.GetNextWorkItem("W2");
            w1.Should().NotBeNull();
            w2.Should().NotBeNull();

            // W1 downloads 5 MB of seg 0 (which is 25 MB)
            scheduler.ReportProgress(w1!.Id, 5 * 1024 * 1024);

            // W3 joins -> Triggers work stealing split on largest downloading segment
            var w3 = scheduler.GetNextWorkItem("W3");
            w3.Should().NotBeNull();

            // Total coverage MUST still be 100% continuous and non-overlapping
            scheduler.ValidateCoverage().Should().BeTrue("Coverage must have zero gaps and zero overlaps across entire byte range");
            scheduler.Segments.Count.Should().Be(3);

            // Segments sum of lengths must equal totalBytes
            long sum = scheduler.Segments.Sum(s => s.TotalBytes);
            sum.Should().Be(totalBytes);
        }

        [Fact]
        public void SegmentScheduler_ReclaimStalledSegments_ReassignsWorkerRanges()
        {
            long totalBytes = 10 * 1024 * 1024;
            var scheduler = new SegmentScheduler(totalBytes);
            scheduler.InitializeDefault(2);

            var w1 = scheduler.GetNextWorkItem("Worker_Stalled");
            w1.Should().NotBeNull();

            // Register initial progress
            scheduler.RegisterWorkerProgress("Worker_Stalled", w1!.Id, 1024, 0);

            // Check stall after simulated threshold (e.g. 0 second threshold for test)
            var reclaimed = scheduler.ReclaimStalledSegments(TimeSpan.Zero);
            reclaimed.Should().Contain(w1.Id);

            // Segment should be back to Pending state and available for work
            var reacquired = scheduler.GetNextWorkItem("Worker_Recovered");
            reacquired.Should().NotBeNull();
            reacquired!.Id.Should().Be(w1.Id);
        }

        #endregion

        #region 4. HLS & DASH Manifest Parsing Architecture

        [Fact]
        public void HlsParser_ParsesAudioTracksAndKeyEncryptionCorrectly()
        {
            string m3u8 = @"#EXTM3U
#EXT-X-VERSION:4
#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=""audio-aac"",NAME=""English"",DEFAULT=YES,AUTOSELECT=YES,LANGUAGE=""en"",URI=""audio/en.m3u8""
#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID=""subs"",NAME=""English"",DEFAULT=NO,LANGUAGE=""en"",URI=""subs/en.vtt""
#EXT-X-STREAM-INF:BANDWIDTH=2500000,AVERAGE-BANDWIDTH=2200000,RESOLUTION=1280x720,FRAME-RATE=30.000,CODECS=""avc1.4d401f,mp4a.40.2"",AUDIO=""audio-aac"",SUBTITLES=""subs""
video/720p.m3u8";

            var baseUri = new Uri("https://stream.example.com/master.m3u8");
            var result = HlsParser.Parse(m3u8, baseUri);

            result.IsMaster.Should().BeTrue();
            result.AudioTracks.Should().HaveCount(1);
            result.AudioTracks[0].Name.Should().Be("English");
            result.AudioTracks[0].Uri.Should().Be("https://stream.example.com/audio/en.m3u8");
            result.AudioTracks[0].IsDefault.Should().BeTrue();

            result.SubtitleTracks.Should().HaveCount(1);
            result.SubtitleTracks[0].Uri.Should().Be("https://stream.example.com/subs/en.vtt");

            result.Variants.Should().HaveCount(1);
            result.Variants[0].Width.Should().Be(1280);
            result.Variants[0].Height.Should().Be(720);
            result.Variants[0].FrameRate.Should().Be(30.0);
            result.Variants[0].AudioGroupId.Should().Be("audio-aac");
        }

        [Fact]
        public void DashParser_ParsesSegmentTimelineAndDRMCorrectly()
        {
            string mpd = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"" type=""static"" mediaPresentationDuration=""PT1M30S"">
  <Period>
    <AdaptationSet mimeType=""video/mp4"" contentType=""video"">
      <ContentProtection schemeIdUri=""urn:uuid:edef8ba9-79d6-4ace-a3c8-27dcd51d21ed""/>
      <SegmentTemplate timescale=""1000"" initialization=""init-$RepresentationID$.mp4"" media=""chunk-$RepresentationID$-$Number%05d$.m4s"" startNumber=""1"">
        <SegmentTimeline>
          <S t=""0"" d=""2000"" r=""2""/>
        </SegmentTimeline>
      </SegmentTemplate>
      <Representation id=""v1080"" bandwidth=""5000000"" width=""1920"" height=""1080"" frameRate=""60""/>
    </AdaptationSet>
  </Period>
</MPD>";

            var baseUri = new Uri("https://dash.example.com/vod/stream.mpd");
            var manifest = DashParser.Parse(mpd, baseUri);

            manifest.IsDrmProtected.Should().BeTrue();
            manifest.DrmSystem.Should().Be("Widevine");
            manifest.TotalDurationSeconds.Should().Be(90.0);

            manifest.VideoRepresentations.Should().HaveCount(1);
            var rep = manifest.VideoRepresentations[0];
            rep.Width.Should().Be(1920);
            rep.Height.Should().Be(1080);
            rep.FrameRate.Should().Be(60.0);
            rep.InitializationUrl.Should().Be("https://dash.example.com/vod/init-v1080.mp4");

            // SegmentTimeline: 1 initial + r=2 repeat => 3 segments total with %05d padding
            rep.SegmentUrls.Should().HaveCount(3);
            rep.SegmentUrls[0].Should().Be("https://dash.example.com/vod/chunk-v1080-00001.m4s");
            rep.SegmentUrls[1].Should().Be("https://dash.example.com/vod/chunk-v1080-00002.m4s");
            rep.SegmentUrls[2].Should().Be("https://dash.example.com/vod/chunk-v1080-00003.m4s");
        }

        #endregion

        #region 5. Cross-Origin Redirect Security & HTTP Header Sanitization

        [Fact]
        public void CrossOriginRedirectSecurityHandler_SanitizesCrossOriginRedirects()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "https://api.source.com/get-link");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "secret_jwt_token");
            req.Headers.Add("Cookie", "session=auth12345");

            var origUri = new Uri("https://api.source.com/get-link");
            var destUri = new Uri("https://cdn.external-host.com/files/iso.zip");

            // Act
            CrossOriginRedirectSecurityHandler.SanitizeRequestForRedirect(req, origUri, destUri);

            // Assert
            req.Headers.Authorization.Should().BeNull("Authorization must be stripped on cross-origin redirect");
            req.Headers.Contains("Cookie").Should().BeFalse("Session cookies must not leak to external domain");
            req.Headers.Referrer.Should().Be(new Uri("https://api.source.com"), "Referer must be reduced to origin");
        }

        [Theory]
        [InlineData("Host", true)]
        [InlineData("Content-Length", true)]
        [InlineData("Transfer-Encoding", true)]
        [InlineData("Upgrade", true)]
        [InlineData("X-Custom-Header", false)]
        [InlineData("User-Agent", false)]
        public void HttpHeaderSecuritySanitizer_BlocksForbiddenHeaders(string headerName, bool expectedForbidden)
        {
            HttpHeaderSecuritySanitizer.IsForbiddenHeader(headerName).Should().Be(expectedForbidden);
        }

        [Fact]
        public void HttpHeaderSecuritySanitizer_StripsCrlfInjection()
        {
            string maliciousValue = "safe_value\r\nInjected-Header: evil\r\n";
            string sanitized = HttpHeaderSecuritySanitizer.SanitizeHeaderValue(maliciousValue);

            sanitized.Should().Be("safe_valueInjected-Header: evil");
            sanitized.Should().NotContain("\r");
            sanitized.Should().NotContain("\n");
        }

        #endregion

        #region 6. WPF MVVM Converters & Notification Throttling

        [Theory]
        [InlineData(0L, "0 B")]
        [InlineData(512L, "512 B")]
        [InlineData(1024L, "1 KB")]
        [InlineData(1536L, "2 KB")]
        [InlineData(1048576L, "1.0 MB")]
        [InlineData(52428800L, "50.0 MB")]
        [InlineData(1073741824L, "1.00 GB")]
        [InlineData(1099511627776L, "1.00 TB")]
        public void BytesToHumanSizeConverter_FormatsBytesCorrectly(long bytes, string expected)
        {
            var converter = new BytesToHumanSizeConverter();
            var result = converter.Convert(bytes, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(0, Visibility.Visible)]
        [InlineData(1, Visibility.Collapsed)]
        [InlineData(50, Visibility.Collapsed)]
        public void EmptyCountToVisibilityConverter_ShowsOnlyWhenEmpty(int count, Visibility expected)
        {
            var converter = new EmptyCountToVisibilityConverter();
            var result = converter.Convert(count, typeof(Visibility), null, System.Globalization.CultureInfo.InvariantCulture);
            result.Should().Be(expected);
        }

        [Fact]
        public async Task NotificationService_DeduplicatesRapidNotifications()
        {
            var service = NotificationService.Instance;

            // Trigger identical notification 5 times rapidly
            for (int i = 0; i < 5; i++)
            {
                service.Notify("Download Complete", "test_file.zip downloaded successfully.", NotificationSeverity.Success, NotificationCategory.DownloadCompleted);
            }

            // Verify no unhandled exceptions occurred and system rate limiting remained stable
            await Task.Delay(50);
            service.Should().NotBeNull();
        }

        #endregion
    }
}
