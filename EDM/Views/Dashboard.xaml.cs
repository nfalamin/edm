using System;
using System.Linq;
using Microsoft.Win32;
using EDM.ViewModels;
// Explicit aliases to resolve ambiguity with System.Windows.Forms
using WpfApp        = System.Windows.Application;
using WpfWindow     = System.Windows.Window;
using WpfColor      = System.Windows.Media.Color;
using WpfColorConv  = System.Windows.Media.ColorConverter;
using WpfMsgBox     = System.Windows.MessageBox;
using WpfMsgBtn     = System.Windows.MessageBoxButton;
using WpfMsgImg     = System.Windows.MessageBoxImage;
using WpfSolidBrush = System.Windows.Media.SolidColorBrush;
using WpfBrushConv  = System.Windows.Media.BrushConverter;
using WpfResDict    = System.Windows.ResourceDictionary;
using WpfBorder     = System.Windows.Controls.Border;
using WpfButton     = System.Windows.Controls.Button;
using WpfUri        = System.Uri;

namespace EDM.Views
{
    /// <summary>
    /// Dashboard UserControl — Main content area
    /// Supports: Dark/Light Mica theme toggle, System theme auto-detection, Silent instant switching
    /// </summary>
    public partial class Dashboard : System.Windows.Controls.UserControl
    {
        private DownloadManagerViewModel? _viewModel;
        private bool _isDarkMode = true;

        // Theme XAML resource URIs
        private static readonly WpfUri DarkThemeUri  = new WpfUri("pack://application:,,,/EDM;component/Themes/DarkTheme.xaml");
        private static readonly WpfUri LightThemeUri = new WpfUri("pack://application:,,,/EDM;component/Themes/LightTheme.xaml");

        public Dashboard()
        {
            InitializeComponent();
            InitializeViewModel();
            DetectAndApplySystemTheme();
        }

        // ===================================================================
        // VIEWMODEL INITIALIZATION
        // ===================================================================

        private void InitializeViewModel()
        {
            _viewModel = new DownloadManagerViewModel(); // history loads from SQLite in constructor
            this.DataContext = _viewModel;

            if (DownloadsTableControl != null)
                DownloadsTableControl.ViewModel = _viewModel;

            try
            {
                string uName = Environment.UserName;
                if (!string.IsNullOrWhiteSpace(uName) && UserNameTextBlock != null && UserAvatarInitials != null)
                {
                    UserNameTextBlock.Text = uName;
                    UserAvatarInitials.Text = uName.Length >= 2 ? uName.Substring(0, 2).ToUpper() : uName.ToUpper();
                }
            }
            catch { }

            _ = _viewModel.StartMetricsUpdates(500);
        }

        // ===================================================================
        // THEME SYSTEM — DARK / LIGHT (Mica-inspired)
        // ===================================================================

