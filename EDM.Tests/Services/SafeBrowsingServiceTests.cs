using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using EDM.Services;
using EDM.Services.Interfaces;

namespace EDM.Tests.Services
{
    public class SafeBrowsingServiceTests : TestBase
    {
        [Fact]
        public async Task ScanDownloadedFileAsync_WhenDisabledInSettings_ReturnsDisabledResult()
        {
            // Arrange
            var mockSettings = CreateMock<ISettingsService>();
            mockSettings.Setup(s => s.GetEnablePostDownloadScan()).Returns(false);

            var service = new SafeBrowsingService(mockSettings.Object);
            var tempPath = Path.Combine(Path.GetTempPath(), "EDM_ScanTest_" + Guid.NewGuid() + ".tmp");
            File.WriteAllText(tempPath, "Dummy data for scan test");

            try
            {
                // Act
                var result = await service.ScanDownloadedFileAsync(tempPath, CancellationToken.None);

                // Assert
                result.Should().NotBeNull();
                result.Executed.Should().BeFalse();
                result.IsThreat.Should().BeFalse();
                result.Message.Should().Contain("disabled");
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [Fact]
        public async Task ScanDownloadedFileAsync_WhenFileDoesNotExist_ReturnsNonExecutedResult()
        {
            // Arrange
            var mockSettings = CreateMock<ISettingsService>();
            mockSettings.Setup(s => s.GetEnablePostDownloadScan()).Returns(true);

            var service = new SafeBrowsingService(mockSettings.Object);

            // Act
            var result = await service.ScanDownloadedFileAsync(@"C:\NonExistentDirectory_EDM\nonexistent.tmp", CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Executed.Should().BeFalse();
            result.IsThreat.Should().BeFalse();
            result.Message.Should().Contain("does not exist");
        }
    }
}
