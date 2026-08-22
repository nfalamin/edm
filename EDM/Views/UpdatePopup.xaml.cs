using System;
using System.IO;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using EDM.Services;
using EDM.Services.Interfaces;

namespace EDM.Views
{
    public partial class UpdatePopup : Window
    {
        private readonly UpdateService _updateService;
        private readonly ISettingsService _settingsService;
        private UpdateInfo? _updateInfo;
        private bool _allowClose = false;

        public UpdatePopup(UpdateInfo? updateInfo = null, ISettingsService? settingsService = null)
        {
            InitializeComponent();
            _settingsService = settingsService ?? new SettingsService();
            _updateService = new UpdateService(_settingsService);
            _updateInfo = updateInfo;

            this.Closing += UpdatePopup_Closing;

            if (_updateInfo != null)
            {
                DisplayUpdateInfo(_updateInfo);
            }
            else
            {
                LoadLocalManifest();
            }
        }

        private void UpdatePopup_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // If Tier 1 (Mandatory Update), prevent closing unless explicit exit/update
            if (_updateInfo != null && _updateInfo.IsMandatory && !_allowClose)
            {
                e.Cancel = true;
            }
        }

        public void DisplayUpdateInfo(UpdateInfo info)
        {
            _updateInfo = info;
            Dispatcher.Invoke(() =>
            {
                VersionHeadingText.Text = $"EDM v{info.Version} is Available";
                ChangelogText.Text = string.IsNullOrWhiteSpace(info.Changelog) 
                    ? "• Performance improvements\n• Bug fixes and stability enhancements" 
                    : info.Changelog;

                DownloadBtn.IsEnabled = !string.IsNullOrWhiteSpace(info.DownloadUrl);

                if (info.IsMandatory || string.Equals(info.Severity, "REQUIRED", StringComparison.OrdinalIgnoreCase))
                {
                    // Tier 1: REQUIRED / Critical Update UI
                    TierBadge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 38));
                    TierBadgeText.Text = "🚨 REQUIRED UPDATE";
                    SubtitleText.Text = "A critical security and protocol update is required to continue using EDM.";
                    MandatoryWarningBox.Visibility = Visibility.Visible;
                    
                    CloseBtn.Visibility = Visibility.Collapsed;
                    RemindLaterBtn.Visibility = Visibility.Collapsed;
                    SkipVersionBtn.Visibility = Visibility.Collapsed;
                    ExitAppBtn.Visibility = Visibility.Visible;
                }
                else if (string.Equals(info.Severity, "RECOMMENDED", StringComparison.OrdinalIgnoreCase))
                {
                    // Tier 2: RECOMMENDED Update UI
                    TierBadge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
                    TierBadgeText.Text = "⭐ RECOMMENDED UPDATE";
                    SubtitleText.Text = !string.IsNullOrWhiteSpace(info.Title) ? info.Title : "A recommended performance update is available for EDM.";
                    MandatoryWarningBox.Visibility = Visibility.Collapsed;

                    CloseBtn.Visibility = Visibility.Visible;
                    RemindLaterBtn.Visibility = Visibility.Visible;
                    SkipVersionBtn.Visibility = Visibility.Visible;
                    ExitAppBtn.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Tier 3: OPTIONAL / Feature Update UI
                    TierBadge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(99, 102, 241));
                    TierBadgeText.Text = "✨ OPTIONAL UPDATE";
                    SubtitleText.Text = !string.IsNullOrWhiteSpace(info.Title) ? info.Title : "A new version of Exclusive Download Manager is available for download.";
                    MandatoryWarningBox.Visibility = Visibility.Collapsed;

                    CloseBtn.Visibility = Visibility.Visible;
                    RemindLaterBtn.Visibility = Visibility.Visible;
                    SkipVersionBtn.Visibility = Visibility.Visible;
                    ExitAppBtn.Visibility = Visibility.Collapsed;
                }
            });
        }

        private async void LoadLocalManifest()
        {
            try
            {
                var updatePath = Path.Combine(AppContext.BaseDirectory, "update.json");
                if (File.Exists(updatePath))
                {
                    var info = await _updateService.CheckForUpdatesAsync(updatePath, new Version(1, 0, 0));
                    DisplayUpdateInfo(info);
                }
                else
                {
                    ChangelogText.Text = "No update details available.";
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[UpdatePopup.LoadLocalManifest]", ex);
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            _allowClose = true;
            this.Close();
        }

        private void RemindLaterBtn_Click(object sender, RoutedEventArgs e)
        {
            _allowClose = true;
            this.Close();
        }

        private void SkipVersionBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_updateInfo != null && !string.IsNullOrWhiteSpace(_updateInfo.Version))
            {
                _settingsService.SaveSetting("SkippedUpdateVersion", _updateInfo.Version);
                LoggingService.Log($"[UpdatePopup] User chose to skip version {_updateInfo.Version}");
            }
            _allowClose = true;
            this.Close();
        }

        private void ExitAppBtn_Click(object sender, RoutedEventArgs e)
        {
            _allowClose = true;
            System.Windows.Application.Current.Shutdown();
        }

        private async void DownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_updateInfo == null || string.IsNullOrWhiteSpace(_updateInfo.DownloadUrl))
            {
                System.Windows.MessageBox.Show("No download URL available for this update.", "Update Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DownloadBtn.IsEnabled = false;
            RemindLaterBtn.IsEnabled = false;
            SkipVersionBtn.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;
            ProgressStatusText.Text = "Downloading update installer via EDM engine...";

            var progressReporter = new Progress<DownloadProgressInfo>(info =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    UpdateProgressBar.Value = info.ProgressPercentage;
                    ProgressStatusText.Text = $"Downloading update: {info.ProgressPercentage:F1}% ({(info.SpeedBytesPerSecond / (1024.0 * 1024.0)):F2} MB/s)";
                });
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var pauseToken = new PauseTokenSource();

            try
            {
                string installerPath = await _updateService.DownloadAndVerifyUpdateAsync(_updateInfo, progressReporter, pauseToken, cts.Token);

                ProgressStatusText.Text = "Checksum verified cleanly. Launching installer...";
                await Task.Delay(800);

                // Launch installer and shutdown app to allow binary overwrite
                _allowClose = true;
                Process.Start(new ProcessStartInfo(installerPath) { UseShellExecute = true });
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[UpdatePopup.DownloadBtn_Click]", ex);
                System.Windows.MessageBox.Show($"Update download failed: {ex.Message}", "Update Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                DownloadBtn.IsEnabled = true;
                RemindLaterBtn.IsEnabled = true;
                SkipVersionBtn.IsEnabled = true;
                ProgressPanel.Visibility = Visibility.Collapsed;
            }
        }
    }
}
