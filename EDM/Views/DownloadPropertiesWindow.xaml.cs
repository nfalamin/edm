using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using EDM.Helpers;
using EDM.Models;
using EDM.Services;

namespace EDM.Views
{
    /// <summary>
    /// DownloadPropertiesWindow.xaml.cs - Displays detailed, authoritative metadata for a download item.
    /// </summary>
    public partial class DownloadPropertiesWindow : Window
    {
        private readonly DownloadItem _item;

        public DownloadPropertiesWindow(DownloadItem item)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            InitializeComponent();
            PopulateProperties();
        }

        private void PopulateProperties()
        {
            TxtFileName.Text = _item.FileName ?? string.Empty;
            TxtUrl.Text = _item.Url ?? string.Empty;
            TxtSavePath.Text = _item.SavePath ?? string.Empty;
            TxtCategory.Text = _item.Category ?? "General";
            TxtStatus.Text = _item.Status ?? "Unknown";
            TxtSize.Text = _item.Size ?? "Unknown";

            long downloaded = _item.DownloadedBytes > 0 
                ? _item.DownloadedBytes 
                : (string.Equals(_item.Status, "Completed", StringComparison.OrdinalIgnoreCase) 
                    ? SizeFormatter.ParseToBytes(_item.Size) 
                    : 0);
            TxtDownloaded.Text = SizeFormatter.FormatBytes(downloaded, "0 B");

            TxtProgress.Text = $"{_item.Progress:F1}%";
            TxtSpeed.Text = !string.IsNullOrWhiteSpace(_item.TransferRate) ? _item.TransferRate : "0 B/s";

            TxtVerification.Text = _item.VerificationState.ToString();
            TxtChecksum.Text = !string.IsNullOrWhiteSpace(_item.ComputedVerificationHash) 
                ? _item.ComputedVerificationHash 
                : (!string.IsNullOrWhiteSpace(_item.TrustedVerificationHash) ? _item.TrustedVerificationHash : "--");
        }

        private void CopyUrl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_item.Url))
                {
                    System.Windows.Clipboard.SetText(_item.Url);
                    System.Windows.MessageBox.Show("Download URL copied to clipboard.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadPropertiesWindow] Clipboard copy failed", ex);
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_item.SavePath))
                {
                    string folder = Path.GetDirectoryName(_item.SavePath) ?? _item.SavePath;
                    if (Directory.Exists(folder))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = folder,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadPropertiesWindow] Open folder failed", ex);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
