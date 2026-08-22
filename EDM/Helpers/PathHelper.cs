using System;
using System.IO;
using System.Linq;

namespace EDM.Helpers
{
    public static class PathHelper
    {
        private static readonly string[] AudioExt = new[] { ".mp3", ".wav", ".aac", ".flac", ".m4a", ".ogg" };
        private static readonly string[] VideoExt = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv" };
        private static readonly string[] DocumentExt = new[] { ".pdf", ".docx", ".doc", ".txt", ".xlsx", ".pptx" };
        private static readonly string[] ArchiveExt = new[] { ".zip", ".rar", ".7z", ".tar", ".gz" };

        // Returns a safe categorized full path under Downloads\\EDM\\<Category>\\fileName
        public static string GetCategorizedDownloadPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));

            string downloads = GetDownloadsBase();
            string category = "Others";

            string ext = Path.GetExtension(fileName)?.ToLowerInvariant() ?? string.Empty;

            if (AudioExt.Contains(ext)) category = "Audio";
            else if (VideoExt.Contains(ext)) category = "Video";
            else if (DocumentExt.Contains(ext)) category = "Documents";
            else if (ArchiveExt.Contains(ext)) category = "Documents"; // route archives to Documents as requested
            else category = "Others";

            string folder = Path.Combine(downloads, category);
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception ex)
            {
                try { EDM.Services.LoggingService.LogException("[AutoFix] Failed to create category folder in PathHelper", ex); } catch { }
                // If creating categorized folder fails, fall back to base downloads directory
                folder = downloads;
                try { Directory.CreateDirectory(folder); } catch { /* swallow */ }
            }

            string safeName = MakeSafeFileName(Path.GetFileName(fileName));
            string fullPath = Path.Combine(folder, safeName);

            return MakeUnique(fullPath);
        }

        private static string GetDownloadsBase()
        {
            // Prefer KnownFolders Downloads when available via Environment.SpecialFolder.UserProfile/Downloads
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string downloads = Path.Combine(userProfile, "Downloads", "EDM");
            try { Directory.CreateDirectory(downloads); } catch { }
            return downloads;
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            if (string.IsNullOrWhiteSpace(name)) name = "download_file";
            return name;
        }

        private static string MakeUnique(string path)
        {
            if (!File.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            int i = 1;
            string candidate;
            do
            {
                candidate = Path.Combine(dir, $"{name} ({i}){ext}");
                i++;
            } while (File.Exists(candidate));
            return candidate;
        }
    }
}
