using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EDM.Services;

namespace EDM.Views
{
    public partial class FloatingDropTargetWindow : Window
    {
        public Action<string, string>? OnUrlsDropped { get; set; }
        public event Action? WidgetClosed;
        private EDM.ViewModels.DownloadManagerViewModel? _viewModel;

        public FloatingDropTargetWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => SnapToTopRightCorner();
        }

        public void SetViewModel(EDM.ViewModels.DownloadManagerViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        private void SnapToTopRightCorner()
        {
            var primaryScreen = SystemParameters.WorkArea;
            Left = primaryScreen.Right - Width - 30;
            Top = primaryScreen.Top + 60;
        }

        private void OnWindowDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void OnCloseWidget(object sender, RoutedEventArgs e)
        {
            Hide();
            WidgetClosed?.Invoke();
        }

        private void OnDragEnter(object sender, System.Windows.DragEventArgs e)
        {
            BasketBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50));
            BasketBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xF0, 0x2E, 0x3E, 0x33));
            StatusIcon.Text = "✨ Release to Download";
        }

        private void OnDragLeave(object sender, System.Windows.DragEventArgs e)
        {
            ResetVisualState();
        }

        private void ResetVisualState()
        {
            BasketBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x7C, 0x4D, 0xFF));
            BasketBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xE6, 0x1E, 0x1E, 0x2E));
            StatusIcon.Text = "📥 Drop URL or File";
        }

        private void OnDrop(object sender, System.Windows.DragEventArgs e)
        {
            ResetVisualState();

            string queueName = (QueueSelector.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Default Queue";

            // 1. Check if raw text / URL was dropped
            if (e.Data.GetDataPresent(System.Windows.DataFormats.Text))
            {
                string text = (string)e.Data.GetData(System.Windows.DataFormats.Text);
                var urls = ExtractUrls(text);

                foreach (var url in urls)
                {
                    OnUrlsDropped?.Invoke(url, queueName);
                }

                if (_viewModel != null && urls.Count > 0)
                {
                    foreach (var url in urls)
                    {
                        try
                        {
                            string fileName = string.Empty;
                            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                            {
                                fileName = System.IO.Path.GetFileName(uri.AbsolutePath);
                            }
                            if (string.IsNullOrWhiteSpace(fileName))
                            {
                                fileName = "download_" + Guid.NewGuid().ToString("N")[..8];
                            }

                            var cat = DownloadPathCategoryService.DetermineFileCategory(fileName);
                            string catStr = cat.ToString();
                            string baseDir = DownloadPathCategoryService.GetDefaultBasePath();
                            string saveDir = DownloadPathCategoryService.BuildCategorizedPath(baseDir, fileName);
                            if (!System.IO.Directory.Exists(saveDir))
                            {
                                System.IO.Directory.CreateDirectory(saveDir);
                            }
                            string savePath = System.IO.Path.Combine(saveDir, fileName);

                            var item = new EDM.Models.DownloadItem
                            {
                                FileName = fileName,
                                Url = url,
                                SavePath = savePath,
                                Category = catStr,
                                Status = "Downloading"
                            };

                            _viewModel.AllDownloads.Insert(0, item);
                            _ = _viewModel.StartDownloadProcessAsync(item);
                        }
                        catch (Exception ex)
                        {
                            LoggingService.LogException("[FloatingDropTarget] Failed to start download", ex);
                        }
                    }
                }

                if (urls.Count > 0)
                {
                    StatusIcon.Text = $"✅ Ingested {urls.Count} Link(s)";
                }
            }
            // 2. Check if local file was dropped
            else if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    StatusIcon.Text = $"📁 {files.Length} Local File(s)";
                }
            }
        }

        private List<string> ExtractUrls(string text)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return results;

            var matches = Regex.Matches(text, @"https?://[^\s""'<>]+", RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                if (Uri.TryCreate(m.Value, UriKind.Absolute, out _))
                {
                    results.Add(m.Value);
                }
            }

            if (results.Count == 0 && (text.StartsWith("http://") || text.StartsWith("https://")))
            {
                results.Add(text.Trim());
            }

            return results;
        }

        public void UpdateSpeed(string speed)
        {
            SpeedText.Text = speed;
            SpeedText.Visibility = Visibility.Visible;
        }
    }
}
