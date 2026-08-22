using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EDM.Services;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfClipboard = System.Windows.Clipboard;
using WpfMessageBox = System.Windows.MessageBox;

namespace EDM.Views
{
    public partial class AboutWindow : Window
    {
        private readonly VersionHistoryService _versionService;
        private readonly SystemEnvironmentInfo _sysInfo;

        public AboutWindow()
        {
            InitializeComponent();
            _versionService = VersionHistoryService.Instance;
            _sysInfo = _versionService.GetSystemInfo();

            PopulateMetadata();
            PopulateWhatsNew();
            PopulateVersionHistory();
        }

        private void PopulateMetadata()
        {
            VersionBadgeText.Text = $"v{_sysInfo.ApplicationVersion}";
            ChannelBadgeText.Text = _sysInfo.ReleaseChannel;
            CurrentVersionLabel.Text = $"Version: {_sysInfo.ApplicationVersion}";
            BuildNumberLabel.Text = $"Build: {_sysInfo.BuildNumber}";
            ArchitectureLabel.Text = $"Architecture: {_sysInfo.Architecture} (Windows Desktop / WPF)";
            FrameworkLabel.Text = $"Runtime: {_sysInfo.FrameworkRuntime}";
            MachineNameLabel.Text = $"Device: {_sysInfo.MachineName}";
            CopyrightText.Text = _sysInfo.Copyright;

            SysOsText.Text = _sysInfo.OperatingSystem;
            SysRuntimeText.Text = _sysInfo.FrameworkRuntime;
            SysMemoryText.Text = _sysInfo.ProcessMemory;
            SysCoresText.Text = $"{_sysInfo.ProcessorCount} Logical Processors";
            SysPathText.Text = _sysInfo.InstallationPath;
            SysDbText.Text = _sysInfo.DatabasePath;
        }

        private void PopulateWhatsNew()
        {
            WhatsNewPanel.Children.Clear();
            var releases = _versionService.GetVersionHistory();
            var latest = releases.Count > 0 ? releases[0] : null;
            if (latest == null) return;

            var card = new Border
            {
                Background = (WpfBrush)FindResource("CardInputBg"),
                BorderBrush = (WpfBrush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stack = new StackPanel();

            var title = new TextBlock
            {
                Text = $"{latest.Version} Highlights — {latest.Tagline}",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = (WpfBrush)FindResource("PrimaryTextBrush"),
                Margin = new Thickness(0, 0, 0, 8)
            };
            stack.Children.Add(title);

            AddFeatureCategory(stack, "🚀 Major Features", latest.NewFeatures, "#8B4DFF");
            AddFeatureCategory(stack, "⚡ Performance & Improvements", latest.Improvements, "#10B981");
            AddFeatureCategory(stack, "🛡️ Bug Fixes & Refactorings", latest.BugFixes, "#3B82F6");
            AddFeatureCategory(stack, "🔒 Security Hardening", latest.SecurityUpdates, "#F59E0B");

            card.Child = stack;
            WhatsNewPanel.Children.Add(card);
        }

        private void PopulateVersionHistory()
        {
            VersionHistoryPanel.Children.Clear();
            var releases = _versionService.GetVersionHistory();

            foreach (var rel in releases)
            {
                var card = new Border
                {
                    Background = (WpfBrush)FindResource("CardInputBg"),
                    BorderBrush = (WpfBrush)FindResource("BorderBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(16),
                    Margin = new Thickness(0, 0, 0, 12)
                };

                var stack = new StackPanel();

                var headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var verTitle = new TextBlock
                {
                    Text = $"{rel.Version} — {rel.Tagline}",
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    Foreground = (WpfBrush)FindResource("PrimaryTextBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(verTitle, 0);
                headerGrid.Children.Add(verTitle);

                var dateText = new TextBlock
                {
                    Text = rel.ReleaseDate,
                    FontSize = 11,
                    Foreground = (WpfBrush)FindResource("SecondaryTextBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(dateText, 1);
                headerGrid.Children.Add(dateText);

                stack.Children.Add(headerGrid);

                AddFeatureCategory(stack, "Features", rel.NewFeatures, "#8B4DFF");
                AddFeatureCategory(stack, "Improvements", rel.Improvements, "#10B981");
                AddFeatureCategory(stack, "Bug Fixes", rel.BugFixes, "#3B82F6");
                AddFeatureCategory(stack, "Security", rel.SecurityUpdates, "#F59E0B");

                card.Child = stack;
                VersionHistoryPanel.Children.Add(card);
            }
        }

        private void AddFeatureCategory(StackPanel parent, string title, List<string> items, string colorHex)
        {
            if (items == null || items.Count == 0) return;

            var catHeader = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = new WpfSolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(colorHex)),
                Margin = new Thickness(0, 8, 0, 4)
            };
            parent.Children.Add(catHeader);

            foreach (var item in items)
            {
                var row = new TextBlock
                {
                    Text = $"• {item}",
                    FontSize = 12,
                    Foreground = (WpfBrush)FindResource("SecondaryTextBrush"),
                    Margin = new Thickness(8, 2, 0, 2),
                    TextWrapping = TextWrapping.Wrap
                };
                parent.Children.Add(row);
            }
        }

        private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CheckUpdateBtn.IsEnabled = false;
                UpdateProgressBar.Visibility = Visibility.Visible;
                UpdateStatusText.Text = "Connecting to update server...";

                var settings = new SettingsService();
                var updateSvc = new UpdateService(settings);

                var info = await updateSvc.CheckControlPlaneUpdateAsync(_sysInfo.ApplicationVersion).ConfigureAwait(true);

                if (info != null && info.IsUpdateAvailable)
                {
                    UpdateStatusText.Text = $"New version available: {info.Version}! ({info.Title})";
                    UpdateStatusText.Foreground = new WpfSolidColorBrush(WpfColor.FromRgb(16, 185, 129));
                }
                else
                {
                    UpdateStatusText.Text = $"You are running the latest version (v{_sysInfo.ApplicationVersion}). No updates needed.";
                    UpdateStatusText.Foreground = (WpfBrush)FindResource("SecondaryTextBrush");
                }
            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = "Update service unavailable: System is operating in offline / standalone mode.";
                LoggingService.Log($"[AboutWindow.CheckUpdates] Notice: {ex.Message}");
            }
            finally
            {
                CheckUpdateBtn.IsEnabled = true;
                UpdateProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void CopyDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== Exclusive Download Manager (EDM) System Diagnostics ===");
                sb.AppendLine($"Application Version: {_sysInfo.ApplicationVersion}");
                sb.AppendLine($"Build: {_sysInfo.BuildNumber}");
                sb.AppendLine($"Release Channel: {_sysInfo.ReleaseChannel}");
                sb.AppendLine($"Architecture: {_sysInfo.Architecture}");
                sb.AppendLine($"Runtime Framework: {_sysInfo.FrameworkRuntime}");
                sb.AppendLine($"Operating System: {_sysInfo.OperatingSystem}");
                sb.AppendLine($"Process Memory: {_sysInfo.ProcessMemory}");
                sb.AppendLine($"Processor Cores: {_sysInfo.ProcessorCount}");
                sb.AppendLine($"Device Name: {_sysInfo.MachineName}");
                sb.AppendLine($"Database Location: {_sysInfo.DatabasePath}");
                sb.AppendLine($"Generated: {DateTime.UtcNow:O}");

                WpfClipboard.SetText(sb.ToString());
                WpfMessageBox.Show("System diagnostics successfully copied to clipboard.", "EDM Diagnostics", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Failed to copy diagnostics: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
