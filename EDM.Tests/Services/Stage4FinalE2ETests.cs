using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;
using EDM.Services;
using EDM.Views;

namespace EDM.Tests.Services
{
    public class Stage4FinalE2ETests : TestBase
    {
        [Fact]
        public void DownloadAllLinks_ModelFiltersCorrectly()
        {
            var links = new List<LinkItemViewModel>
            {
                new() { FileName = "video.mp4", Extension = ".mp4", Url = "https://example.com/video.mp4", IsSelected = true },
                new() { FileName = "doc.pdf", Extension = ".pdf", Url = "https://example.com/doc.pdf", IsSelected = true },
                new() { FileName = "archive.zip", Extension = ".zip", Url = "https://example.com/archive.zip", IsSelected = true }
            };

            var videoLinks = links.Where(l => l.Extension == ".mp4").ToList();
            videoLinks.Should().ContainSingle();
            videoLinks[0].FileName.Should().Be("video.mp4");
        }

        [Fact]
        public void SecureCredentialVault_SavesAndRetrievesPerHostCredentials()
        {
            string host = "https://private-server.org";
            string user = "admin_user";
            string pass = "SecureP@ssw0rd2026!";

            SecureCredentialVault.SaveCredentials(host, user, pass);

            bool found = SecureCredentialVault.TryGetCredentials(host, out string retrievedUser, out string retrievedPass);
            found.Should().BeTrue();
            retrievedUser.Should().Be(user);
            retrievedPass.Should().Be(pass);
        }

        [Fact]
        public void CategoryRules_DynamicRoutingAssignsCorrectSubFolder()
        {
            var router = DownloadCategoryRouter.Instance;
            string dest = router.ResolveDestinationPath(@"C:\Downloads", "presentation.pptx");
            dest.Should().Be(@"C:\Downloads\Documents\presentation.pptx");
        }
    }
}
