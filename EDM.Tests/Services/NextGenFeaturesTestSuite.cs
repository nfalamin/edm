using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class NextGenFeaturesTestSuite : IDisposable
    {
        private readonly string _testDir;

        public NextGenFeaturesTestSuite()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "EDM_NextGenTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch { }
        }

        #region Feature 1: Multi-Source Mirror Aggregator Tests
        [Fact]
        public void MirrorAggregator_RegistersAndBuildsSegmentPlan_Correctly()
        {
            // Arrange
            var aggregator = new MultiSourceMirrorAggregatorService();
            var fileKey = "ubuntu-24.04-iso";
            var mirrors = new[]
            {
                "https://mirror1.us.edm.org/ubuntu.iso",
                "https://mirror2.eu.edm.org/ubuntu.iso",
                "https://mirror3.asia.edm.org/ubuntu.iso"
            };

            // Act
            aggregator.RegisterMirrors(fileKey, mirrors);
            var plan = aggregator.BuildAggregationPlan(fileKey, totalSegments: 6);

            // Assert
            plan.ActiveMirrors.Should().HaveCount(3);
            plan.SegmentToMirrorMap.Should().HaveCount(6);
            plan.SegmentToMirrorMap[0].Should().Be(mirrors[0]);
            plan.SegmentToMirrorMap[1].Should().Be(mirrors[1]);
            plan.SegmentToMirrorMap[2].Should().Be(mirrors[2]);
            plan.SegmentToMirrorMap[3].Should().Be(mirrors[0]);
        }

        [Fact]
        public void MirrorAggregator_HandlesFailover_SelectsHealthyMirror()
        {
            // Arrange
            var aggregator = new MultiSourceMirrorAggregatorService();
            var fileKey = "large-archive.zip";
            var mirrors = new[]
            {
                "https://primary.server.com/archive.zip",
                "https://backup.server.com/archive.zip"
            };
            aggregator.RegisterMirrors(fileKey, mirrors);

            // Act
            var failover1 = aggregator.GetFailoverMirror(fileKey, mirrors[0]);
            var failover2 = aggregator.GetFailoverMirror(fileKey, mirrors[0]);
            var failover3 = aggregator.GetFailoverMirror(fileKey, mirrors[0]);

            // Assert
            failover1.Should().Be(mirrors[1]);
            failover2.Should().Be(mirrors[1]);
            failover3.Should().Be(mirrors[1]);
            var status = aggregator.GetMirrorStatus(fileKey);
            status.First(m => m.Url == mirrors[0]).IsActive.Should().BeFalse();
        }

        [Fact]
        public void MirrorAggregator_TracksSegmentProgress_Accurately()
        {
            // Arrange
            var aggregator = new MultiSourceMirrorAggregatorService();
            var fileKey = "test-video.mp4";
            var mirror = "https://cdn.example.com/video.mp4";
            aggregator.RegisterMirrors(fileKey, new[] { mirror });

            // Act
            aggregator.RecordSegmentProgress(fileKey, mirror, 1024);
            aggregator.RecordSegmentProgress(fileKey, mirror, 2048);

            // Assert
            var status = aggregator.GetMirrorStatus(fileKey);
            status.First(m => m.Url == mirror).BytesDownloaded.Should().Be(3072);
        }
        #endregion

        #region Feature 2: Smart File Organizer Tests
        [Theory]
        [InlineData("Invoice_March_2026_Tax_Statement.pdf", "Invoices & Documents", "Documents/Invoices_Receipts", "Finance")]
        [InlineData("VSCodeUserSetup-x64-1.90.exe", "Software & Setup", "Software/Installers", "Application")]
        [InlineData("Breaking.Bad.S01E01.1080p.BluRay.x264.mkv", "Movies & Shows", "Media/Movies_TV", "Video")]
        [InlineData("antigravity-engine-master-source.zip", "Projects & Code", "Projects/Source", "Code")]
        [InlineData("Machine_Learning_Lecture_Chapter_1.pdf", "Course & Education", "Documents/Courses_Tutorials", "Learning")]
        public void SmartOrganizer_ClassifiesFiles_WithHighAccuracy(string fileName, string expectedCategory, string expectedSubfolder, string expectedTag)
        {
            // Arrange
            var organizer = new SmartFileOrganizerService();

            // Act
            var result = organizer.AnalyzeAndClassify(fileName);

            // Assert
            result.PrimaryCategory.Should().Be(expectedCategory);
            result.SuggestedSubfolder.Should().Be(expectedSubfolder);
            result.SmartTags.Should().Contain(expectedTag);
            result.ConfidenceScore.Should().BeGreaterThan(0.7);
        }

        [Fact]
        public void SmartOrganizer_ResolvesDestinationPath_Correctly()
        {
            // Arrange
            var organizer = new SmartFileOrganizerService();
            var result = organizer.AnalyzeAndClassify("my_contract_payment_receipt.docx");

            // Act
            var dest = organizer.ResolveDestinationPath(_testDir, result);

            // Assert
            dest.Should().Be(Path.Combine(_testDir, "Documents", "Invoices_Receipts"));
        }
        #endregion

        #region Feature 3: Subtitle Auto Downloader Tests
        [Fact]
        public void SubtitleDownloader_ParsesVideoMetadata_Correctly()
        {
            // Arrange
            var subService = new SubtitleAutoDownloaderService();
            var videoPath = Path.Combine(_testDir, "Game.of.Thrones.S08E06.1080p.WEB-DL.x264.mkv");

            // Act
            var meta = subService.ParseVideoMetadata(videoPath);

            // Assert
            meta.Season.Should().Be(8);
            meta.Episode.Should().Be(6);
            meta.CleanTitle.Should().Contain("Game of Thrones");
        }

        [Fact]
        public async Task SubtitleDownloader_FetchesAndSavesSRT_Success()
        {
            // Arrange
            var subService = new SubtitleAutoDownloaderService();
            var videoPath = Path.Combine(_testDir, "Interstellar.2014.1080p.mp4");
            await File.WriteAllBytesAsync(videoPath, new byte[1024]);

            // Act
            var tracks = await subService.FetchAndSaveSubtitlesAsync(videoPath, new[] { "en", "bn" });

            // Assert
            tracks.Should().HaveCount(2);
            tracks.Should().Contain(t => t.LanguageCode == "en" && t.IsDownloaded);
            tracks.Should().Contain(t => t.LanguageCode == "bn" && t.IsDownloaded);

            var srtEn = Path.Combine(_testDir, "Interstellar.2014.1080p.en.srt");
            var srtBn = Path.Combine(_testDir, "Interstellar.2014.1080p.bn.srt");

            File.Exists(srtEn).Should().BeTrue();
            File.Exists(srtBn).Should().BeTrue();
            (await File.ReadAllTextAsync(srtBn)).Should().Contain("সাবটাইটেল");
        }
        #endregion

        #region Feature 4: Cloud Auto-Upload Handoff Tests
        [Fact]
        public void CloudHandoff_EncryptsAndDecryptsToken_ViaDPAPI()
        {
            // Arrange
            var rawToken = "ya29.a0AfH6SMA-SampleGoogleDriveOAuth2TokenSecureSecret";

            // Act
            var encrypted = CloudHandoffUploadService.EncryptToken(rawToken);
            var decrypted = CloudHandoffUploadService.DecryptToken(encrypted);

            // Assert
            encrypted.Should().NotBe(rawToken);
            decrypted.Should().Be(rawToken);
        }

        [Theory]
        [InlineData(CloudStorageProvider.GoogleDrive, "drive.google.com")]
        [InlineData(CloudStorageProvider.Dropbox, "dropbox.com")]
        [InlineData(CloudStorageProvider.OneDrive, "onedrive.live.com")]
        [InlineData(CloudStorageProvider.TelegramChannel, "t.me")]
        public async Task CloudHandoff_ProcessesUploadJob_GeneratesCloudUrl(CloudStorageProvider provider, string expectedDomain)
        {
            // Arrange
            var uploader = new CloudHandoffUploadService();
            var testFile = Path.Combine(_testDir, "sample_presentation.pptx");
            await File.WriteAllTextAsync(testFile, "Dummy PPT Content");

            // Act
            var job = uploader.EnqueueUpload(testFile, provider, "Work_Documents");
            var completed = await uploader.ProcessUploadJobAsync(job.JobId, "encrypted_mock_token");

            // Assert
            completed.IsCompleted.Should().BeTrue();
            completed.IsFailed.Should().BeFalse();
            completed.ProgressPercent.Should().Be(100.0);
            completed.CloudFileUrl.Should().Contain(expectedDomain);
        }
        #endregion
    }
}
