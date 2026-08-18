using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EDM.Models;
using EDM.Services;
using EDM.Services.Cloud;
using WpfButton = System.Windows.Controls.Button;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfMessageBox = System.Windows.MessageBox;

namespace EDM.Views
{
    public partial class UserProfileWindow : Window
    {
        private readonly CloudSyncService _cloudService;
        private readonly WebhookNotificationService _webhookService;

        public UserProfileWindow()
        {
            InitializeComponent();
            _cloudService = CloudSyncService.Instance;
            _webhookService = WebhookNotificationService.Instance;

            _cloudService.StateChanged += OnCloudStateChanged;

            LoadProfileData();
            RenderSnapshots();
            RenderLinkedDevices();
            LoadWebhookConfig();
        }

        private void OnCloudStateChanged()
        {
            Dispatcher.Invoke(() =>
            {
                LoadProfileData();
                RenderSnapshots();
                RenderLinkedDevices();
            });
        }

        private void LoadProfileData()
        {
            string userName = _cloudService.Account.IsAuthenticated ? _cloudService.Account.DisplayName : Environment.UserName;
            UserNameText.Text = userName;
            UserEmailText.Text = _cloudService.Account.Email;

            string initials = !string.IsNullOrEmpty(userName)
                ? (userName.Length >= 2 ? userName.Substring(0, 2).ToUpper() : userName.ToUpper())
                : "U";
            AvatarInitialsText.Text = initials;

            TierBadgeText.Text = _cloudService.Account.IsAuthenticated ? "PRO CLOUD VAULT" : "GUEST (LOCAL)";
            TierBadge.Background = _cloudService.Account.IsAuthenticated ? (WpfBrush)FindResource("SuccessBrush") : (WpfBrush)FindResource("SecondaryTextBrush");

            AuthActionBtn.Content = _cloudService.Account.IsAuthenticated ? "🚪 Sign Out" : "⚡ Quick Connect / Passkey";
            AuthActionBtn.Background = _cloudService.Account.IsAuthenticated ? (WpfBrush)FindResource("CardInputBg") : new SolidColorBrush(WpfColor.FromRgb(139, 77, 255));

            MachineNameText.Text = Environment.MachineName;
            try
            {
                HwidText.Text = "EDM-" + Math.Abs((Environment.MachineName + Environment.UserName).GetHashCode()).ToString("X8");
            }
            catch
            {
                HwidText.Text = "EDM-HWID-LOCAL";
            }

            // Storage Quota
            long usedMb = _cloudService.Account.UsedStorageBytes / (1024 * 1024);
            long maxGb = _cloudService.Account.MaxStorageBytes / (1024L * 1024 * 1024);
            StorageQuotaText.Text = $"{usedMb} MB / {maxGb} GB Used";

            double pct = (double)_cloudService.Account.UsedStorageBytes / _cloudService.Account.MaxStorageBytes * 100.0;
            StorageProgressBar.Value = Math.Max(1, Math.Min(100, pct));

            LastSyncLabel.Text = _cloudService.Account.LastSyncTime.HasValue
                ? $"Last Sync: {_cloudService.Account.LastSyncTime.Value:MMM dd, HH:mm}"
                : "Last Sync: Never";
        }

