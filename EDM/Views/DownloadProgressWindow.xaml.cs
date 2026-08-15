using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using EDM.Models;
using EDM.Services;
using EDM.Services.Helpers;
using EDM.Settings;
using YoutubeExplode;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Clipboard = System.Windows.Clipboard;

namespace EDM.Views
{
    public partial class DownloadProgressWindow : Window, INotifyPropertyChanged
    {
        private DownloadItem _downloadItem;
        private string _downloadUrl;
        private string _savePath;
        private string _fileName;

        private readonly DownloadService _downloadService;
        private readonly PauseTokenSource _pauseTokenSource;
        private CancellationTokenSource? _cts;
        private readonly ExternalToolsSettings _toolsSettings;
        private readonly ProgressThrottler<DownloadProgressInfo> _progressThrottler;

        private bool _isDetailsHidden = false;
        private volatile bool _isCompleted = false;
        private int _connectionCount = 8;
        private double _currentSpeedLimitBytesPerSec = -1; // -1 = Unlimited

        // Live Real Graph Ring Buffer (last 60 samples)
        private readonly Queue<double> _speedHistory = new Queue<double>(60);
        private const int MaxGraphSamples = 60;
        private double _peakSpeedObserved = 0;

        public ObservableCollection<ConnectionInfo> Connections { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public DownloadProgressWindow(DownloadItem item, int segmentCount = 8)
        {
            InitializeComponent();
            _downloadItem = item ?? new DownloadItem();
            _downloadUrl = item?.Url ?? string.Empty;
            _savePath = item?.SavePath ?? string.Empty;
            _fileName = item?.FileName ?? System.IO.Path.GetFileName(_savePath) ?? "downloaded_file";

            _downloadService = (App.ServiceProvider?.GetService(typeof(EDM.Services.DownloadService)) as DownloadService) ?? new DownloadService();
            _pauseTokenSource = new PauseTokenSource();

            // Initialize UI progress coalescer/throttler (150ms interval, ~6-7 FPS max UI updates to avoid Dispatcher saturation)
            _progressThrottler = new ProgressThrottler<DownloadProgressInfo>(
                targetAction: info => UpdateUI(info),
                throttleInterval: TimeSpan.FromMilliseconds(150),
                isTerminalPredicate: IsTerminalState,
                dispatchAction: action =>
                {
                    if (Dispatcher.CheckAccess())
                    {
                        action();
                    }
                    else
                    {
                        Dispatcher.BeginInvoke(action);
                    }
                }
            );

            _toolsSettings = new ExternalToolsSettings();
            Connections = new ObservableCollection<ConnectionInfo>();
            _connectionCount = Math.Clamp(segmentCount, 1, 32);
            InitializeConnections(_connectionCount);

            this.DataContext = this;

            FileNameText.Text = _fileName;
            UrlSubtitleText.Text = _downloadUrl;
            WindowTitleText.Text = $"0.0% - {_fileName}";
            this.Title = $"0.0% - {_fileName}";

            UpdateFileCategoryIcon(_fileName);

            this.Loaded += DownloadProgressWindow_Loaded;
            this.Closed += DownloadProgressWindow_Closed;
        }

        public DownloadProgressWindow() : this(new DownloadItem(), 8) { }

        private void UpdateFileCategoryIcon(string fileName)
        {
            string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            FileCategoryIconText.Text = ext switch
            {
                ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" or ".flv" => "🎬",
                ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".m4a" => "🎵",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".iso" => "📦",
                ".exe" or ".msi" or ".bat" or ".cmd" => "⚙️",
                ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" => "📄",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" => "🖼️",
                _ => "📁"
            };
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                    this.DragMove();
            }
            catch (Exception ex) { LoggingService.Log($"[DownloadProgressWindow] DragMove error: {ex.Message}"); }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void InitializeConnections(int count)
        {
            Connections.Clear();
            for (int i = 0; i < count; i++)
            {
                Connections.Add(new ConnectionInfo
                {
                    ConnectionNumber = i + 1,
                    ByteRangeText = "Pending...",
                    DownloadedAmount = "0 B",
                    InfoStatus = "Queued",
                    StatusColor = new SolidColorBrush(Color.FromRgb(100, 116, 139)), // Muted slate gray
                    IndividualProgress = 0,
                    ThreadSpeed = "0 B/s"
                });
            }
        }

        public void CancelDownload()
        {
            try { _cts?.Cancel(); } catch (Exception ex) { LoggingService.Log($"[DownloadProgressWindow] CancelDownload failed: {ex.Message}"); }
        }

        private int _isDownloadRunning = 0;

        public async Task StartDownloadForItemAsync(DownloadItem item)
        {
            if (item == null) return;
            _downloadItem = item;
            _downloadUrl = item.Url ?? string.Empty;
            _savePath = item.SavePath ?? string.Empty;
            _fileName = item.FileName ?? System.IO.Path.GetFileName(_savePath) ?? "downloaded_file";

            await Dispatcher.InvokeAsync(() =>
            {
                FileNameText.Text = _fileName;
                UrlSubtitleText.Text = _downloadUrl;
                WindowTitleText.Text = $"0.0% - {_fileName}";
                this.Title = $"0.0% - {_fileName}";
                UpdateFileCategoryIcon(_fileName);
            });

            await StartDownloadProcessAsync();
        }

        private async void DownloadProgressWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Sync theme dynamically with System.Windows.Application.Current
                var appThemeDict = System.Windows.Application.Current.Resources.MergedDictionaries
                    .FirstOrDefault(d => d.Source != null && (d.Source.OriginalString.Contains("DarkTheme") || d.Source.OriginalString.Contains("LightTheme")));
                if (appThemeDict != null)
                {
                    var existing = this.Resources.MergedDictionaries
                        .Where(d => d.Source != null && (d.Source.OriginalString.Contains("DarkTheme") || d.Source.OriginalString.Contains("LightTheme")))
                        .ToList();
                    foreach (var d in existing) this.Resources.MergedDictionaries.Remove(d);
                    this.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = appThemeDict.Source });
                }

