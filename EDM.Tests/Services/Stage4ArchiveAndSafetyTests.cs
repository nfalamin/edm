using System;
using System.IO;
using System.IO.Compression;
using FluentAssertions;
using Xunit;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class Stage4ArchiveAndSafetyTests : TestBase
    {
        [Fact]
        public void ArchivePreviewService_InspectsZipWithoutExtractingToDisk()
        {
            string tempZip = Path.Combine(Path.GetTempPath(), $"edm_preview_test_{Guid.NewGuid():N}.zip");

            try
            {
                using (var zipStream = new FileStream(tempZip, FileMode.Create))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    var e1 = archive.CreateEntry("documents/readme.txt");
                    using (var w = new StreamWriter(e1.Open())) { w.Write("Hello EDM"); }

                    var e2 = archive.CreateEntry("images/photo.png");
                    using (var w = new StreamWriter(e2.Open())) { w.Write("fake png data"); }
                }

                var preview = ArchivePreviewService.InspectZipArchive(tempZip);
                preview.IsValid.Should().BeTrue();
                preview.TotalEntries.Should().Be(2);
                preview.Entries.Should().Contain(e => e.FullPath == "documents/readme.txt");
                preview.Entries.Should().Contain(e => e.FullPath == "images/photo.png");
                preview.SecurityWarning.Should().BeNull();
            }
            finally
            {
                if (File.Exists(tempZip)) File.Delete(tempZip);
            }
        }

        [Fact]
        public void ArchivePreviewService_FlagsSuspiciousPathTraversalEntries()
        {
            string tempZip = Path.Combine(Path.GetTempPath(), $"edm_preview_evil_{Guid.NewGuid():N}.zip");

            try
            {
                using (var zipStream = new FileStream(tempZip, FileMode.Create))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    var e1 = archive.CreateEntry("../../system32/evil.dll");
                    using (var w = new StreamWriter(e1.Open())) { w.Write("evil payload"); }
                }

                var preview = ArchivePreviewService.InspectZipArchive(tempZip);
                preview.IsValid.Should().BeTrue();
                preview.SecurityWarning.Should().NotBeNull();
                preview.Entries[0].IsPathTraversalSuspicious.Should().BeTrue();
            }
            finally
            {
                if (File.Exists(tempZip)) File.Delete(tempZip);
            }
        }
    }
}
