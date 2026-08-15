using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class SiteGrabberAndQueueParityTests
    {
        [Fact]
        public void SecureCredentialVault_EncryptsAndDecrypts_WithDpapi()
        {
            string host = "test-domain-" + Guid.NewGuid().ToString("N");
            string user = "admin_user";
            string pass = "SuperSecretP@ssw0rd!#";

            // Encrypt and save
            SecureCredentialVault.SaveCredentials(host, user, pass);

            // Retrieve
            bool found = SecureCredentialVault.TryGetCredentials(host, out string fetchedUser, out string fetchedPass);
            found.Should().BeTrue();
            fetchedUser.Should().Be(user);
            fetchedPass.Should().Be(pass);

            // List
            var list = SecureCredentialVault.GetAllCredentials();
            list.Any(c => c.Host == host && c.Username == user).Should().BeTrue();

            // Delete
            SecureCredentialVault.DeleteCredentials(host);
            bool foundAfter = SecureCredentialVault.TryGetCredentials(host, out _, out _);
            foundAfter.Should().BeFalse();
        }

        [Fact]
        public void SecureCredentialVault_RedactsSensitiveInformation()
        {
            string logLine = "Downloading http://example.com/api?password=SuperSecret&token=abc12345 with Basic dXNlcjpwYXNz";
            string redacted = SecureCredentialVault.RedactCredentialsFromText(logLine);

            redacted.Should().NotContain("SuperSecret");
            redacted.Should().NotContain("abc12345");
            redacted.Should().NotContain("dXNlcjpwYXNz");
            redacted.Should().Contain("[REDACTED]");
        }

        [Fact]
        public void DownloadCategoryRouter_RoutesCorrectCategories()
        {
            var router = DownloadCategoryRouter.Instance;

            var videoCat = router.DetermineCategory("movie_trailer.mp4");
            videoCat.CategoryId.Should().Be("video");
            videoCat.DefaultSubFolder.Should().Be("Video");

            var musicCat = router.DetermineCategory("soundtrack.flac");
            musicCat.CategoryId.Should().Be("music");
            musicCat.DefaultSubFolder.Should().Be("Music");

            var zipCat = router.DetermineCategory("backup_archive.7z");
            zipCat.CategoryId.Should().Be("compressed");
            zipCat.DefaultSubFolder.Should().Be("Compressed");

            var docCat = router.DetermineCategory("research_paper.pdf");
            docCat.CategoryId.Should().Be("documents");
            docCat.DefaultSubFolder.Should().Be("Documents");

            var progCat = router.DetermineCategory("installer.msi");
            progCat.CategoryId.Should().Be("programs");
            progCat.DefaultSubFolder.Should().Be("Programs");
        }

        [Fact]
        public void DownloadCategoryRouter_CustomRules_AddAndResolveCorrectly()
        {
            var router = DownloadCategoryRouter.Instance;
            string customId = "ebooks_custom";
            router.AddCustomCategory(customId, "eBooks", "eBooks", new[] { ".mobi", ".azw3", ".kfx" });

            var result = router.DetermineCategory("novel.azw3");
            result.CategoryId.Should().Be(customId);
            result.DefaultSubFolder.Should().Be("eBooks");

            // Clean up
            router.RemoveCategory(customId);
        }

        [Fact]
        public void UrlPatternExpander_ExpandsNumericalAndAlphabeticalRanges()
        {
            // Test 1: Number range [1-4]
            string numPattern = "https://example.com/files/part[1-4].bin";
            var numExpanded = UrlPatternExpander.Expand(numPattern);
            numExpanded.Should().HaveCount(4);
            numExpanded[0].Should().Be("https://example.com/files/part1.bin");
            numExpanded[3].Should().Be("https://example.com/files/part4.bin");

            // Test 2: Alpha range [a-c]
            string alphaPattern = "https://example.com/img_[a-c].png";
            var alphaExpanded = UrlPatternExpander.Expand(alphaPattern);
            alphaExpanded.Should().HaveCount(3);
            alphaExpanded[0].Should().Be("https://example.com/img_a.png");
            alphaExpanded[2].Should().Be("https://example.com/img_c.png");
        }

        [Fact]
        public void SiteGrabber_NormalizesUrlsCorrectly()
        {
            string dirty = "https://example.com/page.html?utm_source=twitter&utm_medium=social#section-1";
            string clean = SiteGrabberService.NormalizeUrl(dirty);

            clean.Should().Be("https://example.com/page.html");
        }
    }
}
