using System;
using System.IO;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class SecurityHardeningTests : TestBase
    {
        [Theory]
        [InlineData("https://example.com/file.zip", true)]
        [InlineData("http://example.com/file.zip", true)]
        [InlineData("ftp://example.com/file.zip", true)]
        [InlineData("javascript:alert(1)", false)]
        [InlineData("file:///C:/Windows/System32/cmd.exe", false)]
        [InlineData("data:text/html,<html></html>", false)]
        public void IsAllowedUrlScheme_RejectsDangerousSchemes(string url, bool expectedResult)
        {
            // Act
            bool result = SecuritySanitizer.IsAllowedUrlScheme(url);

            // Assert
            result.Should().Be(expectedResult);
        }

        [Theory]
        [InlineData("normal_filename.mp4", "normal_filename.mp4")]
        [InlineData("../../etc/passwd", "passwd")]
        [InlineData(@"..\..\Windows\System32\cmd.exe", "cmd.exe")]
        [InlineData("CON.txt", "_CON.txt")]
        [InlineData("AUX.mp3", "_AUX.mp3")]
        [InlineData("file:with*invalid?chars.zip", "filewithinvalidchars.zip")]
        public void SanitizeFileName_StripsDangerousPathsAndReservedNames(string input, string expected)
        {
            // Act
            string sanitized = SecuritySanitizer.SanitizeFileName(input);

            // Assert
            sanitized.Should().Be(expected);
        }

        [Fact]
        public void TrySanitizeDestinationPath_BlocksPathTraversalOutsideBase()
        {
            // Arrange
            string baseDir = Path.GetTempPath();
            string maliciousPath = "../../../Windows/System32/malicious.dll";

            // Act
            bool isValid = SecuritySanitizer.TrySanitizeDestinationPath(baseDir, maliciousPath, out string safePath);

            // Assert
            isValid.Should().BeFalse("Path traversal outside base directory must be rejected");
            safePath.Should().BeEmpty();
        }

        [Fact]
        public void CreateSafeProcessStartInfo_UsesArgumentListWithoutShellConcatenation()
        {
            // Act
            var psi = SecuritySanitizer.CreateSafeProcessStartInfo("ffmpeg.exe", "-i", "input.mp4", "output.mkv; rm -rf /");

            // Assert
            psi.UseShellExecute.Should().BeFalse();
            psi.ArgumentList.Should().HaveCount(3);
            psi.ArgumentList[2].Should().Be("output.mkv; rm -rf /");
        }
    }
}
