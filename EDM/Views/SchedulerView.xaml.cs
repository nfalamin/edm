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

                string? actionVal = settings.GetSetting("ScheduledPowerAction");
                if (!string.IsNullOrEmpty(actionVal) && PowerActionComboBox != null)
                {
                    PowerActionComboBox.SelectedIndex = actionVal switch
                    {
                        "Sleep" => 1,
                        "Hibernate" => 2,
                        "ExitApplication" => 3,
                        _ => 0
                    };
                }

                string? graceVal = settings.GetSetting("ScheduledGracePeriodSeconds");
                if (!string.IsNullOrEmpty(graceVal) && GracePeriodComboBox != null)
                {
                    GracePeriodComboBox.SelectedIndex = graceVal switch
                    {
                        "15" => 0,
                        "60" => 2,
                        _ => 1
                    };
                }

                UpdatePowerPanelState();
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SchedulerView] LoadExistingSettings failed", ex);
            }
        }

        private void ShutdownPcCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdatePowerPanelState();
        }

        private void UpdatePowerPanelState()
        {
            if (PowerOptionsPanel != null)
            {
                bool enabled = ShutdownPcCheckBox.IsChecked == true;
                PowerOptionsPanel.IsEnabled = enabled;
                PowerOptionsPanel.Opacity = enabled ? 1.0 : 0.45;
            }
        }

        private void TestPowerActionBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PowerAction action = PowerActionComboBox?.SelectedIndex switch
                {
                    1 => PowerAction.Sleep,
                    2 => PowerAction.Hibernate,
                    3 => PowerAction.ExitApplication,
                    _ => PowerAction.Shutdown
                };

                int grace = GracePeriodComboBox?.SelectedIndex switch
                {
                    0 => 15,
                    2 => 60,
                    _ => 30
                };

                var countdownWin = new PowerActionCountdownDialog(action, grace);
                countdownWin.Owner = Window.GetWindow(this);
                countdownWin.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to launch test countdown: {ex.Message}", "Test Alert Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                string selectedAction = PowerActionComboBox?.SelectedIndex switch
                {
                    1 => "Sleep",
                    2 => "Hibernate",
                    3 => "ExitApplication",
                    _ => "Shutdown"
                };

                int graceSeconds = GracePeriodComboBox?.SelectedIndex switch
                {
                    0 => 15,
                    2 => 60,
                    _ => 30
                };

                var settings = new SettingsService();
                settings.SetSetting("MaxConcurrentDownloads", maxConcurrency.ToString());
                settings.SetSetting("SpeedLimitKbps", speedLimit.ToString());
                settings.SetSetting("EnableScheduler", enableScheduler.ToString().ToLowerInvariant());
                settings.SetSetting("ShutdownOnQueueComplete", shutdownPc.ToString().ToLowerInvariant());
                settings.SetSetting("ScheduledPowerAction", selectedAction);
                settings.SetSetting("ScheduledGracePeriodSeconds", graceSeconds.ToString());

                // Apply to live backend engines
                DownloadQueueScheduler.Instance.MaxConcurrentDownloads = maxConcurrency;
                BandwidthThrottler.Instance.SetLimit(speedLimit);

                LoggingService.Log($"[SchedulerView] Saved & applied settings: SchedulerEnabled={enableScheduler}, MaxConcurrency={maxConcurrency}, SpeedLimit={speedLimit}Kbps, ShutdownOnFinish={shutdownPc}, PowerAction={selectedAction}");

                System.Windows.MessageBox.Show("Scheduler and Queue Management settings successfully saved and applied to download engine!", "Settings Applied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save scheduler settings: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
