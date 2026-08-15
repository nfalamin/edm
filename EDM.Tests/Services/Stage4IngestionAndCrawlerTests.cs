using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class Stage4IngestionAndCrawlerTests : TestBase
    {
        [Fact]
        public void UniversalIngestion_ClipboardAndDuplicateSuppression_WorksAsExpected()
        {
            var service = new UniversalDownloadIngestionService();
            string clipboard = "Hey check out https://example.com/build.zip and https://example.com/setup.exe";

            // Act 1: Initial Ingest
            var reqs1 = service.IngestFromClipboard(clipboard, @"C:\Downloads");
            reqs1.Should().HaveCount(2);
            reqs1[0].Url.Should().Be("https://example.com/build.zip");
            reqs1[1].Url.Should().Be("https://example.com/setup.exe");

            // Act 2: Immediate duplicate clipboard content -> Suppressed!
            var reqs2 = service.IngestFromClipboard(clipboard, @"C:\Downloads");
            reqs2.Should().BeEmpty("Duplicate URLs must be suppressed on repeated clipboard polling");
        }

        [Fact]
        public void UniversalIngestion_CommandLineParsing_ValidatesAndSanitizesParameters()
        {
            var service = new UniversalDownloadIngestionService();

            // Test 1: Valid CLI arguments
            string[] args1 = new[] { "--url", "https://example.com/game.iso", "--out", @"C:\Games", "--filename", "game_v2.iso", "--silent", "--exit" };
            var result1 = service.IngestFromCommandLine(args1, @"C:\Downloads");

            result1.ExitCode.Should().Be(0);
            result1.Requests.Should().ContainSingle();
            result1.Requests[0].Url.Should().Be("https://example.com/game.iso");
            result1.Requests[0].DestinationDirectory.Should().Be(@"C:\Games");
            result1.Requests[0].SuggestedFileName.Should().Be("game_v2.iso");
            result1.Requests[0].SilentMode.Should().BeTrue();
            result1.Requests[0].ExitAfterDownload.Should().BeTrue();

            // Test 2: Missing required --url
            string[] args2 = new[] { "--out", @"C:\Games" };
            var result2 = service.IngestFromCommandLine(args2, @"C:\Downloads");
            result2.ExitCode.Should().Be(1, "Missing URL must return code 1");

            // Test 3: Unsafe URL scheme
            string[] args3 = new[] { "--url", "javascript:alert(1)" };
            var result3 = service.IngestFromCommandLine(args3, @"C:\Downloads");
            result3.ExitCode.Should().Be(2, "Unsafe URL scheme must return security exit code 2");
        }

        [Fact]
        public void WebCrawler_SSRFProtection_BlocksPrivateAndLoopbackTargets()
        {
            // Block localhost / 127.0.0.1
            WebCrawlerSubsystem.IsSafeTargetUrl("http://localhost/admin", out string r1).Should().BeFalse();
            r1.Should().Contain("SSRF");

            WebCrawlerSubsystem.IsSafeTargetUrl("http://127.0.0.1:8080/secrets", out string r2).Should().BeFalse();
            r2.Should().Contain("SSRF");

            // Block Private RFC1918 ranges
            WebCrawlerSubsystem.IsSafeTargetUrl("http://10.0.0.5/api", out string r3).Should().BeFalse();
            WebCrawlerSubsystem.IsSafeTargetUrl("http://192.168.1.1/router", out string r4).Should().BeFalse();
            WebCrawlerSubsystem.IsSafeTargetUrl("http://172.16.0.1/intranet", out string r5).Should().BeFalse();

            // Allow public internet URLs
            WebCrawlerSubsystem.IsSafeTargetUrl("https://example.com/docs", out _).Should().BeTrue();
        }
    }
}
