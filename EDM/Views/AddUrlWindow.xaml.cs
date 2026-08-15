using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using EDM.Models;
using EDM.Services;
using EDM.ViewModels;

namespace EDM.Views
{
    /// <summary>
    /// AddUrlWindow.xaml.cs - Dialog for adding new downloads with URL, path, and automatic category routing
    /// </summary>
    public partial class AddUrlWindow : Window
    {
        private DownloadManagerViewModel? _viewModel;
        private string _baseDownloadFolder = string.Empty;

        public AddUrlWindow()
        {
            InitializeComponent();
            _baseDownloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            SavePathTextBox.Text = _baseDownloadFolder;
        }

        /// <summary>
        /// Initialize dialog with a reference to the ViewModel and optional prefilled URL (e.g. from clipboard or extension)
        /// </summary>
        public void Initialize(DownloadManagerViewModel? viewModel, string? prefillUrl = null)
        {
            _viewModel = viewModel;
            if (!string.IsNullOrWhiteSpace(prefillUrl))
            {
                UrlTextBox.Text = prefillUrl;
                UrlTextBox.SelectAll();
            }
        }

        private void UrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string url = UrlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url)) return;

            // Auto-detect category and routing
            try
            {
                string pathFileName = Path.GetFileName(new Uri(url.Contains("://") ? url : "https://" + url).AbsolutePath);
                if (!string.IsNullOrWhiteSpace(pathFileName))
                {
                    var catRule = DownloadCategoryRouter.Instance.DetermineCategory(pathFileName);
                    if (catRule != null && CategoryComboBox != null)
                    {
                        for (int i = 0; i < CategoryComboBox.Items.Count; i++)
                        {
                            if (CategoryComboBox.Items[i] is ComboBoxItem cbi &&
                                string.Equals(cbi.Content?.ToString(), catRule.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                CategoryComboBox.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryComboBox?.SelectedItem is ComboBoxItem cbi && !string.IsNullOrWhiteSpace(_baseDownloadFolder))
            {
                string catName = cbi.Content?.ToString() ?? "General";
                string subFolder = catName switch
                {
                    "Video" => "Video",
                    "Audio" => "Audio",
                    "Documents" => "Documents",
                    "Programs" => "Programs",
                    "Compressed" => "Compressed",
                    _ => "Others"
                };

                if (CreateSubfolderCheckBox?.IsChecked == true)
                {
                    SavePathTextBox.Text = Path.Combine(_baseDownloadFolder, subFolder);
                }
            }
        }

        /// <summary>
        /// Handle Browse button click - Opens folder selection dialog
        /// </summary>
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Select a folder to save the download";

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SavePathTextBox.Text = dialog.SelectedPath;
                _baseDownloadFolder = dialog.SelectedPath;
            }
        }

        /// <summary>
        /// Handle Start Download button click - Validates and submits form
        /// </summary>
        private void StartDownload_Click(object sender, RoutedEventArgs e)
        {
            // Validate URL
            string rawUrl = UrlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                System.Windows.MessageBox.Show("Please enter a valid download URL.", "Missing URL", MessageBoxButton.OK, MessageBoxImage.Warning);
                UrlTextBox.Focus();
                return;
            }

            if (!rawUrl.Contains("://"))
            {
                rawUrl = "https://" + rawUrl;
            }

            // Validate Save Path
            string targetFolder = SavePathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(targetFolder))
            {
                targetFolder = _baseDownloadFolder;
            }

            if (!Directory.Exists(targetFolder))
            {
                try { Directory.CreateDirectory(targetFolder); } catch { }
            }

            // Extract or generate filename
            string fileName = "EDM_Download_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            bool isYouTubeUrl = rawUrl.Contains("youtube.com/", StringComparison.OrdinalIgnoreCase) || rawUrl.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);

            try
            {
                var uri = new Uri(rawUrl);
                string pathFileName = Path.GetFileName(uri.AbsolutePath);

                if (isYouTubeUrl)
                {
                    string videoId = "video";
                    if (rawUrl.Contains("v="))
                    {
                        int vIndex = rawUrl.IndexOf("v=");
                        int ampIndex = rawUrl.IndexOf("&", vIndex);
                        videoId = ampIndex > 0 ? rawUrl.Substring(vIndex + 2, ampIndex - (vIndex + 2)) : rawUrl.Substring(vIndex + 2);
                    }
                    fileName = $"YouTube_Video_{videoId}.mp4";
                }
                else if (!string.IsNullOrWhiteSpace(pathFileName) && pathFileName.Contains("."))
                {
                    fileName = pathFileName;
                }
                else
                {
                    fileName = pathFileName.Length > 0 ? pathFileName : "download.dat";
                }
            }
            catch
            {
                fileName = "download.dat";
            }

            string fullSavePath = Path.Combine(targetFolder, fileName);
            string selectedCat = (CategoryComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "General";

            // Create download item
            var newDownload = new DownloadItem
            {
                FileName = fileName,
                Url = rawUrl,
                SavePath = fullSavePath,
                Category = selectedCat,
                Status = AutoStartCheckBox.IsChecked == true ? "Downloading" : "Queued",
                Progress = 0,
                LastTryDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Size = "0 B",
                TransferRate = "0 B/s"
            };

            // Add to ViewModel
            if (_viewModel != null)
            {
                _viewModel.AddDownload(newDownload);
            }
            else if (System.Windows.Application.Current?.MainWindow?.DataContext is DownloadManagerViewModel mainVm)
            {
                mainVm.AddDownload(newDownload);
            }

            // Open Progress Window if auto start enabled
            if (AutoStartCheckBox.IsChecked == true)
            {
                try
                {
                    var progressWindow = new DownloadProgressWindow(newDownload);
                    progressWindow.Owner = System.Windows.Application.Current?.MainWindow;
                    progressWindow.Show();
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[AddUrlWindow] Failed to show DownloadProgressWindow", ex);
                }
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
