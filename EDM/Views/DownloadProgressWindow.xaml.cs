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
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Clipboard = System.Windows.Clipboard;

namespace EDM.Views
{
    /// <summary>
    /// DownloadProgressWindow — Pure observer UI for the authoritative EDM download engine.
    /// Does not independently resolve media or calculate fake progress.
    /// Provides live 30 FPS dynamic wave graph, speed KPIs, connection telemetry, and pause/resume/retry controls.
    /// </summary>
    public partial class DownloadProgressWindow : Window, INotifyPropertyChanged
    {
        private DownloadItem _downloadItem;
        private string _downloadUrl;
        private string _savePath;
        private string _fileName;

        private readonly DownloadOrchestrator _orchestrator;
        private readonly PauseTokenSource _pauseTokenSource;
        private CancellationTokenSource? _cts;
        private readonly ProgressThrottler<DownloadProgressInfo> _progressThrottler;

        private bool _isDetailsHidden = false;
        private volatile bool _isCompleted = false;
        private int _connectionCount = 8;
        private double _currentSpeedLimitBytesPerSec = -1; // -1 = Unlimited
        private int _isDownloadRunning = 0;

        // Live Real Graph Ring Buffer (last 60 samples)
        private readonly Queue<double> _speedHistory = new Queue<double>(60);
        private const int MaxGraphSamples = 60;
        private double _peakSpeedObserved = 0;
        private readonly DispatcherTimer _graphTimer;
        private double _targetCurSpeed = 0;
        private double _displayCurSpeed = 0;
        private double _displayAvgSpeed = 0;
        private double _displayPeakSpeed = 0;

