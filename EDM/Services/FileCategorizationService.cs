using System;
using System.IO;

namespace EDM.Services
{
    public static class FileCategorizationService
    {
        public static string GetTargetSubfolder(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return "General";

            string ext = Path.GetExtension(fileName).ToLowerInvariant();

            return ext switch
            {
                ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" or ".ts" or ".flv" or ".m3u8" => "Videos",
                ".mp3" or ".flac" or ".wav" or ".aac" or ".m4a" or ".ogg" or ".wma" => "Music",
                ".pdf" or ".docx" or ".xlsx" or ".pptx" or ".txt" or ".epub" or ".csv" => "Documents",
                ".exe" or ".msi" or ".bat" or ".cmd" or ".ps1" or ".apk" => "Programs",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".iso" or ".cab" => "Compressed",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" or ".ico" => "Images",
                _ => "General"
            };
        }

        public static string ResolveDestinationPath(string baseDownloadDir, string fileName)
        {
            string subfolder = GetTargetSubfolder(fileName);
            string targetDir = Path.Combine(baseDownloadDir, subfolder);

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            return Path.Combine(targetDir, fileName);
        }
    }
}
