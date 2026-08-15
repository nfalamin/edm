using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using EDM.Services;

namespace EDM.Views
{
    public partial class RefreshExpiredUrlDialog : Window
    {
        private readonly long _expectedSize;
        private readonly string? _expectedEtag;
        private readonly string? _pageReferrerUrl;

        public string? ValidatedNewUrl { get; private set; }

        public RefreshExpiredUrlDialog(
            string fileName,
            long expectedSize,
            string? expectedEtag,
            double progressPercent,
            string? pageReferrerUrl = null)
        {
            InitializeComponent();
            _expectedSize = expectedSize;
            _expectedEtag = expectedEtag;
            _pageReferrerUrl = pageReferrerUrl;

            FileNameText.Text = fileName;
            ProgressText.Text = $"{progressPercent:F1}% downloaded (Existing segments preserved)";
        }

        private void OnOpenInBrowser(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_pageReferrerUrl))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _pageReferrerUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Could not launch browser: {ex.Message}", "EDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                System.Windows.MessageBox.Show("No referrer page recorded for this download.", "EDM", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void OnValidateAndResume(object sender, RoutedEventArgs e)
        {
            string url = NewUrlBox.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                StatusMessageText.Text = "Please enter the new download URL.";
                return;
            }

            StatusMessageText.Foreground = System.Windows.Media.Brushes.Orange;
            StatusMessageText.Text = "Validating new URL with server...";

            var result = await UrlRefreshOrchestrator.Instance.ValidateAndSwapUrlAsync(url, _expectedSize, _expectedEtag);

            if (result.Success)
            {
                ValidatedNewUrl = url;
                DialogResult = true;
                Close();
            }
            else
            {
                StatusMessageText.Foreground = System.Windows.Media.Brushes.Red;
                StatusMessageText.Text = result.Message ?? "Validation failed.";
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
