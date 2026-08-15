using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using EDM.Models;
using EDM.Services;

namespace EDM.Views
{
    public partial class ResumeDialog : Window
    {
        public ObservableCollection<DownloadItem> Items { get; } = new ObservableCollection<DownloadItem>();

        private readonly ResumeScannerService _scanner;
        private readonly DownloadHistoryService _historyService;

        public ResumeDialog(ResumeScannerService scanner, DownloadHistoryService history)
        {
            InitializeComponent();
            _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
            _historyService = history ?? throw new ArgumentNullException(nameof(history));

            // bind list and wire buttons
            try { ResumableList.ItemsSource = Items; } catch (Exception ex) { LoggingService.Log($"[ResumeDialog] Failed to initialize ResumableList: {ex.Message}"); }
            try { ResumeButton.Click += ResumeButton_Click; } catch (Exception ex) { LoggingService.Log($"[ResumeDialog] Failed to attach ResumeButton_Click: {ex.Message}"); }
            try { DeleteButton.Click += DeleteButton_Click; } catch (Exception ex) { LoggingService.Log($"[ResumeDialog] Failed to attach DeleteButton_Click: {ex.Message}"); }
            try { CloseButton.Click += (s, e) => this.Close(); } catch (Exception ex) { LoggingService.Log($"[ResumeDialog] Failed to attach Close handler: {ex.Message}"); }

            this.Closed += ResumeDialog_Closed;

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                var found = await _scanner.FindResumableDownloadsAsync().ConfigureAwait(false);
                await Dispatcher.InvokeAsync(() =>
                {
                    Items.Clear();
                    foreach (var d in found) Items.Add(d);
                });
            }
            catch (Exception ex) { LoggingService.Log($"[ResumeDialog.LoadAsync] {ex.Message}"); }
        }

        private async void ResumeButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (ResumableList.SelectedItem is not DownloadItem sel) return;
                // open download window and start resume
                var win = new DownloadProgressWindow(sel);
                win.Owner = this.Owner;
                win.Show();
                Items.Remove(sel);
                try { await _historyService.SaveHistoryAsync(new System.Collections.ObjectModel.ObservableCollection<DownloadItem>(Items)).ConfigureAwait(false); } catch (Exception ex) { LoggingService.Log($"[ResumeDialog.ResumeButton_Click] SaveHistory failed: {ex.Message}"); }
            }
            catch (Exception ex) { LoggingService.Log($"[ResumeDialog.ResumeButton_Click] {ex.Message}"); }
        }

        private void DeleteButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (ResumableList.SelectedItem is not DownloadItem sel) return;
                // Attempt to remove .tmp_<filename> directory next to SavePath
                try
                {
                    var dir = Path.GetDirectoryName(sel.SavePath) ?? ".";
                    var metaDir = Path.Combine(dir, ".tmp_" + Path.GetFileName(sel.SavePath));
                    if (Directory.Exists(metaDir)) Directory.Delete(metaDir, true);
                    // also try to delete part files
                    var parts = Directory.EnumerateFiles(dir, Path.GetFileName(sel.SavePath) + ".part*");
                    foreach (var p in parts) try { File.Delete(p); } catch (Exception ex) { LoggingService.Log($"[ResumeDialog.DeleteButton_Click] Failed to delete part file {p}: {ex.Message}"); }
                }
                catch (Exception ex) { LoggingService.Log($"[ResumeDialog.DeleteButton_Click] Cleanup failed: {ex.Message}"); }

                Items.Remove(sel);
                _ = _historyService.SaveHistoryAsync(new System.Collections.ObjectModel.ObservableCollection<DownloadItem>(Items));
            }
            catch (Exception ex) { LoggingService.Log($"[ResumeDialog.DeleteButton_Click] {ex.Message}"); }
        }

        private void ResumeDialog_Closed(object? sender, EventArgs e)
        {
            try { ResumeButton.Click -= ResumeButton_Click; } catch (Exception ex) { LoggingService.Log($"[ResumeDialog] Failed to unsubscribe ResumeButton_Click: {ex.Message}"); }
            try { DeleteButton.Click -= DeleteButton_Click; } catch (Exception ex) { LoggingService.Log($"[ResumeDialog] Failed to unsubscribe DeleteButton_Click: {ex.Message}"); }
            try { CloseButton.Click -= (s, ev) => this.Close(); } catch (Exception ex) { LoggingService.Log($"[ResumeDialog] Failed to unsubscribe Close handler: {ex.Message}"); }
        }
    }
}
