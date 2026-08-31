using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using EDM.Services;
using EDM.Models;
using System.Diagnostics;
using System.Windows.Forms; // requires reference to System.Windows.Forms
using EDM.Services.Interfaces;

namespace EDM.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly ISettingsService _settingsService;
        private readonly ExternalBackendService _externalBackend;

        public SettingsWindow()
        {
            InitializeComponent();

            // Resolve services from DI with resilient fallback
            _settingsService = (App.ServiceProvider?.GetService(typeof(EDM.Services.Interfaces.ISettingsService)) as ISettingsService) ?? new SettingsService();
            _externalBackend = (App.ServiceProvider?.GetService(typeof(EDM.Services.ExternalBackendService)) as ExternalBackendService) ?? new ExternalBackendService();

            DefaultPathTextBox.Text = _settingsService.GetDefaultDownloadPath();
            try
            {
                var ff = this.FindName("FfmpegPathTextBox") as System.Windows.Controls.TextBox;
                if (ff != null) ff.Text = _settingsService.GetFfmpegPath();
            }
            catch (Exception ex) { LoggingService.LogException("[SettingsWindow] Failed to initialize FfmpegPathTextBox", ex); }
            try
            {
                var y = this.FindName("YtDlpPathTextBox") as System.Windows.Controls.TextBox;
                if (y != null) y.Text = _settingsService.GetYtDlpPath();
            }
            catch (Exception ex) { LoggingService.LogException("[SettingsWindow] Failed to initialize YtDlpPathTextBox", ex); }
            try
            {
                var a = this.FindName("Aria2PathTextBox") as System.Windows.Controls.TextBox;
                if (a != null) a.Text = _settingsService.GetAria2Path();
            }
            catch (Exception ex) { LoggingService.LogException("[SettingsWindow] Failed to initialize Aria2PathTextBox", ex); }
            try
            {
                var fa = this.FindName("FormatArgsTextBox") as System.Windows.Controls.TextBox;
                if (fa != null) fa.Text = _settingsService.GetDefaultFormatArgs();
            }
            catch (Exception ex) { LoggingService.LogException("[SettingsWindow] Failed to initialize FormatArgsTextBox", ex); }
            try
            {
                var cb = this.FindName("AutoConvertCheckBox") as System.Windows.Controls.CheckBox;
                if (cb != null) cb.IsChecked = _settingsService.GetAutoConvertToMp3();
            }
            catch (Exception ex) { LoggingService.LogException("[SettingsWindow] Failed to initialize AutoConvertCheckBox", ex); }

            // Initialize crash reporting preference checkbox (default OFF)
            try
            {
                var crashCb = this.FindName("SendCrashReportsCheckBox") as System.Windows.Controls.CheckBox;
                if (crashCb != null) crashCb.IsChecked = _settingsService.GetSendAnonymousCrashReports();
            }
            catch (Exception ex) { LoggingService.LogException("[SettingsWindow] Failed to initialize SendCrashReportsCheckBox", ex); }

            LoadUrlSafetySettings();
            LoadClipboardSettings();
            LoadBrowserSettings();
            LoadProxySettings();
            LoadBandwidthSchedules();
            UpdateContextMenuStatus();
            LoadResourceOptimizerSettings();
        }

        private void LoadUrlSafetySettings()
        {
            try
            {
                var enableCheckBox = this.FindName("EnableSafetyCheckBox") as System.Windows.Controls.CheckBox;
                if (enableCheckBox != null)
                {
                    enableCheckBox.IsChecked = _settingsService.GetEnableUrlSafetyCheck();
                }

                var apiKeyBox = this.FindName("ApiKeyPasswordBox") as System.Windows.Controls.PasswordBox;
                if (apiKeyBox != null)
                {
                    apiKeyBox.Password = _settingsService.GetGoogleSafeBrowsingApiKey();
                }

                var statusText = this.FindName("ApiKeyStatusText") as System.Windows.Controls.TextBlock;
                var optionsPanel = this.FindName("SafetyOptionsPanel") as System.Windows.Controls.StackPanel;

                if (enableCheckBox != null && optionsPanel != null)
                {
                    optionsPanel.IsEnabled = enableCheckBox.IsChecked == true;
                }

                UpdateApiKeyStatus();
            }
            catch (Exception ex) { LoggingService.Log($"[SettingsWindow.LoadUrlSafetySettings] Failed: {ex.Message}"); }
        }

        private void SafetyEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                var optionsPanel = this.FindName("SafetyOptionsPanel") as System.Windows.Controls.StackPanel;
                var enableCheckBox = this.FindName("EnableSafetyCheckBox") as System.Windows.Controls.CheckBox;
                if (optionsPanel != null && enableCheckBox != null)
                {
                    optionsPanel.IsEnabled = enableCheckBox.IsChecked == true;
                }
            }
            catch (Exception ex) { LoggingService.Log($"[SettingsWindow.SafetyEnabledCheckBox_Changed] {ex.Message}"); }
        }

        private void UpdateApiKeyStatus()
        {
            try
            {
                var statusText = this.FindName("ApiKeyStatusText") as System.Windows.Controls.TextBlock;
                var apiKeyBox = this.FindName("ApiKeyPasswordBox") as System.Windows.Controls.PasswordBox;

                if (statusText != null && apiKeyBox != null)
                {
                    if (string.IsNullOrEmpty(apiKeyBox.Password))
                    {
                        statusText.Text = "❌ Not configured";
                        statusText.Foreground = System.Windows.Media.Brushes.Red;
                    }
                    else
                    {
                        statusText.Text = "✓ Configured";
                        statusText.Foreground = System.Windows.Media.Brushes.Green;
                    }
                }
            }
            catch (Exception ex) { LoggingService.Log($"[SettingsWindow.UpdateApiKeyStatus] {ex.Message}"); }
        }

        private void LoadClipboardSettings()
        {
            try
            {
                var enabled = _settingsService.GetEnableClipboardMonitoring();
                var enableCheckBox = this.FindName("EnableClipboardMonitoringCheckBox") as System.Windows.Controls.CheckBox;
                var optionsPanel = this.FindName("ClipboardOptionsPanel") as System.Windows.Controls.StackPanel;
                var httpBox = this.FindName("ClipboardHttpCheckBox") as System.Windows.Controls.CheckBox;
                var httpsBox = this.FindName("ClipboardHttpsCheckBox") as System.Windows.Controls.CheckBox;
                var ftpBox = this.FindName("ClipboardFtpCheckBox") as System.Windows.Controls.CheckBox;
                var actionCombo = this.FindName("ClipboardActionComboBox") as System.Windows.Controls.ComboBox;
                var dedupBox = this.FindName("ClipboardIgnoreDuplicatesCheckBox") as System.Windows.Controls.CheckBox;
                var notifyBox = this.FindName("ClipboardShowNotificationCheckBox") as System.Windows.Controls.CheckBox;

                if (enableCheckBox != null) enableCheckBox.IsChecked = enabled;
                if (optionsPanel != null) optionsPanel.IsEnabled = enabled;
                if (httpBox != null) httpBox.IsChecked = _settingsService.GetClipboardMonitorHttp();
                if (httpsBox != null) httpsBox.IsChecked = _settingsService.GetClipboardMonitorHttps();
                if (ftpBox != null) ftpBox.IsChecked = _settingsService.GetClipboardMonitorFtp();
                if (actionCombo != null)
                {
                    actionCombo.SelectedIndex = (int)_settingsService.GetClipboardAction();
                    if (actionCombo.SelectedIndex < 0) actionCombo.SelectedIndex = 0;
                }
                if (dedupBox != null) dedupBox.IsChecked = _settingsService.GetClipboardIgnoreDuplicates();
                if (notifyBox != null) notifyBox.IsChecked = _settingsService.GetClipboardShowNotification();
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SettingsWindow.LoadClipboardSettings] Failed", ex);
            }
        }

        private void ClipboardMonitoringCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                var optionsPanel = this.FindName("ClipboardOptionsPanel") as System.Windows.Controls.StackPanel;
                var enableCheckBox = this.FindName("EnableClipboardMonitoringCheckBox") as System.Windows.Controls.CheckBox;
                if (optionsPanel != null && enableCheckBox != null)
                {
                    optionsPanel.IsEnabled = enableCheckBox.IsChecked == true;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[SettingsWindow.ClipboardMonitoringCheckBox_Changed] {ex.Message}");
            }
        }

        private void LoadBrowserSettings()
        {
            try
            {
                var enabled = _settingsService.GetEnableBrowserIntegration();
                var enableCheckBox = this.FindName("EnableBrowserIntegrationCheckBox") as System.Windows.Controls.CheckBox;
                var optionsPanel = this.FindName("BrowserOptionsPanel") as System.Windows.Controls.StackPanel;
                var captureBox = this.FindName("BrowserCaptureDownloadsCheckBox") as System.Windows.Controls.CheckBox;
                var confirmBox = this.FindName("BrowserShowConfirmationCheckBox") as System.Windows.Controls.CheckBox;
                var notifyBox = this.FindName("BrowserShowNotificationCheckBox") as System.Windows.Controls.CheckBox;

                if (enableCheckBox != null) enableCheckBox.IsChecked = enabled;
                if (optionsPanel != null) optionsPanel.IsEnabled = enabled;
                if (captureBox != null) captureBox.IsChecked = _settingsService.GetBrowserCaptureDownloads();
                if (confirmBox != null) confirmBox.IsChecked = _settingsService.GetBrowserShowConfirmation();
                if (notifyBox != null) notifyBox.IsChecked = _settingsService.GetBrowserShowNotification();
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SettingsWindow.LoadBrowserSettings] Failed", ex);
            }
        }

        private void BrowserIntegrationCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                var optionsPanel = this.FindName("BrowserOptionsPanel") as System.Windows.Controls.StackPanel;
                var enableCheckBox = this.FindName("EnableBrowserIntegrationCheckBox") as System.Windows.Controls.CheckBox;
                if (optionsPanel != null && enableCheckBox != null)
                {
                    optionsPanel.IsEnabled = enableCheckBox.IsChecked == true;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[SettingsWindow.BrowserIntegrationCheckBox_Changed] {ex.Message}");
            }
        }

        private void LoadProxySettings()
        {
            try
            {
                var proxy = _settingsService.GetProxySettings();
                ProxyEnabledCheckBox.IsChecked = proxy.Enabled;
                ProxyTypeComboBox.SelectedIndex = (int)proxy.Type - 1; // ProxyType.Http=1 -> index 0
                if (ProxyTypeComboBox.SelectedIndex < 0) ProxyTypeComboBox.SelectedIndex = 0;
                ProxyHostTextBox.Text = proxy.Host;
                ProxyPortTextBox.Text = proxy.Port > 0 ? proxy.Port.ToString() : string.Empty;
                ProxyUsernameTextBox.Text = proxy.Username;
                // Password is never round-tripped into the UI in plain text; leave blank.
                // Saving without touching it keeps the previously stored encrypted password.
                ProxyBypassLocalCheckBox.IsChecked = proxy.BypassLocalAddresses;
                ProxyOptionsPanel.IsEnabled = proxy.Enabled;
            }
            catch (Exception ex) { LoggingService.Log($"[SettingsWindow.LoadProxySettings] Failed to load proxy settings: {ex.Message}"); }
        }

        private void ProxyEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            ProxyOptionsPanel.IsEnabled = ProxyEnabledCheckBox.IsChecked == true;
        }

        private void LoadBandwidthSchedules()
        {
            try
            {
                var listBox = this.FindName("BandwidthSchedulesListBox") as System.Windows.Controls.ListBox;
                if (listBox == null) return;

                var schedules = _settingsService.GetBandwidthSchedules();
                listBox.Items.Clear();
                foreach (var schedule in schedules)
                {
                    if (schedule.TimeRange != null)
                    {
                        string displayText = $"{schedule.TimeRange.StartHour:D2}:00 - {schedule.TimeRange.EndHour:D2}:00 | Limit: {schedule.SpeedLimitKbps} KB/s";
                        listBox.Items.Add(new { Schedule = schedule, DisplayText = displayText });
                    }
                }
            }
            catch (Exception ex) { LoggingService.LogException("[SettingsWindow.LoadBandwidthSchedules]", ex); }
        }

        private void AddSchedule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var startHourBox = this.FindName("ScheduleStartHourTextBox") as System.Windows.Controls.TextBox;
                var endHourBox = this.FindName("ScheduleEndHourTextBox") as System.Windows.Controls.TextBox;
                var speedLimitBox = this.FindName("ScheduleSpeedLimitTextBox") as System.Windows.Controls.TextBox;

                if (startHourBox == null || endHourBox == null || speedLimitBox == null)
                    return;

                if (!int.TryParse(startHourBox.Text, out int startHour) ||
                    !int.TryParse(endHourBox.Text, out int endHour) ||
                    !int.TryParse(speedLimitBox.Text, out int speedLimit))
                {
                    EDM.Services.ErrorDialogService.ShowWarning("[SettingsWindow]", "Please enter valid numbers for hours (0-23) and speed limit (KB/s).", "Invalid Input");
                    return;
                }

                if (startHour < 0 || startHour > 23 || endHour < 0 || endHour > 23 || speedLimit < 0)
                {
                    EDM.Services.ErrorDialogService.ShowWarning("[SettingsWindow]", "Hours must be 0-23, and speed limit must be >= 0.", "Invalid Range");
                    return;
                }

                var newSchedule = new BandwidthSchedule
                {
                    TimeRange = new Models.TimeRange(startHour, endHour),
                    SpeedLimitKbps = speedLimit
                };

                var schedules = _settingsService.GetBandwidthSchedules();
                schedules.Add(newSchedule);
                _settingsService.SetBandwidthSchedules(schedules);

                // Refresh UI
                LoadBandwidthSchedules();
                startHourBox.Clear();
                endHourBox.Clear();
                speedLimitBox.Clear();
                LoggingService.Log($"[SettingsWindow] Added bandwidth schedule: {startHour:D2}:00 - {endHour:D2}:00 @ {speedLimit} KB/s");
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SettingsWindow.AddSchedule_Click]", ex);
                EDM.Services.ErrorDialogService.ShowError("[SettingsWindow]", ex, "Failed to add schedule.", "Error");
            }
        }

        private void RemoveSchedule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var listBox = this.FindName("BandwidthSchedulesListBox") as System.Windows.Controls.ListBox;
                if (listBox == null || listBox.SelectedItem == null) return;
                dynamic item = listBox.SelectedItem;
                var schedule = item.Schedule as BandwidthSchedule;
                if (schedule == null) return;
                var schedules = _settingsService.GetBandwidthSchedules();
                // Remove by matching Start/End/Limit to avoid reference equality issues
                var toRemove = schedules.Find(s => s.TimeRange != null && s.TimeRange.StartHour == schedule.TimeRange.StartHour && s.TimeRange.EndHour == schedule.TimeRange.EndHour && s.SpeedLimitKbps == schedule.SpeedLimitKbps);
                if (toRemove != null)
                {
                    schedules.Remove(toRemove);
                    _settingsService.SetBandwidthSchedules(schedules);
                    LoadBandwidthSchedules();
                    LoggingService.Log("[SettingsWindow] Removed selected bandwidth schedule.");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SettingsWindow.RemoveSchedule_Click]", ex);
                EDM.Services.ErrorDialogService.ShowError("[SettingsWindow]", ex, "Failed to remove schedule.", "Error");
            }
        }

        private Models.ProxySettings BuildProxySettingsFromUi()
        {
            var type = ProxyTypeComboBox.SelectedIndex switch
            {
                0 => Models.ProxyType.Http,
                1 => Models.ProxyType.Https,
                2 => Models.ProxyType.Socks5,
                _ => Models.ProxyType.Http
            };

            int.TryParse(ProxyPortTextBox.Text, out int port);

            return new Models.ProxySettings
            {
                Enabled = ProxyEnabledCheckBox.IsChecked == true,
                Type = type,
                Host = ProxyHostTextBox.Text?.Trim() ?? string.Empty,
                Port = port,
                Username = ProxyUsernameTextBox.Text?.Trim() ?? string.Empty,
                BypassLocalAddresses = ProxyBypassLocalCheckBox.IsChecked == true
            };
        }

        private async void TestProxy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _ = Dispatcher.BeginInvoke(() => ProxyTestResultText.Text = "সংযোগ পরীক্ষা করা হচ্ছে...");
                var settings = BuildProxySettingsFromUi();
                // Use whatever password is currently typed for the test, without persisting it yet.
                var plainPassword = ProxyPasswordBox.Password;
                if (!string.IsNullOrEmpty(plainPassword))
                {
                    settings.EncryptedPassword = Services.ProxyService.EncryptPassword(plainPassword);
                }
                else if (string.Equals(settings.Username, _settingsService.GetProxySettings().Username, StringComparison.Ordinal))
                {
                    settings.EncryptedPassword = _settingsService.GetProxySettings().EncryptedPassword;
                }

                var (success, message) = await Services.ProxyService.TestProxyAsync(settings).ConfigureAwait(false);
                _ = Dispatcher.BeginInvoke(() =>
                {
                    ProxyTestResultText.Text = message;
                    ProxyTestResultText.Foreground = success
                        ? System.Windows.Media.Brushes.LightGreen
                        : System.Windows.Media.Brushes.OrangeRed;
                });
            }
            catch (Exception ex)
            {
                EDM.Services.LoggingService.LogException("[SettingsWindow.TestProxy_Click] Failed to test proxy", ex);
                _ = Dispatcher.BeginInvoke(() => ProxyTestResultText.Text = "Proxy test failed");
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.SelectedPath = DefaultPathTextBox.Text;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    DefaultPathTextBox.Text = dlg.SelectedPath;
                }
            }
        }

        private void BrowseFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Filter = "FFmpeg executable|ffmpeg.exe|All files|*.*";
            if (ofd.ShowDialog() == true)
            {
                try {
                    var ff = this.FindName("FfmpegPathTextBox") as System.Windows.Controls.TextBox;
                    if (ff != null) ff.Text = ofd.FileName;
                } catch (Exception ex) { LoggingService.LogException("[SettingsWindow] BrowseFfmpeg_Click handler failed", ex); }
            }
        }

        private void BrowseYtDlp_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Filter = "yt-dlp executable|yt-dlp.exe|All files|*.*";
            if (ofd.ShowDialog() == true)
            {
                try {
                    var y = this.FindName("YtDlpPathTextBox") as System.Windows.Controls.TextBox;
                    if (y != null) y.Text = ofd.FileName;
                } catch (Exception ex) { LoggingService.LogException("[SettingsWindow] BrowseYtDlp_Click handler failed", ex); }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var path = DefaultPathTextBox.Text;
            if (!Directory.Exists(path))
            {
                EDM.Services.ErrorDialogService.ShowError("[SettingsWindow.Save_Click]", new System.Exception("Selected folder does not exist"), "Selected folder does not exist.", "Error");
                return;
            }

            try
            {
                var ff = this.FindName("FfmpegPathTextBox") as System.Windows.Controls.TextBox;
                if (ff != null && !string.IsNullOrWhiteSpace(ff.Text) && !File.Exists(ff.Text))
                {
                    EDM.Services.ErrorDialogService.ShowWarning("[SettingsWindow.Save_Click]", $"The selected FFmpeg path does not exist: {ff.Text}", "Warning");
                    return;
                }

                var y = this.FindName("YtDlpPathTextBox") as System.Windows.Controls.TextBox;
                if (y != null && !string.IsNullOrWhiteSpace(y.Text) && !File.Exists(y.Text))
                {
                    EDM.Services.ErrorDialogService.ShowWarning("[SettingsWindow.Save_Click]", $"The selected yt-dlp path does not exist: {y.Text}", "Warning");
                    return;
                }

                var a = this.FindName("Aria2PathTextBox") as System.Windows.Controls.TextBox;
                if (a != null && !string.IsNullOrWhiteSpace(a.Text) && !File.Exists(a.Text))
                {
                    EDM.Services.ErrorDialogService.ShowWarning("[SettingsWindow.Save_Click]", $"The selected aria2c path does not exist: {a.Text}", "Warning");
                    return;
                }
            }
            catch (Exception ex)
            {
                EDM.Services.ErrorDialogService.ShowError("[SettingsWindow.Save_Click]", ex, $"Validation failed: {ex.Message}", "Error");
                return;
            }

            _settingsService.SetDefaultDownloadPath(path);
            try
            {
                var ff = this.FindName("FfmpegPathTextBox") as System.Windows.Controls.TextBox;
                if (ff != null) _settingsService.SetFfmpegPath(ff.Text ?? string.Empty);
            }
            catch (Exception ex) { LoggingService.LogException("[SettingsWindow] Failed to set FfmpegPath", ex); }
            try
            {
                var y = this.FindName("YtDlpPathTextBox") as System.Windows.Controls.TextBox;
                if (y != null) _settingsService.SetYtDlpPath(y.Text ?? string.Empty);
            }
            catch (Exception ex) { LoggingService.LogException("[SettingsWindow] Failed to set YtDlpPath", ex); }
            try
            {
                var a = this.FindName("Aria2PathTextBox") as System.Windows.Controls.TextBox;
                if (a != null) _settingsService.SetAria2Path(a.Text ?? string.Empty);
            }
            catch (Exception ex) { LoggingService.LogException("[SettingsWindow] Failed to set Aria2Path", ex); }
            try
            {
                var fa = this.FindName("FormatArgsTextBox") as System.Windows.Controls.TextBox;
                if (fa != null) _settingsService.SetDefaultFormatArgs(fa.Text ?? string.Empty);
            }
            catch (Exception ex) { LoggingService.LogException("[SettingsWindow] Failed to set FormatArgs", ex); }
            try
            {
                var cb = this.FindName("AutoConvertCheckBox") as System.Windows.Controls.CheckBox;
                if (cb != null) _settingsService.SetAutoConvertToMp3(cb.IsChecked == true);
            }
            catch (Exception ex) { LoggingService.LogException("[SettingsWindow] Failed to set AutoConvert", ex); }

            // Save URL Safety Check settings
            try
            {
                var enableCheckBox = this.FindName("EnableSafetyCheckBox") as System.Windows.Controls.CheckBox;
                if (enableCheckBox != null)
                {
                    _settingsService.SetEnableUrlSafetyCheck(enableCheckBox.IsChecked == true);
                }

                var apiKeyBox = this.FindName("ApiKeyPasswordBox") as System.Windows.Controls.PasswordBox;
                if (apiKeyBox != null)
                {
                    _settingsService.SetGoogleSafeBrowsingApiKey(apiKeyBox.Password ?? string.Empty);
                }

                // Crash reporting preference
                var crashCb = this.FindName("SendCrashReportsCheckBox") as System.Windows.Controls.CheckBox;
                if (crashCb != null)
                {
                    _settingsService.SetSendAnonymousCrashReports(crashCb.IsChecked == true);
                }
            }
            catch (Exception ex) { LoggingService.LogException("[SettingsWindow] Failed to save URL Safety settings", ex); }

            // Save Clipboard Monitoring settings
            try
            {
                var enableCheckBox = this.FindName("EnableClipboardMonitoringCheckBox") as System.Windows.Controls.CheckBox;
                var httpBox = this.FindName("ClipboardHttpCheckBox") as System.Windows.Controls.CheckBox;
                var httpsBox = this.FindName("ClipboardHttpsCheckBox") as System.Windows.Controls.CheckBox;
                var ftpBox = this.FindName("ClipboardFtpCheckBox") as System.Windows.Controls.CheckBox;
                var actionCombo = this.FindName("ClipboardActionComboBox") as System.Windows.Controls.ComboBox;
                var dedupBox = this.FindName("ClipboardIgnoreDuplicatesCheckBox") as System.Windows.Controls.CheckBox;
                var notifyBox = this.FindName("ClipboardShowNotificationCheckBox") as System.Windows.Controls.CheckBox;

                if (enableCheckBox != null)
                {
                    _settingsService.SetEnableClipboardMonitoring(enableCheckBox.IsChecked == true);
                }
                if (httpBox != null)
                {
                    _settingsService.SetClipboardMonitorHttp(httpBox.IsChecked == true);
                }
                if (httpsBox != null)
                {
                    _settingsService.SetClipboardMonitorHttps(httpsBox.IsChecked == true);
                }
                if (ftpBox != null)
                {
                    _settingsService.SetClipboardMonitorFtp(ftpBox.IsChecked == true);
                }
                if (actionCombo != null)
                {
                    var action = actionCombo.SelectedIndex switch
                    {
                        0 => ClipboardAction.AskBeforeDownload,
                        1 => ClipboardAction.AutoDownload,
                        2 => ClipboardAction.Ignore,
                        _ => ClipboardAction.AskBeforeDownload
                    };
                    _settingsService.SetClipboardAction(action);
                }
                if (dedupBox != null)
                {
                    _settingsService.SetClipboardIgnoreDuplicates(dedupBox.IsChecked == true);
                }
                if (notifyBox != null)
                {
                    _settingsService.SetClipboardShowNotification(notifyBox.IsChecked == true);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SettingsWindow] Failed to save clipboard settings", ex);
            }

            // Save Browser Integration settings
            try
            {
                var enableBrowserBox = this.FindName("EnableBrowserIntegrationCheckBox") as System.Windows.Controls.CheckBox;
                var captureBox = this.FindName("BrowserCaptureDownloadsCheckBox") as System.Windows.Controls.CheckBox;
                var confirmBox = this.FindName("BrowserShowConfirmationCheckBox") as System.Windows.Controls.CheckBox;
                var notifyBrowserBox = this.FindName("BrowserShowNotificationCheckBox") as System.Windows.Controls.CheckBox;

                if (enableBrowserBox != null)
                {
                    _settingsService.SetEnableBrowserIntegration(enableBrowserBox.IsChecked == true);
                }
                if (captureBox != null)
                {
                    _settingsService.SetBrowserCaptureDownloads(captureBox.IsChecked == true);
                }
                if (confirmBox != null)
                {
                    bool showConfirm = confirmBox.IsChecked == true;
                    _settingsService.SetBrowserShowConfirmation(showConfirm);
                    _settingsService.SetBrowserDownloadMode(showConfirm ? "ShowDialog" : "StartImmediately");
                }
                if (notifyBrowserBox != null)
                {
                    _settingsService.SetBrowserShowNotification(notifyBrowserBox.IsChecked == true);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SettingsWindow] Failed to save browser integration settings", ex);
            }

            try
            {
                var proxySettings = BuildProxySettingsFromUi();
                if (proxySettings.Enabled && string.IsNullOrWhiteSpace(proxySettings.Host))
                {
                    EDM.Services.ErrorDialogService.ShowWarning("[SettingsWindow.Save_Click]", "প্রক্সি চালু করা হয়েছে কিন্তু হোস্ট খালি — প্রক্সি বন্ধ রেখে সেভ করা হচ্ছে।", "Proxy");
                    proxySettings.Enabled = false;
                }

                var typedPassword = ProxyPasswordBox.Password;
                _settingsService.SetProxySettings(proxySettings, string.IsNullOrEmpty(typedPassword) ? null : typedPassword);

                // Apply immediately: rebuild the shared HttpClient and re-point any live DownloadService at it,
                // so the new proxy takes effect without restarting EDM.
                SharedHttpClient.ApplyProxySettings(_settingsService.GetProxySettings());
                (App.ServiceProvider?.GetService(typeof(EDM.Services.DownloadService)) as EDM.Services.DownloadService)?.RefreshHttpClient();

                // Apply bandwidth throttle immediately according to current schedules
                try
                {
                    var schedules = _settingsService.GetBandwidthSchedules();
                    int currentHour = DateTime.Now.Hour;
                    int? activeLimit = null;
                    if (schedules != null)
                    {
                        foreach (var schedule in schedules)
                        {
                            if (schedule.TimeRange?.IsInRange(currentHour) == true)
                            {
                                activeLimit = schedule.SpeedLimitKbps;
                                break;
                            }
                        }
                    }

                    if (activeLimit.HasValue)
                    {
                        SharedHttpClient.SetBandwidthThrottle(activeLimit.Value);
                    }
                    else
                    {
                        SharedHttpClient.SetBandwidthThrottle(0);
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[SettingsWindow.Save_Click] Apply bandwidth schedule failed", ex);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[SettingsWindow.Save_Click] Failed to save/apply proxy settings: {ex.Message}");
            }

            this.DialogResult = true;
            this.Close();
        }

        private async void TestYtDlp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var y = this.FindName("YtDlpPathTextBox") as System.Windows.Controls.TextBox;
                if (y == null || string.IsNullOrWhiteSpace(y.Text)) { EDM.Services.ErrorDialogService.ShowWarning("[SettingsWindow.TestYtDlp_Click]", "Please provide yt-dlp path.", "Test yt-dlp"); return; }
                var ok = false;
                try { ok = await _externalBackend.ValidateExecutableAsync(y.Text).ConfigureAwait(false); }
                catch (Exception ex)
                {
                    EDM.Services.LoggingService.LogException("[SettingsWindow.TestYtDlp_Click] Validation failed", ex);
                    _ = Dispatcher.BeginInvoke(() => EDM.Services.ErrorDialogService.ShowError("[SettingsWindow.TestYtDlp_Click]", ex, ex.Message, "Error"));
                    return;
                }
                _ = Dispatcher.BeginInvoke(() => EDM.Services.ErrorDialogService.ShowInfo("[SettingsWindow.TestYtDlp_Click]", ok ? "yt-dlp found and valid." : "yt-dlp not found or not runnable.", "Test yt-dlp"));
            }
            catch (Exception ex)
            {
                EDM.Services.LoggingService.LogException("[SettingsWindow.TestYtDlp_Click] Unexpected error", ex);
                _ = Dispatcher.BeginInvoke(() => EDM.Services.ErrorDialogService.ShowError("[SettingsWindow.TestYtDlp_Click]", ex, ex.Message, "Error"));
            }
        }

        private async void TestAria2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var a = this.FindName("Aria2PathTextBox") as System.Windows.Controls.TextBox;
                if (a == null || string.IsNullOrWhiteSpace(a.Text)) { EDM.Services.ErrorDialogService.ShowWarning("[SettingsWindow.TestAria2_Click]", "Please provide aria2c path.", "Test aria2c"); return; }
                var ok = false;
                try { ok = await _externalBackend.ValidateExecutableAsync(a.Text).ConfigureAwait(false); }
                catch (Exception ex)
                {
                    EDM.Services.LoggingService.LogException("[SettingsWindow.TestAria2_Click] Validation failed", ex);
                    _ = Dispatcher.BeginInvoke(() => EDM.Services.ErrorDialogService.ShowError("[SettingsWindow.TestAria2_Click]", ex, ex.Message, "Error"));
                    return;
                }
                _ = Dispatcher.BeginInvoke(() => EDM.Services.ErrorDialogService.ShowInfo("[SettingsWindow.TestAria2_Click]", ok ? "aria2c found and valid." : "aria2c not found or not runnable.", "Test aria2c"));
            }
            catch (Exception ex)
            {
                EDM.Services.LoggingService.LogException("[SettingsWindow.TestAria2_Click] Unexpected error", ex);
                _ = Dispatcher.BeginInvoke(() => EDM.Services.ErrorDialogService.ShowError("[SettingsWindow.TestAria2_Click]", ex, ex.Message, "Error"));
            }
        }

        private void OpenCrashReportsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EDM", "crash_reports");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                try
                {
                    Process.Start("explorer.exe", folder);
                }
                catch
                {
                    var psi = new ProcessStartInfo { FileName = folder, UseShellExecute = true };
                    Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SettingsWindow.OpenCrashReportsFolder_Click]", ex);
                ErrorDialogService.ShowError("[SettingsWindow.OpenCrashReportsFolder_Click]", ex, "Failed to open crash reports folder", "Error");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ToggleContextMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show confirmation dialog
                var dlg = new ContextMenuRegistrationWindow(isRegister: !EDM.Services.ContextMenuService.IsContextMenuActive());
                if (dlg.ShowDialog() == true)
                {
                    UpdateContextMenuStatus();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SettingsWindow.ToggleContextMenu_Click]", ex);
                ErrorDialogService.ShowError("[SettingsWindow.ToggleContextMenu_Click]", ex, ex.Message, "Error");
            }
        }

        private void UpdateContextMenuStatus()
        {
            try
            {
                var statusText = this.FindName("ContextMenuStatusText") as System.Windows.Controls.TextBlock;
                var toggleButton = this.FindName("ToggleContextMenuButton") as System.Windows.Controls.Button;

                if (statusText != null && toggleButton != null)
                {
                    if (EDM.Services.ContextMenuService.IsContextMenuActive())
                    {
                        statusText.Text = "✓ Context menu is registered";
                        statusText.Foreground = System.Windows.Media.Brushes.Green;
                        toggleButton.Content = "Disable";
                    }
                    else
                    {
                        statusText.Text = "✗ Context menu is not registered";
                        statusText.Foreground = System.Windows.Media.Brushes.Red;
                        toggleButton.Content = "Enable";
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[SettingsWindow.UpdateContextMenuStatus] Failed: {ex.Message}");
            }
        }
        private void OpenSiteLoginsManager_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new SiteLoginsManagerWindow();
                win.Owner = this;
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SettingsWindow.OpenSiteLoginsManager_Click]", ex);
            }
        }

        private void OpenCategoryRulesEditor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new CategoryRulesEditorWindow();
                win.Owner = this;
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SettingsWindow.OpenCategoryRulesEditor_Click]", ex);
            }
        }

        private void LoadResourceOptimizerSettings()
        {
            try
            {
                var opt = SystemResourceOptimizerService.Instance;
                if (SystemHardwareInfoText != null)
                {
                    SystemHardwareInfoText.Text = opt.GetSystemMemoryStatus();
                }
                if (CurrentEdmRamText != null)
                {
                    CurrentEdmRamText.Text = opt.GetProcessMemoryUsageFormatted();
                }
                if (ResourceOptimizerModeCombo != null)
                {
                    ResourceOptimizerModeCombo.SelectedIndex = opt.CurrentMode switch
                    {
                        ResourceOptimizationMode.EcoLowMemory => 0,
                        ResourceOptimizationMode.BalancedSmart => 1,
                        ResourceOptimizationMode.UltraTurbo => 2,
                        _ => 1
                    };
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SettingsWindow.LoadResourceOptimizerSettings]", ex);
            }
        }

        private void FreeRamNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var opt = SystemResourceOptimizerService.Instance;
                long freed = opt.OptimizeMemoryNow();
                if (CurrentEdmRamText != null)
                {
                    CurrentEdmRamText.Text = opt.GetProcessMemoryUsageFormatted();
                }
                System.Windows.MessageBox.Show($"RAM Compaction Complete!\n\nCurrent EDM Memory: {opt.GetProcessMemoryUsageFormatted()}", 
                    "RAM Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SettingsWindow.FreeRamNow_Click]", ex);
            }
        }

        private void ResourceOptimizerModeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (ResourceOptimizerModeCombo == null) return;
                var mode = ResourceOptimizerModeCombo.SelectedIndex switch
                {
                    0 => ResourceOptimizationMode.EcoLowMemory,
                    1 => ResourceOptimizationMode.BalancedSmart,
                    2 => ResourceOptimizationMode.UltraTurbo,
                    _ => ResourceOptimizationMode.BalancedSmart
                };
                SystemResourceOptimizerService.Instance.SaveMode(mode);
                if (CurrentEdmRamText != null)
                {
                    CurrentEdmRamText.Text = SystemResourceOptimizerService.Instance.GetProcessMemoryUsageFormatted();
                }
            }
            catch { }
        }
    }
}
