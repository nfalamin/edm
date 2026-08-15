using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EDM.Converters
{
    /// <summary>
    /// Converts boolean selection state to a semi-transparent highlight background.
    /// </summary>
    public class BoolToBackgroundConverter : IValueConverter
    {
        private static readonly SolidColorBrush SelectedBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 139, 92, 246)); // #1E8B5CF6 (semi-transparent purple)
        private static readonly SolidColorBrush DefaultBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0)); // Transparent

        static BoolToBackgroundConverter()
        {
            SelectedBrush.Freeze();
            DefaultBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? SelectedBrush : DefaultBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Converts download status strings to appropriate Brush colors for display in the Downloads table.
    /// </summary>
    public class StatusToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush GreenBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 16, 185, 129)); // #10B981 (Downloading Green)
        private static readonly SolidColorBrush BlueBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 59, 130, 246));  // #3B82F6 (Completed Blue)
        private static readonly SolidColorBrush AmberBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 245, 158, 11)); // #F59E0B (Paused Orange)
        private static readonly SolidColorBrush PurpleBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 139, 92, 246)); // #8B5CF6 (Queued Purple)
        private static readonly SolidColorBrush CyanBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 6, 182, 212));   // #06B6D4 (Connecting Cyan)
        private static readonly SolidColorBrush RedBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 239, 68, 68));    // #EF4444 (Cancelled/Error Red)
        private static readonly SolidColorBrush GrayBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 156, 163, 175)); // #9CA3AF

        static StatusToColorConverter()
        {
            GreenBrush.Freeze();
            BlueBrush.Freeze();
            AmberBrush.Freeze();
            PurpleBrush.Freeze();
            CyanBrush.Freeze();
            RedBrush.Freeze();
            GrayBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                var normalizedStatus = status?.ToLowerInvariant() ?? string.Empty;

                if (normalizedStatus.Contains("download"))
                    return GreenBrush;

                if (normalizedStatus.Contains("pause"))
                    return AmberBrush;

                if (normalizedStatus.Contains("queue"))
                    return PurpleBrush;

                if (normalizedStatus.Contains("connect"))
                    return CyanBrush;

                if (normalizedStatus.Contains("complet") || normalizedStatus.Contains("finish") || normalizedStatus.Contains("done"))
                    return BlueBrush;

                if (normalizedStatus.Contains("error") || normalizedStatus.Contains("fail") || normalizedStatus.Contains("cancel") || normalizedStatus.Contains("stop"))
                    return RedBrush;
            }

            return GrayBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Converts progress percentage (0-100) to a LinearGradientBrush with status-based colors.
    /// </summary>
    public class ProgressToGradientConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double progress && parameter is string status)
            {
                var normalizedStatus = status?.ToLowerInvariant() ?? string.Empty;

                if (normalizedStatus.Contains("download"))
                {
                    // Blue gradient for downloading
                    return new LinearGradientBrush(
                        new GradientStopCollection
                        {
                            new GradientStop(System.Windows.Media.Color.FromArgb(255, 59, 130, 246), 0.0), // #3B82F6
                            new GradientStop(System.Windows.Media.Color.FromArgb(255, 29, 78, 216), 1.0)   // #1D4ED8
                        },
                        new System.Windows.Point(0, 0),
                        new System.Windows.Point(1, 0)
                    );
                }
                else if (normalizedStatus.Contains("pause"))
                {
                    // Amber gradient for paused
                    return new LinearGradientBrush(
                        new GradientStopCollection
                        {
                            new GradientStop(System.Windows.Media.Color.FromArgb(255, 245, 158, 11), 0.0), // #F59E0B
                            new GradientStop(System.Windows.Media.Color.FromArgb(255, 217, 119, 6), 1.0)   // #D97706
                        },
                        new System.Windows.Point(0, 0),
                        new System.Windows.Point(1, 0)
                    );
                }
                else if (normalizedStatus.Contains("complet"))
                {
                    // Green gradient for completed
                    return new LinearGradientBrush(
                        new GradientStopCollection
                        {
                            new GradientStop(System.Windows.Media.Color.FromArgb(255, 16, 185, 129), 0.0), // #10B981
                            new GradientStop(System.Windows.Media.Color.FromArgb(255, 5, 150, 105), 1.0)   // #059669
                        },
                        new System.Windows.Point(0, 0),
                        new System.Windows.Point(1, 0)
                    );
                }
                else if (normalizedStatus.Contains("queue"))
                {
                    // Purple gradient for queued
                    return new LinearGradientBrush(
                        new GradientStopCollection
                        {
                            new GradientStop(System.Windows.Media.Color.FromArgb(255, 168, 85, 247), 0.0), // #A855F7
                            new GradientStop(System.Windows.Media.Color.FromArgb(255, 126, 34, 206), 1.0)  // #7E22CE
                        },
                        new System.Windows.Point(0, 0),
                        new System.Windows.Point(1, 0)
                    );
                }
            }

            // Default: Gray gradient
            return new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(System.Windows.Media.Color.FromArgb(255, 156, 163, 175), 0.0), // #9CA3AF
                    new GradientStop(System.Windows.Media.Color.FromArgb(255, 107, 114, 128), 1.0)  // #6B7280
                },
                new System.Windows.Point(0, 0),
                new System.Windows.Point(1, 0)
            );
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
