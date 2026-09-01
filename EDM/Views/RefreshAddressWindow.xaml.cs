using System;
using System.Diagnostics;
using System.Windows;
using EDM.Helpers;
using EDM.Models;
using EDM.Services;
using EDM.ViewModels;
using WpfClipboard = System.Windows.Clipboard;
using WpfColor = System.Windows.Media.Color;
using WpfMessageBox = System.Windows.MessageBox;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace EDM.Views
{
    public partial class RefreshAddressWindow : Window
    {
        private readonly DownloadItem _downloadItem;
        private readonly DownloadManagerViewModel? _viewModel;

        public RefreshAddressWindow(DownloadItem downloadItem, DownloadManagerViewModel? viewModel = null)
        {
            InitializeComponent();
            _downloadItem = downloadItem ?? throw new ArgumentNullException(nameof(downloadItem));
            _viewModel = viewModel;

            FileNameTextBlock.Text = _downloadItem.FileName ?? "Unknown File";
            OldUrlTextBox.Text = _downloadItem.Url ?? string.Empty;

            long total = SizeFormatter.ParseToBytes(_downloadItem.Size);
            long downloaded = _downloadItem.DownloadedBytes;
            double pct = total > 0 ? (downloaded * 100.0 / total) : _downloadItem.Progress;

            ProgressTextBlock.Text = total > 0
                ? $"{SizeFormatter.FormatBytes(downloaded, "0 B")} / {SizeFormatter.FormatBytes(total, "0 B")} ({pct:F1}%)"
                : $"{_downloadItem.Progress:F1}%";

            RefreshResumeBtn.IsEnabled = false;
        }

        private void NewUrlTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string url = NewUrlTextBox.Text.Trim();
            RefreshResumeBtn.IsEnabled = Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                                          (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            ValidationStatusText.Text = string.Empty;
        }

        private void PasteClipboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WpfClipboard.ContainsText())
                {
                    NewUrlTextBox.Text = WpfClipboard.GetText().Trim();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[RefreshAddressWindow] Paste clipboard failed", ex);
            }
        }

        private void OpenInBrowser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string urlToOpen = !string.IsNullOrWhiteSpace(_downloadItem.Referer)
                    ? _downloadItem.Referer
                    : (!string.IsNullOrWhiteSpace(_downloadItem.PageUrl) ? _downloadItem.PageUrl : _downloadItem.Url);

                if (!string.IsNullOrWhiteSpace(urlToOpen))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = urlToOpen,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Could not open page in browser: {ex.Message}", "Browser Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void RefreshResumeBtn_Click(object sender, RoutedEventArgs e)
        {
            string newUrl = NewUrlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(newUrl)) return;

            try
            {
                RefreshResumeBtn.IsEnabled = false;
                ValidationStatusText.Foreground = new WpfSolidColorBrush(WpfColor.FromRgb(0x38, 0xBD, 0xF8));
                ValidationStatusText.Text = "⏳ Validating replacement URL and byte compatibility...";

                long expectedLength = SizeFormatter.ParseToBytes(_downloadItem.Size);
                var validationResult = await UrlRefreshOrchestrator.Instance.ValidateAndSwapUrlAsync(
                    newUrl,
                    expectedLength,
                    null
                ).ConfigureAwait(true);

                if (!validationResult.Success)
                {
                    ValidationStatusText.Foreground = new WpfSolidColorBrush(WpfColor.FromRgb(0xEF, 0x44, 0x44));
                    ValidationStatusText.Text = $"⚠️ {validationResult.Message}";
                    RefreshResumeBtn.IsEnabled = true;
                    return;
                }

                // URL is valid and byte-compatible! Update DownloadItem
                _downloadItem.Url = newUrl;
                if (validationResult.TotalContentLength > 0 && expectedLength <= 0)
                {
                    _downloadItem.Size = SizeFormatter.FormatBytes(validationResult.TotalContentLength, "0 B");
                }

                _downloadItem.Status = "Queued";
                _downloadItem.ErrorMessage = string.Empty;

                LoggingService.Log($"[RefreshAddressWindow] URL successfully refreshed for '{_downloadItem.FileName}' -> {newUrl}");

                // Auto-resume download seamlessly
                _downloadItem.Status = "Downloading";
                if (_viewModel != null)
                {
                    _ = _viewModel.StartDownloadProcessAsync(_downloadItem);
                }
                else
                {
                    var win = new DownloadProgressWindow(_downloadItem);
                    win.Show();
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                ValidationStatusText.Foreground = new WpfSolidColorBrush(WpfColor.FromRgb(0xEF, 0x44, 0x44));
                ValidationStatusText.Text = $"Failed to refresh download: {ex.Message}";
                RefreshResumeBtn.IsEnabled = true;
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
