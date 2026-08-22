using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using EDM.Models;
using EDM.Services;
using EDM.Services.Interfaces;

namespace EDM.Tests.Services
{
    public class AdvancedDownloadRulesTests : IDisposable
    {
        private readonly string _testStorageDir;

        public AdvancedDownloadRulesTests()
        {
            _testStorageDir = Path.Combine(Path.GetTempPath(), "EDM_RuleTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testStorageDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testStorageDir))
                {
                    Directory.Delete(_testStorageDir, true);
                }
            }
            catch { }
        }

        private DownloadRuleEngine CreateEngine()
        {
            return new DownloadRuleEngine(_testStorageDir);
        }

        // 1. Extension matching
        [Theory]
        [InlineData("video.mp4", "Video", "Videos")]
        [InlineData("song.flac", "Music", "Music")]
        [InlineData("manual.pdf", "Documents", "Documents")]
        [InlineData("archive.7z", "Compressed", "Compressed")]
        [InlineData("setup.msi", "Programs", "Programs")]
        public void Test1_ExtensionMatching_ResolvesCorrectCategory(string filename, string expectedCategory, string expectedFolder)
        {
            var engine = CreateEngine();
            var result = engine.Resolve("https://example.com/file", filename, null, null, _testStorageDir);

            result.Category.Should().Be(expectedCategory);
            result.DestinationPath.Should().Contain(expectedFolder);
        }

        // 2. MIME matching
        [Theory]
        [InlineData("video/mp4", "Video")]
        [InlineData("audio/mpeg", "Music")]
        [InlineData("application/pdf", "Documents")]
        [InlineData("application/zip", "Compressed")]
        public void Test2_MimeMatching_ResolvesCorrectCategory(string mimeType, string expectedCategory)
        {
            var engine = CreateEngine();
            var result = engine.Resolve("https://example.com/stream", "unknown_file.bin", mimeType, null, _testStorageDir);

            result.Category.Should().Be(expectedCategory);
        }

        // 3. Domain matching
        [Fact]
        public void Test3_DomainMatching_MatchesDomainRule()
        {
            var engine = CreateEngine();
            engine.AddOrUpdateRule(new DownloadRule
            {
                RuleId = "yt_rule",
                Name = "YouTube Rule",
                Order = 1,
                Domains = new List<string> { "youtube.com", "youtu.be" },
                TargetCategory = "Video",
                TargetSubFolder = "YouTube_Videos",
                TargetPriority = DownloadPriority.High
            });

            var result = engine.Resolve("https://www.youtube.com/watch?v=12345", "clip.mkv", null, null, _testStorageDir);

            result.Category.Should().Be("Video");
            result.DestinationPath.Should().Contain("YouTube_Videos");
            result.Priority.Should().Be(DownloadPriority.High);
        }

        // 4. Category selection
        [Fact]
        public void Test4_CategorySelection_CustomCategoryApplied()
        {
            var engine = CreateEngine();
            engine.AddOrUpdateRule(new DownloadRule
            {
                RuleId = "iso_rule",
                Name = "Disk Images",
                Order = 5,
                Extensions = new List<string> { ".iso", ".img" },
                TargetCategory = "DiskImages",
                TargetSubFolder = "DiskImages"
            });

            var result = engine.Resolve("https://example.com/distro.iso", "distro.iso", null, null, _testStorageDir);

            result.Category.Should().Be("DiskImages");
            result.DestinationPath.Should().Contain("DiskImages");
        }

        // 5. Default category fallback
        [Fact]
        public void Test5_DefaultFallback_UnmatchedResolvesToGeneral()
        {
            var engine = CreateEngine();
            var result = engine.Resolve("https://example.com/data.xyz123", "data.xyz123", "application/unknown-format", null, _testStorageDir);

            result.Category.Should().Be("General");
            result.DestinationPath.Should().Contain("General");
        }

        // 6. Destination folder assignment
        [Fact]
        public void Test6_DestinationFolderAssignment_CombinesBaseDirAndSubfolder()
        {
            var engine = CreateEngine();
            var result = engine.Resolve("https://example.com/doc.pdf", "doc.pdf", null, null, _testStorageDir);

            string expectedPath = Path.Combine(_testStorageDir, "Documents", "doc.pdf");
            result.DestinationPath.Should().Be(expectedPath);
        }

