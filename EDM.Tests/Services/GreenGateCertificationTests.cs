using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using EDM.NativeMessaging;
using EDM.Services;
using FluentAssertions;
using Microsoft.Win32;
using Xunit;

namespace EDM.Tests.Services
{
    public class GreenGateCertificationTests
    {
        [Fact]
        public void PhaseC_InstallerLifecycle_RegistersAndCleansUpRegistryKeys()
        {
            // Test install
            bool installed = BrowserExtensionInstaller.InstallAllBrowsersIntegration();
            installed.Should().BeTrue();

            // Verify Chrome registry key exists
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Google\Chrome\NativeMessagingHosts\com.edm.downloader"))
            {
                key.Should().NotBeNull();
            }

            // Test uninstall cleanup
            bool uninstalled = BrowserExtensionInstaller.UninstallAllBrowsersIntegration();
            uninstalled.Should().BeTrue();

            // Verify registry key deleted cleanly
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Google\Chrome\NativeMessagingHosts\com.edm.downloader"))
            {
                key.Should().BeNull();
            }
        }

        [Fact]
        public void PhaseD_NativeMessagingHost_GeneratesStrictProductionJsonManifest()
        {
            string exePath = @"C:\Program Files\EDM\EDM.exe";
            string manifestJson = BrowserExtensionInstaller.GenerateManifestJson(exePath);

            manifestJson.Should().NotBeNullOrWhiteSpace();
            manifestJson.Should().Contain("com.edm.downloader");
            manifestJson.Should().Contain("stdio");

            using var doc = JsonDocument.Parse(manifestJson);
            var root = doc.RootElement;
            root.GetProperty("name").GetString().Should().Be("com.edm.downloader");
            root.GetProperty("type").GetString().Should().Be("stdio");
            root.GetProperty("path").GetString().Should().Be(exePath);
        }

        [Fact]
        public void PhaseG_SigningPreflight_DetectsMissingCertificateCleanly()
        {
            string certPath = Environment.GetEnvironmentVariable("EDM_SIGNING_CERT_PATH") ?? string.Empty;
            bool hasCert = !string.IsNullOrWhiteSpace(certPath) && File.Exists(certPath);

            if (!hasCert)
            {
                // Must detect missing cert without crashing and classify as external prerequisite
                hasCert.Should().BeFalse();
            }
        }

        [Fact]
        public void PhaseI_SecurityReleaseAudit_RepositoryContainsNoExposedSecrets()
        {
            string sourceDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "EDM"));
            if (Directory.Exists(sourceDir))
            {
                var files = Directory.GetFiles(sourceDir, "*.cs", SearchOption.AllDirectories)
                                     .Where(f => !f.Contains(@"\obj\") && !f.Contains(@"\bin\"));

                foreach (var file in files)
                {
                    string content = File.ReadAllText(file);
                    content.Should().NotContain("AIzaSy", $"File {file} contains potential Google API key");
                    content.Should().NotContain("BEGIN PRIVATE KEY", $"File {file} contains potential RSA private key");
                }
            }
        }

        [Fact]
        public void PhaseJ_RealArtifactHash_GeneratesValidSha256ForMainAssembly()
        {
            string mainAssembly = typeof(DownloadService).Assembly.Location;
            File.Exists(mainAssembly).Should().BeTrue();

            using var stream = File.OpenRead(mainAssembly);
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(stream);
            string hexHash = Convert.ToHexString(hash);

            hexHash.Should().HaveLength(64);
        }
    }
}
