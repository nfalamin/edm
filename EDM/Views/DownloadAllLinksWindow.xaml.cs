using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace EDM.Views
{
    public class LinkItemViewModel
    {
        public bool IsSelected { get; set; } = true;
        public string FileName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
    }

    public partial class DownloadAllLinksWindow : Window
    {
        public ObservableCollection<LinkItemViewModel> Links { get; set; } = new();
        private readonly List<LinkItemViewModel> _masterLinks = new();
        public Action<List<string>, string>? OnDownloadsConfirmed { get; set; }

        public DownloadAllLinksWindow(IEnumerable<string>? initialUrls = null)
        {
            InitializeComponent();
            LinksGrid.ItemsSource = Links;

            SaveFolderBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads";

            if (initialUrls != null)
            {
                foreach (var url in initialUrls)
                {
                    string name = Path.GetFileName(new Uri(url).AbsolutePath);
                    if (string.IsNullOrEmpty(name)) name = "download";
                    string ext = Path.GetExtension(name);

                    var item = new LinkItemViewModel
                    {
                        IsSelected = true,
                        FileName = name,
                        Url = url,
                        Extension = ext
                    };
                    _masterLinks.Add(item);
                    Links.Add(item);
                }
            }
        }

        private void OnFilterAll(object sender, RoutedEventArgs e) => ApplyFilter(_ => true);
        private void OnFilterVideos(object sender, RoutedEventArgs e) => ApplyFilter(ext => new[] { ".mp4", ".mkv", ".webm", ".avi" }.Contains(ext));
        private void OnFilterArchives(object sender, RoutedEventArgs e) => ApplyFilter(ext => new[] { ".zip", ".rar", ".7z", ".tar" }.Contains(ext));
        private void OnFilterDocs(object sender, RoutedEventArgs e) => ApplyFilter(ext => new[] { ".pdf", ".doc", ".docx", ".xls", ".txt" }.Contains(ext));
        private void OnFilterImages(object sender, RoutedEventArgs e) => ApplyFilter(ext => new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" }.Contains(ext));

        private void ApplyFilter(Func<string, bool> predicate)
        {
            Links.Clear();
            foreach (var item in _masterLinks)
            {
                if (predicate(item.Extension.ToLowerInvariant()))
                {
                    Links.Add(item);
                }
            }
        }

        private void OnSelectAll(object sender, RoutedEventArgs e)
        {
            foreach (var item in Links) item.IsSelected = true;
            LinksGrid.Items.Refresh();
        }

        private void OnDeselectAll(object sender, RoutedEventArgs e)
        {
            foreach (var item in Links) item.IsSelected = false;
            LinksGrid.Items.Refresh();
        }

        private void OnBrowseFolder(object sender, RoutedEventArgs e)
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SaveFolderBox.Text = dlg.SelectedPath;
            }
        }

        private void OnAddToQueue(object sender, RoutedEventArgs e)
        {
            ConfirmDownloads(startImmediately: false);
        }

        private void OnStartDownload(object sender, RoutedEventArgs e)
        {
            ConfirmDownloads(startImmediately: true);
        }

        private void ConfirmDownloads(bool startImmediately)
        {
            var selectedUrls = Links.Where(l => l.IsSelected).Select(l => l.Url).ToList();
            if (selectedUrls.Count == 0)
            {
                System.Windows.MessageBox.Show("Please select at least one link to download.", "EDM", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OnDownloadsConfirmed?.Invoke(selectedUrls, SaveFolderBox.Text);
            DialogResult = true;
            Close();
        }
    }
}