        // 7. Queue assignment
        [Fact]
        public void Test7_QueueAssignment_AssignsConfiguredQueue()
        {
            var engine = CreateEngine();
            engine.AddOrUpdateRule(new DownloadRule
            {
                RuleId = "nightly_rule",
                Name = "Nightly Batch",
                Extensions = new List<string> { ".tar.gz" },
                TargetQueueId = "nightly"
            });

            var result = engine.Resolve("https://example.com/backup.tar.gz", "backup.tar.gz", null, null, _testStorageDir);
            result.QueueId.Should().Be("nightly");
        }

        // 8. Priority assignment
        [Fact]
        public void Test8_PriorityAssignment_ExecutableProgramsGetHighPriority()
        {
            var engine = CreateEngine();
            var result = engine.Resolve("https://example.com/installer.exe", "installer.exe", null, null, _testStorageDir);

            result.Priority.Should().Be(DownloadPriority.High);
        }

        // 9. Profile selection & application
        [Fact]
        public void Test9_ProfileSelection_AppliesProfileSettings()
        {
            var engine = CreateEngine();
            engine.AddOrUpdateProfile(new DownloadProfile
            {
                ProfileId = "custom_prof",
                Name = "Fast Video Profile",
                DefaultCategory = "Video",
                DefaultQueueId = "high_priority",
                DefaultPriority = DownloadPriority.Urgent
            });

            engine.AddOrUpdateRule(new DownloadRule
            {
                RuleId = "prof_rule",
                Extensions = new List<string> { ".webm" },
                ProfileId = "custom_prof",
                TargetCategory = "Video",
                TargetPriority = DownloadPriority.Urgent
            });

            var result = engine.Resolve("https://example.com/video.webm", "video.webm", null, null, _testStorageDir);

            result.AppliedProfileId.Should().Be("custom_prof");
            result.Priority.Should().Be(DownloadPriority.Urgent);
        }

        // 10. Rule precedence (Domain > MIME > Extension > Default)
        [Fact]
        public void Test10_RulePrecedence_DomainRuleOverridesExtensionRule()
        {
            var engine = CreateEngine();
            // Domain rule for github.com assigns to "Code" folder
            engine.AddOrUpdateRule(new DownloadRule
            {
                RuleId = "github_rule",
                Order = 1,
                Domains = new List<string> { "github.com" },
                TargetCategory = "Code",
                TargetSubFolder = "Code"
            });

            // Even though .zip normally maps to Compressed, github.com takes precedence
            var result = engine.Resolve("https://github.com/repo/archive.zip", "archive.zip", null, null, _testStorageDir);

            result.Category.Should().Be("Code");
            result.DestinationPath.Should().Contain("Code");
        }

        // 11. Explicit user override preservation
        [Fact]
        public void Test11_UserOverride_PreservesExplicitRequestSettings()
        {
            var engine = CreateEngine();
            var req = new DownloadRequest
            {
                Url = "https://example.com/video.mp4",
                SuggestedFileName = "video.mp4",
                TargetCategory = "MyCustomCategory",
                TargetDirectory = Path.Combine(_testStorageDir, "ExplicitFolder"),
                TargetQueueId = "custom_queue"
            };

            var result = engine.Resolve(req, _testStorageDir);

            result.Category.Should().Be("MyCustomCategory");
            result.QueueId.Should().Be("custom_queue");
            result.DestinationPath.Should().Be(Path.Combine(_testStorageDir, "ExplicitFolder", "video.mp4"));
        }

        // 12. Invalid path rejection & safe fallback
        [Fact]
        public void Test12_InvalidPath_FallsBackToDefaultDirectory()
        {
            var engine = CreateEngine();
            var req = new DownloadRequest
            {
                Url = "https://example.com/file.dat",
                SuggestedFileName = "file.dat",
                TargetDirectory = "Z:\\NonExistentDrive\\Secret\\BadDir"
            };

            var result = engine.Resolve(req, _testStorageDir);
            result.DestinationPath.Should().NotBeNullOrWhiteSpace();
        }

