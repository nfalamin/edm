using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using EDM.Services.Interfaces;
using Moq;
using Xunit;

namespace EDM.Tests.Services
{
    public class SecurityPipelineTests : IDisposable
    {
        private readonly string _testDir;

        public SecurityPipelineTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "EDM_Security_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, recursive: true);
                }
            }
            catch { }
        }

        #region 1. URL & SCHEME VALIDATION TESTS

        [Theory]
        [InlineData("https://example.com/file.iso", true)]
        [InlineData("http://download.org/archive.zip", true)]
        [InlineData("ftp://files.mirror.net/setup.exe", true)]
        [InlineData("ftps://secure.host.com/pkg.tar.gz", true)]
        [InlineData("magnet:?xt=urn:btih:d3b07384d113edec49eaa6238ad5ff00", true)]
        [InlineData("javascript:alert(1)", false)]
        [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==", false)]
        [InlineData("file:///C:/Windows/System32/calc.exe", false)]
        [InlineData("blob:https://youtube.com/123-abc", false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("not-a-valid-url", false)]
        public void Test01_UrlValidation_EnforcesStrictSecuritySchemes(string url, bool expectedValid)
        {
            var pipeline = DownloadSecurityPipeline.Instance;
            bool isValid = pipeline.ValidateUrl(url, out var error);
            Assert.Equal(expectedValid, isValid);
            if (!expectedValid)
            {
                Assert.NotEmpty(error);
            }
        }

        #endregion

        #region 2. PATH TRAVERSAL & DESTINATION SANITIZATION

        [Fact]
        public void Test02_DestinationSanitization_BlocksPathTraversalEscapes()
        {
            var pipeline = DownloadSecurityPipeline.Instance;
            string maliciousName = @"..\..\Windows\System32\cmd.exe";

            string safeDestination = pipeline.SanitizeDestination(_testDir, maliciousName);

            // Must not escape the base test directory
            Assert.StartsWith(_testDir, safeDestination, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("..", safeDestination);
        }

        #endregion

        #region 3. REDIRECT SECURITY POLICY

        [Fact]
        public void Test03_RedirectValidation_BlocksDangerousTargetSchemes()
        {
            var pipeline = DownloadSecurityPipeline.Instance;
            var original = new Uri("https://trusted.site/download");
            var dangerousTarget = new Uri("javascript:executeMalware()");

            bool isAllowed = pipeline.ValidateRedirect(original, dangerousTarget, out var error);
            Assert.False(isAllowed);
            Assert.Contains("forbidden scheme", error, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region 4. STREAMING SHA-256 HASH VERIFICATION

        [Fact]
        public async Task Test04_PostDownloadSecurity_ValidatesExpectedHashSuccessfully()
        {
            string filePath = Path.Combine(_testDir, "valid_payload.bin");
            byte[] fileBytes = Encoding.UTF8.GetBytes("Exclusive Download Manager High-Speed Secure Payload Content");
            await File.WriteAllBytesAsync(filePath, fileBytes);

            using var sha = SHA256.Create();
            string expectedHex = BitConverter.ToString(sha.ComputeHash(fileBytes)).Replace("-", string.Empty).ToLowerInvariant();

            var pipeline = DownloadSecurityPipeline.Instance;
            var context = new DownloadSecurityContext
            {
                Url = "https://example.com/payload.bin",
                FilePath = filePath,
                ExpectedSize = fileBytes.Length,
                ExpectedHashHex = expectedHex
            };

            var result = await pipeline.ProcessPostDownloadSecurityAsync(context, CancellationToken.None);

            Assert.Equal(SecurityDecision.SecurityApproved, result.Decision);
            Assert.Equal(VerificationState.Verified, result.VerificationState);
            Assert.Equal(expectedHex, result.ComputedHash);
        }

        [Fact]
        public async Task Test05_PostDownloadSecurity_FailsOnHashMismatch()
        {
            string filePath = Path.Combine(_testDir, "corrupted_payload.bin");
            await File.WriteAllBytesAsync(filePath, Encoding.UTF8.GetBytes("Corrupted content"));

            var pipeline = DownloadSecurityPipeline.Instance;
            var context = new DownloadSecurityContext
            {
                Url = "https://example.com/payload.bin",
                FilePath = filePath,
                ExpectedHashHex = "0000000000000000000000000000000000000000000000000000000000000000" // Wrong hash
            };

            var result = await pipeline.ProcessPostDownloadSecurityAsync(context, CancellationToken.None);

            Assert.Equal(SecurityDecision.SecurityVerificationFailed, result.Decision);
            Assert.Equal(VerificationState.VerificationFailed, result.VerificationState);
            Assert.Contains("mismatch", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region 5. AUTHENTICODE VERIFICATION

        [Fact]
        public void Test06_AuthenticodeVerifier_ReportsUnsignedCorrectly()
        {
            string fakeExe = Path.Combine(_testDir, "unsigned_tool.exe");
            File.WriteAllBytes(fakeExe, new byte[] { 0x4D, 0x5A, 0x90, 0x00 }); // MZ header

            var sigResult = AuthenticodeVerifier.VerifyFile(fakeExe);
            Assert.False(sigResult.IsSigned);
            Assert.False(sigResult.IsValid);
            Assert.Contains("Unsigned", sigResult.StatusMessage);
        }

        #endregion

        #region 6. STATE SYNCHRONIZATION

        [Fact]
        public void Test07_ApplySecurityResult_SynchronizesDownloadItemState()
        {
            var item = new DownloadItem
            {
                FileName = "Installer.exe",
                Url = "https://cdn.example.org/Installer.exe",
                SavePath = @"C:\Downloads\Installer.exe"
            };

            var result = new SecurityPipelineResult
            {
                Decision = SecurityDecision.SecurityApproved,
                VerificationState = VerificationState.Verified,
                ComputedHash = "abcdef123456",
                ExpectedHash = "abcdef123456",
                Message = "Security Approved"
            };

            DownloadSecurityPipeline.Instance.ApplySecurityResultToDownloadItem(item, result);

            Assert.Equal(VerificationState.Verified, item.VerificationState);
            Assert.Equal("abcdef123456", item.ComputedVerificationHash);
            Assert.Equal("SHA-256", item.VerificationAlgorithm);
            Assert.Equal("Security Approved", item.VerificationMessage);
        }

        #endregion
    }
}
