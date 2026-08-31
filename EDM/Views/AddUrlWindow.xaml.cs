using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using EDM.Models;
using EDM.NativeMessaging;
using EDM.Services;
using EDM.Services.Helpers;
using EDM.ViewModels;

namespace EDM.Views
{
    /// <summary>
    /// AddUrlWindow.xaml.cs - Production-grade dialog for adding downloads with real-time media analysis,
    /// dynamic quality resolution, strict validation, and double-download prevention.
    /// </summary>
    public partial class AddUrlWindow : Window
    {
        private DownloadManagerViewModel? _viewModel;
        private string _baseDownloadFolder = string.Empty;
        private bool _isSubmitting = false;
        private bool _isAnalyzing = false;
        private List<MediaVariantOption> _detectedVariants = new();
        private readonly System.Windows.Threading.DispatcherTimer _autoAnalyzeTimer;
        private string _lastAnalyzedUrl = string.Empty;
        private string _lastAutoFilename = string.Empty; // tracks auto-populated filename to avoid overwriting user edits

        public ICommand StartDownloadKeyCommand { get; }
        public ICommand CancelKeyCommand { get; }

        public AddUrlWindow()
        {
            StartDownloadKeyCommand = new RelayCommand(() => StartDownload_Click(this, new RoutedEventArgs()));
            CancelKeyCommand = new RelayCommand(() => Cancel_Click(this, new RoutedEventArgs()));

            InitializeComponent();
            _baseDownloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            SavePathTextBox.Text = _baseDownloadFolder;
            UpdatePlaceholder();

            _autoAnalyzeTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(0)
            };
            _autoAnalyzeTimer.Tick += AutoAnalyzeTimer_Tick;

            // Detect and sync app theme (light/dark adaptive)
            SyncThemeWithApp();
        }

