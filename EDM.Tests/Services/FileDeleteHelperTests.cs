using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services.Helpers;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class FileDeleteHelperTests : IDisposable
    {
        private readonly string _tempFile;

        public FileDeleteHelperTests()
        {
            _tempFile = Path.Combine(Path.GetTempPath(), $"EDM_TestDel_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(_tempFile, "Test File Content for Deletion");
        }

        public void Dispose()
        {
            try { if (File.Exists(_tempFile)) File.Delete(_tempFile); } catch { }
        }

        [Fact]
        public async Task DeleteFileSafeAsync_DeletesExistingFileOnFirstAttempt()
        {
            File.Exists(_tempFile).Should().BeTrue();

            bool result = await FileDeleteHelper.DeleteFileSafeAsync(_tempFile);

            result.Should().BeTrue("File should be successfully deleted");
            File.Exists(_tempFile).Should().BeFalse();
        }

        [Fact]
        public async Task DeleteFileSafeAsync_ReturnsTrueForAlreadyDeletedOrNonExistentFile()
        {
            string missingPath = Path.Combine(Path.GetTempPath(), $"EDM_Missing_{Guid.NewGuid():N}.tmp");

            bool result = await FileDeleteHelper.DeleteFileSafeAsync(missingPath);

            result.Should().BeTrue("Non-existent files should return true immediately");
        }

        [Fact]
        public async Task DeleteFileSafeAsync_RetriesAndSucceedsWhenLockIsReleased()
        {
            // Lock the file briefly, then release it asynchronously after 50ms
            var fileStream = new FileStream(_tempFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var releaseTask = Task.Run(async () =>
            {
                await Task.Delay(50);
                fileStream.Dispose();
            });

            bool result = await FileDeleteHelper.DeleteFileSafeAsync(_tempFile, maxAttempts: 3, delayMs: 100);
            await releaseTask;

            result.Should().BeTrue("Deletion should succeed on retry once file lock is released");
            File.Exists(_tempFile).Should().BeFalse();
        }

        [Fact]
        public async Task DeleteFileSafeAsync_ReturnsFalseWhenFileRemainsLockedConsecutively()
        {
            // Keep file locked permanently for duration of test
            using var fileStream = new FileStream(_tempFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            bool result = await FileDeleteHelper.DeleteFileSafeAsync(_tempFile, maxAttempts: 3, delayMs: 20);

            result.Should().BeFalse("Deletion should return false when 3 consecutive attempts fail due to lock");
            File.Exists(_tempFile).Should().BeTrue();
        }

        [Fact]
        public async Task DeleteFileSafeAsync_RespectsCancellationToken()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            bool result = await FileDeleteHelper.DeleteFileSafeAsync(_tempFile, cancellationToken: cts.Token);

            result.Should().BeFalse("Cancelled operation must return false without deleting");
        }

        [Fact]
        public async Task DeleteFileSafeAsync_AbortsAndReturnsFalseForDirectoryPath()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"EDM_TestDir_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                bool result = await FileDeleteHelper.DeleteFileSafeAsync(tempDir);
                result.Should().BeFalse("Directory paths must not be deleted by FileDeleteHelper");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir);
            }
        }
    }
}
