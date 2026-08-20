using System;
using System.Globalization;
using System.Windows.Data;

namespace EDM.Converters
{
    /// <summary>
    /// Converts a numeric file size in bytes to a human-readable GB format string
    /// </summary>
    public class BytesToGBConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                double gb = bytes / (1024.0 * 1024.0 * 1024.0);
                return $"{gb:F2} GB";
            }

            if (value is double doubleValGB)
            {
                return $"{doubleValGB:F2} GB";
            }

            return "0 GB";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Formats a speed value (in bytes per second) to MB/s or GB/s display format
    /// </summary>
    public class SpeedFormatter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double speedBps)
            {
                double speedMbps = speedBps / (1024.0 * 1024.0);

                if (speedMbps > 1000)
                {
                    return $"{speedMbps / 1024:F1} GB/s";
                }

                return $"{speedMbps:F1} MB/s";
            }

            if (value is string strSpeed)
            {
                return strSpeed; // Already formatted
            }

            return "0 MB/s";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a numeric count to a formatted string with optional suffix
    /// </summary>
    public class CountFormatter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count.ToString("D");
            }

            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts raw byte counts into human readable B, KB, MB, GB, TB format using SizeFormatter.
    /// </summary>
    public class BytesToHumanSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                return EDM.Helpers.SizeFormatter.FormatBytes(bytes, "0 B");
            }
            if (value is int intBytes)
            {
                return EDM.Helpers.SizeFormatter.FormatBytes(intBytes, "0 B");
            }
            if (value is double dBytes)
            {
                return EDM.Helpers.SizeFormatter.FormatBytes((long)Math.Max(0, dBytes), "0 B");
            }
            if (value is string s && long.TryParse(s, out long parsed))
            {
                return EDM.Helpers.SizeFormatter.FormatBytes(parsed, "0 B");
            }

            return "0 B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Returns Visible when count is 0 (or null/empty collection), otherwise Collapsed.
    /// </summary>
    public class EmptyCountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            if (value is System.Collections.ICollection coll)
            {
                return coll.Count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            return System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
