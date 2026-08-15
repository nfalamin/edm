using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using EDM.Services;

namespace EDM.Views
{
    public class RemoteZipEntryItem
    {
        public string FullPath { get; set; } = string.Empty;
        public long UncompressedSize { get; set; }
        public long CompressedSize { get; set; }
        public double Ratio { get; set; }

        public string FormattedUncompSize => $"{UncompressedSize / (1024.0 * 1024.0):F2} MB";
        public string FormattedCompSize => $"{CompressedSize / (1024.0 * 1024.0):F2} MB";
        public string FormattedRatio => $"{Ratio:F1}x";
    }

    public partial class RemoteZipPreviewWindow : Window
    {
        public ObservableCollection<RemoteZipEntryItem> Entries { get; set; } = new();
        private readonly string _url;

        public Action<string>? OnDownloadRequested { get; set; }

        public RemoteZipPreviewWindow(string url, ArchivePreviewResult? result = null)
        {
            InitializeComponent();
            _url = url;
            ArchiveUrlText.Text = $"📦 Remote Archive Inspection: {url}";
            EntriesGrid.ItemsSource = Entries;

            if (result != null && result.Entries.Count > 0)
            {
                foreach (var entry in result.Entries)
                {
                    Entries.Add(new RemoteZipEntryItem
                    {
                        FullPath = entry.FullPath,
                        UncompressedSize = entry.UncompressedSizeBytes,
                        CompressedSize = entry.CompressedSizeBytes,
                        Ratio = entry.CompressionRatio
                    });
                }

                SummaryText.Text = $"Total: {result.TotalEntries} files | Uncompressed: {result.TotalUncompressedBytes / (1024.0 * 1024.0):F2} MB";
            }
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnDownloadFull(object sender, RoutedEventArgs e)
        {
            OnDownloadRequested?.Invoke(_url);
            DialogResult = true;
            Close();
        }
    }
}