        private void SyncThemeWithApp()
        {
            try
            {
                var appRes = System.Windows.Application.Current?.Resources?.MergedDictionaries;
                var themeDict = appRes?.FirstOrDefault(d => d.Source != null &&
                    (d.Source.OriginalString.Contains("LightTheme") || d.Source.OriginalString.Contains("DarkTheme")));

                bool isLight = themeDict?.Source?.OriginalString?.Contains("LightTheme") == true;
                if (isLight)
                {
                    this.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xF0, 0xF4, 0xFF));
                    this.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x1E, 0x29, 0x3B));
                    this.Resources["DlgBg"]            = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF0, 0xF4, 0xFF));
                    this.Resources["DlgCardBg"]        = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
                    this.Resources["DlgHeaderBg"]      = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xEF, 0xFF));
                    this.Resources["DlgInputBg"]       = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0xF8, 0xFF));
                    this.Resources["DlgBorder"]        = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC7, 0xD2, 0xF7));
                    this.Resources["DlgBorderHover"]   = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x63, 0x66, 0xF1));
                    this.Resources["DlgTextPrimary"]   = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x29, 0x3B));
                    this.Resources["DlgTextSecondary"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4B, 0x55, 0x63));
                    this.Resources["DlgTextMuted"]     = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6B, 0x72, 0x80));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[AddUrlWindow] SyncThemeWithApp failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Initialize dialog with a reference to the ViewModel and optional prefilled URL
        /// </summary>
        public void Initialize(DownloadManagerViewModel? viewModel, string? prefillUrl = null)
        {
            _viewModel = viewModel;
            if (!string.IsNullOrWhiteSpace(prefillUrl))
            {
                UrlTextBox.Text = prefillUrl.Trim();
                UrlTextBox.SelectAll();
                UpdatePlaceholder();
                TriggerAutoAnalyze();
            }
        }

        private string _handoffCookies = string.Empty;
        private string _handoffReferer = string.Empty;
        private string _handoffUserAgent = string.Empty;
        private string _handoffAuthHeader = string.Empty;
        private string _handoffVideoUrl = string.Empty;
        private string _handoffAudioUrl = string.Empty;
        private bool _handoffRequiresFfmpegMerge = false;
        private string _handoffQuality = string.Empty;
        private long _handoffEstimatedSize = 0;

        /// <summary>
        /// Populates the dialog from a browser extension Native Messaging handoff,
        /// auto-detecting category, target subfolder, and setting focus on Start Download.
        /// </summary>
        public void PrefillFromHandoff(IpcHandoffPayload payload, DownloadManagerViewModel? viewModel = null)
        {
            _viewModel = viewModel ?? (System.Windows.Application.Current?.MainWindow?.DataContext as DownloadManagerViewModel);
            if (payload == null) return;

            string url = (payload.Url ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(url))
            {
                UrlTextBox.Text = url;
                UpdatePlaceholder();
            }

            _handoffCookies = payload.Cookies ?? string.Empty;
            _handoffReferer = payload.Referer ?? payload.PageUrl ?? string.Empty;
            _handoffUserAgent = payload.UserAgent ?? string.Empty;
            _handoffAuthHeader = payload.AuthHeader ?? string.Empty;
            _handoffVideoUrl = payload.VideoUrl ?? string.Empty;
            _handoffAudioUrl = payload.AudioUrl ?? string.Empty;
            _handoffRequiresFfmpegMerge = payload.RequiresFfmpegMerge;
            _handoffQuality = payload.Quality ?? string.Empty;
            _handoffEstimatedSize = payload.EstimatedSizeBytes ?? 0;

            string suggestedFileName = !string.IsNullOrWhiteSpace(payload.Filename)
                ? payload.Filename
                : (!string.IsNullOrWhiteSpace(payload.Title) ? payload.Title : string.Empty);

            if (string.IsNullOrWhiteSpace(suggestedFileName) && !string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    var uri = new Uri(url);
                    suggestedFileName = Path.GetFileName(uri.LocalPath);
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(suggestedFileName))
            {
                suggestedFileName = FileNamingHelper.SanitizeFileName(suggestedFileName);
                FileNameTextBox.Text = suggestedFileName;
                _lastAutoFilename = suggestedFileName;
            }

            // Auto-detect Category based on file extension
            string ext = Path.GetExtension(suggestedFileName).TrimStart('.').ToLowerInvariant();
            string categoryName = CategorizeExtension(ext);
            SelectCategoryByName(categoryName);

            // Set destination folder to category folder
            string subFolder = categoryName switch
            {
                "Video" => "Video",
                "Audio" => "Audio",
                "Documents" => "Documents",
                "Programs" => "Programs",
                "Compressed" => "Compressed",
                _ => "Others"
            };
            string targetFolder = Path.Combine(_baseDownloadFolder, subFolder);
            try
            {
                if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);
                SavePathTextBox.Text = targetFolder;
            }
            catch
            {
                SavePathTextBox.Text = _baseDownloadFolder;
            }

            // Bring window to front over browser
            this.Topmost = true;
            this.Show();
            this.Activate();
            this.Focus();
            this.Topmost = false;

            StartDownloadButton.Focus();
        }

        private static string CategorizeExtension(string ext)
        {
            if (string.IsNullOrWhiteSpace(ext)) return "General";
            switch (ext.ToLowerInvariant())
            {
                case "zip": case "rar": case "7z": case "tar": case "gz": case "bz2": case "xz": case "iso": case "cab": case "dmg":
                    return "Compressed";
                case "exe": case "msi": case "apk": case "bin": case "appx":
                    return "Programs";
                case "mp4": case "mkv": case "webm": case "avi": case "mov": case "wmv": case "flv": case "ts":
                    return "Video";
                case "mp3": case "m4a": case "aac": case "flac": case "wav": case "wma": case "ogg": case "opus":
                    return "Audio";
                case "pdf": case "doc": case "docx": case "xls": case "xlsx": case "ppt": case "pptx": case "txt": case "epub":
                    return "Documents";
                default:
                    return "General";
            }
        }

        private void SelectCategoryByName(string categoryName)
        {
            if (CategoryComboBox == null) return;
            foreach (var item in CategoryComboBox.Items)
            {
                if (item is ComboBoxItem cbi && string.Equals(cbi.Content?.ToString(), categoryName, StringComparison.OrdinalIgnoreCase))
                {
                    CategoryComboBox.SelectedItem = cbi;
                    return;
                }
            }
        }

        private void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    string text = System.Windows.Clipboard.GetText().Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        UrlTextBox.Text = text;
                        UrlTextBox.CaretIndex = text.Length;
                        UpdatePlaceholder();
                        TriggerAutoAnalyze();
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[AddUrlWindow] Clipboard paste error: {ex.Message}");
            }
        }

        private void AutoAnalyzeTimer_Tick(object? sender, EventArgs e)
        {
            _autoAnalyzeTimer.Stop();
            string rawUrl = UrlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(rawUrl) || rawUrl == _lastAnalyzedUrl) return;

            if (ValidateUrlInput(rawUrl, out string normalizedUrl, out _))
            {
                _lastAnalyzedUrl = rawUrl;
                AnalyzeButton_Click(this, new RoutedEventArgs());
            }
        }

        private void TriggerAutoAnalyze()
        {
            _autoAnalyzeTimer.Stop();
            _autoAnalyzeTimer.Start();
        }

        private void UpdatePlaceholder()
        {
            if (UrlPlaceholderText != null && UrlTextBox != null)
            {
                UrlPlaceholderText.Visibility = string.IsNullOrEmpty(UrlTextBox.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void UrlTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            UpdatePlaceholder();
        }

        private void UrlTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdatePlaceholder();
        }

        private void UrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePlaceholder();
            if (SubmissionStatusText != null) SubmissionStatusText.Text = string.Empty;

            string url = UrlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url)) return;

            // ── INSTANT filename extraction (0ms, no HTTP probe needed) ──
            try
            {
                string rawForParse = url.Contains("://") ? url : "https://" + url;
                if (Uri.TryCreate(rawForParse, UriKind.Absolute, out var parsedUri))
                {
                    string pathFileName = Path.GetFileName(parsedUri.AbsolutePath);

                    // Show filename immediately in the FileNameTextBox if it looks like a real file
                    if (!string.IsNullOrWhiteSpace(pathFileName) && pathFileName.Contains('.') && FileNameTextBox != null)
                    {
                        // Don't overwrite if user already typed something manually
                        if (string.IsNullOrWhiteSpace(FileNameTextBox.Text) || _lastAutoFilename == FileNameTextBox.Text)
                        {
                            _lastAutoFilename = Uri.UnescapeDataString(pathFileName);
                            FileNameTextBox.Text = _lastAutoFilename;
                        }
                    }

                    // Also show instant analysis status card with filename
                    if (AnalysisStatusCard != null && !string.IsNullOrWhiteSpace(pathFileName) && pathFileName.Contains('.'))
                    {
                        AnalysisStatusCard.Visibility = Visibility.Visible;
                        AnalysisStatusIcon.Text = "📁";
                        AnalysisStatusTitle.Text = Uri.UnescapeDataString(pathFileName);
                        AnalysisStatusText.Text = "Analyzing stream & server capabilities...";
                    }

                    // Instant 0-second YouTube & Video Title prefetch
                    if (MediaVariantResolver.IsYouTubeUrl(url))
                    {
                        if (AnalysisStatusCard != null)
                        {
                            AnalysisStatusCard.Visibility = Visibility.Visible;
                            AnalysisStatusIcon.Text = "🎬";
                            AnalysisStatusTitle.Text = "Detecting Video Title...";
                            AnalysisStatusText.Text = "Fetching media metadata (0s fast detection)...";
                        }

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                string? realTitle = await MediaVariantResolver.FetchYouTubeTitleFastAsync(url, CancellationToken.None).ConfigureAwait(false);
                                if (!string.IsNullOrWhiteSpace(realTitle))
                                {
                                    string safeFileName = FileNamingHelper.SanitizeFileName(realTitle);
                                    if (!safeFileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) safeFileName += ".mp4";

                                    await Dispatcher.InvokeAsync(() =>
                                    {
                                        if (FileNameTextBox != null && (string.IsNullOrWhiteSpace(FileNameTextBox.Text) || 
                                            _lastAutoFilename == FileNameTextBox.Text || 
                                            FileNameTextBox.Text.StartsWith("YouTube_Video_") ||
                                            FileNameTextBox.Text == "download.mp4"))
                                        {
                                            _lastAutoFilename = safeFileName;
                                            FileNameTextBox.Text = safeFileName;
                                        }

                                        if (AnalysisStatusTitle != null) AnalysisStatusTitle.Text = realTitle;
                                        if (AnalysisStatusText != null) AnalysisStatusText.Text = "Ready to download in ultra high speed";
                                    });
                                }
                            }
                            catch { }
                        });
                    }

                    // Auto-detect category from filename instantly
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

            // Debounce auto-analysis (full HTTP probe runs after 0ms idle)
            TriggerAutoAnalyze();
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

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder for downloaded files",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SavePathTextBox.Text = dialog.SelectedPath;
                _baseDownloadFolder = dialog.SelectedPath;
            }
        }

        /// <summary>
        /// Perform non-blocking stream / media inspection
        /// </summary>
        private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isAnalyzing) return;

            string rawUrl = UrlTextBox.Text.Trim();
            if (!ValidateUrlInput(rawUrl, out string normalizedUrl, out string validationError))
            {
                ShowError(validationError);
                return;
            }

            _isAnalyzing = true;
            AnalyzeButton.IsEnabled = false;
            StartDownloadButton.IsEnabled = false;
            AnalysisStatusCard.Visibility = Visibility.Visible;
            AnalysisStatusIcon.Text = "⏳";
            AnalysisStatusTitle.Text = "Inspecting Stream...";
            AnalysisStatusText.Text = "Analyzing media and server capabilities...";

            try
            {
                bool isStreaming = Regex.IsMatch(normalizedUrl, @"youtube\.com|youtu\.be|vimeo\.com|dailymotion\.com|twitch\.tv|twitter\.com|x\.com|tiktok\.com|instagram\.com|\.m3u8|\.mpd", RegexOptions.IgnoreCase);

                if (isStreaming)
                {
                    var resolver = new MediaVariantResolver();
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                    var result = await resolver.ResolveVariantsAsync(normalizedUrl, cancellationToken: cts.Token).ConfigureAwait(true);

                    if (result.Success && result.Variants != null && result.Variants.Count > 0)
                    {
                        _detectedVariants = result.Variants;
                        QualityComboBox.Items.Clear();

                        foreach (var variant in result.Variants)
                        {
                            var cbi = new ComboBoxItem
                            {
                                Content = variant.FormattedDetails,
                                Tag = variant
                            };
                            QualityComboBox.Items.Add(cbi);
                        }
                        QualityComboBox.SelectedIndex = 0;

                        // Populate format containers
                        FormatComboBox.Items.Clear();
                        var containers = result.Variants.Select(v => v.Container.ToUpperInvariant()).Distinct().ToList();
                        if (containers.Count == 0) containers.Add("MP4");
                        foreach (var c in containers) FormatComboBox.Items.Add(new ComboBoxItem { Content = $"{c} Container" });
                        FormatComboBox.SelectedIndex = 0;

                        AnalysisStatusIcon.Text = "🎬";
                        AnalysisStatusTitle.Text = !string.IsNullOrWhiteSpace(result.Title) ? result.Title : "Media Stream Detected";
                        AnalysisStatusText.Text = $"✓ Detected {result.Variants.Count} stream(s) • Dynamic video & audio merge ready";
                        
                        if (CategoryComboBox != null)
                        {
                            CategoryComboBox.SelectedIndex = 1; // Video
                        }
                    }
                    else
                    {
                        SetDirectStreamFallbackUI();
                    }
                }
                else
                {
                    // Direct file probe
                    try
                    {
                        var probe = new HttpProbeService();
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        var probeResult = await probe.ProbeUrlAsync(normalizedUrl, string.Empty, cancellationToken: cts.Token).ConfigureAwait(true);

                        string sizeText = probeResult.TotalBytes.HasValue && probeResult.TotalBytes.Value > 0
                            ? FormatBytes(probeResult.TotalBytes.Value)
                            : "Size: Unknown";

                        QualityComboBox.Items.Clear();
                        QualityComboBox.Items.Add(new ComboBoxItem { Content = $"Direct Stream ({sizeText})" });
                        QualityComboBox.SelectedIndex = 0;

                        FormatComboBox.Items.Clear();
                        string ext = Path.GetExtension(probeResult.InferredFileName).TrimStart('.').ToUpperInvariant();
                        FormatComboBox.Items.Add(new ComboBoxItem { Content = !string.IsNullOrEmpty(ext) ? $"{ext} File" : "Binary File" });
                        FormatComboBox.SelectedIndex = 0;

                        var cat = DownloadCategoryRouter.Instance.DetermineCategory(probeResult.InferredFileName, probeResult.ContentType, normalizedUrl);
                        if (cat != null && CategoryComboBox != null)
                        {
                            for (int i = 0; i < CategoryComboBox.Items.Count; i++)
                            {
                                if (CategoryComboBox.Items[i] is ComboBoxItem cbi && string.Equals(cbi.Content?.ToString(), cat.Name, StringComparison.OrdinalIgnoreCase))
                                {
                                    CategoryComboBox.SelectedIndex = i;
                                    break;
                                }
                            }
                        }

                        AnalysisStatusIcon.Text = "📁";
                        AnalysisStatusTitle.Text = probeResult.InferredFileName;
                        AnalysisStatusText.Text = $"✓ Verified direct file • {sizeText} • Resume {(probeResult.ServerSupportsResume ? "Supported (206 OK)" : "Not Supported")}";
                    }
                    catch
                    {
                        SetDirectStreamFallbackUI();
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[AddUrlWindow] Analysis info: {ex.Message}");
                SetDirectStreamFallbackUI();
            }
            finally
            {
                _isAnalyzing = false;
                AnalyzeButton.IsEnabled = true;
                StartDownloadButton.IsEnabled = true;
            }
        }

        private void SetDirectStreamFallbackUI()
        {
            QualityComboBox.Items.Clear();
            QualityComboBox.Items.Add(new ComboBoxItem { Content = "Direct Stream (Original Quality)" });
            QualityComboBox.SelectedIndex = 0;

            FormatComboBox.Items.Clear();
            FormatComboBox.Items.Add(new ComboBoxItem { Content = "Direct File" });
            FormatComboBox.SelectedIndex = 0;

            AnalysisStatusIcon.Text = "ℹ️";
            AnalysisStatusTitle.Text = "Direct File Stream";
            AnalysisStatusText.Text = "Direct stream ready for high-speed multi-threaded download.";
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Validates URL input string strictly and checks protocol support
        /// </summary>
        public static bool ValidateUrlInput(string? input, out string normalizedUrl, out string errorMessage)
        {
            normalizedUrl = string.Empty;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                errorMessage = "Download URL cannot be empty.";
                return false;
            }

            string raw = input.Trim();

            // Reject unsafe / forbidden schemes
            if (raw.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                raw.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
                raw.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Unsupported or unsafe URL protocol.";
                return false;
            }

            if (!raw.Contains("://") && !raw.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            {
                raw = "https://" + raw;
            }

            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || 
                !(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || 
                  uri.Scheme == Uri.UriSchemeFtp || uri.Scheme == Uri.UriSchemeFtps || uri.Scheme == "magnet"))
            {
                errorMessage = "Please enter a valid HTTP, HTTPS, FTP, or Magnet URL.";
                return false;
            }

            normalizedUrl = raw;
            return true;
        }

        /// <summary>
        /// Handle Start Download button click - Validates input, builds DownloadItem, prevents double submission, and closes dialog
        /// </summary>
        private void StartDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_isSubmitting) return;

            string rawUrl = UrlTextBox.Text.Trim();
            if (!ValidateUrlInput(rawUrl, out string normalizedUrl, out string validationError))
            {
                ShowError(validationError);
                UrlTextBox.Focus();
                return;
            }

            // Lock submission
            _isSubmitting = true;
            StartDownloadButton.IsEnabled = false;
            AnalyzeButton.IsEnabled = false;

            // Validate Save Path
            string targetFolder = SavePathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(targetFolder))
            {
                targetFolder = _baseDownloadFolder;
            }

            try
            {
                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Cannot create directory: {ex.Message}");
                _isSubmitting = false;
                StartDownloadButton.IsEnabled = true;
                AnalyzeButton.IsEnabled = true;
                return;
            }

            // Resolve file name
            string fileName = "EDM_Download_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            bool isYouTubeUrl = normalizedUrl.Contains("youtube.com/", StringComparison.OrdinalIgnoreCase) || normalizedUrl.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);

            try
            {
                var uri = new Uri(normalizedUrl);
                string pathFileName = Path.GetFileName(uri.AbsolutePath);

                // Use inspected title from analysis if available
                if (AnalysisStatusTitle != null && !string.IsNullOrWhiteSpace(AnalysisStatusTitle.Text) && 
                    AnalysisStatusTitle.Text != "Media Stream Detected" && AnalysisStatusTitle.Text != "Direct File Stream")
                {
                    string safeTitle = FileNamingHelper.SanitizeFileName(AnalysisStatusTitle.Text.Trim());
                    if (!string.IsNullOrWhiteSpace(safeTitle))
                    {
                        fileName = safeTitle.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ? safeTitle : $"{safeTitle}.mp4";
                    }
                }
                else if (isYouTubeUrl)
                {
                    string videoId = "video";
                    if (normalizedUrl.Contains("youtu.be/"))
                    {
                        string afterHost = uri.AbsolutePath.TrimStart('/');
                        int qIdx = afterHost.IndexOfAny(new[] { '?', '&' });
                        videoId = qIdx > 0 ? afterHost.Substring(0, qIdx) : afterHost;
                    }
                    else if (normalizedUrl.Contains("/shorts/"))
                    {
                        string afterShorts = uri.AbsolutePath.Substring(uri.AbsolutePath.IndexOf("/shorts/") + 8);
                        int slashIdx = afterShorts.IndexOf('/');
                        videoId = slashIdx > 0 ? afterShorts.Substring(0, slashIdx) : afterShorts;
                    }
                    else if (normalizedUrl.Contains("v="))
                    {
                        int vIndex = normalizedUrl.IndexOf("v=");
                        int ampIndex = normalizedUrl.IndexOf("&", vIndex);
                        videoId = ampIndex > 0 ? normalizedUrl.Substring(vIndex + 2, ampIndex - (vIndex + 2)) : normalizedUrl.Substring(vIndex + 2);
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

                // If user specified custom filename in the input box, prioritize it
                if (FileNameTextBox != null && !string.IsNullOrWhiteSpace(FileNameTextBox.Text))
                {
                    string customName = FileNamingHelper.SanitizeFileName(FileNameTextBox.Text.Trim());
                    if (!string.IsNullOrWhiteSpace(customName))
                    {
                        fileName = customName;
                    }
                }
            }
            catch
            {
                fileName = "download.dat";
            }

            // Handle custom selected format
            if (FormatComboBox.SelectedItem is ComboBoxItem fItem)
            {
                string fStr = fItem.Content?.ToString() ?? "";
                if (fStr.Contains("MP3", StringComparison.OrdinalIgnoreCase) && !fileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    fileName = Path.ChangeExtension(fileName, ".mp3");
                }
                else if (fStr.Contains("MKV", StringComparison.OrdinalIgnoreCase) && !fileName.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase))
                {
                    fileName = Path.ChangeExtension(fileName, ".mkv");
                }
            }

            string fullSavePath = Path.Combine(targetFolder, fileName);
            string selectedCat = (CategoryComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "General";

            bool startImmediately = AutoStartCheckBox.IsChecked == true && AddToQueueCheckBox.IsChecked != true;

            // Create authoritative download item
            var newDownload = new DownloadItem
            {
                FileName = fileName,
                Url = normalizedUrl,
                SavePath = fullSavePath,
                Category = selectedCat,
                Status = startImmediately ? "Downloading" : "Queued",
                Progress = 0,
                LastTryDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Size = "0 B",
                TransferRate = "0 B/s"
            };

            // Attach handoff metadata from browser extension if available
            if (!string.IsNullOrWhiteSpace(_handoffCookies)) newDownload.Cookies = _handoffCookies;
            if (!string.IsNullOrWhiteSpace(_handoffReferer)) newDownload.Referer = _handoffReferer;
            if (!string.IsNullOrWhiteSpace(_handoffUserAgent)) newDownload.UserAgent = _handoffUserAgent;
            if (!string.IsNullOrWhiteSpace(_handoffAuthHeader)) newDownload.AuthHeader = _handoffAuthHeader;
            if (!string.IsNullOrWhiteSpace(_handoffVideoUrl)) newDownload.VideoUrl = _handoffVideoUrl;
            if (!string.IsNullOrWhiteSpace(_handoffAudioUrl)) newDownload.AudioUrl = _handoffAudioUrl;
            if (_handoffRequiresFfmpegMerge) newDownload.RequiresFfmpegMerge = true;
            if (!string.IsNullOrWhiteSpace(_handoffQuality)) newDownload.Quality = _handoffQuality;
            if (_handoffEstimatedSize > 0 && newDownload.EstimatedSizeBytes <= 0)
            {
                newDownload.EstimatedSizeBytes = _handoffEstimatedSize;
                newDownload.Size = FormatBytes(_handoffEstimatedSize);
            }

            // Attach selected MediaVariantOption if analyzed
            if (QualityComboBox.SelectedItem is ComboBoxItem qItem && qItem.Tag is MediaVariantOption v)
            {
                newDownload.VideoUrl = !string.IsNullOrWhiteSpace(v.DirectUrl) ? v.DirectUrl : normalizedUrl;
                newDownload.AudioUrl = v.AudioStreamUrl ?? string.Empty;
                newDownload.RequiresFfmpegMerge = v.RequiresFfmpegMerge;
                newDownload.Quality = v.QualityLabel;
                newDownload.EstimatedSizeBytes = v.EstimatedSizeBytes;
                newDownload.Container = v.Container;
                newDownload.Codec = v.Codec;
                newDownload.AudioCodec = v.AudioCodec;
                newDownload.IsAudioOnly = v.IsAudioOnly;
                if (v.EstimatedSizeBytes > 0)
                {
                    newDownload.Size = FormatBytes(v.EstimatedSizeBytes);
                }
            }

            // Dispatch to ViewModel / Engine
            if (_viewModel != null)
            {
                _viewModel.AddDownload(newDownload);
            }
            else if (System.Windows.Application.Current?.MainWindow?.DataContext is DownloadManagerViewModel mainVm)
            {
                mainVm.AddDownload(newDownload);
            }

            // Open Progress Window if starting immediately
            if (startImmediately)
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var progressWindow = new DownloadProgressWindow(newDownload);
                        progressWindow.Show();
                        progressWindow.Activate();
                        progressWindow.Focus();
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogException("[AddUrlWindow] Failed to show DownloadProgressWindow", ex);
                    }
                }), System.Windows.Threading.DispatcherPriority.Normal);
            }

            DialogResult = true;
            Close();
        }

        private void ShowError(string msg)
        {
            if (SubmissionStatusText != null)
            {
                SubmissionStatusText.Text = msg;
            }
            else
            {
                System.Windows.MessageBox.Show(msg, "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