        private void RenderSnapshots()
        {
            SnapshotsPanel.Children.Clear();
            var snapshots = _cloudService.Snapshots;

            if (!snapshots.Any())
            {
                var empty = new TextBlock
                {
                    Text = "No cloud backup snapshots yet. Click 'Backup Now' to create your first encrypted snapshot.",
                    Foreground = (WpfBrush)FindResource("SecondaryTextBrush"),
                    FontSize = 11.5,
                    Margin = new Thickness(4)
                };
                SnapshotsPanel.Children.Add(empty);
                return;
            }

            foreach (var snap in snapshots)
            {
                var itemBorder = new Border
                {
                    Background = (WpfBrush)FindResource("BackgroundBrush"),
                    BorderBrush = (WpfBrush)FindResource("BorderBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var leftStack = new StackPanel();
                leftStack.Children.Add(new TextBlock
                {
                    Text = snap.Title,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    Foreground = (WpfBrush)FindResource("PrimaryTextBrush")
                });
                leftStack.Children.Add(new TextBlock
                {
                    Text = $"{snap.CreatedAt:MMM dd, yyyy HH:mm} • {snap.TotalDownloadsCount} tasks • {snap.SnapshotSizeBytes / 1024} KB • {snap.DeviceOrigin}",
                    FontSize = 10.5,
                    Foreground = (WpfBrush)FindResource("SecondaryTextBrush"),
                    Margin = new Thickness(0, 2, 0, 0)
                });
                Grid.SetColumn(leftStack, 0);
                grid.Children.Add(leftStack);

                var restoreBtn = new WpfButton
                {
                    Content = "📥 Restore",
                    Padding = new Thickness(8, 4, 8, 4),
                    Background = (WpfBrush)FindResource("CardInputBg"),
                    Foreground = (WpfBrush)FindResource("AccentBrush"),
                    BorderBrush = (WpfBrush)FindResource("BorderBrush"),
                    BorderThickness = new Thickness(1),
                    FontSize = 11,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                restoreBtn.Resources.Add(typeof(Border), new Style(typeof(Border))
                {
                    Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(4)) }
                });

                string snapId = snap.SnapshotId;
                restoreBtn.Click += async (s, e) =>
                {
                    try
                    {
                        var restored = await _cloudService.RestoreFromSnapshotAsync(snapId);
                        WpfMessageBox.Show($"Successfully decrypted and restored {restored.Count} download entries from cloud vault snapshot.", "Cloud Restore", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        WpfMessageBox.Show($"Failed to restore snapshot: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                Grid.SetColumn(restoreBtn, 1);
                grid.Children.Add(restoreBtn);

                itemBorder.Child = grid;
                SnapshotsPanel.Children.Add(itemBorder);
            }
        }

        private void RenderLinkedDevices()
        {
            LinkedDevicesPanel.Children.Clear();
            var devices = _cloudService.Account.LinkedDevices;

            foreach (var dev in devices)
            {
                var card = new Border
                {
                    Background = (WpfBrush)FindResource("CardInputBg"),
                    BorderBrush = (WpfBrush)FindResource("BorderBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 10, 12, 10),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var stack = new StackPanel();
                var titlePanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                titlePanel.Children.Add(new TextBlock
                {
                    Text = (dev.IsCurrentDevice ? "💻 " : "📱 ") + dev.DeviceName,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Foreground = (WpfBrush)FindResource("PrimaryTextBrush")
                });

                if (dev.IsCurrentDevice)
                {
                    var thisPcBadge = new Border
                    {
                        Background = (WpfBrush)FindResource("AccentBrush"),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(4, 1, 4, 1),
                        Margin = new Thickness(6, 0, 0, 0)
                    };
                    thisPcBadge.Child = new TextBlock { Text = "THIS PC", FontSize = 9, Foreground = WpfBrushes.White, FontWeight = FontWeights.Bold };
                    titlePanel.Children.Add(thisPcBadge);
                }

                stack.Children.Add(titlePanel);
                stack.Children.Add(new TextBlock
                {
                    Text = $"{dev.Platform} • ID: {dev.DeviceId} • Last Active: {dev.LastActive:MMM dd, HH:mm}",
                    FontSize = 10.5,
                    Foreground = (WpfBrush)FindResource("SecondaryTextBrush"),
                    Margin = new Thickness(0, 2, 0, 0)
                });

                Grid.SetColumn(stack, 0);
                grid.Children.Add(stack);

                if (!dev.IsCurrentDevice)
                {
                    var unlinkBtn = new WpfButton
                    {
                        Content = "Unlink",
                        Padding = new Thickness(8, 4, 8, 4),
                        Background = (WpfBrush)FindResource("CardInputBg"),
                        Foreground = (WpfBrush)FindResource("ErrorBrush"),
                        BorderBrush = (WpfBrush)FindResource("BorderBrush"),
                        BorderThickness = new Thickness(1),
                        FontSize = 11,
                        Cursor = System.Windows.Input.Cursors.Hand
                    };
                    unlinkBtn.Resources.Add(typeof(Border), new Style(typeof(Border))
                    {
                        Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(4)) }
                    });

                    string devId = dev.DeviceId;
                    unlinkBtn.Click += (s, e) => _cloudService.UnlinkDevice(devId);

                    Grid.SetColumn(unlinkBtn, 1);
                    grid.Children.Add(unlinkBtn);
                }

                card.Child = grid;
                LinkedDevicesPanel.Children.Add(card);
            }
        }

        private void LoadWebhookConfig()
        {
            EnableWebhookCheckBox.IsChecked = _webhookService.Config.IsEnabled;
            if (!string.IsNullOrEmpty(_webhookService.Config.WebhookUrl))
                WebhookUrlTextBox.Text = _webhookService.Config.WebhookUrl;
        }

        private async void AuthActionBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_cloudService.Account.IsAuthenticated)
            {
                await _cloudService.SignOutAsync();
                WpfMessageBox.Show("Signed out of EDM Cloud Vault. Returned to local Guest Mode.", "EDM Account", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                await _cloudService.SignInWithPasskeyOrMagicLinkAsync(string.Empty);
                WpfMessageBox.Show("Authenticated successfully via Windows Passkey! Cloud Vault sync enabled.", "EDM Cloud Vault", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void BackupNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var snap = await _cloudService.CreateBackupSnapshotAsync(null, $"Manual Cloud Backup ({DateTime.Now:HH:mm:ss})");
                WpfMessageBox.Show($"Encrypted snapshot created successfully!\nSnapshot ID: {snap.SnapshotId}\nEncrypted Size: {snap.SnapshotSizeBytes / 1024} KB", "Backup Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Failed to create backup: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TestWebhook_Click(object sender, RoutedEventArgs e)
        {
            var dummyItem = new DownloadItem
            {
                FileName = "Ubuntu-24.04-LTS-Desktop.iso",
                Size = "4.6 GB",
                Category = "Compressed"
            };

            bool ok = await _webhookService.SendDownloadNotificationAsync(dummyItem, true);
            if (ok)
            {
                WpfMessageBox.Show("Webhook alert dispatched successfully!", "Webhook Test", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                WpfMessageBox.Show("Webhook request was not delivered. Please verify the target URL.", "Webhook Alert", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveWebhook_Click(object sender, RoutedEventArgs e)
        {
            _webhookService.Config.IsEnabled = EnableWebhookCheckBox.IsChecked == true;
            _webhookService.Config.WebhookUrl = WebhookUrlTextBox.Text.Trim();
            WpfMessageBox.Show("Webhook configuration saved successfully.", "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AboutBtn_Click(object sender, RoutedEventArgs e)
        {
            var win = new AboutWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void SupportBtn_Click(object sender, RoutedEventArgs e)
        {
            var win = new SupportCenterWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void PrivacyBtn_Click(object sender, RoutedEventArgs e)
        {
            var win = new PrivacyPolicyWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _cloudService.StateChanged -= OnCloudStateChanged;
            base.OnClosed(e);
        }
    }
}
