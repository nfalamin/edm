using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class Stage4AutomationAndSafetyTests : TestBase
    {
        [Fact]
        public async Task PowerActionScheduler_GracePeriodCountdownAndCancellation_WorksCorrectly()
        {
            var scheduler = new PowerActionScheduler();
            int ticks = 0;
            scheduler.CountdownTick += (sec, action) => ticks++;

            // Start 3-second grace period
            var task = scheduler.TriggerPowerActionAsync(PowerAction.ExitApplication, gracePeriodSeconds: 3);
            await Task.Delay(1500);

            // User cancels countdown
            scheduler.CancelCountdown();
            bool result = await task;

            result.Should().BeFalse("Cancelled countdown must return false without executing power action");
            ticks.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task PowerActionScheduler_SuppressesActionWhenActiveDownloadsExist()
        {
            var scheduler = new PowerActionScheduler();
            bool result = await scheduler.TriggerPowerActionAsync(
                PowerAction.Shutdown,
                gracePeriodSeconds: 1,
                hasActiveDownloads: () => true); // Active downloads running!

            result.Should().BeFalse("Power action must be suppressed when active downloads exist");
        }

        [Fact]
        public void SoundNotificationService_ConfiguresEventsAndMutesCleanly()
        {
            var audio = new SoundNotificationService();
            audio.MasterSoundEnabled.Should().BeTrue();

            audio.SetCustomSound(SoundEvent.DownloadCompleted, @"C:\Sounds\complete.wav", enabled: true);
            // Master mute
            audio.MasterSoundEnabled = false;

            // Triggering event while muted does not crash
            Action act = () => audio.PlayEvent(SoundEvent.DownloadCompleted);
            act.Should().NotThrow();
        }

        [Fact]
        public void CustomAntivirusScanner_ResolvesPlaceholdersCorrectly()
        {
            var av = new CustomAntivirusScannerService();
            av.ActiveProfile.ProfileName.Should().Be("Windows Defender");

            av.SetActiveProfile("avast");
            av.ActiveProfile.ProfileName.Should().Contain("Avast");

            av.SetActiveProfile("eset");
            av.ActiveProfile.ProfileName.Should().Contain("ESET");
        }

        [Fact]
        public void DownloadCategoryRouter_RoutesExtensionsToCorrectSubfolders()
        {
            var router = new DownloadCategoryRouter();

            var catZip = router.DetermineCategory("archive.7z");
            catZip.CategoryId.Should().Be("compressed");
            catZip.DefaultSubFolder.Should().Be("Compressed");

            var catVideo = router.DetermineCategory("movie.mkv");
            catVideo.CategoryId.Should().Be("video");

            var catDoc = router.DetermineCategory("report.pdf");
            catDoc.CategoryId.Should().Be("documents");

            // Custom Category
            router.AddCustomCategory("cad", "CAD Drawings", "CAD", new[] { ".dwg", ".dxf" });
            var catCad = router.DetermineCategory("blueprint.dwg");
            catCad.CategoryId.Should().Be("cad");
            catCad.DefaultSubFolder.Should().Be("CAD");
        }
    }
}
