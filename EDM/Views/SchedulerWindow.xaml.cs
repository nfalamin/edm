using System;
using System.Windows;

namespace EDM.Views
{
    public partial class SchedulerWindow : Window
    {
        public bool IsSchedulerEnabled { get; private set; }
        public TimeSpan ScheduledTime { get; private set; }

        public SchedulerWindow(bool isCurrentlyActive, TimeSpan currentScheduledTime)
        {
            InitializeComponent();

            // পূর্ববর্তী সেটিংস লোড করা হচ্ছে
            EnableSchedulerCheckBox.IsChecked = isCurrentlyActive;
            HourTextBox.Text = currentScheduledTime.Hours.ToString("D2");
            MinuteTextBox.Text = currentScheduledTime.Minutes.ToString("D2");

            ToggleInputFields(isCurrentlyActive);
        }

        private void EnableSchedulerCheckBox_Click(object sender, RoutedEventArgs e)
        {
            ToggleInputFields(EnableSchedulerCheckBox.IsChecked == true);
        }

        private void ToggleInputFields(bool isEnabled)
        {
            if (HourTextBox != null && MinuteTextBox != null)
            {
                HourTextBox.IsEnabled = isEnabled;
                MinuteTextBox.IsEnabled = isEnabled;
                HourTextBox.Opacity = isEnabled ? 1.0 : 0.4;
                MinuteTextBox.Opacity = isEnabled ? 1.0 : 0.4;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            bool isEnabled = EnableSchedulerCheckBox.IsChecked == true;

            if (isEnabled)
            {
                if (int.TryParse(HourTextBox.Text, out int hours) && int.TryParse(MinuteTextBox.Text, out int minutes))
                {
                    if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59)
                    {
                        EDM.Services.ErrorDialogService.ShowWarning("[SchedulerWindow.SaveButton_Click]","Please enter a valid 24-hour time (00:00 to 23:59).","Scheduler Warning");
                        return;
                    }
                    ScheduledTime = new TimeSpan(hours, minutes, 0);
                }
                else
                {
                    EDM.Services.ErrorDialogService.ShowWarning("[SchedulerWindow.SaveButton_Click]","Please enter numeric values for hours and minutes.","Scheduler Warning");
                    return;
                }
            }

            IsSchedulerEnabled = isEnabled;
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}