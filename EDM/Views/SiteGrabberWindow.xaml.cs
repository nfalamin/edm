using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using EDM.Services;

namespace EDM.Views
{
    public class GrabbedAssetItem
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = "Media";
        public string Url { get; set; } = string.Empty;
    }

    public partial class SiteGrabberWindow : Window
    {
        public ObservableCollection<GrabbedAssetItem> GrabbedAssets { get; } = new();
        private CancellationTokenSource? _cts;

        public SiteGrabberWindow()
        {
            InitializeComponent();
            AssetsDataGrid.ItemsSource = GrabbedAssets;
        }

        private async void StartCrawlButton_Click(object sender, RoutedEventArgs e)
        {
            string url = UrlInputTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                System.Windows.MessageBox.Show("Please enter a valid website URL (e.g. https://example.com/gallery).", "Invalid URL", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StartCrawlButton.IsEnabled = false;
            StatusTextBlock.Text = "🔍 Crawling website assets... Please wait...";
            GrabbedAssets.Clear();

            _cts = new CancellationTokenSource();

            try
            {
                int depth = DepthComboBox.SelectedIndex + 1;
                var grabber = new SiteGrabberService();
                var result = await grabber.CrawlWebsiteAsync(url, depth, _cts.Token).ConfigureAwait(true);

                foreach (var asset in result)
                {
                    string filename = Path.GetFileName(new Uri(asset).AbsolutePath);
                    if (string.IsNullOrEmpty(filename)) filename = "asset_" + Guid.NewGuid().ToString("N").Substring(0, 8);

                    GrabbedAssets.Add(new GrabbedAssetItem
                    {
                        Name = filename,
                        Category = DownloadPathCategoryService.GetCategorySubfolderByFileName(filename) switch
                        {
                            "Audio" => "Audio 🎵",
                            "Video" => "Video 🎬",
                            "Documents" => "Document 📄",
                            _ => "File 📦"
                        },
                        Url = asset
                    });
                }

                StatusTextBlock.Text = $"Crawling finished! Discovered {GrabbedAssets.Count} media assets.";
                CountSummaryTextBlock.Text = $"{GrabbedAssets.Count} Assets Discovered";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Crawling failed or cancelled.";
                System.Windows.MessageBox.Show($"Error during website grabber crawl: {ex.Message}", "Crawl Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                StartCrawlButton.IsEnabled = true;
            }
        }

        private void DownloadAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (GrabbedAssets.Count == 0)
            {
                System.Windows.MessageBox.Show("No discovered assets to download.", "No Assets Found", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string baseDir = DownloadPathCategoryService.GetDefaultBasePath();
            foreach (var asset in GrabbedAssets)
            {
                string savePath = Path.Combine(baseDir, asset.Name);
                Services.History.DownloadHistoryRecorder.CreateEntry(asset.Url, savePath, -1);
            }

            System.Windows.MessageBox.Show($"Successfully queued {GrabbedAssets.Count} assets in EDM Download Manager!", "Assets Queued", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            Close();
        }
    }
}
