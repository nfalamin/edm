using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EDM.Converters
{
    /// <summary>
    /// Converts a boolean or string status flag to a Brush color for downloading/paused states.
    /// Active (downloading) → Deep Neon Blue gradient
    /// Paused/Inactive or Bottleneck → Soft Gray
    /// Error/Stopped → Muted Red
    /// This converter is culture-invariant and tolerant to minor variations in status text.
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        // EDM Turbo Active Download Gradient: Deep Neon Blue
        private static readonly LinearGradientBrush ActiveBrush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(0, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(System.Windows.Media.Color.FromArgb(255, 91, 155, 213), 0.0),   // #5B9BD5
                new GradientStop(System.Windows.Media.Color.FromArgb(255, 31, 78, 121), 1.0)     // #1F4E79
            }
        };

        // Paused/Inactive → Soft Gray
        private static readonly SolidColorBrush PausedBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 200, 200, 200));

        // Bottleneck / slow → Muted Amber
        private static readonly SolidColorBrush BottleneckBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 210, 180, 70));

        // Error/Stopped → Muted Red
        private static readonly SolidColorBrush ErrorBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 212, 64, 64));

        static BoolToColorConverter()
        {
            ActiveBrush.Freeze();
            PausedBrush.Freeze();
            BottleneckBrush.Freeze();
            ErrorBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Boolean path: true = active, false = paused
            if (value is bool b)
            {
                return b ? (System.Windows.Media.Brush)ActiveBrush : (System.Windows.Media.Brush)PausedBrush;
            }

            // String status path: tolerant matching
            if (value is string status)
            {
                if (string.IsNullOrWhiteSpace(status)) return PausedBrush;

                var s = status.Trim().ToLowerInvariant();

                if (s.Contains("error") || s.Contains("failed") || s.Contains("stopped"))
                    return ErrorBrush;

                if (s.Contains("pause") || s.Contains("paused") || s.Contains("inactive"))
                    return PausedBrush;

                if (s.Contains("bottleneck") || s.Contains("slow") || s.Contains("stalled"))
                    return BottleneckBrush;

                if (s.Contains("download") || s.Contains("receiving") || s.Contains("active") || s.Contains("connecting"))
                    return ActiveBrush;

                // Default fallback
                return PausedBrush;
            }

            // If value is numeric (e.g., a latency or speed indicator) we can map thresholds when passed as parameter.
            if (value is int || value is long || value is double || value is float)
            {
                try
                {
                    double numeric = System.Convert.ToDouble(value);
                    // numeric could represent kb/s or a health score: simple mapping
                    if (numeric <= 0) return PausedBrush;
                    if (numeric < 1024) return BottleneckBrush; // slow
                    return ActiveBrush;
                }
                catch
                {
                    return PausedBrush;
                }
            }

            // Default: paused
            return PausedBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
