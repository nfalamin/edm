using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using EDM.Services;
using FluentAssertions;
using Microsoft.Win32;
using Xunit;

namespace EDM.Tests.Services
{
    public class ProductionReleaseCertificationTests
    {
        [Fact]
        public void PartA_ReleaseBuild_BinaryExistsAndHasValidVersion()
        {
            string assemblyPath = typeof(DownloadService).Assembly.Location;
            File.Exists(assemblyPath).Should().BeTrue();

            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(assemblyPath);
            versionInfo.FileVersion.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void PartC_NativeHostManifest_GeneratesValidJsonWithCorrectOrigins()
        {
            string samplePath = @"C:\Program Files\EDM\EDM.NativeHost.exe";
            string chromeJson = BrowserExtensionInstaller.GenerateChromiumManifestJson(samplePath);
            string firefoxJson = BrowserExtensionInstaller.GenerateFirefoxManifestJson(samplePath);

            chromeJson.Should().Contain("com.edm.downloader");
            chromeJson.Should().Contain("stdio");
            chromeJson.Should().Contain("chrome-extension://knldjmfmopnpolahpmmgbagdohdnhkda/");

            firefoxJson.Should().Contain("com.edm.downloader");
            firefoxJson.Should().Contain("stdio");
            firefoxJson.Should().Contain("edm-extension@edm.app");

            using var doc = JsonDocument.Parse(chromeJson);
            var root = doc.RootElement;
            root.GetProperty("name").GetString().Should().Be("com.edm.downloader");
            root.GetProperty("type").GetString().Should().Be("stdio");
        }

        [Fact]
        public void PartB_BrowserRegistration_RegistryInstallerExecutesWithoutException()
        {
            bool success = BrowserExtensionInstaller.InstallAllBrowsersIntegration();
            success.Should().BeTrue();

            // Verify Chrome registry entry
            using var chromeKey = Registry.CurrentUser.OpenSubKey(@"Software\Google\Chrome\NativeMessagingHosts\com.edm.downloader");
            chromeKey.Should().NotBeNull();

            // Verify Edge registry entry
            using var edgeKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Edge\NativeMessagingHosts\com.edm.downloader");
            edgeKey.Should().NotBeNull();

            // Verify Firefox registry entry
            using var firefoxKey = Registry.CurrentUser.OpenSubKey(@"Software\Mozilla\NativeMessagingHosts\com.edm.downloader");
            firefoxKey.Should().NotBeNull();

            // Cleanup
            BrowserExtensionInstaller.UninstallAllBrowsersIntegration();
        }

        [Fact]
        public void PartH_ReleaseArtifacts_CalculatesValidSha256Checksum()
        {
            string assemblyPath = typeof(DownloadService).Assembly.Location;
            using var stream = File.OpenRead(assemblyPath);
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(stream);
            string hexHash = Convert.ToHexString(hash);

            hexHash.Should().HaveLength(64);
        }
    }
}