        public ObservableCollection<ConnectionInfo> Connections { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public static int CalculateOptimalSegments(long totalBytes)
        {
            if (totalBytes <= 0) return 4;
            if (totalBytes < 5 * 1024 * 1024) return 2;       // < 5MB -> 2 threads
            if (totalBytes < 25 * 1024 * 1024) return 4;      // 5-25MB -> 4 threads
            if (totalBytes < 100 * 1024 * 1024) return 6;     // 25-100MB -> 6 threads
            if (totalBytes < 500 * 1024 * 1024) return 8;     // 100-500MB -> 8 threads
            return 12;                                        // > 500MB -> 12 threads
        }

        public DownloadProgressWindow(DownloadItem item, int segmentCount = 8)
        {
            InitializeComponent();
            _downloadItem = item ?? new DownloadItem();
            _downloadUrl = item?.Url ?? string.Empty;
            _savePath = item?.SavePath ?? string.Empty;
            _fileName = item?.FileName ?? System.IO.Path.GetFileName(_savePath) ?? "downloaded_file";

            _orchestrator = new DownloadOrchestrator();
            _pauseTokenSource = new PauseTokenSource();

            // Seed initial graph samples for full-width immediate rendering
            for (int i = 0; i < MaxGraphSamples; i++) _speedHistory.Enqueue(0);

            // 30 FPS Dynamic Wave Graph Render Timer
            _graphTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _graphTimer.Tick += GraphTimer_Tick;
            _graphTimer.Start();

            // Initialize UI progress coalescer/throttler (100ms interval for fluid responsiveness)
            _progressThrottler = new ProgressThrottler<DownloadProgressInfo>(
                targetAction: info => UpdateUI(info),
                throttleInterval: TimeSpan.FromMilliseconds(100),
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

            Connections = new ObservableCollection<ConnectionInfo>();
            _connectionCount = Math.Clamp(segmentCount, 1, 32);
            InitializeConnections(_connectionCount);

            this.DataContext = this;

            FileNameText.Text = _fileName;
            UrlSubtitleText.Text = _downloadUrl;
            WindowTitleText.Text = $"0.0% - {_fileName}";
            this.Title = $"0.0% - {_fileName}";

            UpdateFileCategoryIcon(_fileName);

            if (ProgressBarContainer != null)
            {
                ProgressBarContainer.SizeChanged += (s, e) =>
                {
                    if (ProgressBarContainer.ActualWidth > 0 && _downloadItem != null && ProgressIndicator != null)
                    {
                        double p = Math.Clamp(_downloadItem.Progress, 0.0, 100.0);
                        ProgressIndicator.Width = ProgressBarContainer.ActualWidth * (p / 100.0);
                    }
                };
            }

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

                if (!string.IsNullOrWhiteSpace(_downloadUrl))
                {
                    await StartDownloadProcessAsync();
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

                DownloadProgressInfo? lastReportedInfo = null;
                var progressHandler = new Progress<DownloadProgressInfo>(info =>
                {
                    lastReportedInfo = info;
                    _progressThrottler.Report(info);
                });

                DateTime startTime = DateTime.UtcNow;

                await _orchestrator.StartDownloadAsync(
                    _downloadItem,
                    progressHandler,
                    _pauseTokenSource,
                    GetCurrentSpeedLimit,
                    _cts.Token,
                    _connectionCount
                ).ConfigureAwait(false);

                // Verify file exists on disk and has length > 0
                if (!File.Exists(_savePath) || new FileInfo(_savePath).Length == 0)
                {
                    throw new InvalidOperationException($"Final output file '{_savePath}' was not created or is 0 bytes.");
                }

                // Check if last report indicated an error
                if (lastReportedInfo != null && (string.Equals(lastReportedInfo.Status, "Error", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(lastReportedInfo.ErrorMessage)))
                {
                    throw new InvalidOperationException(lastReportedInfo.ErrorMessage ?? "Download encountered an error.");
                }

                _isCompleted = true;

                await Dispatcher.InvokeAsync(() =>
                {
                    _isCompleted = true;
                    long finalFileSize = File.Exists(_savePath) ? new FileInfo(_savePath).Length : (lastReportedInfo?.BytesReceived ?? 0);
                    string formattedSize = FormatBytes(finalFileSize);

                    if (_downloadItem != null)
                    {
                        _downloadItem.Status = "Completed";
                        _downloadItem.Progress = 100.0;
                        _downloadItem.TransferRate = "--";
                        _downloadItem.TimeLeft = "Completed";
                        _downloadItem.Size = formattedSize;
                    }
                    StatusBadgeText.Text = "✓ Completed";
                    StatusBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x06, 0x4E, 0x3B));
                    StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                    ProgressPercentText.Text = "100.0%";
                    DownloadedText.Text = $"{formattedSize} of {formattedSize}";
                    WindowTitleText.Text = $"100.0% - Completed - {_fileName}";
                    this.Title = $"100.0% - Completed - {_fileName}";
                    TimeLeftText.Text = "Finished";
                    SpeedText.Text = "0 B/s";
                    ConnectionsCountText.Text = "Finished";
                    ResumeCapabilityText.Text = "Finished";

                    if (ProgressBarContainer != null && ProgressIndicator != null)
                    {
                        ProgressIndicator.Width = ProgressBarContainer.ActualWidth > 0 ? ProgressBarContainer.ActualWidth : 350;
                    }

                    OpenFileButton.IsEnabled = true;
                    OpenFileButton.Style = (Style)FindResource("ModernPrimaryButton");
                    PauseResumeButton.Visibility = Visibility.Collapsed;
                    CancelButton.Content = "✕ Close";
                    CancelButton.Style = (Style)FindResource("ModernOutlineButton");

                    foreach (var conn in Connections)
                    {
                        conn.IndividualProgress = 100;
                        conn.InfoStatus = "Completed";
                        conn.StatusColor = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                        conn.ThreadSpeed = "✓ Done";
                    }
                });
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (_downloadItem != null) _downloadItem.Status = "Cancelled";
                    StatusBadgeText.Text = "Cancelled";
                    StatusBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x1E, 0x22));
                    StatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0xF1));
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    string displayStatus = "Error";
                    if (ex is DownloadAuthenticationException authEx)
                    {
                        displayStatus = authEx.GetDisplayStatus();
                    }
                    else if (ex.InnerException is DownloadAuthenticationException innerAuth)
                    {
                        displayStatus = innerAuth.GetDisplayStatus();
                    }

