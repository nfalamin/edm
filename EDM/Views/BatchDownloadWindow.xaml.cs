using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using EDM.Services;

namespace EDM.Views
{
    public class BatchLinkItem
    {
        public string Url { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = true;
    }

    public partial class BatchDownloadWindow : Window
    {
        public ObservableCollection<BatchLinkItem> GeneratedLinks { get; } = new();

        public BatchDownloadWindow()
        {
            InitializeComponent();
            LinksListView.ItemsSource = GeneratedLinks;
            SavePathTextBox.Text = DownloadPathCategoryService.GetDefaultBasePath();
        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            string pattern = PatternInputTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(pattern))
            {
                System.Windows.MessageBox.Show("Please enter a URL pattern with wildcards (e.g. http://example.com/file_[01-05].zip).", "Batch Pattern Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var links = UrlPatternExpander.Expand(pattern);
                GeneratedLinks.Clear();
                foreach (var link in links)
                {
                    GeneratedLinks.Add(new BatchLinkItem { Url = link, IsSelected = true });
                }

                StatusTextBlock.Text = $"Generated {links.Count} links successfully.";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to expand pattern: {ex.Message}", "Pattern Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SavePathTextBox.Text = dlg.SelectedPath;
            }
        }

        private void EnqueueButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = GeneratedLinks.Where(l => l.IsSelected).ToList();
            if (selected.Count == 0)
            {
                System.Windows.MessageBox.Show("Please select at least one link to download.", "No Links Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string targetDir = SavePathTextBox.Text?.Trim() ?? DownloadPathCategoryService.GetDefaultBasePath();
            Directory.CreateDirectory(targetDir);

            foreach (var item in selected)
            {
                string filename = string.Empty;
                if (Uri.TryCreate(item.Url, UriKind.Absolute, out var uri))
                {
                    filename = Path.GetFileName(uri.AbsolutePath);
                }
                if (string.IsNullOrWhiteSpace(filename)) filename = "file_" + Guid.NewGuid().ToString("N")[..8];
                string savePath = Path.Combine(targetDir, filename);

                Services.History.DownloadHistoryRecorder.CreateEntry(item.Url, savePath, -1);
            }

            System.Windows.MessageBox.Show($"Successfully added {selected.Count} downloads to EDM Queue!", "Batch Download Queued", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