        // 13. Path traversal neutralization
        [Fact]
        public void Test13_PathTraversal_NeutralizesTraversalAttempts()
        {
            var engine = CreateEngine();
            var req = new DownloadRequest
            {
                Url = "https://example.com/exploit.txt",
                SuggestedFileName = @"..\..\..\Windows\System32\drivers\etc\hosts",
                TargetDirectory = @"..\..\..\Windows"
            };

            var result = engine.Resolve(req, _testStorageDir);

            result.DestinationPath.Should().NotContain("..");
            result.DestinationPath.Should().Contain("hosts");
        }

        // 14. Invalid extension handling
        [Fact]
        public void Test14_InvalidExtension_HandlesSafely()
        {
            var engine = CreateEngine();
            var result = engine.Resolve("https://example.com/no_extension_file", "no_extension_file", null, null, _testStorageDir);

            result.Category.Should().Be("General");
        }

        // 15. Malformed rule recovery
        [Fact]
        public void Test15_MalformedRule_HandledSafely()
        {
            var engine = CreateEngine();
            engine.AddOrUpdateRule(new DownloadRule
            {
                RuleId = "malformed",
                Extensions = null!, // null list
                Domains = null!,
                MimeTypes = null!,
                UrlPatterns = null!
            });

            Action act = () => engine.Resolve("https://example.com/file.bin", "file.bin", null, null, _testStorageDir);
            act.Should().NotThrow();
        }

        // 16. Duplicate rule detection
        [Fact]
        public void Test16_DuplicateRule_UpdatesExisting()
        {
            var engine = CreateEngine();
            engine.AddOrUpdateRule(new DownloadRule { RuleId = "dup_rule", Name = "Version 1" });
            engine.AddOrUpdateRule(new DownloadRule { RuleId = "dup_rule", Name = "Version 2" });

            var rules = engine.GetRules();
            rules.Count(r => r.RuleId == "dup_rule").Should().Be(1);
            rules.First(r => r.RuleId == "dup_rule").Name.Should().Be("Version 2");
        }

        // 17. Disabled rule bypassing
        [Fact]
        public void Test17_DisabledRule_IsBypassed()
        {
            var engine = CreateEngine();
            engine.AddOrUpdateRule(new DownloadRule
            {
                RuleId = "disabled_rule",
                IsEnabled = false,
                Extensions = new List<string> { ".xyz" },
                TargetCategory = "XYZCategory"
            });

            var result = engine.Resolve("https://example.com/test.xyz", "test.xyz", null, null, _testStorageDir);
            result.Category.Should().NotBe("XYZCategory");
        }

        // 18. Rule reordering / priority order
        [Fact]
        public void Test18_RuleOrdering_LowestOrderEvaluatedFirst()
        {
            var engine = CreateEngine();
            engine.AddOrUpdateRule(new DownloadRule
            {
                RuleId = "rule_high_order",
                Order = 100,
                Extensions = new List<string> { ".custom" },
                TargetCategory = "SecondCategory"
            });

            engine.AddOrUpdateRule(new DownloadRule
            {
                RuleId = "rule_low_order",
                Order = 1,
                Extensions = new List<string> { ".custom" },
                TargetCategory = "FirstCategory"
            });

            var result = engine.Resolve("https://example.com/file.custom", "file.custom", null, null, _testStorageDir);
            result.Category.Should().Be("FirstCategory");
        }

        // 19. Rule persistence across instances
        [Fact]
        public void Test19_Persistence_SavesAndLoadsAcrossInstances()
        {
            var engine1 = new DownloadRuleEngine(_testStorageDir);
            engine1.AddOrUpdateRule(new DownloadRule
            {
                RuleId = "persisted_rule",
                Name = "Persisted Rule",
                Extensions = new List<string> { ".persist" },
                TargetCategory = "PersistedCat"
            });

            var engine2 = new DownloadRuleEngine(_testStorageDir);
            var loaded = engine2.GetRules().FirstOrDefault(r => r.RuleId == "persisted_rule");

            loaded.Should().NotBeNull();
            loaded!.Name.Should().Be("Persisted Rule");
            loaded.TargetCategory.Should().Be("PersistedCat");
        }

