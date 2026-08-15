using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using EDM.ViewModels;
// Alias to resolve Point ambiguity between System.Drawing.Point and System.Windows.Point
using WpfPoint = System.Windows.Point;

namespace EDM.Views
{
    /// <summary>
    /// Sidebar UserControl — Left navigation panel with categories and real-time speed graph
    /// </summary>
    public partial class Sidebar : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(DownloadManagerViewModel), typeof(Sidebar));

        public DownloadManagerViewModel ViewModel
        {
            get => (DownloadManagerViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        private System.Windows.Controls.Button? _activeButton;

        // ===== REAL-TIME SPEED GRAPH STATE =====
        private DispatcherTimer? _graphTimer;
        private readonly Queue<double> _speedHistory = new Queue<double>();
        private readonly int _maxGraphPoints = 32;
        private readonly Random _rng = new Random();
        private double _currentSpeed = 0.0;

        public Sidebar()
        {
            InitializeComponent();
            _activeButton = DashboardBtn;
            Loaded += Sidebar_Loaded;
            Unloaded += Sidebar_Unloaded;
        }

        // ===================================================================
        // REAL-TIME SPEED GRAPH — DispatcherTimer + Canvas Polyline
        // ===================================================================

        private void Sidebar_Loaded(object sender, RoutedEventArgs e)
        {
            if (_activeButton != null)
            {
                SetButtonActiveState(_activeButton, true);
            }

            // Pre-fill history with zeros
            for (int i = 0; i < _maxGraphPoints; i++)
                _speedHistory.Enqueue(0.0);

            // Start the graph update timer — every 600ms
            _graphTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(600)
            };
            _graphTimer.Tick += GraphTimer_Tick;
            _graphTimer.Start();
        }

        private void Sidebar_Unloaded(object sender, RoutedEventArgs e)
        {
            _graphTimer?.Stop();
            _graphTimer = null;
        }

        private void GraphTimer_Tick(object? sender, EventArgs e)
        {
            // Pure truthful speed from active downloads in ViewModel
            double target = GetSpeedFromViewModel();

            if (target <= 0.001)
            {
                _currentSpeed = 0.0;
            }
            else
            {
                _currentSpeed = (_currentSpeed * 0.5) + (target * 0.5);
            }

            // Enqueue new sample, dequeue oldest
            _speedHistory.Enqueue(_currentSpeed);
            if (_speedHistory.Count > _maxGraphPoints)
                _speedHistory.Dequeue();

            // Update the canvas rendering
            RedrawSpeedGraph();

            // Update speed label text (strictly non-negative)
            if (FindName("SpeedValueLabel") is TextBlock lbl)
                lbl.Text = $"{Math.Max(0.0, _currentSpeed):F1} MB/s";

            if (FindName("SpeedPeakLabel") is TextBlock peak)
            {
                double peakVal = 0;
                foreach (var s in _speedHistory) peakVal = Math.Max(peakVal, s);
                peak.Text = $"↑ Peak: {peakVal:F1} MB/s";
            }
        }

        private double GetSpeedFromViewModel()
        {
            if (ViewModel == null) return 0.0;

            var speedStr = ViewModel.CurrentDownloadSpeed;
            if (string.IsNullOrWhiteSpace(speedStr) || speedStr == "0 B/s" || speedStr == "0 MB/s")
                return 0.0;

            try
            {
                string s = speedStr.Trim();
                if (s.EndsWith("/s", StringComparison.OrdinalIgnoreCase)) s = s[..^2].Trim();

                if (s.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(s[..^2].Trim(), out double gb)) return gb * 1024.0;
                }
                else if (s.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(s[..^2].Trim(), out double mb)) return mb;
                }
                else if (s.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(s[..^2].Trim(), out double kb)) return kb / 1024.0;
                }
                else if (s.EndsWith("B", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(s[..^1].Trim(), out double b)) return b / (1024.0 * 1024.0);
                }
                else if (double.TryParse(s, out double val))
                {
                    return val;
                }
            }
            catch { }

            return 0.0;
        }

        private void RedrawSpeedGraph()
        {
            // Find canvas elements by name
            if (FindName("SpeedGraphCanvas") is not System.Windows.Controls.Canvas canvas) return;
            if (FindName("SpeedGraphLine") is not Polyline polyline) return;
            if (FindName("SpeedGraphFill") is not Polygon fillPoly) return;

            double w = canvas.ActualWidth > 0 ? canvas.ActualWidth : 210;
            double h = canvas.ActualHeight > 0 ? canvas.ActualHeight : 72;

            // Find max for scaling
            double maxSpeed = 0;
            foreach (var s in _speedHistory) maxSpeed = Math.Max(maxSpeed, s);
            maxSpeed = Math.Max(maxSpeed, 1.0);

            var points = _speedHistory.ToArray();
            int count = points.Length;

            var linePoints = new PointCollection();
            var fillPoints = new PointCollection();

            // Bottom-left anchor for fill polygon
            fillPoints.Add(new WpfPoint(0, h));

            for (int i = 0; i < count; i++)
            {
                double x = (double)i / (count - 1) * w;
                double y = h - (points[i] / maxSpeed) * (h - 4);

                linePoints.Add(new WpfPoint(x, y));
                fillPoints.Add(new WpfPoint(x, y));
            }

            // Bottom-right anchor for fill polygon
            fillPoints.Add(new WpfPoint(w, h));

            polyline.Points = linePoints;
            fillPoly.Points = fillPoints;
        }

        // ===================================================================
        // NAVIGATION HANDLERS
        // ===================================================================

        private void NavCategory_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            if (btn?.Tag is string category)
            {
                if (category == "Scheduler")
                {
                    try
                    {
                        var win = new SchedulerWindow(false, TimeSpan.FromHours(2));
                        win.Owner = Window.GetWindow(this);
                        win.ShowDialog();
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
                    return;
                }
                else if (category == "Settings")
                {
                    try
                    {
                        var win = new SettingsWindow();
                        win.Owner = Window.GetWindow(this);
                        win.ShowDialog();
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
                    return;
                }

                // Update active nav state
                if (_activeButton != null && _activeButton != btn)
                {
                    SetButtonActiveState(_activeButton, false);
                }

                _activeButton = btn;
                SetButtonActiveState(_activeButton, true);

                // Apply filter
                if (ViewModel != null)
                {
                    ViewModel.CurrentFilter = category;
                    System.Diagnostics.Debug.WriteLine($"[Sidebar] Filter: {category}");
                }

                btn.Focus();
            }
        }

        private void SetButtonActiveState(System.Windows.Controls.Button btn, bool isActive)
        {
            btn.ApplyTemplate();
            if (btn.Template.FindName("BtnBorder", btn) is Border border)
            {
                var iconText = btn.Template.FindName("IconText", btn) as TextBlock;
                var labelText = btn.Template.FindName("LabelText", btn) as TextBlock;

                if (isActive)
                {
                    if (TryFindResource("PrimaryPillGradient") is System.Windows.Media.Brush pillGrad)
                    {
                        border.Background = pillGrad;
                    }
                    else
                    {
                        border.SetResourceReference(Border.BackgroundProperty, "NavActiveBg");
                    }

                    border.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7C3AED"),
                        BlurRadius = 16,
                        ShadowDepth = 2,
                        Opacity = 0.6
                    };

                    if (iconText != null) iconText.Foreground = System.Windows.Media.Brushes.White;
                    if (labelText != null)
                    {
                        labelText.Foreground = System.Windows.Media.Brushes.White;
                        labelText.FontWeight = FontWeights.Bold;
                    }
                }
                else
                {
                    border.Background = System.Windows.Media.Brushes.Transparent;
                    border.Effect = null;
                    iconText?.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "SidebarTextBrush");
                    if (labelText != null)
                    {
                        labelText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "SidebarTextBrush");
                        labelText.FontWeight = FontWeights.SemiBold;
                    }
                }
            }
        }
    }
}
