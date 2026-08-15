using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using EDM.Services;

namespace EDM.Views
{
    public partial class SiteGrabberWizardWindow : Window
    {
        private int _currentStep = 1;
        private readonly List<string> _discoveredUrls = new();
        private readonly List<string> _filteredUrls = new();
        private CancellationTokenSource? _crawlerCts;

        public Action<List<string>, string>? OnGrabberFinished { get; set; }

        public SiteGrabberWizardWindow()
        {
            InitializeComponent();
            UpdateStepVisibility();
        }

        private void OnPrevStep(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 1)
            {
                _currentStep--;
                UpdateStepVisibility();
            }
        }

        private async void OnNextStep(object sender, RoutedEventArgs e)
        {
            if (_currentStep == 1)
            {
                string url = StartUrlBox.Text.Trim();
                if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
                {
                    System.Windows.MessageBox.Show("Please enter a valid start URL (e.g. https://example.com/gallery).", "Invalid URL", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                NextBtn.IsEnabled = false;
                NextBtn.Content = "Scanning...";

                _crawlerCts = new CancellationTokenSource();
                try
                {
                    int depth = CrawlDepthCombo.SelectedIndex + 1;
                    var grabber = new SiteGrabberService();

                    var options = new GrabberScanOptions
                    {
                        MaxDepth = depth,
                        SameDomainOnly = StayOnDomainCheck.IsChecked == true
                    };

                    var scannedItems = await grabber.ScanSiteAsync(url, options, progress: null, _crawlerCts.Token).ConfigureAwait(true);

                    _discoveredUrls.Clear();
                    foreach (var item in scannedItems)
                    {
                        if (!string.IsNullOrWhiteSpace(item.Url) && !_discoveredUrls.Contains(item.Url))
                        {
                            _discoveredUrls.Add(item.Url);
                        }
                    }

                    DiscoveryList.ItemsSource = null;
                    DiscoveryList.ItemsSource = _discoveredUrls;

                    _currentStep = 2;
                    UpdateStepVisibility();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error while crawling target URL: {ex.Message}", "Crawl Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    NextBtn.IsEnabled = true;
                    NextBtn.Content = "Next ›";
                }
            }
            else if (_currentStep == 2)
            {
                _currentStep = 3;
                UpdateStepVisibility();
            }
            else if (_currentStep == 3)
            {
                // Apply filters
                string exts = FilterExtBox.Text.Trim();
                var allowedExts = exts.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(e => e.StartsWith(".") ? e : "." + e)
                                      .ToHashSet(StringComparer.OrdinalIgnoreCase);

                string regexPattern = FilterRegexBox.Text.Trim();
                Regex? filterRegex = null;
                if (!string.IsNullOrEmpty(regexPattern))
                {
                    try { filterRegex = new Regex(regexPattern, RegexOptions.IgnoreCase); }
                    catch { /* ignore invalid regex */ }
                }

                _filteredUrls.Clear();
                foreach (var u in _discoveredUrls)
                {
                    string ext = Path.GetExtension(u);
                    bool matchExt = allowedExts.Count == 0 || allowedExts.Contains(ext);
                    bool matchRegex = filterRegex == null || filterRegex.IsMatch(u);

                    if (matchExt && matchRegex)
                    {
                        _filteredUrls.Add(u);
                    }
                }

                SummaryText.Text = $"Discovered {_discoveredUrls.Count} total assets. {_filteredUrls.Count} matched filter criteria.";
                _currentStep = 4;
                UpdateStepVisibility();
            }
            else if (_currentStep == 4)
            {
                string targetQueue = (TargetQueueCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Default Queue";
                var itemsToIngest = _filteredUrls.Count > 0 ? _filteredUrls : _discoveredUrls;

                // Queue to real history / download engine
                string baseDir = DownloadPathCategoryService.GetDefaultBasePath();
                foreach (var assetUrl in itemsToIngest)
                {
                    string fileName = string.Empty;
                    if (Uri.TryCreate(assetUrl, UriKind.Absolute, out var uri))
                    {
                        fileName = Path.GetFileName(uri.AbsolutePath);
                    }
                    if (string.IsNullOrWhiteSpace(fileName)) fileName = "asset_" + Guid.NewGuid().ToString("N")[..8];
                    string savePath = Path.Combine(baseDir, fileName);
                    Services.History.DownloadHistoryRecorder.CreateEntry(assetUrl, savePath, -1);
                }

                OnGrabberFinished?.Invoke(itemsToIngest, targetQueue);
                DialogResult = true;
                Close();
            }
        }

        private void UpdateStepVisibility()
        {
            PageStep1.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            PageStep2.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            PageStep3.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
            PageStep4.Visibility = _currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;

            PrevBtn.IsEnabled = _currentStep > 1;
            NextBtn.Content = _currentStep == 4 ? "Start Downloading" : "Next ›";

            // Update Header labels
            Step1Label.FontWeight = _currentStep == 1 ? FontWeights.Bold : FontWeights.Normal;
            Step2Label.FontWeight = _currentStep == 2 ? FontWeights.Bold : FontWeights.Normal;
            Step3Label.FontWeight = _currentStep == 3 ? FontWeights.Bold : FontWeights.Normal;
            Step4Label.FontWeight = _currentStep == 4 ? FontWeights.Bold : FontWeights.Normal;
        }

        protected override void OnClosed(EventArgs e)
        {
            _crawlerCts?.Cancel();
            base.OnClosed(e);
        }
    }
}
