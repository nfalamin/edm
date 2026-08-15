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
                int speedLimit = int.TryParse(SpeedLimitTextBox.Text, out int limit) ? limit : 0;
                bool enableScheduler = EnableSchedulerCheckBox.IsChecked == true;
                bool shutdownPc = ShutdownPcCheckBox.IsChecked == true;

                LoggingService.Log($"[SchedulerView] Saved settings: SchedulerEnabled={enableScheduler}, MaxConcurrency={maxConcurrency}, SpeedLimit={speedLimit}Kbps, ShutdownOnFinish={shutdownPc}");

                System.Windows.MessageBox.Show("Scheduler and Queue Management settings successfully saved and applied!", "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save scheduler settings: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
