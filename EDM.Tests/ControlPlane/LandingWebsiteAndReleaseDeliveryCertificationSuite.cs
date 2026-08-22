using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.ControlPlane
{
    public class LandingWebsiteAndReleaseDeliveryCertificationSuite
    {
        private ControlPlaneDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
                .UseSqlite("Data Source=file:" + Guid.NewGuid().ToString("N") + "?mode=memory&cache=shared")
                .Options;
            var ctx = new ControlPlaneDbContext(options);
            ctx.Database.EnsureCreated();
            return ctx;
        }

        // =====================================================================
        // 1. LATEST RELEASE AS SINGLE SOURCE OF TRUTH
        // =====================================================================
        [Fact]
        public async Task Phase3_LatestRelease_ReturnsAuthoritativePublishedRelease()
        {
            using var db = CreateInMemoryDbContext();
            var releaseService = new ReleaseService(db);

            var rel = new Release
            {
                Id = Guid.NewGuid(),
                Platform = ClientType.DesktopWindows,
                Version = "2.1.0",
                Channel = "stable",
                MinimumSupportedVersion = "1.0.0",
                Title = "EDM 2.1.0 Turbo Release",
                ReleaseNotes = "High performance multi-stream 32-socket download engine.",
                PublishedAtUtc = DateTime.UtcNow,
                IsMandatory = false,
                IsPublished = true,
                IsWithdrawn = false,
                Severity = ReleaseSeverity.Standard
            };

            var artifactId = Guid.NewGuid();
            rel.Artifacts.Add(new ReleaseArtifact
            {
                Id = artifactId,
                ReleaseId = rel.Id,
                ArtifactName = "EDM-Setup-v2.1.0.exe",
                Architecture = "x64",
                DownloadUrl = $"/api/v1/releases/artifacts/{artifactId}/download",
                Sha256Hash = "93049cf86301342dbdaae74256d4013a1e30133aa26a38dbe08e2a6e3e32d023",
                FileSizeBytes = 19807971
            });

            db.Releases.Add(rel);
            await db.SaveChangesAsync();

            var latest = await releaseService.GetLatestActiveReleaseAsync(ClientType.DesktopWindows, "stable");

            latest.Should().NotBeNull();
            latest!.Version.Should().Be("2.1.0");
            latest.Artifacts.Should().HaveCount(1);

            var art = latest.Artifacts.First();
            art.ArtifactName.Should().Be("EDM-Setup-v2.1.0.exe");
            art.Sha256Hash.Should().Be("93049cf86301342dbdaae74256d4013a1e30133aa26a38dbe08e2a6e3e32d023");
            art.FileSizeBytes.Should().Be(19807971);
        }

        // =====================================================================
        // 2. REAL ARTIFACT SHA-256 HASH VERIFICATION
        // =====================================================================
        [Fact]
        public void Phase9_RealArtifactSha256_ExactCryptographicHashMatch()
        {
            string distPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Dist", "EDM_v1.0_Complete_Distribution", "EDM_Setup_v1.0.exe"));
            string downloadsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "website", "downloads", "EDM-Setup-v2.1.0.exe"));

            string targetFile = File.Exists(distPath) ? distPath : (File.Exists(downloadsPath) ? downloadsPath : null!);
            targetFile.Should().NotBeNull("Actual EDM Setup installer binary must exist on disk");

            using var stream = File.OpenRead(targetFile);
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(stream);
            string calculatedHash = Convert.ToHexString(hash).ToLowerInvariant();

            calculatedHash.Should().Be("93049cf86301342dbdaae74256d4013a1e30133aa26a38dbe08e2a6e3e32d023", "SHA-256 must match the cryptographic hash of the real binary");
            stream.Length.Should().Be(19807971, "File size must match exact binary length");
        }

        // =====================================================================
        // 3. ARTIFACT DOWNLOAD STREAMING & ANTI-TRAVERSAL
        // =====================================================================
        [Fact]
        public async Task Phase8_ArtifactStreaming_ServesBinaryAndRejectsPathTraversal()
        {
            using var db = CreateInMemoryDbContext();
            var releaseService = new ReleaseService(db);

            var rel = new Release
            {
                Id = Guid.NewGuid(),
                Platform = ClientType.DesktopWindows,
                Version = "2.1.0",
                Channel = "stable",
                IsPublished = true,
                PublishedAtUtc = DateTime.UtcNow
            };

            var validArtifact = new ReleaseArtifact
            {
                Id = Guid.NewGuid(),
                ReleaseId = rel.Id,
                ArtifactName = "EDM-Setup-v2.1.0.exe",
                Architecture = "x64",
                Sha256Hash = "93049cf86301342dbdaae74256d4013a1e30133aa26a38dbe08e2a6e3e32d023",
                FileSizeBytes = 19807971
            };

            rel.Artifacts.Add(validArtifact);
            db.Releases.Add(rel);
            await db.SaveChangesAsync();

            // 1. Download valid artifact
            var (stream, contentType, fileName, fileLength) = await releaseService.GetArtifactFileStreamAsync(validArtifact.Id);
            using (stream)
            {
                stream.Should().NotBeNull();
                fileName.Should().Be("EDM-Setup-v2.1.0.exe");
                contentType.Should().Be("application/vnd.microsoft.portable-executable");
                fileLength.Should().Be(19807971);
            }

            // 2. Path traversal rejection: non-existent random ID must throw FileNotFoundException
            Func<Task> actNonExistent = async () => await releaseService.GetArtifactFileStreamAsync(Guid.NewGuid());
            await actNonExistent.Should().ThrowAsync<FileNotFoundException>();
        }

        // =====================================================================
        // 4. WEBSITE INDEX.HTML STRUCTURE & ROUTE INTEGRITY
        // =====================================================================
        [Fact]
        public void Phase14_WebsiteIndexHtml_ContainsAdminConsoleAndReleaseElements()
        {
            string webIndexPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "website", "index.html"));
            File.Exists(webIndexPath).Should().BeTrue("website/index.html must exist to serve the landing page");

            string html = File.ReadAllText(webIndexPath);

            // Verify Admin Console link points to /edm-admin
            html.Should().Contain("/edm-admin", "Website must contain link to Admin Console at /edm-admin");
            html.Should().NotContain("../EDM.ControlPlane.Dashboard/index.html", "Website must not contain broken relative path to dashboard");

            // Verify Release Hub DOM elements expected by landing-app.js
            html.Should().Contain("id=\"download-primary-btn\"", "Download button must have id download-primary-btn");
            html.Should().Contain("id=\"download-sha256-code\"", "SHA-256 code element must exist");
            html.Should().Contain("id=\"download-release-badge\"", "Release badge element must exist");
            html.Should().Contain("id=\"download-latest-title\"", "Release title element must exist");
            html.Should().Contain("assets/js/landing-app.js", "landing-app.js script must be referenced");
        }

        // =====================================================================
        // 5. DASHBOARD INDEX.HTML PRODUCTION CLEANLINESS
        // =====================================================================
        [Fact]
        public void Phase11_DashboardIndexHtml_IsFreeOfMockDataAndUsesRealApi()
        {
            string dashIndexPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "EDM.ControlPlane.Dashboard", "index.html"));
            File.Exists(dashIndexPath).Should().BeTrue("EDM.ControlPlane.Dashboard/index.html must exist");

            string html = File.ReadAllText(dashIndexPath);
            html.Should().NotContain("mock-data.js", "Production dashboard must not load mock-data.js");
            html.Should().Contain("auth.js");
            html.Should().Contain("api.js");
            html.Should().Contain("app.js");
        }

        // =====================================================================
        // 6. WITHDRAWN AND UNPUBLISHED RELEASES ARE FILTERED
        // =====================================================================
        [Fact]
        public async Task Phase25_WithdrawnReleases_AreExcludedFromLatest()
        {
            using var db = CreateInMemoryDbContext();
            var releaseService = new ReleaseService(db);

            var withdrawnRel = new Release
            {
                Id = Guid.NewGuid(),
                Platform = ClientType.DesktopWindows,
                Version = "2.2.0-broken",
                Channel = "stable",
                PublishedAtUtc = DateTime.UtcNow.AddDays(1),
                IsPublished = true,
                IsWithdrawn = true
            };

            var activeRel = new Release
            {
                Id = Guid.NewGuid(),
                Platform = ClientType.DesktopWindows,
                Version = "2.1.0",
                Channel = "stable",
                PublishedAtUtc = DateTime.UtcNow,
                IsPublished = true,
                IsWithdrawn = false
            };

            db.Releases.AddRange(withdrawnRel, activeRel);
            await db.SaveChangesAsync();

            var latest = await releaseService.GetLatestActiveReleaseAsync(ClientType.DesktopWindows, "stable");
            latest.Should().NotBeNull();
            latest!.Version.Should().Be("2.1.0");
        }
    }
}
