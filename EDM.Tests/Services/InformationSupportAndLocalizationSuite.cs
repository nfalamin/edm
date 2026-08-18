using System;
using System.Linq;
using EDM.Services;
using Xunit;

namespace EDM.Tests.Services
{
    [Collection("LocalizationTestCollection")]
    public class InformationSupportAndLocalizationSuite
    {
        // ====================================================================
        // 1. LOCALIZATION SERVICE TESTS (All 7 Languages + RTL Support)
        // ====================================================================

        [Fact]
        public void Localization_AllSevenLanguagePacks_AreLoadedAndValid()
        {
            var loc = LocalizationService.Instance;
            var packs = loc.GetAvailableLanguagePacks();

            Assert.NotNull(packs);
            Assert.True(packs.Count >= 7, $"Expected at least 7 language packs, found {packs.Count}");

            string[] expectedCultures = { "en-US", "bn-BD", "hi-IN", "te-IN", "es-ES", "ar-SA", "ur-PK" };
            foreach (var culture in expectedCultures)
            {
                var pack = loc.GetLanguagePack(culture);
                Assert.NotNull(pack);
                Assert.Equal(culture, pack.CultureCode);
                Assert.False(string.IsNullOrWhiteSpace(pack.DisplayName));
                Assert.False(string.IsNullOrWhiteSpace(pack.NativeName));
                Assert.False(string.IsNullOrWhiteSpace(pack.FlagEmoji));
                Assert.True(pack.Strings.Count > 30, $"Pack {culture} has only {pack.Strings.Count} string keys.");
            }
        }

        [Theory]
        [InlineData("ar-SA", true)]
        [InlineData("ur-PK", true)]
        [InlineData("en-US", false)]
        [InlineData("bn-BD", false)]
        [InlineData("hi-IN", false)]
        [InlineData("te-IN", false)]
        [InlineData("es-ES", false)]
        public void Localization_RightToLeftFlag_MatchesExpectedLanguageOrientation(string culture, bool expectedRtl)
        {
            var loc = LocalizationService.Instance;
            loc.SetLanguage(culture);

            Assert.Equal(culture, loc.CurrentCulture);
            Assert.Equal(expectedRtl, loc.IsCurrentRtl);
        }

        [Fact]
        public void Localization_StringResolution_ReturnsLocalizedAndFallbackKeys()
        {
            var loc = LocalizationService.Instance;

            // Test English
            loc.SetLanguage("en-US");
            Assert.Equal("Exclusive Download Manager (EDM)", loc.GetString("App_Title"));
            Assert.Equal("Dashboard", loc.GetString("Nav_Dashboard"));
            Assert.Equal("Add URL", loc.GetString("Btn_AddUrl"));

            // Test Bangla
            loc.SetLanguage("bn-BD");
            Assert.Equal("এক্সক্লুসিভ ডাউনলোড ম্যানেজার (EDM)", loc.GetString("App_Title"));
            Assert.Equal("ড্যাশবোর্ড", loc.GetString("Nav_Dashboard"));
            Assert.Equal("URL যোগ করুন", loc.GetString("Btn_AddUrl"));

            // Test Hindi
            loc.SetLanguage("hi-IN");
            Assert.Equal("एक्सक्लूसिव डाउनलोड मैनेजर (EDM)", loc.GetString("App_Title"));
            Assert.Equal("डैशबोर्ड", loc.GetString("Nav_Dashboard"));

            // Test Spanish
            loc.SetLanguage("es-ES");
            Assert.Equal("Panel Principal", loc.GetString("Nav_Dashboard"));

            // Test Arabic
            loc.SetLanguage("ar-SA");
            Assert.Equal("لوحة التحكم", loc.GetString("Nav_Dashboard"));

            // Test Fallback for missing custom key
            string fallbackVal = loc.GetString("NonExistent_Test_Key_123", "DefaultText");
            Assert.Equal("DefaultText", fallbackVal);

            // Revert to English
            loc.SetLanguage("en-US");
        }

        // ====================================================================
        // 2. VERSION HISTORY & SYSTEM ENVIRONMENT TESTS
        // ====================================================================

        [Fact]
        public void VersionHistory_SystemInfo_ReadsRealApplicationMetadata()
        {
            var versionSvc = VersionHistoryService.Instance;
            var sysInfo = versionSvc.GetSystemInfo();

            Assert.NotNull(sysInfo);
            Assert.False(string.IsNullOrWhiteSpace(sysInfo.ApplicationVersion));
            Assert.False(string.IsNullOrWhiteSpace(sysInfo.Architecture));
            Assert.False(string.IsNullOrWhiteSpace(sysInfo.FrameworkRuntime));
            Assert.False(string.IsNullOrWhiteSpace(sysInfo.OperatingSystem));
            Assert.False(string.IsNullOrWhiteSpace(sysInfo.ProcessMemory));
            Assert.True(sysInfo.ProcessorCount > 0);
            Assert.Contains("Exclusive Download Manager", sysInfo.Copyright);
        }

        [Fact]
        public void VersionHistory_ChangelogTimeline_ContainsStructuredReleases()
        {
            var versionSvc = VersionHistoryService.Instance;
            var releases = versionSvc.GetVersionHistory();

            Assert.NotNull(releases);
            Assert.True(releases.Count >= 4, "Expected at least 4 releases in changelog history.");

            var current = releases.FirstOrDefault(r => r.IsCurrent);
            Assert.NotNull(current);
            Assert.True(current.Version == "v1.0.0" || current.Version == "v6.0.0");
            Assert.True(current.NewFeatures.Count > 0);
            Assert.True(current.Improvements.Count > 0);
            Assert.True(current.BugFixes.Count > 0);
            Assert.True(current.SecurityUpdates.Count > 0);
        }

