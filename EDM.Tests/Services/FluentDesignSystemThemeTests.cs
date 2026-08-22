using System;
using System.IO;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class FluentDesignSystemThemeTests
    {
        [Fact]
        public void DarkTheme_ContainsDeepCobaltBlueNightPalette()
        {
            string themePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "EDM", "Themes", "DarkTheme.xaml");
            if (!File.Exists(themePath))
            {
                themePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "EDM", "Themes", "DarkTheme.xaml"));
            }

            File.Exists(themePath).Should().BeTrue();
            string xamlContent = File.ReadAllText(themePath);

            xamlContent.Should().Contain("#070D1E", "Midnight Indigo primary surface hex code must be present in DarkTheme.xaml");
            xamlContent.Should().Contain("#0D162E", "Midnight Indigo container hex code must be present in DarkTheme.xaml");
            xamlContent.Should().Contain("#7C3AED", "Midnight Indigo accent hex code must be present in DarkTheme.xaml");
            xamlContent.Should().Contain("PrimaryPillGradient", "Dynamic PrimaryPillGradient brush must be defined in DarkTheme.xaml");
        }

        [Fact]
        public void ThemeResources_DefinesShimmerAndCobaltPalette()
        {
            string resPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "EDM", "Themes", "ThemeResources.xaml");
            if (!File.Exists(resPath))
            {
                resPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "EDM", "Themes", "ThemeResources.xaml"));
            }

            File.Exists(resPath).Should().BeTrue();
            string content = File.ReadAllText(resPath);

            content.Should().Contain("#070D1E", "Deep Void Blue primary color must match #070D1E");
            content.Should().Contain("#0D162E", "Glassmorphic Card color must match #0D162E");
            content.Should().Contain("ShimmerBrush", "ShimmerBrush linear gradient must be defined");
        }
    }
}