        /// <summary>
        /// Detect Windows system theme via Registry.
        /// AppsUseLightTheme: 0 = Dark, 1 = Light
        /// </summary>
        private bool IsSystemDarkTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                if (value is int intVal)
                    return intVal == 0;
            }
            catch (Exception ex) { try { EDM.Services.LoggingService.LogException("[AutoFix] Swallowed exception in IsSystemDarkTheme", ex); } catch { } }
            return true;
        }

        private void DetectAndApplySystemTheme()
        {
            try
            {
                var settings = new EDM.Services.SettingsService();
                var savedTheme = settings.GetSetting("SelectedTheme");
                if (string.IsNullOrEmpty(savedTheme))
                {
                    // Default to Light theme as per user requirement
                    _isDarkMode = false;
                }
                else
                {
                    _isDarkMode = !string.Equals(savedTheme, "Light", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                _isDarkMode = true; // Default to Dark
            }

            ApplyTheme(_isDarkMode, updateButton: true);
        }

        /// <summary>
        /// Instantly swap theme ResourceDictionary — no popup, no lag.
        /// Also applies theme-aware colors to all hardcoded-color elements.
        /// </summary>
        private void ApplyTheme(bool isDark, bool updateButton = true)
        {
            _isDarkMode = isDark;
            var mergedDicts = WpfApp.Current.Resources.MergedDictionaries;

            // Remove existing theme dictionary
            var toRemove = mergedDicts
                .Where(d => d.Source != null &&
                            (d.Source.OriginalString.Contains("DarkTheme") ||
                             d.Source.OriginalString.Contains("LightTheme")))
                .ToList();
            foreach (var d in toRemove)
                mergedDicts.Remove(d);

            // Inject new theme — all DynamicResource bindings in XAML update automatically
            mergedDicts.Add(new WpfResDict { Source = isDark ? DarkThemeUri : LightThemeUri });

            System.Diagnostics.Debug.WriteLine(
                $"[EDM Theme] Applied: {(isDark ? "Dark" : "Light")}");
        }

        private static T? FindVisualChild<T>(System.Windows.DependencyObject parent)
            where T : System.Windows.DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T result) return result;
                var found = FindVisualChild<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        // Generic helper to find a named child of any type in the visual tree
        private static T? FindVisualChildByName<T>(System.Windows.DependencyObject parent, string name)
            where T : System.Windows.FrameworkElement
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T fe && fe.Name == name) return fe;
                var found = FindVisualChildByName<T>(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private void LightModeBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ApplyTheme(false, updateButton: true);
            try
            {
                var settings = new EDM.Services.SettingsService();
                settings.SaveSetting("SelectedTheme", "Light");
            }
            catch (Exception ex)
            {
                EDM.Services.LoggingService.LogException("[Dashboard] Failed to persist SelectedTheme", ex);
            }
        }

        private void DarkModeBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ApplyTheme(true, updateButton: true);
            try
            {
                var settings = new EDM.Services.SettingsService();
                settings.SaveSetting("SelectedTheme", "Dark");
            }
            catch (Exception ex)
            {
                EDM.Services.LoggingService.LogException("[Dashboard] Failed to persist SelectedTheme", ex);
            }
        }

        private void ThemeToggle_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _isDarkMode = !_isDarkMode;
            ApplyTheme(_isDarkMode, updateButton: true);

            try
            {
                var settings = new EDM.Services.SettingsService();
                settings.SaveSetting("SelectedTheme", _isDarkMode ? "Dark" : "Light");
            }
            catch (Exception ex)
            {
                EDM.Services.LoggingService.LogException("[Dashboard] Failed to persist SelectedTheme", ex);
            }
        }

        // ===================================================================
        // OVERVIEW CARD CLICK → FILTER
        // ===================================================================

        /// <summary>
        /// Overview KPI card click handler — applies filter to the download list.
        /// Tag on the Border tells us which filter to apply.
        /// </summary>
        private void OverviewCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is WpfBorder card && card.Tag is string filter)
            {
                if (_viewModel != null)
                {
                    _viewModel.CurrentFilter = filter;
                    System.Diagnostics.Debug.WriteLine($"[Dashboard] Overview card filter: {filter}");
                }
            }
        }

        private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (FindName("SearchWatermark") is System.Windows.Controls.TextBlock watermark)
            {
                watermark.Visibility = string.IsNullOrEmpty(SearchTextBox.Text) 
                    ? System.Windows.Visibility.Visible 
                    : System.Windows.Visibility.Collapsed;
            }

            if (_viewModel != null)
            {
                _viewModel.SearchQuery = SearchTextBox.Text;
            }
        }

        // ===================================================================
        // HEADER ACTIONS
        // ===================================================================

        private void Notification_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var notifs = EDM.Services.NotificationService.Instance.GetRecentNotifications();
            if (notifs.Count == 0)
            {
                WpfMsgBox.Show(
                    "No new notifications.",
                    "EDM Notifications",
                    WpfMsgBtn.OK,
                    WpfMsgImg.Information);
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Notifications ({notifs.Count}):\n");
                int idx = 1;
                foreach (var n in notifs.Take(5))
                {
                    string icon = n.Severity == EDM.Services.NotificationSeverity.Success ? "✅" :
                                 n.Severity == EDM.Services.NotificationSeverity.Error ? "❌" :
                                 n.Severity == EDM.Services.NotificationSeverity.Warning ? "⚠️" : "ℹ️";
                    sb.AppendLine($"{idx++}. {icon} {n.Title} - {n.Message} ({n.Timestamp:HH:mm:ss})");
                }

                EDM.Services.NotificationService.Instance.MarkAllAsRead();
                WpfMsgBox.Show(
                    sb.ToString(),
                    "EDM Notifications",
                    WpfMsgBtn.OK,
                    WpfMsgImg.Information);
            }
        }

        private void ProfilePill_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            OnSettingsClicked();
        }

        // ===================================================================
        // QUICK ACTION TOOLBAR
        // ===================================================================

        private void ActionButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is WpfButton btn && btn.Tag is string action)
            {
                switch (action)
                {
                    case "AddUrl":    OnAddUrlClicked();    break;
                    case "Resume":    OnResumeClicked();    break;
                    case "Pause":     OnPauseClicked();     break;
                    case "Stop":      OnStopClicked();      break;
                    case "Delete":    OnDeleteClicked();    break;
                    case "DeleteAll": OnDeleteAllClicked(); break;
                    case "Scheduler": OnSchedulerClicked(); break;
                    case "Settings":  OnSettingsClicked();  break;
                }
            }
        }

        private void OnAddUrlClicked()
        {
            var existing = System.Windows.Application.Current.Windows.OfType<AddUrlWindow>().FirstOrDefault();
            if (existing != null)
            {
                existing.Activate();
                return;
            }

            var dlg = new AddUrlWindow();
            dlg.Initialize(_viewModel);
            dlg.Owner = WpfWindow.GetWindow(this);

            try
            {
                if (System.Windows.Forms.Clipboard.ContainsText())
                {
                    string text = System.Windows.Forms.Clipboard.GetText().Trim();
                    if (WpfUri.TryCreate(text, UriKind.Absolute, out var uri) &&
                        (uri.Scheme == WpfUri.UriSchemeHttp || uri.Scheme == WpfUri.UriSchemeHttps))
                    {
                        dlg.UrlTextBox.Text = text;
                    }
                }
            }
            catch (Exception ex) { try { EDM.Services.LoggingService.LogException("[AutoFix] Swallowed exception in Dashboard clipboard handling", ex); } catch { } }

            dlg.ShowDialog();
        }

        private void OnResumeClicked()    => _viewModel?.ResumeAll();
        private void OnPauseClicked()     => _viewModel?.PauseAll();
        private void OnStopClicked()      => _viewModel?.StopAll();
        private void OnDeleteClicked()    => _viewModel?.DeleteSelected();

        private void OnDeleteAllClicked()
        {
            if (_viewModel == null || !_viewModel.AllDownloads.Any())
            {
                WpfMsgBox.Show("There are no downloads to delete.", "Delete All", WpfMsgBtn.OK, WpfMsgImg.Information);
                return;
            }

            var result = WpfMsgBox.Show(
                "Are you sure you want to delete all downloads from the list and history?",
                "Confirm Delete All",
                WpfMsgBtn.YesNo,
                WpfMsgImg.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _viewModel.DeleteAll();
            }
        }

        private void OnSchedulerClicked()
        {
            try
            {
                var win = new SchedulerWindow(false, TimeSpan.FromHours(2));
                win.Owner = WpfWindow.GetWindow(this);
                win.ShowDialog();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        private void OnSettingsClicked()
        {
            try
            {
                var win = new SettingsWindow();
                win.Owner = WpfWindow.GetWindow(this);
                win.ShowDialog();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        }
    }
}
