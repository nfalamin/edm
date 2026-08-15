using System.IO;
using EDM.Services.Helpers;
using Xunit;

namespace EDM.Tests
{
    public class FileNamingHelperTests
    {
        [Fact]
        public void SanitizeFileName_RemovesInvalidChars()
        {
            var invalids = Path.GetInvalidFileNameChars();
            string input = "test" + new string(invalids) + "name.txt";
            var outName = FileNamingHelper.SanitizeFileName(input);
            foreach (var c in invalids)
            {
                Assert.DoesNotContain(c, outName);
            }
            Assert.Contains("test", outName);
            Assert.Contains("name.txt", outName);
        }

        [Theory]
        [InlineData("video/mp4", ".mp4")]
        [InlineData("video/mp4; charset=utf-8", ".mp4")]
        [InlineData("application/json", ".json")]
        [InlineData("unknown/mime", ".bin")]
        public void GetExtensionFromMime_ReturnsExpected(string mime, string expected)
        {
            var ext = FileNamingHelper.GetExtensionFromMime(mime);
            Assert.Equal(expected, ext);
        }
    }
}
