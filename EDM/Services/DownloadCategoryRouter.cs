using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EDM.Services
{
    public class CategoryRule
    {
        public string CategoryId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DefaultSubFolder { get; set; } = string.Empty;
        public HashSet<string> Extensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Automatic Category Routing & Destination Folder Engine.
    /// Maps downloaded filenames to customizable categories and automatic sub-directories.
    /// </summary>
    public class DownloadCategoryRouter
    {
        private static readonly Lazy<DownloadCategoryRouter> _instance = new(() => new DownloadCategoryRouter());
        public static DownloadCategoryRouter Instance => _instance.Value;

        private readonly List<CategoryRule> _categories = new();

        public DownloadCategoryRouter()
        {
            // Compressed
            _categories.Add(new CategoryRule
            {
                CategoryId = "compressed",
                Name = "Compressed",
                DefaultSubFolder = "Compressed",
                Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".iso" }
            });

            // Video
            _categories.Add(new CategoryRule
            {
                CategoryId = "video",
                Name = "Video",
                DefaultSubFolder = "Video",
                Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".webm", ".avi", ".mov", ".flv", ".ts" }
            });

            // Music / Audio
            _categories.Add(new CategoryRule
            {
                CategoryId = "music",
                Name = "Music",
                DefaultSubFolder = "Music",
                Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".opus" }
            });

            // Documents
            _categories.Add(new CategoryRule
            {
                CategoryId = "documents",
                Name = "Documents",
                DefaultSubFolder = "Documents",
                Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".epub" }
            });

            // Programs
            _categories.Add(new CategoryRule
            {
                CategoryId = "programs",
                Name = "Programs",
                DefaultSubFolder = "Programs",
                Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".exe", ".msi", ".bat", ".cmd", ".apk", ".jar" }
            });
        }

        public CategoryRule DetermineCategory(string filename)
        {
            return DetermineCategory(filename, null, null, null);
        }

        public CategoryRule DetermineCategory(string filename, string? contentType, string? url = null, byte[]? headerBytes = null)
        {
            var detected = FileTypeDetector.DetectFromSignals(filename, contentType, url, headerBytes);

            string targetCategoryName = detected switch
            {
                DetectedFileType.Compressed => "Compressed",
                DetectedFileType.Video => "Video",
                DetectedFileType.Audio => "Music",
                DetectedFileType.Documents => "Documents",
                DetectedFileType.Programs => "Programs",
                _ => "General"
            };

            var match = _categories.FirstOrDefault(c => c.Name.Equals(targetCategoryName, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            string ext = Path.GetExtension(filename);
            if (!string.IsNullOrEmpty(ext))
            {
                var extMatch = _categories.FirstOrDefault(c => c.Extensions.Contains(ext));
                if (extMatch != null) return extMatch;
            }

            return new CategoryRule
            {
                CategoryId = "general",
                Name = "General",
                DefaultSubFolder = "General"
            };
        }

        public string ResolveDestinationPath(string baseDownloadDir, string filename)
        {
            var cat = DetermineCategory(filename);
            string targetFolder = Path.Combine(baseDownloadDir, cat.DefaultSubFolder);
            return Path.Combine(targetFolder, filename);
        }

        public IReadOnlyList<CategoryRule> GetCategories() => _categories.ToList();

        public void RemoveCategory(string id)
        {
            _categories.RemoveAll(c => c.CategoryId.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

        public void AddCustomCategory(string id, string name, string subFolder, IEnumerable<string> extensions)
        {
            _categories.RemoveAll(c => c.CategoryId.Equals(id, StringComparison.OrdinalIgnoreCase));
            _categories.Add(new CategoryRule
            {
                CategoryId = id,
                Name = name,
                DefaultSubFolder = subFolder,
                Extensions = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase)
            });
        }
    }
}
