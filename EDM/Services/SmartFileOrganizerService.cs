using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace EDM.Services
{
    public class SmartOrganizationResult
    {
        public string PrimaryCategory { get; set; } = "General";
        public string SuggestedSubfolder { get; set; } = "General";
        public List<string> SmartTags { get; set; } = new();
        public string CleanedFileName { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; } = 1.0;
    }

    public class SmartFileOrganizerService
    {
        private static readonly Dictionary<string, (string Subfolder, string[] Keywords, string[] Tags)> CategoryRules = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Software & Setup"] = ("Software/Installers", 
                new[] { "setup", "installer", "install", "x64", "x86", "win64", "win32", "build", "portable", "patch", "driver" },
                new[] { "Application", "Executable", "Utility" }),

            ["Projects & Code"] = ("Projects/Source",
                new[] { "master", "main", "repo", "source", "commit", "release", "sdk", "api", "node_modules", "package" },
                new[] { "Code", "Development", "Git" }),

            ["Invoices & Documents"] = ("Documents/Invoices_Receipts",
                new[] { "invoice", "receipt", "bill", "statement", "payment", "tax", "payroll", "payslip", "contract" },
                new[] { "Finance", "Official", "Document" }),

            ["Course & Education"] = ("Documents/Courses_Tutorials",
                new[] { "lecture", "tutorial", "chapter", "lesson", "course", "assignment", "syllabus", "cheatsheet", "handbook", "exam" },
                new[] { "Learning", "Study", "Education" }),

            ["Movies & Shows"] = ("Media/Movies_TV",
                new[] { "1080p", "720p", "2160p", "4k", "bluray", "webrip", "hdrip", "x264", "x265", "hevc", "season", "s01", "s02", "e01", "e02" },
                new[] { "Entertainment", "Video", "Cinematic" }),

            ["Music & Audio"] = ("Media/Audio",
                new[] { "soundtrack", "ost", "remix", "flac", "podcast", "album", "single", "ep", "audiobook" },
                new[] { "Audio", "Music" }),

            ["Archives & Backups"] = ("Archives/Compressed",
                new[] { "backup", "dump", "archive", "snapshot", "tar", "iso", "img", "vmdk" },
                new[] { "Storage", "Backup" }),
        };

        public SmartOrganizationResult AnalyzeAndClassify(string fileName, string? sourceUrl = null, string? mimeType = null)
        {
            var result = new SmartOrganizationResult
            {
                CleanedFileName = CleanFileName(fileName)
            };

            var lowerName = (fileName ?? string.Empty).ToLowerInvariant();
            var lowerUrl = (sourceUrl ?? string.Empty).ToLowerInvariant();
            var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();

            int highestMatchCount = 0;
            string bestCategory = "General";
            string bestSubfolder = "General";
            var matchedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in CategoryRules)
            {
                int score = 0;
                foreach (var keyword in kvp.Value.Keywords)
                {
                    if (lowerName.Contains(keyword)) score += 2;
                    if (lowerUrl.Contains(keyword)) score += 1;
                }

                if (score > highestMatchCount)
                {
                    highestMatchCount = score;
                    bestCategory = kvp.Key;
                    bestSubfolder = kvp.Value.Subfolder;
                    foreach (var tag in kvp.Value.Tags)
                    {
                        matchedTags.Add(tag);
                    }
                }
            }

            // Fallback by extension if no keyword match
            if (highestMatchCount == 0)
            {
                (bestCategory, bestSubfolder, var extTags) = ClassifyByExtension(extension);
                foreach (var t in extTags) matchedTags.Add(t);
                result.ConfidenceScore = 0.7;
            }
            else
            {
                result.ConfidenceScore = Math.Min(1.0, 0.7 + (highestMatchCount * 0.1));
            }

            // Add extension tag
            if (!string.IsNullOrEmpty(extension))
            {
                matchedTags.Add(extension.TrimStart('.').ToUpperInvariant());
            }

            result.PrimaryCategory = bestCategory;
            result.SuggestedSubfolder = bestSubfolder;
            result.SmartTags = matchedTags.ToList();

            return result;
        }

        public string ResolveDestinationPath(string rootDownloadDir, SmartOrganizationResult orgResult)
        {
            var normalizedSubfolder = orgResult.SuggestedSubfolder.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var folder = Path.Combine(rootDownloadDir, normalizedSubfolder);
            return folder;
        }

        private static (string Category, string Subfolder, string[] Tags) ClassifyByExtension(string ext)
        {
            return ext switch
            {
                ".exe" or ".msi" or ".bat" or ".cmd" or ".apk" => ("Software & Setup", "Software/Executables", new[] { "App" }),
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => ("Archives & Backups", "Archives", new[] { "Compressed" }),
                ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt" => ("Invoices & Documents", "Documents", new[] { "Document" }),
                ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" or ".ts" => ("Movies & Shows", "Media/Videos", new[] { "Video" }),
                ".mp3" or ".flac" or ".wav" or ".aac" or ".ogg" or ".m4a" => ("Music & Audio", "Media/Audio", new[] { "Audio" }),
                ".iso" or ".img" or ".vhd" => ("Archives & Backups", "Disk_Images", new[] { "Image" }),
                _ => ("General", "General", new[] { "File" })
            };
        }

        private static string CleanFileName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return "unnamed_file";
            var sanitized = Regex.Replace(rawName, @"[\\/:*?""<>|]", "_");
            // Remove multiple consecutive underscores
            sanitized = Regex.Replace(sanitized, @"_{2,}", "_");
            return sanitized.Trim();
        }
    }
}