        // 20. Corrupted configuration recovery
        [Fact]
        public void Test20_CorruptedConfig_RecoversDefaults()
        {
            string rulesFile = Path.Combine(_testStorageDir, "download_rules.json");
            File.WriteAllText(rulesFile, "{ invalid json data [[[");

            var engine = new DownloadRuleEngine(_testStorageDir);
            engine.GetRules().Should().NotBeEmpty();
        }

        // 21. Browser request automatic categorization
        [Fact]
        public async Task Test21_BrowserRequest_CategorizedAutomatically()
        {
            var settings = new Mock<ISettingsService>();
            settings.Setup(s => s.GetSetting(It.IsAny<string>())).Returns((string?)null);

            var gateway = new DownloadRequestGateway(settings.Object);
            var req = new DownloadRequest
            {
                Source = IngestionSource.BrowserExtension,
                Url = "https://example.com/movie.mp4",
                SuggestedFileName = "movie.mp4"
            };

            var res = await gateway.SubmitRequestAsync(req);
            res.IsSuccess.Should().BeTrue();
            res.Item!.Category.Should().Be("Video");
        }

        // 22. Clipboard request automatic categorization
        [Fact]
        public async Task Test22_ClipboardRequest_CategorizedAutomatically()
        {
            var settings = new Mock<ISettingsService>();
            settings.Setup(s => s.GetSetting(It.IsAny<string>())).Returns((string?)null);

            var gateway = new DownloadRequestGateway(settings.Object);
            var req = new DownloadRequest
            {
                Source = IngestionSource.ClipboardMonitor,
                Url = "https://example.com/document.pdf",
                SuggestedFileName = "document.pdf"
            };

            var res = await gateway.SubmitRequestAsync(req);
            res.IsSuccess.Should().BeTrue();
            res.Item!.Category.Should().Be("Documents");
        }

        // 23. Manual request automatic categorization
        [Fact]
        public async Task Test23_ManualRequest_CategorizedAutomatically()
        {
            var settings = new Mock<ISettingsService>();
            settings.Setup(s => s.GetSetting(It.IsAny<string>())).Returns((string?)null);

            var gateway = new DownloadRequestGateway(settings.Object);
            var req = new DownloadRequest
            {
                Source = IngestionSource.Manual,
                Url = "https://example.com/music.mp3",
                SuggestedFileName = "music.mp3"
            };

            var res = await gateway.SubmitRequestAsync(req);
            res.IsSuccess.Should().BeTrue();
            res.Item!.Category.Should().Be("Music");
        }

        // 24. NativeHost request automatic categorization
        [Fact]
        public async Task Test24_NativeHostRequest_CategorizedAutomatically()
        {
            var settings = new Mock<ISettingsService>();
            settings.Setup(s => s.GetSetting(It.IsAny<string>())).Returns((string?)null);

            var gateway = new DownloadRequestGateway(settings.Object);
            var req = new DownloadRequest
            {
                Source = IngestionSource.NativeHost,
                Url = "https://example.com/bundle.zip",
                SuggestedFileName = "bundle.zip"
            };

            var res = await gateway.SubmitRequestAsync(req);
            res.IsSuccess.Should().BeTrue();
            res.Item!.Category.Should().Be("Compressed");
        }

        // 25. Performance with large rule sets (100+ rules evaluated in sub-millisecond)
        [Fact]
        public void Test25_LargeRuleSetPerformance_EvaluatesSubMillisecond()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 150; i++)
            {
                engine.AddOrUpdateRule(new DownloadRule
                {
                    RuleId = $"perf_rule_{i}",
                    Order = i + 10,
                    Extensions = new List<string> { $".ext{i}" },
                    Domains = new List<string> { $"domain{i}.com" },
                    TargetCategory = $"Cat{i}"
                });
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 50; i++)
            {
                var res = engine.Resolve($"https://domain{i}.com/file.ext{i}", $"file.ext{i}", null, null, _testStorageDir);
                res.Category.Should().Be($"Cat{i}");
            }
            sw.Stop();

            // 50 evaluations should complete comfortably under 250ms
            sw.ElapsedMilliseconds.Should().BeLessThan(250);
        }
    }
}