                if (!string.IsNullOrWhiteSpace(_downloadUrl) && Interlocked.CompareExchange(ref _isDownloadRunning, 1, 0) == 0)
                {
                    await StartDownloadProcessCoreAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadProgressWindow] Loaded event execution failed", ex);
            }
        }

        private async Task StartDownloadProcessAsync()
        {
            if (Interlocked.CompareExchange(ref _isDownloadRunning, 1, 0) == 0)
            {
                await StartDownloadProcessCoreAsync();
            }
        }

        private async Task StartDownloadProcessCoreAsync()
        {
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            try
            {
                ErrorAlertCard.Visibility = Visibility.Collapsed;
                StatusBadgeText.Text = "Connecting...";
                StatusBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x3E, 0x62));
                StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));

                if (IsYouTubeUrl(_downloadUrl))
                {
                    StatusBadgeText.Text = "Extracting Video Stream...";
                    bool extractedViaYoutubeExplode = false;
                    try
                    {
                        var youtube = new YoutubeClient();
                        var video = await youtube.Videos.GetAsync(_downloadUrl, _cts.Token);
                        string cleanTitle = string.Join("_", video.Title.Split(System.IO.Path.GetInvalidFileNameChars()));
                        _fileName = cleanTitle + ".mp4";

                        await Dispatcher.InvokeAsync(() =>
                        {
                            FileNameText.Text = _fileName;
                            WindowTitleText.Text = $"0.0% - {_fileName}";
                            this.Title = $"0.0% - {_fileName}";
                        });

                        string userDownloadsFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Video");
                        if (!Directory.Exists(userDownloadsFolder)) Directory.CreateDirectory(userDownloadsFolder);
                        _savePath = System.IO.Path.Combine(userDownloadsFolder, _fileName);
                        _downloadItem.FileName = _fileName;
                        _downloadItem.SavePath = _savePath;

                        var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id, _cts.Token);
                        var streamInfo = streamManifest.GetMuxedStreams().OrderByDescending(s => s.VideoQuality.MaxHeight).FirstOrDefault();
                        if (streamInfo != null)
                        {
                            _downloadUrl = streamInfo.Url;
                            extractedViaYoutubeExplode = true;
                        }
                    }
                    catch (Exception ytEx)
                    {
                        LoggingService.LogWarning($"[DownloadProgressWindow] YoutubeExplode extraction failed: {ytEx.Message}. Falling back to yt-dlp engine...");
                    }

                    if (!extractedViaYoutubeExplode)
                    {
                        StatusBadgeText.Text = "Downloading with Turbo Extractor...";
                        var ytDlpService = new YtDlpService();
                        _ = ytDlpService.AutoUpdateEngineAsync(_cts.Token);

                        string userDownloadsFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Video");
                        if (!Directory.Exists(userDownloadsFolder)) Directory.CreateDirectory(userDownloadsFolder);
                        if (string.IsNullOrWhiteSpace(_fileName) || _fileName == "downloaded_file") _fileName = "YouTube_Video.mp4";
                        _savePath = System.IO.Path.Combine(userDownloadsFolder, _fileName);
                        _downloadItem.FileName = _fileName;
                        _downloadItem.SavePath = _savePath;

                        await ytDlpService.DownloadAsync(_downloadUrl, _savePath, "-f bestvideo+bestaudio/best", (pct, status) =>
                        {
                            _progressThrottler.Report(new DownloadProgressInfo
                            {
                                BytesReceived = (long)(pct * 1024 * 1024),
                                TotalBytes = 100 * 1024 * 1024,
                                ProgressPercentage = pct,
                                Status = status,
                                SpeedBytesPerSecond = 10 * 1024 * 1024
                            });
                        }, _cts.Token).ConfigureAwait(false);

                        _isCompleted = true;
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (_downloadItem != null)
                            {
                                _downloadItem.Status = "Completed";
                                _downloadItem.Progress = 100.0;
                            }
                            StatusBadgeText.Text = "Completed";
                            StatusBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x06, 0x4E, 0x3B));
                            StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                            ProgressPercentText.Text = "100.0%";
                            WindowTitleText.Text = $"100.0% - {_fileName}";
                            this.Title = $"100.0% - {_fileName}";
                            TimeLeftText.Text = "Complete";
                            SpeedText.Text = "0 B/s";
                            PauseResumeButton.Content = "✓ Done";
                            PauseResumeButton.IsEnabled = false;
                            OpenFileButton.IsEnabled = true;
                        });
                        return;
                    }
                }

                var progressHandler = new Progress<DownloadProgressInfo>(info => _progressThrottler.Report(info));

                await _downloadService.StartDownloadAsync(
                    _downloadUrl,
                    _savePath,
                    progressHandler,
                    _pauseTokenSource,
                    GetCurrentSpeedLimit,
                    _cts.Token,
                    _connectionCount,
                    _downloadItem?.BuildCredentials(),
                    _downloadItem?.Cookies
                ).ConfigureAwait(false);

                // Verify file exists on disk
                if (File.Exists(_savePath))
                {
                    var fi = new FileInfo(_savePath);
                    LoggingService.Log($"[DownloadProgressWindow] Download verified on disk: '{_savePath}' ({fi.Length} bytes)");
                }

                _isCompleted = true;

                await Dispatcher.InvokeAsync(() =>
                {
                    if (_downloadItem != null)
                    {
                        _downloadItem.Status = "Completed";
                        _downloadItem.Progress = 100.0;
                        _downloadItem.TransferRate = "--";
                        _downloadItem.TimeLeft = "Completed";
                    }
                    StatusBadgeText.Text = "Completed";
                    StatusBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x06, 0x4E, 0x3B));
                    StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                    ProgressPercentText.Text = "100.0%";
                    WindowTitleText.Text = $"100.0% - {_fileName}";
                    this.Title = $"100.0% - {_fileName}";
                    TimeLeftText.Text = "Complete";
                    SpeedText.Text = "0 B/s";
                    ConnectionsCountText.Text = $"0 Active / {_connectionCount} Max (Completed)";
                    if (ProgressBarContainer != null && ProgressIndicator != null)
                    {
                        ProgressIndicator.Width = ProgressBarContainer.ActualWidth > 0 ? ProgressBarContainer.ActualWidth : 350;
                    }
                    PauseResumeButton.Content = "✓ Done";
                    PauseResumeButton.IsEnabled = false;
                    OpenFileButton.IsEnabled = true;
                });
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (_downloadItem != null) _downloadItem.Status = "Cancelled";
                    StatusBadgeText.Text = "Cancelled";
                    StatusBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x1E, 0x22));
                    StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (_downloadItem != null) _downloadItem.Status = "Error";
                    StatusBadgeText.Text = "Error";
                    StatusBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x45, 0x0A, 0x0A));
                    StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                    ShowFormattedError(ex);
                });
            }
            finally
            {
                Interlocked.Exchange(ref _isDownloadRunning, 0);
            }
        }

        private void ShowFormattedError(Exception ex)
        {
            ErrorAlertCard.Visibility = Visibility.Visible;
            string msg = ex.Message;

            if (msg.Contains("401") || msg.Contains("403") || msg.Contains("Unauthorized"))
                ErrorMessageText.Text = "Authentication Required: Remote server denied access. Check credentials or cookies.";
            else if (msg.Contains("Range") || msg.Contains("206"))
                ErrorMessageText.Text = "Server Range Failure: Remote server does not support byte ranges or multi-part downloads.";
            else if (msg.Contains("space") || msg.Contains("disk full"))
                ErrorMessageText.Text = "Insufficient Storage: The destination drive does not have enough free disk space.";
            else if (msg.Contains("Access") || msg.Contains("UnauthorizedAccess"))
                ErrorMessageText.Text = "Access Denied: EDM cannot write to the selected destination folder.";
            else if (msg.Contains("timed out") || msg.Contains("reset") || msg.Contains("SocketException"))
                ErrorMessageText.Text = "Connection Interrupted: Remote server reset the socket or network timed out.";
            else
                ErrorMessageText.Text = $"Download Failed: {msg}";
        }

        private static bool IsTerminalState(DownloadProgressInfo info)
        {
            if (info == null) return false;
            if (info.IsCompleted) return true;
            if (!string.IsNullOrEmpty(info.ErrorMessage)) return true;

            if (!string.IsNullOrEmpty(info.Status))
            {
                var s = info.Status.Trim();
                if (string.Equals(s, "Finished", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "Completed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "Complete", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "Error", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "Failed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsYouTubeUrl(string url)
        {
            return !string.IsNullOrEmpty(url) && (url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) || url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase));
        }

        public double GetCurrentSpeedLimit() => _currentSpeedLimitBytesPerSec;

        private void UpdateUI(DownloadProgressInfo info)
        {
            if (_isCompleted) return;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => UpdateUI(info)));
                return;
            }

            // 1. Update Status Badge
            if (!string.IsNullOrWhiteSpace(info.Status) && !_pauseTokenSource.IsPaused)
            {
                StatusBadgeText.Text = info.Status;
                _downloadItem.Status = info.Status;
            }

            // 2. Update Progress Percentage & High-Res Smooth Progress Bar
            double progressVal = Math.Clamp(info.ProgressPercentage, 0.0, 100.0);
            ProgressPercentText.Text = $"{progressVal:F1}%";
            WindowTitleText.Text = $"{progressVal:F1}% - {_fileName}";
            this.Title = $"{progressVal:F1}% - {_fileName}";
            _downloadItem.Progress = progressVal;

            if (ProgressBarContainer != null && ProgressIndicator != null)
            {
                double totalW = ProgressBarContainer.ActualWidth > 0 ? ProgressBarContainer.ActualWidth : 350;
                ProgressIndicator.Width = totalW * (progressVal / 100.0);
            }

            // 3. Update File Size & Downloaded Bytes
            if (info.TotalBytes.HasValue && info.TotalBytes.Value > 0)
            {
                long total = info.TotalBytes.Value;
                DownloadedText.Text = $"{FormatBytes(info.BytesReceived)} of {FormatBytes(total)}";
                _downloadItem.Size = FormatBytes(total);
            }
            else
            {
                DownloadedText.Text = $"{FormatBytes(info.BytesReceived)} (Unknown Size)";
            }

            // 4. Update Time Remaining (ETA)
            if (info.RemainingSeconds > 0 && !double.IsInfinity(info.RemainingSeconds))
            {
                TimeLeftText.Text = FormatTime(info.RemainingSeconds);
                _downloadItem.TimeLeft = TimeLeftText.Text;
            }
            else if (progressVal >= 100.0)
            {
                TimeLeftText.Text = "Complete";
            }
            else
            {
                TimeLeftText.Text = "Calculating...";
            }

            // 5. Update Speed & Throughput KPIs
            double curSpeed = Math.Max(0, info.SpeedBytesPerSecond);
            double avgSpeed = Math.Max(0, info.AverageSpeedBytesPerSecond > 0 ? info.AverageSpeedBytesPerSecond : curSpeed);
            if (curSpeed > _peakSpeedObserved) _peakSpeedObserved = curSpeed;

            SpeedText.Text = $"{FormatBytes((long)curSpeed)}/s";
            SpeedAvgText.Text = $"{FormatBytes((long)avgSpeed)}/s";
            SpeedPeakText.Text = $"{FormatBytes((long)_peakSpeedObserved)}/s";
            _downloadItem.TransferRate = SpeedText.Text;

            // 6. Update Connection Count & Resume Capability
            int activeConns = info.ActiveConnections > 0 ? info.ActiveConnections : (info.SegmentCount > 0 ? info.SegmentCount : _connectionCount);
            ConnectionsCountText.Text = $"{activeConns} Active / {_connectionCount} Max";
            ResumeCapabilityText.Text = info.ServerSupportsResume ? " • Resume: Yes (206)" : " • Resume: No";

            // 7. Update Live Real Throughput Graph
            UpdateTransferGraph(curSpeed, avgSpeed, _peakSpeedObserved);

            // 8. Update Real Segment Telemetry Table
            UpdateSegmentRows(info);
        }

        private void UpdateTransferGraph(double curSpeed, double avgSpeed, double peakSpeed)
        {
            if (TransferGraphCanvas == null) return;

            // Add sample to ring buffer
            if (_speedHistory.Count >= MaxGraphSamples)
            {
                _speedHistory.Dequeue();
            }
            _speedHistory.Enqueue(curSpeed);

            double canvasWidth = TransferGraphCanvas.ActualWidth > 0 ? TransferGraphCanvas.ActualWidth : 400;
            double canvasHeight = TransferGraphCanvas.ActualHeight > 0 ? TransferGraphCanvas.ActualHeight : 80;

            TransferGraphCanvas.Children.Clear();

            GraphPeakOverlayText.Text = $"Peak: {FormatBytes((long)peakSpeed)}/s";

            if (_speedHistory.Count < 2) return;

            double maxScale = Math.Max(peakSpeed * 1.15, 1024 * 100); // at least 100 KB/s scale
            double stepX = canvasWidth / (MaxGraphSamples - 1);

            var polylinePoints = new PointCollection();
            var polygonPoints = new PointCollection { new Point(0, canvasHeight) };

            int index = 0;
            int offset = MaxGraphSamples - _speedHistory.Count;

            foreach (var speed in _speedHistory)
            {
                double x = (offset + index) * stepX;
                double normalizedY = Math.Clamp(speed / maxScale, 0.0, 1.0);
                double y = canvasHeight - (normalizedY * (canvasHeight - 6)) - 2;

                var pt = new Point(x, y);
                polylinePoints.Add(pt);
                polygonPoints.Add(pt);
                index++;
            }

            polygonPoints.Add(new Point((offset + index - 1) * stepX, canvasHeight));

            // Fill under curve
            var fillPolygon = new Polygon
            {
                Points = polygonPoints,
                Fill = new LinearGradientBrush(
                    Color.FromArgb(0x80, 0x63, 0x66, 0xF1),
                    Color.FromArgb(0x05, 0x63, 0x66, 0xF1),
                    new Point(0, 0),
                    new Point(0, 1)
                )
            };
            TransferGraphCanvas.Children.Add(fillPolygon);

            // Top stroke line
            var strokeLine = new Polyline
            {
                Points = polylinePoints,
                Stroke = new SolidColorBrush(Color.FromRgb(0x81, 0x8C, 0xF8)),
                StrokeThickness = 2
            };
            TransferGraphCanvas.Children.Add(strokeLine);

            // Rolling Average horizontal line
            if (avgSpeed > 0)
            {
                double avgNormalizedY = Math.Clamp(avgSpeed / maxScale, 0.0, 1.0);
                double avgY = canvasHeight - (avgNormalizedY * (canvasHeight - 6)) - 2;

                var avgLine = new Line
                {
                    X1 = 0,
                    Y1 = avgY,
                    X2 = canvasWidth,
                    Y2 = avgY,
                    Stroke = new SolidColorBrush(Color.FromArgb(0xAA, 0xF5, 0x9E, 0x0B)),
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    StrokeThickness = 1.2
                };
                TransferGraphCanvas.Children.Add(avgLine);
            }
        }

        private void UpdateSegmentRows(DownloadProgressInfo info)
        {
            if (info.ChunkStats != null && info.ChunkStats.Count > 0)
            {
                var stats = info.ChunkStats;
                while (Connections.Count < stats.Count)
                {
                    Connections.Add(new ConnectionInfo { ConnectionNumber = Connections.Count + 1 });
                }

                foreach (var kvp in stats)
                {
                    int idx = kvp.Key;
                    var stat = kvp.Value;
                    if (idx >= 0 && idx < Connections.Count)
                    {
                        var conn = Connections[idx];
                        conn.ConnectionNumber = idx + 1;
                        conn.TargetBytes = stat.TotalBytes;
                        conn.RealDownloadedBytes = stat.Downloaded;
                        conn.DownloadedAmount = $"{FormatBytes(stat.Downloaded)}";
                        conn.ByteRangeText = stat.TotalBytes > 0 ? $"{FormatBytes(stat.TotalBytes)}" : "--";

                        int pct = stat.TotalBytes > 0 ? (int)Math.Clamp(Math.Round((stat.Downloaded / (double)stat.TotalBytes) * 100.0), 0, 100) : 0;
                        conn.IndividualProgress = pct;

                        if (_pauseTokenSource.IsPaused)
                        {
                            conn.InfoStatus = "Paused";
                            conn.StatusColor = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                            conn.ThreadSpeed = "0 B/s";
                        }
                        else if (stat.Downloaded >= stat.TotalBytes && stat.TotalBytes > 0)
                        {
                            conn.InfoStatus = "Completed";
                            conn.StatusColor = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                            conn.ThreadSpeed = "--";
                        }
                        else if (stat.IsActive)
                        {
                            conn.InfoStatus = "Transferring";
                            conn.StatusColor = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
                            double segSpeed = info.SpeedBytesPerSecond / Math.Max(1, info.ActiveConnections);
                            conn.ThreadSpeed = $"{FormatBytes((long)segSpeed)}/s";
                        }
                        else
                        {
                            conn.InfoStatus = "Queued";
                            conn.StatusColor = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                            conn.ThreadSpeed = "0 B/s";
                        }
                    }
                }
            }
            else if (info.SegmentBytes != null && info.SegmentBytes.Length > 0)
            {
                for (int i = 0; i < info.SegmentBytes.Length && i < Connections.Count; i++)
                {
                    var conn = Connections[i];
                    long downloaded = info.SegmentBytes[i];
                    conn.RealDownloadedBytes = downloaded;
                    conn.DownloadedAmount = FormatBytes(downloaded);
                    if (_pauseTokenSource.IsPaused)
                    {
                        conn.InfoStatus = "Paused";
                        conn.StatusColor = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                    }
                    else
                    {
                        conn.InfoStatus = "Active";
                        conn.StatusColor = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                    }
                }
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_pauseTokenSource.IsPaused)
                {
                    _pauseTokenSource.Pause();
                    PauseResumeButton.Content = "▶ Resume";
                    StatusBadgeText.Text = "Paused";
                    StatusBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x45, 0x27, 0x0A));
                    StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                    SpeedText.Text = "0 B/s";
                    _downloadItem.Status = "Paused";
                }
                else
                {
                    _pauseTokenSource.Resume();
                    PauseResumeButton.Content = "⏸ Pause";
                    StatusBadgeText.Text = "Downloading...";
                    StatusBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x3E, 0x62));
                    StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
                    _downloadItem.Status = "Downloading";
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadProgressWindow] Pause toggle failed", ex);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CancelDownload();
                this.Close();
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadProgressWindow] Cancel failed", ex);
            }
        }

        private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_downloadUrl))
                {
                    Clipboard.SetText(_downloadUrl);
                    CopyUrlButton.Content = "✓ Copied!";
                    Task.Delay(1500).ContinueWith(_ => Dispatcher.Invoke(() => CopyUrlButton.Content = "📋 Copy URL"));
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadProgressWindow] Copy URL failed", ex);
            }
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_savePath) && File.Exists(_savePath))
                {
                    Process.Start(new ProcessStartInfo(_savePath) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadProgressWindow] Open File failed", ex);
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_savePath))
                {
                    if (File.Exists(_savePath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{_savePath}\"");
                    }
                    else
                    {
                        string dir = System.IO.Path.GetDirectoryName(_savePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        if (Directory.Exists(dir)) Process.Start("explorer.exe", $"\"{dir}\"");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadProgressWindow] Open Folder failed", ex);
            }
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            _isCompleted = false;
            ErrorAlertCard.Visibility = Visibility.Collapsed;
            StatusBadgeText.Text = "Reconnecting...";
            StatusBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x3E, 0x62));
            StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
            PauseResumeButton.Content = "⏸ Pause";
            PauseResumeButton.IsEnabled = true;
            _ = StartDownloadProcessAsync();
        }

        private void ToggleConnectionsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ConnectionsListView == null) return;
                if (ConnectionsListView.Visibility == Visibility.Visible)
                {
                    ConnectionsListView.Visibility = Visibility.Collapsed;
                    ToggleConnectionsButton.Content = "Show Details";
                    _isDetailsHidden = true;
                }
                else
                {
                    ConnectionsListView.Visibility = Visibility.Visible;
                    ToggleConnectionsButton.Content = "Hide Details";
                    _isDetailsHidden = false;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadProgressWindow] ToggleConnections failed", ex);
            }
        }

        private void SpeedLimitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SpeedLimitComboBox.SelectedItem is ComboBoxItem item)
            {
                string tag = item.Content.ToString() ?? "Unlimited";
                int limitKbps = 0;

                _currentSpeedLimitBytesPerSec = tag switch
                {
                    "100 KB/s" => (limitKbps = 100) * 1024.0,
                    "250 KB/s" => (limitKbps = 250) * 1024.0,
                    "500 KB/s" => (limitKbps = 500) * 1024.0,
                    "1 MB/s" => (limitKbps = 1024) * 1024.0,
                    "2 MB/s" => (limitKbps = 2048) * 1024.0,
                    "5 MB/s" => (limitKbps = 5120) * 1024.0,
                    "10 MB/s" => (limitKbps = 10240) * 1024.0,
                    _ => -1 // Unlimited
                };

                // Immediately update process-wide BandwidthThrottler
                BandwidthThrottler.Instance.SetLimit(limitKbps);

                LoggingService.Log($"[DownloadProgressWindow] Speed limit updated: {tag} ({_currentSpeedLimitBytesPerSec} B/s)");
            }
        }

        private void DownloadProgressWindow_Closed(object? sender, EventArgs e)
        {
            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
                _progressThrottler?.Dispose();
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DownloadProgressWindow] Cleanup on Closed failed: {ex.Message}");
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "0 B";
            string[] suffix = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double dblSbyte = bytes;
            while (dblSbyte >= 1024 && i < suffix.Length - 1)
            {
                dblSbyte /= 1024.0;
                i++;
            }
            return $"{dblSbyte:F2} {suffix[i]}";
        }

        private static string FormatTime(double seconds)
        {
            if (double.IsInfinity(seconds) || double.IsNaN(seconds) || seconds <= 0) return "Calculating...";
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            if (time.TotalHours >= 1)
                return $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
            return $"{time.Minutes:D2}:{time.Seconds:D2}";
        }
    }

    public class ConnectionInfo : INotifyPropertyChanged
    {
        private string _byteRangeText = "--";
        private string _downloadedAmount = "0 B";
        private string _infoStatus = "Queued";
        private SolidColorBrush _statusColor = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        private int _individualProgress = 0;
        private string _threadSpeed = "0 B/s";
        private bool _isPeakThread = false;

        public long TargetBytes { get; set; } = 0;
        public long RealDownloadedBytes { get; set; } = 0;

        public int ConnectionNumber { get; set; }
        public string ByteRangeText { get => _byteRangeText; set { _byteRangeText = value; OnPropertyChanged(); } }
        public string DownloadedAmount { get => _downloadedAmount; set { _downloadedAmount = value; OnPropertyChanged(); } }
        public string InfoStatus { get => _infoStatus; set { _infoStatus = value; OnPropertyChanged(); } }
        public SolidColorBrush StatusColor { get => _statusColor; set { _statusColor = value; OnPropertyChanged(); } }
        public int IndividualProgress { get => _individualProgress; set { _individualProgress = value; OnPropertyChanged(); } }
        public string ThreadSpeed { get => _threadSpeed; set { _threadSpeed = value; OnPropertyChanged(); } }
        public bool IsPeakThread { get => _isPeakThread; set { _isPeakThread = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
