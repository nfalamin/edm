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
}