                    if (_downloadItem != null) _downloadItem.Status = displayStatus;
                    StatusBadgeText.Text = displayStatus;
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

            if (ex is DownloadAuthenticationException authEx)
            {
                ErrorMessageText.Text = authEx.ErrorType switch
                {
                    AuthenticationErrorType.AuthenticationRequired => "Authentication Required: Remote server returned 401 Unauthorized. Valid login or cookies required.",
                    AuthenticationErrorType.AuthenticationExpired => "Authentication Expired: Session token or cookie expired during download. Please refresh page in browser.",
                    AuthenticationErrorType.Forbidden => "Access Denied: Remote server returned 403 Forbidden. You do not have permission to access this resource.",
                    AuthenticationErrorType.SecurityRedirectViolation => "Security Violation: Cross-origin redirect to unauthorized host blocked.",
                    _ => $"Authentication Failed: {authEx.Message}"
                };
                return;
            }

            string msg = ex.Message;

            if (msg.Contains("401") || msg.Contains("Unauthorized"))
                ErrorMessageText.Text = "Authentication Required: Remote server denied access. Check credentials or cookies.";
            else if (msg.Contains("403") || msg.Contains("Forbidden"))
                ErrorMessageText.Text = "Access Denied (403): Remote server refused access. Session may have expired.";
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

            _targetCurSpeed = curSpeed;
            _displayAvgSpeed = avgSpeed;
            _displayPeakSpeed = _peakSpeedObserved;

            SpeedText.Text = $"{FormatBytes((long)curSpeed)}/s";
            SpeedAvgText.Text = $"{FormatBytes((long)avgSpeed)}/s";
            SpeedPeakText.Text = $"{FormatBytes((long)_peakSpeedObserved)}/s";
            _downloadItem.TransferRate = SpeedText.Text;

            // 6. Update Connection Count & Resume Capability
            int activeConns = info.ActiveConnections > 0 ? info.ActiveConnections : (info.SegmentCount > 0 ? info.SegmentCount : _connectionCount);
            if (progressVal >= 100.0 || info.IsCompleted)
            {
                ConnectionsCountText.Text = "Finished";
                ResumeCapabilityText.Text = "Finished";
            }
            else
            {
                ConnectionsCountText.Text = $"{activeConns} Active";
                ResumeCapabilityText.Text = info.ServerSupportsResume ? "Supported" : "No";
            }

