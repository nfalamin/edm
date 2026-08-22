using System.Threading;
using System.Windows;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.UI
{
    public class SystemTrayTests
    {
        [Fact]
        public void SystemTrayManager_InitializationAndNotification_RunsWithoutExceptions()
        {
            // SystemTrayManager requires STA thread for WPF Window binding
            var thread = new Thread(() =>
            {
                var window = new Window();
                using var trayManager = new SystemTrayManager(window);

                trayManager.Should().NotBeNull();

                // Test notification call does not throw exception
                trayManager.ShowNotification("Test Title", "Test Message");
                trayManager.ShowDownloadCompletedNotification("file.zip", "C:\\Downloads\\file.zip");
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(5000).Should().BeTrue("STA thread completed in time");
        }
    }
}
