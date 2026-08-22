using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class Stage3BrowserExtensionTests
    {
        private readonly string _rootDir;

        public Stage3BrowserExtensionTests()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo dir = new DirectoryInfo(baseDir);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "EDM.slnx")))
            {
                dir = dir.Parent;
            }
            _rootDir = dir != null ? dir.FullName : Directory.GetCurrentDirectory();
        }

        [Fact]
        public void Stage3_ContentScript_ImplementsConfidenceAndCandidateHierarchy()
        {
            string contentJsPath = Path.Combine(_rootDir, "extension", "chrome", "content.js");
            File.Exists(contentJsPath).Should().BeTrue();

            string js = File.ReadAllText(contentJsPath);

            // CandidateConfidence levels
            js.Should().Contain("CandidateConfidence");
            js.Should().Contain("HIGH: 'HIGH'");
            js.Should().Contain("MEDIUM: 'MEDIUM'");
            js.Should().Contain("LOW: 'LOW'");

            // CandidateState lifecycle
            js.Should().Contain("CandidateState");
            js.Should().Contain("DISCOVERED");
            js.Should().Contain("ANALYZING");
            js.Should().Contain("READY");
            js.Should().Contain("SELECTOR_OPEN");
            js.Should().Contain("DOWNLOADING");
            js.Should().Contain("COMPLETED");
            js.Should().Contain("FAILED");
            js.Should().Contain("STALE");
            js.Should().Contain("DESTROYED");
        }

        [Fact]
        public void Stage3_ContentScript_FiltersOutAdsAndDecorativeGIFs()
        {
            string contentJsPath = Path.Combine(_rootDir, "extension", "chrome", "content.js");
            string js = File.ReadAllText(contentJsPath);

            // Anti-false-positive method exists
            js.Should().Contain("isAdOrDecorativeElement");
            js.Should().Contain("ad-showing");
            js.Should().Contain("video-ads");

            // Minimum dimension bounds for valid video
            js.Should().Contain("isValidMediaElement");
            js.Should().Contain("180"); // min width
            js.Should().Contain("100"); // min height
        }

        [Fact]
        public void Stage3_ContentScript_SortsRepresentationsDescendingByHeight()
        {
            string contentJsPath = Path.Combine(_rootDir, "extension", "chrome", "content.js");
            string js = File.ReadAllText(contentJsPath);

            // Verify sorting logic in renderVariants: Height descending, then EstimatedSizeBytes, then Bitrate
            js.Should().Contain("sortedVariants");
            js.Should().Contain("hB - hA");
            js.Should().Contain("sB - sA");
        }

        [Fact]
        public void Stage3_ContentScript_HandlesRealSizeAndUnknownSizeTruthfully()
        {
            string contentJsPath = Path.Combine(_rootDir, "extension", "chrome", "content.js");
            string js = File.ReadAllText(contentJsPath);

            // Size formatter returns truthful unavailable string instead of 0 MB
            js.Should().Contain("function formatBytes(bytes)");
            js.Should().Contain("Size unavailable");
            js.Should().NotContain("'0 MB'");
        }

        [Fact]
        public void Stage3_ContentScript_ProtectsAgainstStaleResponsesOnSpaNavigation()
        {
            string contentJsPath = Path.Combine(_rootDir, "extension", "chrome", "content.js");
            string js = File.ReadAllText(contentJsPath);

            // Stale protection checks
            js.Should().Contain("currentRequestId");
            js.Should().Contain("this.currentRequestId !== requestId");
            js.Should().Contain("hookHistoryApi");
            js.Should().Contain("pushState");
            js.Should().Contain("replaceState");
            js.Should().Contain("yt-navigate-finish");
            js.Should().Contain("popstate");
        }

        [Fact]
        public void Stage3_ContentScript_PreservesDeterministicDownloadIdentityAndPreventsDuplicates()
        {
            string contentJsPath = Path.Combine(_rootDir, "extension", "chrome", "content.js");
            string js = File.ReadAllText(contentJsPath);

            // Deterministic downloadIdentity calculation
            js.Should().Contain("generateDownloadIdentity");
            js.Should().Contain("activeJobIdentities");
            js.Should().Contain("activeJobIdentities.has(downloadIdentity)");
        }

        [Fact]
        public void Stage3_ContentCss_IsFullyIsolatedWithEdmNamespace()
        {
            string contentCssPath = Path.Combine(_rootDir, "extension", "chrome", "content.css");
            File.Exists(contentCssPath).Should().BeTrue();

            string css = File.ReadAllText(contentCssPath);

            // Core namespaces
            css.Should().Contain(".edm-floating-panel");
            css.Should().Contain(".edm-floating-btn");
            css.Should().Contain(".edm-dropdown-card");
            css.Should().Contain(".edm-variant-row");
            css.Should().Contain(".edm-audio-row");
            css.Should().Contain(".edm-spinner");

            // Isolation: every CSS rule in file should be scoped under .edm-
            // Strip comments and keyframes
            string strippedCss = Regex.Replace(css, @"/\*[\s\S]*?\*/", "");
            strippedCss = Regex.Replace(strippedCss, @"@keyframes[^{]+\{(?:[^{}]+|\{[^{}]*\})*\}", "");

            var matches = Regex.Matches(strippedCss, @"(?m)^([^{]+)\{");
            foreach (Match m in matches)
            {
                string selector = m.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(selector)) continue;
                if (selector.StartsWith("@keyframes") || selector.StartsWith("from") || selector.StartsWith("to")) continue;
                selector.Should().Contain(".edm-", $"Selector '{selector}' must be strictly scoped to .edm- namespace");
            }
        }

        [Fact]
        public void Stage3_BackgroundWorker_PreservesAuthoritativeContractFields()
        {
            string bgJsPath = Path.Combine(_rootDir, "extension", "chrome", "background.js");
            File.Exists(bgJsPath).Should().BeTrue();

            string bgJs = File.ReadAllText(bgJsPath);

            // All 22 contract fields
            bgJs.Should().Contain("downloadIdentity");
            bgJs.Should().Contain("correlationId");
            bgJs.Should().Contain("videoUrl");
            bgJs.Should().Contain("audioUrl");
            bgJs.Should().Contain("manifestUrl");
            bgJs.Should().Contain("pageUrl");
            bgJs.Should().Contain("filename");
            bgJs.Should().Contain("fileName");
            bgJs.Should().Contain("quality");
            bgJs.Should().Contain("format");
            bgJs.Should().Contain("formatId");
            bgJs.Should().Contain("formatArg");
            bgJs.Should().Contain("width");
            bgJs.Should().Contain("height");
            bgJs.Should().Contain("fps");
            bgJs.Should().Contain("videoCodec");
            bgJs.Should().Contain("codec");
            bgJs.Should().Contain("audioCodec");
            bgJs.Should().Contain("container");
            bgJs.Should().Contain("requiresFfmpegMerge");
            bgJs.Should().Contain("estimatedSizeBytes");
            bgJs.Should().Contain("isAudioOnly");
        }

        [Fact]
        public void Stage3_AllExtensionDirectories_AreSynchronized()
        {
            string canonicalJs = File.ReadAllText(Path.Combine(_rootDir, "extension", "chrome", "content.js"));
            string canonicalCss = File.ReadAllText(Path.Combine(_rootDir, "extension", "chrome", "content.css"));

            string[] targetDirs = new[]
            {
                Path.Combine(_rootDir, "extension", "firefox"),
                Path.Combine(_rootDir, "tools", "chrome-extension"),
                Path.Combine(_rootDir, "tools", "edge-extension"),
                Path.Combine(_rootDir, "tools", "firefox-extension")
            };

            foreach (var dir in targetDirs)
            {
                string jsPath = Path.Combine(dir, "content.js");
                string cssPath = Path.Combine(dir, "content.css");

                File.Exists(jsPath).Should().BeTrue($"content.js must exist in {dir}");
                File.Exists(cssPath).Should().BeTrue($"content.css must exist in {dir}");

                string targetJs = File.ReadAllText(jsPath);
                string targetCss = File.ReadAllText(cssPath);

                targetJs.Should().Be(canonicalJs, $"content.js in {dir} must be bit-for-bit synchronized with canonical version");
                targetCss.Should().Be(canonicalCss, $"content.css in {dir} must be bit-for-bit synchronized with canonical version");
            }
        }
    }
}
