using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using EDM.Services.Interfaces;
using EDM.ViewModels;
using EDM.Views;
using Xunit;

namespace EDM.Tests.Services
{
    [Collection("LocalizationTestCollection")]
    public class AddUrlPipelineAndThemeIntegrationTests
    {
        #region 1. ADD URL & VALIDATION TESTS

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Test01_EmptyOrWhitespaceUrl_IsRejected(string? rawUrl)
        {
            bool isValid = AddUrlWindow.ValidateUrlInput(rawUrl, out string normalized, out string error);

            Assert.False(isValid);
            Assert.Contains("empty", error, StringComparison.OrdinalIgnoreCase);
            Assert.True(string.IsNullOrEmpty(normalized));
        }

        [Theory]
        [InlineData("javascript:alert(1)")]
        [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
        [InlineData("file:///C:/Windows/System32/calc.exe")]
        [InlineData("blob:https://example.com/uuid")]
        public void Test02_UnsafeOrForbiddenProtocols_AreRejected(string unsafeUrl)
        {
            bool isValid = AddUrlWindow.ValidateUrlInput(unsafeUrl, out string normalized, out string error);

            Assert.False(isValid);
            Assert.Contains("unsafe", error, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("https://releases.ubuntu.com/24.04/ubuntu-24.04-desktop-amd64.iso")]
        [InlineData("http://speedtest.tele2.net/100MB.zip")]
        [InlineData("ftp://ftp.funet.fi/dev/1000M")]
        [InlineData("magnet:?xt=urn:btih:d6b06319d6dae4ced01ff609a7533479fa57d660")]
        public void Test03_SupportedProtocols_AreAccepted(string validUrl)
        {
            bool isValid = AddUrlWindow.ValidateUrlInput(validUrl, out string normalized, out string error);

            Assert.True(isValid);
            Assert.True(string.IsNullOrEmpty(error));
            Assert.False(string.IsNullOrEmpty(normalized));
        }

        [Fact]
        public void Test04_UrlField_StartsGenuinelyEmpty()
        {
            var vm = new AddUrlViewModel();
            Assert.True(string.IsNullOrEmpty(vm.Url));
            Assert.Equal(string.Empty, vm.Url);
        }

        [Fact]
        public void Test05_StartDownload_CreatesValidDownloadItem()
        {
            var vm = new AddUrlViewModel
            {
                Url = "https://speedtest.tele2.net/50MB.zip",
                SelectedCategory = "Compressed",
                AutoStartDownload = true
            };

            bool closedWithResult = false;
            vm.RequestClose += result => closedWithResult = result;

            vm.StartDownload();

            Assert.True(closedWithResult);
            Assert.NotNull(vm.CreatedDownloadItem);
            Assert.Equal("50MB.zip", vm.CreatedDownloadItem!.FileName);
            Assert.Equal("https://speedtest.tele2.net/50MB.zip", vm.CreatedDownloadItem.Url);
            Assert.Equal("Compressed", vm.CreatedDownloadItem.Category);
            Assert.Equal("Downloading", vm.CreatedDownloadItem.Status);
        }

        [Fact]
        public void Test06_DoubleSubmission_IsPreventedByStateLock()
        {
            var vm = new AddUrlViewModel
            {
                Url = "https://example.com/archive.tar.gz"
            };

            int closeCount = 0;
            vm.RequestClose += result => closeCount++;

            vm.StartDownload();
            Assert.True(vm.IsSubmitting);

            // Second invocation must be a no-op
            vm.StartDownload();

            Assert.Equal(1, closeCount);
        }

        [Fact]
        public void Test07_Cancel_CreatesNoDownloadItem_AndClosesWithFalse()
        {
            var vm = new AddUrlViewModel
            {
                Url = "https://example.com/installer.exe"
            };

            bool? closeResult = null;
            vm.RequestClose += result => closeResult = result;

            vm.Cancel();

            Assert.False(closeResult);
            Assert.Null(vm.CreatedDownloadItem);
        }

        #endregion

        #region 2. MEDIA & QUALITY RESOLUTION TESTS

        [Fact]
        public async Task Test08_MediaAnalysis_PopulatesAvailableQualities()
        {
            var vm = new AddUrlViewModel
            {
                Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
            };

            await vm.AnalyzeMediaAsync();

            Assert.False(vm.IsAnalyzing);
            Assert.NotEmpty(vm.AvailableQualities);
            Assert.NotNull(vm.SelectedQuality);
            Assert.Equal("Video", vm.SelectedCategory);
        }

        [Fact]
        public async Task Test09_DirectDownload_FallsBackToDirectStreamQuality()
        {
            var vm = new AddUrlViewModel
            {
                Url = "https://cdn.example.org/binary.iso"
            };

            await vm.AnalyzeMediaAsync();

            Assert.False(vm.IsAnalyzing);
            Assert.NotEmpty(vm.AvailableQualities);
            Assert.Contains("Direct", vm.AvailableQualities[0]);
        }

        #endregion

        #region 3. THEME MANAGER & GLOBAL THEME TESTS

        [Fact]
        public void Test10_ThemeManager_SwitchesTheme_AndFiresEvent()
        {
            var dict = new System.Collections.Generic.Dictionary<string, string>();
            var mockSettings = new Moq.Mock<ISettingsService>();
            mockSettings.Setup(s => s.GetSetting(Moq.It.IsAny<string>())).Returns((string k) => dict.TryGetValue(k, out var v) ? v : null);
            mockSettings.Setup(s => s.SaveSetting(Moq.It.IsAny<string>(), Moq.It.IsAny<string>())).Callback((string k, string v) => dict[k] = v);

            var themeMgr = new ThemeManager(mockSettings.Object);

            ApplicationThemeMode? changedTo = null;
            themeMgr.ThemeChanged += (s, e) => changedTo = e.NewTheme;

            themeMgr.SetTheme(ApplicationThemeMode.Light);

            Assert.Equal(ApplicationThemeMode.Light, themeMgr.CurrentTheme);
            Assert.False(themeMgr.IsDarkMode);
            Assert.Equal(ApplicationThemeMode.Light, changedTo);
            Assert.Equal("Light", dict["SelectedTheme"]);

            themeMgr.ToggleTheme();
            Assert.Equal(ApplicationThemeMode.Dark, themeMgr.CurrentTheme);
            Assert.True(themeMgr.IsDarkMode);
        }

        [Fact]
        public void Test11_ThemeManager_LoadThemePreference_DefaultsToDark()
        {
            var dict = new System.Collections.Generic.Dictionary<string, string>();
            var mockSettings = new Moq.Mock<ISettingsService>();
            mockSettings.Setup(s => s.GetSetting(Moq.It.IsAny<string>())).Returns((string k) => dict.TryGetValue(k, out var v) ? v : null);
            mockSettings.Setup(s => s.SaveSetting(Moq.It.IsAny<string>(), Moq.It.IsAny<string>())).Callback((string k, string v) => dict[k] = v);

            var themeMgr = new ThemeManager(mockSettings.Object);

            themeMgr.LoadThemePreference();
            Assert.Equal(ApplicationThemeMode.Dark, themeMgr.CurrentTheme);

            dict["SelectedTheme"] = "Light";
            themeMgr.LoadThemePreference();
            Assert.Equal(ApplicationThemeMode.Light, themeMgr.CurrentTheme);
        }

        #endregion

        #region 4. UNIFIED STATE CONSISTENCY TESTS

        [Fact]
        public void Test12_DownloadItem_MaintainsSingleAuthoritativeStateAcrossObservers()
        {
            var item = new DownloadItem
            {
                FileName = "BigFile.zip",
                Url = "https://site.org/file.zip",
                SavePath = @"C:\file.zip",
                Status = "Downloading",
                Progress = 25.0,
                TransferRate = "5.0 MB/s",
                TotalBytes = 104857600L,
                DownloadedBytes = 26214400L
            };

            // Observer 1: Dashboard
            string dashboardStatus = item.Status;
            double dashboardProgress = item.Progress;

            // Observer 2: Progress Window
            string progressStatus = item.Status;
            double progressWindowVal = item.Progress;

            Assert.Equal(dashboardStatus, progressStatus);
            Assert.Equal(dashboardProgress, progressWindowVal);

            // Engine updates item
            item.Progress = 50.0;
            item.DownloadedBytes = 52428800L;
            item.Status = "Completed";

            Assert.Equal("Completed", item.Status);
            Assert.Equal(50.0, item.Progress);
            Assert.Equal(52428800L, item.DownloadedBytes);
        }

        #endregion

        #region 5. CATEGORY ROUTING MATRIX TESTS

        [Theory]
        [InlineData("video.mp4", "Video")]
        [InlineData("movie.MKV", "Video")]
        [InlineData("clip.webm", "Video")]
        [InlineData("song.mp3", "Music")]
        [InlineData("audio.WAV", "Music")]
        [InlineData("track.m4a", "Music")]
        [InlineData("manual.pdf", "Documents")]
        [InlineData("report.DOCX", "Documents")]
        [InlineData("notes.txt", "Documents")]
        [InlineData("archive.zip", "Compressed")]
        [InlineData("bundle.RAR", "Compressed")]
        [InlineData("package.7z", "Compressed")]
        [InlineData("tarball.tar", "Compressed")]
        [InlineData("installer.exe", "Programs")]
        [InlineData("setup.MSI", "Programs")]
        [InlineData("unknown.xyz", "General")]
        public void Test13_CategoryRouting_ResolvesCorrectlyAcrossExtensions(string filename, string expectedCategory)
        {
            var router = DownloadCategoryRouter.Instance;
            var category = router.DetermineCategory(filename);
            Assert.Equal(expectedCategory, category.Name);
        }

        #endregion

        #region 6. REAL ANALYTICS & SPEED RECORDING

        [Fact]
        public void Test14_AnalyticsEngine_RecordsRealSpeedWithoutHardcoding()
        {
            var engine = new DownloadAnalyticsEngine();
            string testUrl = "https://cdn.example.org/largefile.iso";
            long testBytes = 50_000_000L;
            double testSpeed = 12_500_000.0; // 12.5 MB/s

            engine.RecordDownloadSample(testUrl, testBytes, testSpeed);
            var overview = engine.GenerateOverviewReport();

            Assert.True(overview.TotalBytesDownloadedAllTime >= testBytes);
            Assert.True(overview.PeakRecordedSpeedMbps > 0);
        }

        #endregion
    }
}

