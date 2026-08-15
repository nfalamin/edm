using System;

namespace EDM.Services.Interfaces
{
    /// <summary>
    /// IThemeService - Defines the contract for theme management.
    /// Provides methods to dynamically switch between light and dark themes,
    /// get current theme state, and subscribe to theme change notifications.
    /// </summary>
    public interface IThemeService
    {
        /// <summary>
        /// Gets the current active theme (Light or Dark)
        /// </summary>
        ApplicationThemeMode CurrentTheme { get; }

        /// <summary>
        /// Raised when the theme changes
        /// </summary>
        event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        /// <summary>
        /// Switch to a specific theme
        /// </summary>
        /// <param name="theme">The theme to switch to (Light or Dark)</param>
        void SetTheme(ApplicationThemeMode theme);

        /// <summary>
        /// Toggle between light and dark themes
        /// </summary>
        void ToggleTheme();

        /// <summary>
        /// Load the theme preference from persistent storage (settings)
        /// </summary>
        void LoadThemePreference();

        /// <summary>
        /// Save the current theme preference to persistent storage
        /// </summary>
        void SaveThemePreference();

        /// <summary>
        /// Get a brush resource for the current theme
        /// </summary>
        /// <param name="resourceKey">The key of the brush resource (e.g., "AccentBrush", "PrimaryTextBrush")</param>
        /// <returns>The brush object, or null if not found</returns>
        object? GetBrushResource(string resourceKey);
    }

    /// <summary>
    /// Enumeration of available themes (renamed from ThemeMode to avoid System.Windows.ThemeMode conflict)
    /// </summary>
    public enum ApplicationThemeMode
    {
        Light = 0,
        Dark = 1
    }

    /// <summary>
    /// Event arguments for theme change events
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        public ApplicationThemeMode OldTheme { get; set; }
        public ApplicationThemeMode NewTheme { get; set; }

        public ThemeChangedEventArgs(ApplicationThemeMode oldTheme, ApplicationThemeMode newTheme)
        {
            OldTheme = oldTheme;
            NewTheme = newTheme;
        }
    }
}
