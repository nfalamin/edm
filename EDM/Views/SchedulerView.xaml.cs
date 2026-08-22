using System;
using System.Windows;
using System.Windows.Controls;
using EDM.Services;

namespace EDM.Views
{
    public partial class SchedulerView : System.Windows.Controls.UserControl
    {
        public SchedulerView()
        {
            InitializeComponent();
            LoadExistingSettings();
        }

        private void LoadExistingSettings()
        {
            try
            {
                var settings = new SettingsService();
                if (int.TryParse(settings.GetSetting("MaxConcurrentDownloads"), out int maxConc) && maxConc >= 1 && maxConc <= 16)
                {
                    ConcurrencySlider.Value = maxConc;
                    ConcurrencyValueText.Text = $"{maxConc} Downloads";
                }

                if (int.TryParse(settings.GetSetting("SpeedLimitKbps"), out int speed) && speed >= 0)
                {
                    SpeedLimitTextBox.Text = speed.ToString();
                }

                string? schedVal = settings.GetSetting("EnableScheduler");
                if (!string.IsNullOrEmpty(schedVal))
                {
                    EnableSchedulerCheckBox.IsChecked = string.Equals(schedVal, "true", StringComparison.OrdinalIgnoreCase);
                }

                string? shutdownVal = settings.GetSetting("ShutdownOnQueueComplete");
                if (!string.IsNullOrEmpty(shutdownVal))
                {
                    ShutdownPcCheckBox.IsChecked = string.Equals(shutdownVal, "true", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SchedulerView] LoadExistingSettings failed", ex);
            }
        }

        private void ConcurrencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ConcurrencyValueText != null)
            {
                ConcurrencyValueText.Text = $"{(int)e.NewValue} Downloads";
            }
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int maxConcurrency = (int)ConcurrencySlider.Value;
                int speedLimit = int.TryParse(SpeedLimitTextBox.Text, out int limit) ? Math.Max(0, limit) : 0;
                bool enableScheduler = EnableSchedulerCheckBox.IsChecked == true;
                bool shutdownPc = ShutdownPcCheckBox.IsChecked == true;

                var settings = new SettingsService();
                settings.SetSetting("MaxConcurrentDownloads", maxConcurrency.ToString());
                settings.SetSetting("SpeedLimitKbps", speedLimit.ToString());
                settings.SetSetting("EnableScheduler", enableScheduler.ToString().ToLowerInvariant());
                settings.SetSetting("ShutdownOnQueueComplete", shutdownPc.ToString().ToLowerInvariant());

                // Apply to live backend engines
                DownloadQueueScheduler.Instance.MaxConcurrentDownloads = maxConcurrency;
                BandwidthThrottler.Instance.SetLimit(speedLimit);

                LoggingService.Log($"[SchedulerView] Saved & applied settings: SchedulerEnabled={enableScheduler}, MaxConcurrency={maxConcurrency}, SpeedLimit={speedLimit}Kbps, ShutdownOnFinish={shutdownPc}");

                System.Windows.MessageBox.Show("Scheduler and Queue Management settings successfully saved and applied to download engine!", "Settings Applied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save scheduler settings: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
