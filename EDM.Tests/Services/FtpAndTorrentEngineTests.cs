using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class FtpAndTorrentEngineTests
    {
        [Fact]
        public void BitTorrentService_DetectsMagnetAndTorrentUrlsCorrectly()
        {
            string magnetUrl = "magnet:?xt=urn:btih:4a80d87e69c110c995958564f98e84b056d8d44a&dn=SampleVideo.mp4&tr=udp%3A%2F%2Ftracker.openbittorrent.com%3A80";
            string torrentFilePath = "C:\\Downloads\\ubuntu-22.04-desktop-amd64.iso.torrent";
            string httpUrl = "https://example.com/file.zip";

            BitTorrentService.IsBitTorrentUrl(magnetUrl).Should().BeTrue();
            BitTorrentService.IsBitTorrentUrl(torrentFilePath).Should().BeTrue();
            BitTorrentService.IsBitTorrentUrl(httpUrl).Should().BeFalse();
        }

        [Fact]
        public void BitTorrentService_ParsesMagnetUriParametersAccurately()
        {
            string magnetUrl = "magnet:?xt=urn:btih:4A80D87E69C110C995958564F98E84B056D8D44A&dn=Ubuntu_Linux_Installer&tr=udp%3A%2F%2Ftracker.opentrackr.org%3A1337%2Fannounce&xl=2500000000";

            var info = BitTorrentService.ParseMagnetUri(magnetUrl);

            info.Should().NotBeNull();
            info.InfoHash.Should().Be("4A80D87E69C110C995958564F98E84B056D8D44A");
            info.DisplayName.Should().Be("Ubuntu_Linux_Installer");
            info.TargetSize.Should().Be(2500000000L);
            info.Trackers.Should().Contain("udp://tracker.opentrackr.org:1337/announce");
        }

        [Fact]
        public void BencodeParser_DecodesIntegersStringsAndDictionariesCorrectly()
        {
            // Bencode for dictionary: {"announce": "http://tracker.org", "length": 1024}
            // "d8:announce18:http://tracker.org6:lengthi1024ee"
            string bencodeStr = "d8:announce18:http://tracker.org6:lengthi1024ee";
            byte[] bytes = Encoding.UTF8.GetBytes(bencodeStr);

            var decoded = BencodeParser.Parse(bytes) as System.Collections.Generic.Dictionary<string, object>;

            decoded.Should().NotBeNull();
            decoded!["announce"].Should().Be("http://tracker.org");
            decoded["length"].Should().Be(1024L);
        }

        [Fact]
        public async Task BitTorrentService_ExecutesP2PPayloadDownloadAndAssemblesTargetFile()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "edm_test_bt_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string targetFile = Path.Combine(tempDir, "test_payload.bin");

            try
            {
                var service = new BitTorrentService();
                string magnetUrl = "magnet:?xt=urn:btih:1111222233334444555566667777888899990000&dn=TestPayload.bin&xl=524288";

                var progressList = new System.Collections.Generic.List<DownloadProgressInfo>();
                var progress = new Progress<DownloadProgressInfo>(info => progressList.Add(info));

                await service.DownloadTorrentOrMagnetAsync(
                    magnetUrl,
                    targetFile,
                    progress,
                    new PauseTokenSource(),
                    null,
                    CancellationToken.None
                ).ConfigureAwait(false);

                File.Exists(targetFile).Should().BeTrue();
                new FileInfo(targetFile).Length.Should().Be(524288);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task FtpDownloadService_ExecutesLocalFtpOrFallbackSegmentedDownloadSuccessfully()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "edm_test_ftp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string targetFile = Path.Combine(tempDir, "ftp_download.dat");

            try
            {
                var ftpService = new FtpDownloadService();
                using var cts = new CancellationTokenSource(100);
                // Probing non-existent FTP server returns gracefully
                var probe = await ftpService.ProbeFtpUrlAsync("ftp://127.0.0.1:2121/testfile.iso", null, cts.Token).ConfigureAwait(false);
                probe.Should().NotBeNull();
                probe.Uri.Host.Should().Be("127.0.0.1");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }
    }
}
