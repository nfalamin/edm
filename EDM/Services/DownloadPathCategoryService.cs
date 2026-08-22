using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EDM.Services
{
    /// <summary>
    /// Service for automatically categorizing downloads into subfolders based on file extensions.
    /// Implements custom logic to organize files by type without hardcoded paths.
    /// </summary>
    public class DownloadPathCategoryService
    {
        // File extension categories
        private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".aac", ".flac", ".m4a", ".ogg", ".wma", ".aiff", ".alac", ".ape"
        };

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mkv", ".avi", ".mov", ".flv", ".wmv", ".webm", ".m4v", ".3gp", ".ts", ".mts"
        };

        private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt", ".txt", ".rtf", ".odt"
        };

        private static readonly HashSet<string> CompressedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".iso"
        };

        private static readonly ConcurrentDictionary<string, string> CustomCategoryMappings = new(StringComparer.OrdinalIgnoreCase);

        public static void RegisterCustomCategoryMapping(string extension, string subfolderName)
        {
            if (string.IsNullOrWhiteSpace(extension) || string.IsNullOrWhiteSpace(subfolderName)) return;
            string cleanExt = extension.StartsWith(".") ? extension : "." + extension;
            CustomCategoryMappings[cleanExt] = subfolderName.Trim();
        }

        public static bool RemoveCustomCategoryMapping(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return false;
            string cleanExt = extension.StartsWith(".") ? extension : "." + extension;
            return CustomCategoryMappings.TryRemove(cleanExt, out _);
        }

        public static IReadOnlyDictionary<string, string> GetCustomCategoryMappings()
        {
            return CustomCategoryMappings;
        }

        public enum FileCategory
        {
            Audio,
            Video,
            Documents,
            Unknown
        }

        /// <summary>
        /// Determines the category of a file based on its extension
        /// </summary>
        public static FileCategory DetermineFileCategory(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return FileCategory.Unknown;

            string extension = Path.GetExtension(fileName);

            if (AudioExtensions.Contains(extension))
                return FileCategory.Audio;

            if (VideoExtensions.Contains(extension))
                return FileCategory.Video;

            if (DocumentExtensions.Contains(extension) || CompressedExtensions.Contains(extension))
                return FileCategory.Documents;

            return FileCategory.Unknown;
        }

        /// <summary>
        /// Gets the category subfolder name for a file
        /// </summary>
        public static string GetCategorySubfolder(FileCategory category)
        {
            return category switch
            {
                FileCategory.Audio => "Audio",
                FileCategory.Video => "Video",
                FileCategory.Documents => "Documents",
                _ => string.Empty
            };
        }

        /// <summary>
        /// Gets the category subfolder name based on file name
        /// </summary>
        public static string GetCategorySubfolderByFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return string.Empty;
            string ext = Path.GetExtension(fileName);
            if (!string.IsNullOrEmpty(ext) && CustomCategoryMappings.TryGetValue(ext, out var customFolder))
            {
                return customFolder;
            }
            return GetCategorySubfolder(DetermineFileCategory(fileName));
        }

        /// <summary>
        /// Builds the full download path with category subfolder
        /// </summary>
        /// <param name="baseDownloadPath">Base path (e.g., Downloads\EDM)</param>
        /// <param name="fileName">File name with extension</param>
        /// <returns>Full path with category subfolder (e.g., Downloads\EDM\Audio)</returns>
        public static string BuildCategorizedPath(string baseDownloadPath, string fileName)
        {
            if (string.IsNullOrWhiteSpace(baseDownloadPath))
                throw new ArgumentNullException(nameof(baseDownloadPath));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            string subfolder = GetCategorySubfolderByFileName(fileName);

            if (string.IsNullOrEmpty(subfolder))
                return baseDownloadPath;

            return Path.Combine(baseDownloadPath, subfolder);
        }

        /// <summary>
        /// Ensures the categorized directory exists, creating it if necessary
        /// </summary>
        /// <param name="baseDownloadPath">Base path (e.g., Downloads\EDM)</param>
        /// <param name="fileName">File name with extension</param>
        /// <returns>The full path after ensuring directory exists</returns>
        public static string EnsureCategorizedDirectoryExists(string baseDownloadPath, string fileName)
        {
            try
            {
                string fullPath = BuildCategorizedPath(baseDownloadPath, fileName);

                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    LoggingService.Log($"[DownloadPathCategoryService] Created directory: {fullPath}");
                }

                return fullPath;
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadPathCategoryService] Error ensuring directory exists", ex);
                // Fallback to base path if directory creation fails
                EnsureBaseDirectoryExists(baseDownloadPath);
                return baseDownloadPath;
            }
        }

        /// <summary>
        /// Ensures the base download directory exists
        /// </summary>
        public static void EnsureBaseDirectoryExists(string baseDownloadPath)
        {
            try
            {
                if (!Directory.Exists(baseDownloadPath))
                {
                    Directory.CreateDirectory(baseDownloadPath);
                    LoggingService.Log($"[DownloadPathCategoryService] Created base directory: {baseDownloadPath}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadPathCategoryService] Error creating base directory", ex);
            }
        }

        /// <summary>
        /// Gets the default base download path (Downloads\EDM)
        /// </summary>
        public static string GetDefaultBasePath()
        {
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(userProfile, "Downloads", "EDM");
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadPathCategoryService] Error getting default base path", ex);
                // Fallback to app directory
                return Path.Combine(AppContext.BaseDirectory, "Downloads", "EDM");
            }
        }

        /// <summary>
        /// Standardized EDM local workspace folder layout definition for Windows File Explorer.
        /// </summary>
        public static readonly string[] StandardWorkspaceFolders = new[]
        {
            "Downloads",
            "Files",
            "Projects",
            "Documents",
            "Media",
            "Uploads",
            "Synced",
            "Cache",
            "Config"
        };

        /// <summary>
        /// Gets the standard root EDM workspace folder (e.g. %UserProfile%\EDM).
        /// </summary>
        public static string GetWorkspaceRootPath()
        {
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(userProfile, "EDM");
            }
            catch
            {
                return Path.Combine(AppContext.BaseDirectory, "EDM");
            }
        }

        /// <summary>
        /// Ensures all standard workspace folders exist on the local file system.
        /// </summary>
        public static void EnsureWorkspaceStructure(string? customRoot = null)
        {
            try
            {
                string root = customRoot ?? GetWorkspaceRootPath();
                Directory.CreateDirectory(root);
                foreach (var folder in StandardWorkspaceFolders)
                {
                    Directory.CreateDirectory(Path.Combine(root, folder));
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadPathCategoryService] Failed to create workspace folder structure", ex);
            }
        }

        /// <summary>
        /// Gets all category subfolder names
        /// </summary>
        public static IEnumerable<string> GetAllCategorySubfolders()
        {
            yield return GetCategorySubfolder(FileCategory.Audio);
            yield return GetCategorySubfolder(FileCategory.Video);
            yield return GetCategorySubfolder(FileCategory.Documents);
        }
    }
}
