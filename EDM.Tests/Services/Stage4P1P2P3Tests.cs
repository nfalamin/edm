using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class Stage4P1P2P3Tests : TestBase
    {
        [Fact]
        public void SoundNotificationService_ConfiguresEventsProperly()
        {
            var soundService = SoundNotificationService.Instance;
            soundService.SetCustomSound(SoundEvent.DownloadResumed, @"C:\Sounds\resumed.wav", enabled: true);
            soundService.MasterSoundEnabled = true;

            // Preview sound fallback does not throw
            var act = () => soundService.PreviewSound(@"C:\NonExistent\test.wav");
            act.Should().NotThrow();
        }

        [Fact]
        public void FtpsClientEngine_ConstructsCorrectly()
        {
            var engine = new FtpsClientEngine("ftp.secure-host.org", 990, "user1", "pass1", useTls: true);
            engine.Should().NotBeNull();
        }
    }
}
