using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace EDM.Services
{
    public class ThemeManifest
    {
        public string ThemeId { get; set; } = "default-dark";
        public string Name { get; set; } = "Default Modern Dark";
        public string Author { get; set; } = "EDM Team";
        public string Version { get; set; } = "2.0";
        public bool IsHighContrast { get; set; } = false;

        public Dictionary<string, string> Colors { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Background"] = "#121214",
            ["SecondaryBackground"] = "#18181B",
            ["CardBackground"] = "#27272A",
            ["SidebarBg"] = "#18181B",
            ["PrimaryText"] = "#F4F4F5",
            ["SecondaryText"] = "#A1A1AA",
            ["AccentColor"] = "#3B82F6",
            ["BorderColor"] = "#3F3F46",
            ["SuccessColor"] = "#22C55E",
            ["ErrorColor"] = "#EF4444"
        };

        public string FontFamily { get; set; } = "Segoe UI";
        public double BaseFontSize { get; set; } = 12.0;
        public double CornerRadius { get; set; } = 6.0;
    }

    /// <summary>
    /// Advanced Theme & Skinning Engine.
    /// Manages application-wide color palettes, typography, button styles,
    /// dynamic resource brush injection, import/export, and safe fallback rollback.
    /// </summary>
    public class ThemeSkinningEngine
    {
        private static readonly Lazy<ThemeSkinningEngine> _instance = new(() => new ThemeSkinningEngine());
        public static ThemeSkinningEngine Instance => _instance.Value;

        private ThemeManifest _currentTheme = new();
        private readonly List<ThemeManifest> _availableThemes = new();

        public ThemeManifest CurrentTheme => _currentTheme;
        public event Action<ThemeManifest>? ThemeChanged;

        public ThemeSkinningEngine()
        {
            _availableThemes.Add(new ThemeManifest());

            _availableThemes.Add(new ThemeManifest
            {
                ThemeId = "modern-light",
                Name = "Modern Clean Light",
                Colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Background"] = "#F8FAFC",
                    ["SecondaryBackground"] = "#FFFFFF",
                    ["CardBackground"] = "#FFFFFF",
                    ["SidebarBg"] = "#F1F5F9",
                    ["PrimaryText"] = "#0F172A",
                    ["SecondaryText"] = "#64748B",
                    ["AccentColor"] = "#2563EB",
                    ["BorderColor"] = "#E2E8F0",
                    ["SuccessColor"] = "#16A34A",
                    ["ErrorColor"] = "#DC2626"
                }
            });

            _availableThemes.Add(new ThemeManifest
            {
                ThemeId = "high-contrast",
                Name = "High Contrast Black",
                IsHighContrast = true,
                Colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Background"] = "#000000",
                    ["SecondaryBackground"] = "#000000",
                    ["CardBackground"] = "#000000",
                    ["SidebarBg"] = "#000000",
                    ["PrimaryText"] = "#FFFFFF",
                    ["SecondaryText"] = "#FFFF00",
                    ["AccentColor"] = "#00FFFF",
                    ["BorderColor"] = "#FFFFFF",
                    ["SuccessColor"] = "#00FF00",
                    ["ErrorColor"] = "#FF0000"
                }
            });
        }

        public bool ApplyTheme(string themeId)
        {
            var theme = _availableThemes.Find(t => string.Equals(t.ThemeId, themeId, StringComparison.OrdinalIgnoreCase));
            if (theme == null) return false;

            return ApplyTheme(theme);
        }

        public bool ApplyTheme(ThemeManifest theme)
        {
            try
            {
                _currentTheme = theme;

                if (System.Windows.Application.Current != null)
                {
                    var res = System.Windows.Application.Current.Resources;
                    foreach (var kv in theme.Colors)
                    {
                        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(kv.Value);
                        res[kv.Key + "Brush"] = new SolidColorBrush(color);
                        res[kv.Key + "Color"] = color;
                    }
                }

                ThemeChanged?.Invoke(_currentTheme);
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[ThemeSkinningEngine] Failed to apply theme '{theme.Name}'. Rolling back to default dark.", ex);
                _currentTheme = new ThemeManifest();
                return false;
            }
        }

        public bool ImportTheme(string jsonContent, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                var theme = JsonSerializer.Deserialize<ThemeManifest>(jsonContent);
                if (theme == null || string.IsNullOrWhiteSpace(theme.ThemeId))
                {
                    errorMessage = "Invalid theme manifest format.";
                    return false;
                }

                _availableThemes.RemoveAll(t => t.ThemeId == theme.ThemeId);
                _availableThemes.Add(theme);
                ApplyTheme(theme);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Theme parsing error: {ex.Message}";
                return false;
            }
        }

        public string ExportCurrentThemeToJson()
        {
            return JsonSerializer.Serialize(_currentTheme, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
