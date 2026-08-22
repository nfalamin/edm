using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class BrowserExtensionIntegrityTests
    {
        private readonly string _toolsDir;

        public BrowserExtensionIntegrityTests()
        {
            // Locate solution tools directory
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo dir = new DirectoryInfo(baseDir);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "EDM.slnx")))
            {
                dir = dir.Parent;
            }
            _toolsDir = dir != null ? Path.Combine(dir.FullName, "tools") : string.Empty;
        }

        [Fact]
        public void ExtensionManifests_ExistAndAreValidJson()
        {
            if (string.IsNullOrEmpty(_toolsDir) || !Directory.Exists(_toolsDir)) return;

            string[] extensionDirs = new[] { "chrome-extension", "edge-extension", "firefox-extension" };

            foreach (var extDir in extensionDirs)
            {
                string manifestPath = Path.Combine(_toolsDir, extDir, "manifest.json");
                File.Exists(manifestPath).Should().BeTrue($"Manifest should exist for {extDir}");

                string jsonContent = File.ReadAllText(manifestPath);
                var doc = JsonDocument.Parse(jsonContent);
                doc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);

                doc.RootElement.TryGetProperty("name", out var nameProp).Should().BeTrue();
                nameProp.GetString().Should().NotBeNullOrEmpty();
            }
        }

        [Fact]
        public void ExtensionContentScripts_ContainVideoOverlayAndMutationObserverLogic()
        {
            if (string.IsNullOrEmpty(_toolsDir) || !Directory.Exists(_toolsDir)) return;

            string[] extensionDirs = new[] { "chrome-extension", "edge-extension", "firefox-extension" };

            foreach (var extDir in extensionDirs)
            {
                string contentJsPath = Path.Combine(_toolsDir, extDir, "content.js");
                string contentCssPath = Path.Combine(_toolsDir, extDir, "content.css");

                File.Exists(contentJsPath).Should().BeTrue($"content.js must exist for {extDir}");
                File.Exists(contentCssPath).Should().BeTrue($"content.css must exist for {extDir}");

                string jsText = File.ReadAllText(contentJsPath);
                string cssText = File.ReadAllText(contentCssPath);

                jsText.Should().Contain("MediaCandidateDetector", $"content.js in {extDir} must implement MediaCandidateDetector");
                jsText.Should().Contain("IdmDownloadOverlay", $"content.js in {extDir} must implement IdmDownloadOverlay");
                jsText.Should().Contain("MutationObserver", $"content.js in {extDir} must use MutationObserver for dynamic video detection");
                jsText.Should().Contain("generateDownloadIdentity", $"content.js in {extDir} must implement deterministic DownloadIdentity calculation");

                cssText.Should().Contain("edm-floating-panel", $"content.css in {extDir} must style the floating panel");
                cssText.Should().Contain("edm-floating-btn", $"content.css in {extDir} must style the download button");
            }
        }
    }
}
