using System;
using System.IO;
using System.Threading.Tasks;
using EDM.Services;
using Xunit;

namespace EDM.Tests.Services
{
    public class Stage4EngineCompleteParityTests
    {
        [Fact]
        public void SiteConnectionLimitManager_CalculatesLimitsWithWildcards()
        {
            var manager = SiteConnectionLimitManager.Instance;

            // Default wildcard test
            int rapidgatorLimit = manager.GetMaxConnectionsForHost("https://download.rapidgator.net/file/12345");
            Assert.Equal(1, rapidgatorLimit);

            int githubLimit = manager.GetMaxConnectionsForHost("https://raw.githubusercontent.com/user/repo/archive.zip");
            Assert.Equal(16, manager.GetMaxConnectionsForHost("github.com"));

            // Custom rule setting
            manager.SetRule("*.customhost.com", 3);
            Assert.Equal(3, manager.GetMaxConnectionsForHost("sub.customhost.com"));

            // Fallback default
            Assert.Equal(8, manager.GetMaxConnectionsForHost("unknownsite.org"));
        }

        [Fact]
        public void FileTypesInterceptionManager_MatchesExtensionsAndRespectsBlacklist()
        {
            var manager = FileTypesInterceptionManager.Instance;

            // Common intercepted types
            Assert.True(manager.ShouldIntercept("https://example.com/file.zip"));
            Assert.True(manager.ShouldIntercept("https://example.com/movie.mp4"));
            Assert.True(manager.ShouldIntercept("https://example.com/setup.exe"));
            Assert.True(manager.ShouldIntercept("https://example.com/archive.r01", "archive.r01"));

            // Blacklisted extensions / domains
            Assert.False(manager.ShouldIntercept("https://example.com/page.htm"));
            Assert.False(manager.ShouldIntercept("https://example.com/style.css"));
            Assert.False(manager.ShouldIntercept("https://example.com/script.js"));
            Assert.False(manager.ShouldIntercept("https://windowsupdate.com/update.cab"));
        }

        [Fact]
        public void SoundNotificationService_PlaysConfiguredSoundEvents()
        {
            var soundService = SoundNotificationService.Instance;

            var ex1 = Record.Exception(() => soundService.PlayEvent(SoundEvent.DownloadCompleted));
            Assert.Null(ex1);

            var ex2 = Record.Exception(() => soundService.PlayEvent(SoundEvent.DownloadFailed));
            Assert.Null(ex2);

            var ex3 = Record.Exception(() => soundService.PlayEvent(SoundEvent.QueueCompleted));
            Assert.Null(ex3);
        }
    }
}
