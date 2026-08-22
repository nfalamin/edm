using System.IO;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class FileCategorizationTests
    {
        [Theory]
        [InlineData("movie.mp4", "Videos")]
        [InlineData("song.flac", "Music")]
        [InlineData("report.pdf", "Documents")]
        [InlineData("setup.exe", "Programs")]
        [InlineData("archive.7z", "Compressed")]
        [InlineData("photo.png", "Images")]
        [InlineData("unknown.xyz", "General")]
        public void GetTargetSubfolder_CategorizesCorrectly(string fileName, string expectedCategory)
        {
            string category = FileCategorizationService.GetTargetSubfolder(fileName);
            category.Should().Be(expectedCategory);
        }

        [Fact]
        public void ResolveDestinationPath_CreatesDirectoryAndReturnsPath()
        {
            string tempBaseDir = Path.Combine(Path.GetTempPath(), "EDM_Test_Downloads_" + Path.GetRandomFileName());
            try
            {
                string fullPath = FileCategorizationService.ResolveDestinationPath(tempBaseDir, "video.mp4");
                fullPath.Should().Contain("Videos");
                Directory.Exists(Path.Combine(tempBaseDir, "Videos")).Should().BeTrue();
            }
            finally
            {
                if (Directory.Exists(tempBaseDir))
                {
                    Directory.Delete(tempBaseDir, recursive: true);
                }
            }
        }
    }
}
