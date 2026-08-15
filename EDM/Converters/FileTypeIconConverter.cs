using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;

namespace EDM.Converters
{
    /// <summary>
    /// Represents file type icon and color information
    /// </summary>
    public class FileTypeIcon
    {
        public string IconChar { get; set; } = "\uE8B1"; // Generic file default (Segoe MDL2)
        public System.Windows.Media.Brush ColorBrush { get; set; } = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 156, 163, 175)); // Gray

        public FileTypeIcon(string iconChar, System.Windows.Media.Brush color)
        {
            IconChar = iconChar;
            ColorBrush = color;
            if (ColorBrush is SolidColorBrush scb)
            {
                scb.Freeze();
            }
        }
    }

    /// <summary>
    /// Converts a DownloadItem's FileName (or file extension) to an appropriate icon character 
    /// and background color from Segoe MDL2 Assets font, based on file type.
    /// 
    /// Icon mappings:
    /// - Video (.mp4, .mkv, .avi, .mov, .flv, .wmv, .webm) → 🎬 #E8CC #10B981 (Green)
    /// - Audio (.mp3, .wav, .flac, .aac, .m4a, .wma, .ogg) → 🎵 #E993 #3B82F6 (Blue)
    /// - Compressed (.zip, .rar, .7z, .gz, .tar, .iso) → 📦 #EE6D #8B5CF6 (Purple)
    /// - Executable (.exe, .msi, .bat, .com, .dll, .apk) → ⚙️ #E713 #EC4899 (Pink)
    /// - PDF (.pdf) → 📄 #E8CD #F59E0B (Amber)
    /// - Document (.doc, .docx, .xls, .xlsx, .ppt, .pptx, .txt) → 📋 #E8E8 #06B6D4 (Cyan)
    /// - Image (.jpg, .png, .gif, .bmp, .svg, .webp, .tiff) → 🖼️ #E8B4 #EC4899 (Pink)
    /// - Default → 📄 #E8B1 #9CA3AF (Gray)
    /// </summary>
    public class FileTypeIconConverter : IValueConverter
    {
        // Cached brushes for performance
        private static readonly SolidColorBrush GreenBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 16, 185, 129)); // #10B981
        private static readonly SolidColorBrush BlueBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 59, 130, 246)); // #3B82F6
        private static readonly SolidColorBrush PurpleBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 139, 92, 246)); // #8B5CF6
        private static readonly SolidColorBrush PinkBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 236, 72, 153)); // #EC4899
        private static readonly SolidColorBrush AmberBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 245, 158, 11)); // #F59E0B
        private static readonly SolidColorBrush CyanBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 6, 182, 212)); // #06B6D4
        private static readonly SolidColorBrush GrayBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 156, 163, 175)); // #9CA3AF

        static FileTypeIconConverter()
        {
            GreenBrush.Freeze();
            BlueBrush.Freeze();
            PurpleBrush.Freeze();
            PinkBrush.Freeze();
            AmberBrush.Freeze();
            CyanBrush.Freeze();
            GrayBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string fileName)
            {
                return GetIconForFile(fileName);
            }

            // Default file icon
            return new FileTypeIcon("\uE8B1", GrayBrush);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Analyzes filename and returns appropriate icon character and color brush
        /// </summary>
        private static FileTypeIcon GetIconForFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return new FileTypeIcon("\uE8B1", GrayBrush); // Default file icon
            }

            // Extract file extension
            string extension = Path.GetExtension(fileName).ToLowerInvariant().TrimStart('.');

            // Match against file type categories
            return extension switch
            {
                // Video formats
                "mp4" or "mkv" or "avi" or "mov" or "flv" or "wmv" or "webm" or "m4v" or "mpg" or "mpeg" or "3gp" =>
                    new FileTypeIcon("\uE8CC", GreenBrush), // 🎬 Video

                // Audio formats
                "mp3" or "wav" or "flac" or "aac" or "m4a" or "wma" or "ogg" or "aiff" or "ape" or "opus" =>
                    new FileTypeIcon("\uE993", BlueBrush), // 🎵 Audio

                // Compressed/Archive formats
                "zip" or "rar" or "7z" or "gz" or "tar" or "iso" or "bz2" or "xz" or "lz" =>
                    new FileTypeIcon("\uE8EE", PurpleBrush), // 📦 Package/Archive

                // Executable formats
                "exe" or "msi" or "bat" or "com" or "dll" or "apk" or "app" or "bin" or "cmd" =>
                    new FileTypeIcon("\uE713", PinkBrush), // ⚙️ Settings/Tool

                // PDF
                "pdf" =>
                    new FileTypeIcon("\uE8CD", AmberBrush), // 📄 Document (pdf icon)

                // Documents
                "doc" or "docx" or "xls" or "xlsx" or "ppt" or "pptx" or "txt" or "rtf" or "odt" or "ods" or "odp" =>
                    new FileTypeIcon("\uE78C", CyanBrush), // 📄 Document

                // Images
                "jpg" or "jpeg" or "png" or "gif" or "bmp" or "svg" or "webp" or "tiff" or "ico" or "heic" =>
                    new FileTypeIcon("\uE8B4", PinkBrush), // 🖼️ Picture

                // Default: generic file
                _ => new FileTypeIcon("\uE8B1", GrayBrush)
            };
        }
    }

    /// <summary>
    /// Alternative converter that returns only the icon character (for binding to TextBlock.Text)
    /// </summary>
    public class FileTypeIconCharConverter : IValueConverter
    {
        private static FileTypeIconConverter _baseConverter = new FileTypeIconConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var icon = _baseConverter.Convert(value, targetType, parameter, culture);
            if (icon is FileTypeIcon fileIcon)
            {
                return fileIcon.IconChar;
            }
            return "\uE8B1"; // Default
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Alternative converter that returns only the color brush (for binding to Ellipse.Fill)
    /// </summary>
    public class FileTypeIconColorConverter : IValueConverter
    {
        private static FileTypeIconConverter _baseConverter = new FileTypeIconConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var icon = _baseConverter.Convert(value, targetType, parameter, culture);
            if (icon is FileTypeIcon fileIcon)
            {
                return fileIcon.ColorBrush;
            }
            return new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 156, 163, 175)); // Default gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
