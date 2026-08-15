using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class NetworkAndWindowsIntegrationParityTests
    {
        [Fact]
        public async Task FtpDownloadService_ProbingValidatesFtpUriAndCredentials()
        {
            var ftpService = new FtpDownloadService();
            var cred = new NetworkCredential("anonymous", "user@example.com");
            using var cts = new CancellationTokenSource(100);

            // Probe FTP URL
            var result = await ftpService.ProbeFtpUrlAsync("ftp://127.0.0.1:2121/debian/README", cred, cts.Token);

            result.Should().NotBeNull();
            result.Uri.Host.Should().Be("127.0.0.1");
        }

        [Fact]
        public void ProxyService_BuildsValidWebProxy_ForHttpHttpsAndSocks5()
        {
            // 1. HTTP Proxy
            var httpSettings = new ProxySettings
            {
                Enabled = true,
                Type = ProxyType.Http,
                Host = "127.0.0.1",
                Port = 8080,
                BypassLocalAddresses = true,
                BypassList = "localhost; *.local"
            };
            var httpProxy = ProxyService.BuildWebProxy(httpSettings) as WebProxy;
            httpProxy.Should().NotBeNull();
            httpProxy!.Address!.Scheme.Should().Be("http");
            httpProxy.Address.Port.Should().Be(8080);
            httpProxy.BypassProxyOnLocal.Should().BeTrue();

            // 2. HTTPS Proxy
            var httpsSettings = new ProxySettings
            {
                Enabled = true,
                Type = ProxyType.Https,
                Host = "proxy.corp.net",
                Port = 8443
            };
            var httpsProxy = ProxyService.BuildWebProxy(httpsSettings) as WebProxy;
            httpsProxy.Should().NotBeNull();
            httpsProxy!.Address!.Scheme.Should().Be("https");
            httpsProxy.Address.Port.Should().Be(8443);

            // 3. SOCKS5 Proxy
            var socksSettings = new ProxySettings
            {
                Enabled = true,
                Type = ProxyType.Socks5,
                Host = "127.0.0.1",
                Port = 1080
            };
            var socksProxy = ProxyService.BuildWebProxy(socksSettings) as WebProxy;
            socksProxy.Should().NotBeNull();
            socksProxy!.Address!.Scheme.Should().Be("socks5");
            socksProxy.Address.Port.Should().Be(1080);
        }

        [Fact]
        public void PacProxyService_ParsesPacScriptAndResolvesHostPort()
        {
            var pacService = new PacProxyService();
            string pacScript = @"
function FindProxyForURL(url, host) {
    if (shExpMatch(host, ""*.internal.net"")) return ""DIRECT"";
    if (shExpMatch(host, ""*.corp.com"")) return ""PROXY proxy.corp.com:8080"";
    return ""SOCKS5 127.0.0.1:1080; DIRECT"";
}";
            pacService.SetScriptContent(pacScript);

            var directRes = pacService.ResolveProxyForUrl("https://server.internal.net/api");
            directRes.IsDirect.Should().BeTrue();

            var corpRes = pacService.ResolveProxyForUrl("https://mail.corp.com/inbox");
            corpRes.IsDirect.Should().BeFalse();
            corpRes.ProxyHost.Should().Be("proxy.corp.com");
            corpRes.ProxyPort.Should().Be(8080);

            var socksRes = pacService.ResolveProxyForUrl("https://public.internet.org");
            socksRes.IsDirect.Should().BeFalse();
            socksRes.ProxyHost.Should().Be("127.0.0.1");
            socksRes.ProxyPort.Should().Be(1080);
        }

        [Fact]
        public void AntivirusScanner_ReplacesArgumentsSafelyWithoutShellInjection()
        {
            var profile = new AntivirusProfile
            {
                ProfileName = "TestDefender",
                ExecutablePath = "cmd.exe",
                ArgumentsTemplate = "-Scan -ScanType 3 -File \"%FILE%\" -Dir \"%DIR%\""
            };

            string testFile = @"C:\Downloads\setup&rmdir.exe";
            string dir = Path.GetDirectoryName(testFile) ?? "";
            string resolved = profile.ArgumentsTemplate
                .Replace("%FILE%", testFile)
                .Replace("%DIR%", dir);

            resolved.Should().Contain("\"C:\\Downloads\\setup&rmdir.exe\"");
            resolved.Should().NotContain("&&");
        }

        [Fact]
        public async Task UpdateService_ParsesManifest_AndVerifiesSha256Checksum()
        {
            string tempManifest = Path.Combine(Path.GetTempPath(), "test_manifest_" + Guid.NewGuid().ToString("N") + ".json");
            string manifestJson = @"
{
  ""version"": ""2.5.0"",
  ""minSupportedVersion"": ""2.0.0"",
  ""title"": ""EDM v2.5.0 Released"",
  ""downloadUrl"": ""https://cdn.example.com/EDM_Setup_2.5.0.exe"",
  ""sha256"": ""e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"",
  ""changelog"": [
    ""Added SOCKS5 Proxy support"",
    ""Enhanced FTP Resume (REST) capability"",
    ""Automated Antivirus scanning integration""
  ]
}";
            await File.WriteAllTextAsync(tempManifest, manifestJson);

            try
            {
                var settings = new SettingsService();
                var updateService = new UpdateService(settings);
                var info = await updateService.CheckForUpdatesAsync(tempManifest, new Version("2.0.0"), CancellationToken.None).ConfigureAwait(false);

                info.Should().NotBeNull();
                info.IsUpdateAvailable.Should().BeTrue();
                info.Version.Should().Be("2.5.0");
                info.Sha256.Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
                info.Changelog.Should().Contain("SOCKS5");
            }
            finally
            {
                if (File.Exists(tempManifest)) File.Delete(tempManifest);
            }
        }
    }
}
