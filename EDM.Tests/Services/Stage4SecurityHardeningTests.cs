using System;
using System.IO;
using System.IO.Compression;
using FluentAssertions;
using Xunit;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class Stage4SecurityHardeningTests : TestBase
    {
        [Fact]
        public void SecureCredentialVault_EncryptsAndDecryptsSecretsViaDPAPI()
        {
            string originalSecret = "SuperSecret_P@ssw0rd#2026";
            string encrypted = SecureCredentialVault.EncryptSecret(originalSecret);

            encrypted.Should().NotBeNullOrWhiteSpace();
            encrypted.Should().NotBe(originalSecret, "Plaintext must not be exposed in encrypted output");

            string decrypted = SecureCredentialVault.DecryptSecret(encrypted);
            decrypted.Should().Be(originalSecret, "Decrypted secret must match original input");
        }

        [Fact]
        public void LogCredentialRedaction_StripsSensitiveTokensAndCredentials()
        {
            string rawLog = "Connecting with Basic dXNlcjpwYXNz and Authorization: Bearer eyJhbGciOi... and https://site.com/dl?token=secret123&password=myPass";
            string sanitized = SecureCredentialVault.RedactCredentialsFromText(rawLog);

            sanitized.Should().NotContain("dXNlcjpwYXNz");
            sanitized.Should().NotContain("eyJhbGciOi");
            sanitized.Should().NotContain("secret123");
            sanitized.Should().NotContain("myPass");
            sanitized.Should().Contain("Basic [REDACTED]");
            sanitized.Should().Contain("Bearer [REDACTED]");
            sanitized.Should().Contain("token=[REDACTED]");
            sanitized.Should().Contain("password=[REDACTED]");
        }

        [Fact]
        public void SecuritySanitizer_NormalizesReservedWindowsDeviceNames()
        {
            SecuritySanitizer.SanitizeFileName("CON.txt").Should().Be("_CON.txt");
            SecuritySanitizer.SanitizeFileName("PRN.pdf").Should().Be("_PRN.pdf");
            SecuritySanitizer.SanitizeFileName("NUL.zip").Should().Be("_NUL.zip");
            SecuritySanitizer.SanitizeFileName("COM1.bin").Should().Be("_COM1.bin");
            SecuritySanitizer.SanitizeFileName("LPT1.iso").Should().Be("_LPT1.iso");
        }

        [Fact]
        public void SecuritySanitizer_RejectsUnsafeUrlSchemes()
        {
            SecuritySanitizer.IsAllowedUrlScheme("javascript:alert(1)").Should().BeFalse();
            SecuritySanitizer.IsAllowedUrlScheme("file:///C:/Windows/System32/calc.exe").Should().BeFalse();
            SecuritySanitizer.IsAllowedUrlScheme("data:text/html,<html></html>").Should().BeFalse();

            SecuritySanitizer.IsAllowedUrlScheme("https://example.com/download.zip").Should().BeTrue();
            SecuritySanitizer.IsAllowedUrlScheme("http://example.com/file.bin").Should().BeTrue();
            SecuritySanitizer.IsAllowedUrlScheme("ftp://ftp.example.com/archive.tar").Should().BeTrue();
        }

        [Fact]
        public void SafeProcessStartInfo_CreatesSafeNonShellArguments()
        {
            var psi = SecuritySanitizer.CreateSafeProcessStartInfo("ffmpeg.exe", "-i", "input; rm -rf /", "-c:v", "copy");

            psi.UseShellExecute.Should().BeFalse();
            psi.CreateNoWindow.Should().BeTrue();
            psi.ArgumentList.Should().HaveCount(4);
            psi.ArgumentList[1].Should().Be("input; rm -rf /", "Arguments must be passed as distinct array items without shell string interpolation");
        }

        [Fact]
        public void SafeArchiveExtractor_DetectsAndBlocksZipSlipDirectoryTraversal()
        {
            string tempZip = Path.Combine(Path.GetTempPath(), $"malicious_zipslip_{Guid.NewGuid():N}.zip");
            string targetDir = Path.Combine(Path.GetTempPath(), $"zipslip_target_{Guid.NewGuid():N}");

            try
            {
                // Create a ZIP with a malicious path traversal entry
                using (var zipStream = new FileStream(tempZip, FileMode.Create))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry(@"../../evil.txt");
                    using var writer = new StreamWriter(entry.Open());
                    writer.Write("Malicious payload");
                }

                bool result = SafeArchiveExtractor.SafeExtractZip(tempZip, targetDir, out string error);
                result.Should().BeFalse("ZipSlip traversal must be blocked");
                error.Should().Contain("Path traversal attempt detected");
            }
            finally
            {
                if (File.Exists(tempZip)) File.Delete(tempZip);
                if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
            }
        }
    }
}
