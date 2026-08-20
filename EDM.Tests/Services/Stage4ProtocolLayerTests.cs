using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class Stage4ProtocolLayerTests
    {
        [Theory]
        [InlineData("http://example.com/file.zip", DownloadProtocolType.Http, "HTTP")]
        [InlineData("https://example.com/file.zip", DownloadProtocolType.Https, "HTTPS")]
        [InlineData("ftp://ftp.is.co.za/linux/ubuntu.iso", DownloadProtocolType.Ftp, "FTP")]
        [InlineData("ftps://secure.backup.com/archive.tar.gz", DownloadProtocolType.Ftps, "FTPS")]
        [InlineData("sftp://ssh.server.net/var/log/syslog.log", DownloadProtocolType.Sftp, "SFTP")]
        [InlineData("magnet:?xt=urn:btih:4a80d87e69c110c995958564f98e84b056d8d44a&dn=Sample.mp4", DownloadProtocolType.Magnet, "MAGNET")]
        [InlineData("C:\\Downloads\\archlinux.iso.torrent", DownloadProtocolType.BitTorrent, "TORRENT")]
        [InlineData("https://live.cdn.com/master.m3u8", DownloadProtocolType.Hls, "HLS")]
        [InlineData("https://dash.akamai.com/vod/stream.mpd", DownloadProtocolType.Dash, "DASH")]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", DownloadProtocolType.StreamingMedia, "STREAM")]
        [InlineData("https://vimeo.com/76979871", DownloadProtocolType.StreamingMedia, "STREAM")]
        public void ProtocolDetector_DetectsAllProtocolsAccurately(string url, DownloadProtocolType expectedProto, string expectedScheme)
        {
            var result = ProtocolDetector.Detect(url);

            result.Protocol.Should().Be(expectedProto);
            result.DisplayScheme.Should().Be(expectedScheme);
            result.SupportsResume.Should().BeTrue();
        }

        [Theory]
        [InlineData("ftp://admin:MySecretPass123@ftp.example.com/data.bin", "ftp://admin:***@ftp.example.com/data.bin")]
        [InlineData("http://user:token999@api.server.com/v1/download", "http://user:***@api.server.com/v1/download")]
        [InlineData("https://normal.com/download.zip", "https://normal.com/download.zip")]
        public void ProtocolDetector_SanitizesSensitiveCredentialsFromUrls(string rawUrl, string expectedSanitized)
        {
            string sanitized = ProtocolDetector.SanitizeUrlForLogging(rawUrl);
            sanitized.Should().Be(expectedSanitized);
        }

        [Fact]
        public void ProtocolDetector_ExtractsEmbeddedFtpCredentials()
        {
            string rawUrl = "ftp://backupUser:SecurePassword99@storage.backup.internal:2121/backups/daily.7z";

            bool extracted = ProtocolDetector.TryExtractCredentials(rawUrl, out string cleanUrl, out var cred);

            extracted.Should().BeTrue();
            cred.Should().NotBeNull();
            cred!.UserName.Should().Be("backupUser");
            cred.Password.Should().Be("SecurePassword99");
            cleanUrl.Should().Be("ftp://storage.backup.internal:2121/backups/daily.7z");
        }

        [Fact]
        public void BitTorrentService_ParseTorrentFile_ExtractsMultiFileEntriesAccurately()
        {
            // Bencode dictionary with files list:
            // d8:announce18:http://tracker.org4:infod4:name8:MyBundle5:filesld6:lengthi500e4:pathl5:file1.txteed6:lengthi700e4:pathl5:file2.txteeeee
            string bencode = "d8:announce18:http://tracker.org4:infod4:name8:MyBundle5:filesld6:lengthi500e4:pathl9:file1.txteed6:lengthi700e4:pathl9:file2.txteeeee";
            byte[] bytes = Encoding.UTF8.GetBytes(bencode);

            var meta = BitTorrentService.ParseTorrentFile(bytes);

            meta.Should().NotBeNull();
            meta.Name.Should().Be("MyBundle");
            meta.Trackers.Should().Contain("http://tracker.org");
            meta.TotalSize.Should().Be(1200);
            meta.Files.Should().HaveCount(2);
            meta.Files[0].Path.Should().Be("file1.txt");
            meta.Files[0].Length.Should().Be(500);
            meta.Files[1].Path.Should().Be("file2.txt");
            meta.Files[1].Length.Should().Be(700);
        }

        [Fact]
        public async Task BitTorrentService_StatePersistence_ResumesInterruptedDownload()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "edm_bt_resume_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string targetFile = Path.Combine(tempDir, "resumed_payload.bin");

            try
            {
                var service = new BitTorrentService();
                string magnetUrl = "magnet:?xt=urn:btih:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA&dn=ResumedPayload.bin&xl=1048576";

                var pauseToken = new PauseTokenSource();
                var progress = new Progress<DownloadProgressInfo>();

                // 1. Initial run
                var downloadTask = service.DownloadTorrentOrMagnetAsync(
                    magnetUrl,
                    targetFile,
                    progress,
                    pauseToken,
                    null,
                    CancellationToken.None
                );

                await downloadTask.ConfigureAwait(false);

                File.Exists(targetFile).Should().BeTrue();
                new FileInfo(targetFile).Length.Should().Be(1048576);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void SecuritySanitizer_AllowsValidUrlSchemesIncludingMagnetAndSftp()
        {
            SecuritySanitizer.IsAllowedUrlScheme("http://example.com/file").Should().BeTrue();
            SecuritySanitizer.IsAllowedUrlScheme("https://example.com/file").Should().BeTrue();
            SecuritySanitizer.IsAllowedUrlScheme("ftp://ftp.example.com/file").Should().BeTrue();
            SecuritySanitizer.IsAllowedUrlScheme("ftps://ftps.example.com/file").Should().BeTrue();
            SecuritySanitizer.IsAllowedUrlScheme("sftp://sftp.example.com/file").Should().BeTrue();
            SecuritySanitizer.IsAllowedUrlScheme("magnet:?xt=urn:btih:12345").Should().BeTrue();
            SecuritySanitizer.IsAllowedUrlScheme("javascript:alert(1)").Should().BeFalse();
            SecuritySanitizer.IsAllowedUrlScheme("file:///C:/Windows/notepad.exe").Should().BeFalse();
        }

        [Fact]
        public void SecuritySanitizer_TrySanitizeDestinationPath_BlocksPrefixDirectoryCollision()
        {
            string baseDir = @"C:\Downloads";
            string maliciousPath = @"..\DownloadsEvil\malicious.exe";

            bool isSafe = SecuritySanitizer.TrySanitizeDestinationPath(baseDir, maliciousPath, out string safePath);

            isSafe.Should().BeFalse();
            safePath.Should().BeEmpty();
        }

        [Fact]
        public void SecuritySanitizer_TrySanitizeDestinationPath_AllowsLegitimateSubpaths()
        {
            string baseDir = @"C:\Downloads";
            string legitimatePath = @"subfolder\document.pdf";

            bool isSafe = SecuritySanitizer.TrySanitizeDestinationPath(baseDir, legitimatePath, out string safePath);

            isSafe.Should().BeTrue();
            safePath.Should().Be(@"C:\Downloads\subfolder\document.pdf");
        }

        [Fact]
        public void PartialMediaPreviewService_LaunchPreviewPlayer_RejectsExecutableFiles()
        {
            var previewService = new PartialMediaPreviewService();
            previewService.LaunchPreviewPlayer("C:\\Windows\\System32\\cmd.exe").Should().BeFalse();
            previewService.LaunchPreviewPlayer("C:\\Downloads\\malware.bat").Should().BeFalse();
            previewService.LaunchPreviewPlayer("C:\\Downloads\\script.ps1").Should().BeFalse();
        }

        [Theory]
        [InlineData("CON.txt", "_CON.txt")]
        [InlineData("aux.json", "_aux.json")]
        [InlineData("COM1.tar.gz", "_COM1.tar.gz")]
        [InlineData("NUL", "_NUL")]
        [InlineData("valid_file.pdf", "valid_file.pdf")]
        [InlineData("trailing_dots....", "trailing_dots")]
        [InlineData(".....", "downloaded_file.bin")]
        [InlineData("bad:chars*in?file<name>.zip", "badcharsinfilename.zip")]
        public void SecuritySanitizer_SanitizeFileName_HandlesAllEdgeCases(string raw, string expected)
        {
            string sanitized = SecuritySanitizer.SanitizeFileName(raw);
            sanitized.Should().Be(expected);
        }

        [Fact]
        public void SecuritySanitizer_GetUniqueDestinationPath_IncrementsIfFileExists()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "edm_collision_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string file1 = Path.Combine(tempDir, "document.pdf");
            File.WriteAllText(file1, "test");

            try
            {
                string unique1 = SecuritySanitizer.GetUniqueDestinationPath(file1);
                unique1.Should().Be(Path.Combine(tempDir, "document (1).pdf"));

                File.WriteAllText(unique1, "test2");
                string unique2 = SecuritySanitizer.GetUniqueDestinationPath(file1);
                unique2.Should().Be(Path.Combine(tempDir, "document (2).pdf"));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void HttpHeaderSecuritySanitizer_SanitizeHeaderValue_StripsCrlfInjection()
        {
            string dirtyValue = "MyUserAgent\r\nInjected-Header: evil\r\n";
            string clean = HttpHeaderSecuritySanitizer.SanitizeHeaderValue(dirtyValue);
            clean.Should().Be("MyUserAgentInjected-Header: evil");
            clean.Should().NotContain("\r");
            clean.Should().NotContain("\n");
        }

        [Theory]
        [InlineData("Host")]
        [InlineData("Content-Length")]
        [InlineData("Connection")]
        [InlineData("Transfer-Encoding")]
        public void HttpHeaderSecuritySanitizer_TryApplySafeHeader_RejectsForbiddenHeaders(string forbiddenHeader)
        {
            using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "https://example.com");
            bool applied = HttpHeaderSecuritySanitizer.TryApplySafeHeader(req, forbiddenHeader, "malicious_value");
            applied.Should().BeFalse();
        }

        [Theory]
        [InlineData("javascript:alert(document.cookie)")]
        [InlineData("data:text/html,<script>alert(1)</script>")]
        [InlineData("file:///C:/Windows/System32/cmd.exe")]
        [InlineData("blob:https://evil.com/uuid")]
        [InlineData("vbscript:MsgBox(1)")]
        public void DownloadSecurityPipeline_ValidateUrl_RejectsDangerousSchemes(string dangerousUrl)
        {
            var pipeline = new DownloadSecurityPipeline();
            bool valid = pipeline.ValidateUrl(dangerousUrl, out string error);
            valid.Should().BeFalse();
            error.Should().NotBeNullOrWhiteSpace();
        }
    }
}

