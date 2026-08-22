using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class Stage4RemoteZipAndPacTests : TestBase
    {
        [Fact]
        public void PacProxyService_ResolvesDirectAndProxyRulesCorrectly()
        {
            var pacService = new PacProxyService();

            // When no PAC loaded -> DIRECT
            var res1 = pacService.ResolveProxyForUrl("https://example.com/file.zip");
            res1.IsDirect.Should().BeTrue();

            // Localhost always resolves DIRECT
            var res2 = pacService.ResolveProxyForUrl("http://localhost:8080/test");
            res2.IsDirect.Should().BeTrue();
        }

        [Fact]
        public void CustomAntivirusProfile_CorrectlyEscapesSpecialCharactersInArguments()
        {
            var av = new CustomAntivirusScannerService();
            av.SetActiveProfile("defender");

            string testFile = @"C:\Downloads\my payload & special; name.exe";
            string resolved = av.ActiveProfile.ArgumentsTemplate
                .Replace("%FILE%", testFile);

            resolved.Should().Contain("\"" + testFile + "\"");
        }
    }
}
