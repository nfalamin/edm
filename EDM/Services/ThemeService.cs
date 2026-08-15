using System;
using System.Windows;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    /// <summary>
    /// ThemeService - Manages dynamic theme switching between light and dark modes.
    /// Integrates with WPF ResourceDictionary to apply theme changes at runtime.
    /// 
    /// Usage:
    /// var themeService = new ThemeService(settingsService);
    /// themeService.LoadThemePreference();
    /// themeService.SetTheme(ApplicationThemeMode.Dark);
    /// themeService.ThemeChanged += (s, e) => Console.WriteLine($"Changed from {e.OldTheme} to {e.NewTheme}");
    /// 
    /// Configuration:
    /// - DarkTheme.xaml: Dark theme resource dictionary
    /// - LightTheme.xaml: Light theme resource dictionary
    /// - Stored preference key: "Theme" (settings)
    /// </summary>
    public class ThemeService : IThemeService
    {
        private ApplicationThemeMode _currentTheme = ApplicationThemeMode.Dark; // Default to Dark
        private readonly ISettingsService _settingsService;
        private const string ThemePreferenceKey = "Theme";
        private const string DarkThemeUri = "pack://application:,,,/Themes/DarkTheme.xaml";
        private const string LightThemeUri = "pack://application:,,,/Themes/LightTheme.xaml";

        public ApplicationThemeMode CurrentTheme => _currentTheme;

        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        public ThemeService(ISettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        /// <summary>
        /// Switch to a specific theme
        /// </summary>
        public void SetTheme(ApplicationThemeMode theme)
        {
            if (_currentTheme == theme) return;

            var oldTheme = _currentTheme;
            _currentTheme = theme;

            try
            {
                ApplyThemeToApplication(theme);
                SaveThemePreference();
                ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(oldTheme, theme));
                LoggingService.Log($"[ThemeService] Theme changed from {oldTheme} to {theme}");
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[ThemeService] Failed to apply theme {theme}", ex);
                // Rollback on failure
                _currentTheme = oldTheme;
                throw;
            }
        }

        /// <summary>
        /// Toggle between light and dark themes
        /// </summary>
        public void ToggleTheme()
        {
            SetTheme(_currentTheme == ApplicationThemeMode.Dark ? ApplicationThemeMode.Light : ApplicationThemeMode.Dark);
        }

        /// <summary>
        /// Load the theme preference from persistent storage
        /// </summary>
        public void LoadThemePreference()
        {
            try
            {
                var savedTheme = _settingsService.GetSetting(ThemePreferenceKey);
                if (!string.IsNullOrEmpty(savedTheme) && Enum.TryParse<ApplicationThemeMode>(savedTheme, out var theme))
                {
                    SetTheme(theme);
                    LoggingService.Log($"[ThemeService] Loaded theme preference: {theme}");
                }
                else
                {
                    LoggingService.Log("[ThemeService] No saved theme preference found, using default (Dark)");
                    SetTheme(ApplicationThemeMode.Dark);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[ThemeService] Failed to load theme preference", ex);
                SetTheme(ApplicationThemeMode.Dark); // Fallback to dark theme
            }
        }

        /// <summary>
        /// Save the current theme preference to persistent storage
        /// </summary>
        public void SaveThemePreference()
        {
            try
            {
                _settingsService.SaveSetting(ThemePreferenceKey, _currentTheme.ToString());
                LoggingService.Log($"[ThemeService] Saved theme preference: {_currentTheme}");
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[ThemeService] Failed to save theme preference", ex);
            }
        }

        /// <summary>
        /// Get a brush resource for the current theme
        /// </summary>
        public object? GetBrushResource(string resourceKey)
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app?.Resources.Contains(resourceKey) == true)
                {
                    return app.Resources[resourceKey];
                }

                LoggingService.Log($"[ThemeService] Resource '{resourceKey}' not found in current theme");
                return null;
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[ThemeService] Failed to retrieve resource '{resourceKey}'", ex);
                return null;
            }
        }

        /// <summary>
        /// Apply the specified theme to the application by merging resource dictionaries
        /// </summary>
        private void ApplyThemeToApplication(ApplicationThemeMode theme)
        {
            var app = System.Windows.Application.Current;
            if (app == null)
            {
                throw new InvalidOperationException("Application.Current is null");
            }

            var themeUri = theme == ApplicationThemeMode.Dark ? DarkThemeUri : LightThemeUri;
            var rd = new ResourceDictionary { Source = new Uri(themeUri, UriKind.Absolute) };

            // Remove old theme dictionary
            RemoveThemeDictionary();

            // Add new theme dictionary
            app.Resources.MergedDictionaries.Add(rd);
            LoggingService.Log($"[ThemeService] Applied theme: {theme} from {themeUri}");
        }

        /// <summary>
        /// Remove the current theme dictionary from merged dictionaries
        /// </summary>
        private void RemoveThemeDictionary()
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;

            // Find and remove dark or light theme dictionaries
            for (int i = app.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
            {
                var dict = app.Resources.MergedDictionaries[i];
                if (dict.Source != null)
                {
                    var source = dict.Source.ToString();
                    if (source.Contains("DarkTheme.xaml") || source.Contains("LightTheme.xaml"))
                    {
                        app.Resources.MergedDictionaries.RemoveAt(i);
                        LoggingService.Log($"[ThemeService] Removed theme dictionary: {source}");
                    }
                }
            }
        }
    }
}