        // ====================================================================
        // 3. SUPPORT KNOWLEDGE BASE TESTS (All 32 Categories & Search)
        // ====================================================================

        [Fact]
        public void SupportKnowledgeBase_ContainsAllThirtyTwoCategories()
        {
            var kb = SupportKnowledgeBase.Instance;
            var categories = kb.GetCategories();

            Assert.NotNull(categories);
            Assert.Equal(32, categories.Count);

            for (int i = 1; i <= 32; i++)
            {
                var cat = categories.FirstOrDefault(c => c.Id == i);
                Assert.NotNull(cat);
                Assert.False(string.IsNullOrWhiteSpace(cat.Name));
                Assert.False(string.IsNullOrWhiteSpace(cat.Icon));
                Assert.False(string.IsNullOrWhiteSpace(cat.Description));
                Assert.True(cat.ArticleCount >= 1, $"Category {i} ({cat.Name}) has no articles.");
            }
        }

        [Fact]
        public void SupportKnowledgeBase_ArticlesHaveCompleteTroubleshootingStructure()
        {
            var kb = SupportKnowledgeBase.Instance;
            var articles = kb.GetAllArticles();

            Assert.NotNull(articles);
            Assert.True(articles.Count >= 32);

            foreach (var art in articles)
            {
                Assert.False(string.IsNullOrWhiteSpace(art.Id));
                Assert.True(art.CategoryId >= 1 && art.CategoryId <= 32);
                Assert.False(string.IsNullOrWhiteSpace(art.Title));
                Assert.False(string.IsNullOrWhiteSpace(art.Summary));
                Assert.True(art.PossibleCauses.Count > 0, $"Article '{art.Title}' has empty PossibleCauses");
                Assert.True(art.StepByStepSolution.Count > 0, $"Article '{art.Title}' has empty StepByStepSolution");
                Assert.True(art.WhatToCheck.Count > 0, $"Article '{art.Title}' has empty WhatToCheck");
                Assert.False(string.IsNullOrWhiteSpace(art.WhenToContactSupport), $"Article '{art.Title}' missing WhenToContactSupport");
            }
        }

        [Theory]
        [InlineData("0%", "download-stuck-0")]
        [InlineData("403", "download-failed")]
        [InlineData("speed", "download-speed")]
        [InlineData("duplicate", "duplicate-downloads")]
        [InlineData("sqlite", "database-history")]
        [InlineData("proxy", "proxy-problems")]
        public void SupportKnowledgeBase_Search_FindsRelevantArticles(string keyword, string expectedArticleId)
        {
            var kb = SupportKnowledgeBase.Instance;
            var results = kb.Search(keyword);

            Assert.NotNull(results);
            Assert.NotEmpty(results);
            Assert.Contains(results, r => string.Equals(r.Id, expectedArticleId, StringComparison.OrdinalIgnoreCase));
        }

        // ====================================================================
        // 4. PRIVACY POLICY CONTENT TESTS (All 27 Sections & Search)
        // ====================================================================

        [Fact]
        public void PrivacyPolicy_ContainsAllTwentySevenSections()
        {
            var policy = PrivacyPolicyContent.Instance;
            var sections = policy.GetSections();

            Assert.NotNull(sections);
            Assert.True(sections.Count >= 27);
            Assert.False(string.IsNullOrWhiteSpace(policy.PolicyVersion));
            Assert.Contains("2026", policy.LastUpdatedDate);

            for (int i = 1; i <= sections.Count; i++)
            {
                var sec = sections.FirstOrDefault(s => s.Number == i);
                Assert.NotNull(sec);
                Assert.False(string.IsNullOrWhiteSpace(sec.Title));
                Assert.False(string.IsNullOrWhiteSpace(sec.Content));
                Assert.True(sec.KeyPoints.Count > 0, $"Section {i} ({sec.Title}) has empty KeyPoints");
            }
        }

        [Theory]
        [InlineData("DPAPI", "account-information")]
        [InlineData("SQLite", "download-history")]
        [InlineData("GDPR", "user-rights")]
        [InlineData("yt-dlp", "third-party-services")]
        public void PrivacyPolicy_Search_FindsSpecificSections(string keyword, string expectedSectionId)
        {
            var policy = PrivacyPolicyContent.Instance;
            var results = policy.Search(keyword);

            Assert.NotNull(results);
            Assert.NotEmpty(results);
            Assert.Contains(results, r => string.Equals(r.Id, expectedSectionId, StringComparison.OrdinalIgnoreCase));
        }

        // ====================================================================
        // 5. NOTIFICATION SERVICE CATEGORIES & UNREAD COUNTS
        // ====================================================================

        [Fact]
        public void NotificationService_CategorizationAndUnreadManagement_WorkCorrectly()
        {
            var notif = NotificationService.Instance;
            notif.Clear();

            Assert.Equal(0, notif.GetUnreadCount());

            notif.Notify("Test Download Complete", "File.zip saved", NotificationSeverity.Success, NotificationCategory.DownloadCompleted);
            notif.Notify("Update Available", "v6.1.0 ready", NotificationSeverity.Info, NotificationCategory.UpdateAvailable);
            notif.Notify("License Verified", "Pro active", NotificationSeverity.Info, NotificationCategory.Licensing);

            Assert.Equal(3, notif.GetUnreadCount());

            var recent = notif.GetRecentNotifications();
            Assert.Equal(3, recent.Count);
            Assert.Equal(NotificationCategory.Licensing, recent[0].Category);
            Assert.Equal("🔑", recent[0].CategoryIcon);

            notif.MarkAllAsRead();
            Assert.Equal(0, notif.GetUnreadCount());

            notif.Clear();
            Assert.Empty(notif.GetRecentNotifications());
        }
    }
}
