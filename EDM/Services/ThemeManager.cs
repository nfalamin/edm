using System;
using System.Linq;
using System.Windows;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    /// <summary>
    /// Authoritative central ThemeManager for EDM.
    /// Manages application-wide dynamic theme switching (Light and Dark modes),
    /// maintains single active theme dictionary, persists preferences, and updates all open windows cleanly.
    /// </summary>
    public class ThemeManager : IThemeService
    {
        private static readonly Lazy<ThemeManager> _instance = new(() => new ThemeManager());
        public static ThemeManager Instance => _instance.Value;

        private const string ThemePreferenceKey = "SelectedTheme";
        private const string DarkThemeUri = "pack://application:,,,/Themes/DarkTheme.xaml";
        private const string LightThemeUri = "pack://application:,,,/Themes/LightTheme.xaml";

        private ApplicationThemeMode _currentTheme = ApplicationThemeMode.Dark;
        private readonly ISettingsService _settingsService;

        public ApplicationThemeMode CurrentTheme => _currentTheme;
        public bool IsDarkMode => _currentTheme == ApplicationThemeMode.Dark;

        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        public ThemeManager(ISettingsService? settingsService = null)
        {
            _settingsService = settingsService ?? new SettingsService();
        }

        /// <summary>
        /// Switch to a specific theme cleanly across the entire application
        /// </summary>
        public void SetTheme(ApplicationThemeMode theme)
        {
            var oldTheme = _currentTheme;
            _currentTheme = theme;

            try
            {
                ApplyThemeToApplication(theme);
                SaveThemePreference();
                ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(oldTheme, theme));
                LoggingService.Log($"[ThemeManager] Global theme updated: {theme}");
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[ThemeManager] Failed to apply theme {theme}", ex);
                _currentTheme = oldTheme;
            }
        }

        /// <summary>
        /// Toggle between Light and Dark mode
        /// </summary>
        public void ToggleTheme()
        {
            SetTheme(_currentTheme == ApplicationThemeMode.Dark ? ApplicationThemeMode.Light : ApplicationThemeMode.Dark);
        }

        /// <summary>
        /// Load saved theme preference from settings, defaulting to Dark if not set
        /// </summary>
        public void LoadThemePreference()
        {
            try
            {
                var saved = _settingsService.GetSetting(ThemePreferenceKey);
                if (!string.IsNullOrEmpty(saved) && Enum.TryParse<ApplicationThemeMode>(saved, true, out var theme))
                {
                    SetTheme(theme);
                }
                else
                {
                    SetTheme(ApplicationThemeMode.Dark);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[ThemeManager] Failed to load theme preference", ex);
                SetTheme(ApplicationThemeMode.Dark);
            }
        }

        /// <summary>
        /// Persist theme preference to settings
        /// </summary>
        public void SaveThemePreference()
        {
            try
            {
                _settingsService.SaveSetting(ThemePreferenceKey, _currentTheme.ToString());
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[ThemeManager] Failed to save theme preference", ex);
            }
        }

        /// <summary>
        /// Retrieve brush resource from active theme
        /// </summary>
        public object? GetBrushResource(string resourceKey)
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app != null && app.Resources.Contains(resourceKey))
                {
                    return app.Resources[resourceKey];
                }
                return null;
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[ThemeManager] Resource lookup failed for '{resourceKey}'", ex);
                return null;
            }
        }

        /// <summary>
        /// Swaps the active theme ResourceDictionary in Application.Current.Resources without duplicate dictionaries or memory leaks.
        /// </summary>
        private void ApplyThemeToApplication(ApplicationThemeMode theme)
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;

            var targetUri = theme == ApplicationThemeMode.Dark ? DarkThemeUri : LightThemeUri;
            var newDict = new ResourceDictionary { Source = new Uri(targetUri, UriKind.Absolute) };

            var merged = app.Resources.MergedDictionaries;

            // Remove all existing dark and light theme dictionaries to prevent resource leaks
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var dict = merged[i];
                if (dict.Source != null)
                {
                    var src = dict.Source.ToString();
                    if (src.Contains("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
                        src.Contains("LightTheme.xaml", StringComparison.OrdinalIgnoreCase))
                    {
                        merged.RemoveAt(i);
                    }
                }
            }

            // Add the single new theme dictionary
            merged.Add(newDict);
        }
    }
}