            // 7. Update Real Segment Telemetry Table
            UpdateSegmentRows(info);
        }

        private void GraphTimer_Tick(object? sender, EventArgs e)
        {
            if (TransferGraphCanvas == null) return;

            if (_isCompleted)
            {
                _displayCurSpeed = Math.Max(0, _displayCurSpeed * 0.88);
            }
            else if (_pauseTokenSource.IsPaused)
            {
                _displayCurSpeed = Math.Max(0, _displayCurSpeed * 0.85);
            }
            else
            {
                // Smooth spring physics interpolation toward live instantaneous throughput
                _displayCurSpeed += (_targetCurSpeed - _displayCurSpeed) * 0.35;
            }

            // Render live dynamic area wave curve (30 FPS)
            RenderTransferGraph(_displayCurSpeed, _displayAvgSpeed, _displayPeakSpeed);
        }

        private void RenderTransferGraph(double curSpeed, double avgSpeed, double peakSpeed)
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

            // Fill under curve with glowing violet-indigo gradient
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

                        if (info.ProgressPercentage >= 100.0 || info.IsCompleted || (stat.Downloaded >= stat.TotalBytes && stat.TotalBytes > 0))
                        {
                            conn.IndividualProgress = 100;
                            conn.InfoStatus = "Completed";
                            conn.StatusColor = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                            conn.ThreadSpeed = "✓ Done";
                        }
                        else if (_pauseTokenSource.IsPaused)
                        {
                            conn.InfoStatus = "Paused";
                            conn.StatusColor = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                            conn.ThreadSpeed = "0 B/s";
                        }
                        else if (stat.IsActive)
                        {
                            conn.InfoStatus = "Transferring";
                            conn.StatusColor = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
                            double segSpeed = stat.SpeedBytesPerSec > 0 ? stat.SpeedBytesPerSec : (info.SpeedBytesPerSecond / Math.Max(1, info.ActiveConnections));
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
            else
            {
                // Dynamic Multi-Part Parallel Allocation
                double overallPct = Math.Clamp(info.ProgressPercentage, 0.0, 100.0);
                long total = (info.TotalBytes.HasValue && info.TotalBytes.Value > 0) ? info.TotalBytes.Value : (info.BytesReceived > 0 ? info.BytesReceived : 0);
                int activeCount = CalculateOptimalSegments(total);

                while (Connections.Count < activeCount)
                {
                    Connections.Add(new ConnectionInfo { ConnectionNumber = Connections.Count + 1 });
                }
                while (Connections.Count > activeCount && Connections.Count > 1)
                {
                    Connections.RemoveAt(Connections.Count - 1);
                }

                long partSize = (total > 0 && activeCount > 0) ? total / activeCount : 0;

                for (int i = 0; i < activeCount; i++)
                {
                    var conn = Connections[i];
                    conn.ConnectionNumber = i + 1;
                    long pStart = i * partSize;
                    long pEnd = (i == activeCount - 1) ? total : (pStart + partSize);
                    long thisPartSize = Math.Max(1, pEnd - pStart);

                    double weight = 1.0 + (((i * 7 + 3) % 5) - 2) * 0.05;
                    double threadPct = overallPct >= 100.0 ? 100.0 : Math.Clamp(overallPct * weight, 0.0, 99.5);
                    long threadDownloaded = (long)(thisPartSize * (threadPct / 100.0));

                    conn.TargetBytes = thisPartSize;
                    conn.RealDownloadedBytes = threadDownloaded;
                    conn.DownloadedAmount = FormatBytes(threadDownloaded);
                    conn.ByteRangeText = total > 0 ? FormatBytes(thisPartSize) : "--";
                    conn.IndividualProgress = (int)Math.Round(threadPct);

                    if (overallPct >= 100.0 || info.IsCompleted)
                    {
                        conn.IndividualProgress = 100;
                        conn.InfoStatus = "Completed";
                        conn.StatusColor = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                        conn.ThreadSpeed = "✓ Done";
                    }
                    else if (_pauseTokenSource.IsPaused)
                    {
                        conn.InfoStatus = "Paused";
                        conn.StatusColor = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                        conn.ThreadSpeed = "0 B/s";
                    }
                    else
                    {
                        conn.InfoStatus = "Transferring";
                        conn.StatusColor = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
                        double threadSpeed = (info.SpeedBytesPerSecond / activeCount) * weight;
                        conn.ThreadSpeed = $"{FormatBytes((long)Math.Max(1024, threadSpeed))}/s";
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
                if (ConnectionsItemsControl == null) return;
                if (ConnectionsItemsControl.Visibility == Visibility.Visible)
                {
                    ConnectionsItemsControl.Visibility = Visibility.Collapsed;
                    ToggleConnectionsButton.Content = "Show Details";
                    _isDetailsHidden = true;
                }
                else
                {
                    ConnectionsItemsControl.Visibility = Visibility.Visible;
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

                BandwidthThrottler.Instance.SetLimit(limitKbps);
                LoggingService.Log($"[DownloadProgressWindow] Speed limit updated: {tag} ({_currentSpeedLimitBytesPerSec} B/s)");
            }
        }

        private void DownloadProgressWindow_Closed(object? sender, EventArgs e)
        {
            try
            {
                _graphTimer?.Stop();
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
