using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class Stage4RemainingGapsTests : TestBase
    {
        [Fact]
        public void LocalizationService_HandlesLanguageSwitchingAndFallback()
        {
            var loc = new LocalizationService();

            // Default: en-US
            loc.GetString("Btn_Download").Should().Be("Download");

            // Switch to Bengali (bn-BD)
            loc.SetLanguage("bn-BD");
            loc.GetString("Btn_Download").Should().Be("ডাউনলোড");
            loc.GetString("Btn_Pause").Should().Be("বিরতি");

            // Key missing in Bengali falls back to English
            string fallbackVal = loc.GetString("NonExistent_Key_XYZ", "DefaultFallback");
            fallbackVal.Should().Be("DefaultFallback");

            // Formatting test
            string formatted = loc.GetFormatted("Status_Downloading", "15.5");
            formatted.Should().Contain("15.5");

            // Custom pack import
            string customJson = "{\"CultureCode\": \"de-DE\", \"DisplayName\": \"Deutsch\", \"Strings\": {\"Btn_Download\": \"Herunterladen\"}}";
            bool imported = loc.ImportLanguagePack(customJson, out string err);
            imported.Should().BeTrue();
            err.Should().BeEmpty();

            loc.SetLanguage("de-DE");
            loc.GetString("Btn_Download").Should().Be("Herunterladen");
        }

        [Fact]
        public void ThemeSkinningEngine_AppliesThemesAndHandlesRollback()
        {
            var engine = new ThemeSkinningEngine();

            // Default theme is dark
            engine.CurrentTheme.ThemeId.Should().Be("default-dark");
            engine.CurrentTheme.Colors["Background"].Should().Be("#121214");

            // Apply light theme
            bool appliedLight = engine.ApplyTheme("modern-light");
            appliedLight.Should().BeTrue();
            engine.CurrentTheme.ThemeId.Should().Be("modern-light");

            // Custom Theme import
            string customThemeJson = @"{
                ""ThemeId"": ""neon-cyberpunk"",
                ""Name"": ""Neon Cyberpunk"",
                ""Colors"": {
                    ""Background"": ""#0a0a12"",
                    ""AccentColor"": ""#FF007F""
                }
            }";

            bool imported = engine.ImportTheme(customThemeJson, out string err);
            imported.Should().BeTrue();
            engine.CurrentTheme.ThemeId.Should().Be("neon-cyberpunk");
            engine.CurrentTheme.Colors["AccentColor"].Should().Be("#FF007F");

            // Invalid theme import
            bool failedImport = engine.ImportTheme("invalid-json", out string errInvalid);
            failedImport.Should().BeFalse();
            errInvalid.Should().NotBeEmpty();
        }
    }
}
