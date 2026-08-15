using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace EDM.Services
{
    public enum FileTypeCategory
    {
        All,
        Videos,
        Audio,
        Documents,
        Archives,
        Images
    }

    public static class UrlPatternExpander
    {
        private static readonly Regex NumericPatternRegex = new(@"\[(\d+)-(\d+)\]", RegexOptions.Compiled);
        private static readonly Regex AlphaPatternRegex = new(@"\[([a-zA-Z])-([a-zA-Z])\]", RegexOptions.Compiled);

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".ts", ".m3u8", ".flv" };
        private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".flac", ".wav", ".aac", ".m4a", ".ogg" };
        private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx", ".xlsx", ".pptx", ".txt", ".epub" };
        private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase) { ".zip", ".rar", ".7z", ".tar", ".gz", ".iso" };
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg" };

        public static List<string> Expand(string patternUrl)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(patternUrl)) return results;

            // Check numeric pattern like [01-50] or [1-100]
            var numMatch = NumericPatternRegex.Match(patternUrl);
            if (numMatch.Success)
            {
                string startStr = numMatch.Groups[1].Value;
                string endStr = numMatch.Groups[2].Value;

                if (long.TryParse(startStr, out long start) && long.TryParse(endStr, out long end) && start <= end)
                {
                    int padLength = (startStr.StartsWith('0') && startStr.Length > 1) ? startStr.Length : 0;
                    string format = padLength > 0 ? $"D{padLength}" : "";

                    for (long i = start; i <= end; i++)
                    {
                        string replacement = padLength > 0 ? i.ToString(format) : i.ToString();
                        string expandedUrl = NumericPatternRegex.Replace(patternUrl, replacement, 1);
                        results.Add(expandedUrl);
                    }
                    return results;
                }
            }

            // Check alphabetic pattern like [a-z]
            var alphaMatch = AlphaPatternRegex.Match(patternUrl);
            if (alphaMatch.Success)
            {
                char startChar = alphaMatch.Groups[1].Value[0];
                char endChar = alphaMatch.Groups[2].Value[0];

                if (startChar <= endChar)
                {
                    for (char c = startChar; c <= endChar; c++)
                    {
                        string expandedUrl = AlphaPatternRegex.Replace(patternUrl, c.ToString(), 1);
                        results.Add(expandedUrl);
                    }
                    return results;
                }
            }

            // No pattern found, return single original URL
            results.Add(patternUrl);
            return results;
        }

        public static List<string> FilterByCategory(IEnumerable<string> urls, FileTypeCategory category)
        {
            if (urls == null) return new List<string>();
            if (category == FileTypeCategory.All) return urls.ToList();

            return urls.Where(url =>
            {
                string ext = Path.GetExtension(new Uri(url).AbsolutePath);
                return category switch
                {
                    FileTypeCategory.Videos => VideoExtensions.Contains(ext),
                    FileTypeCategory.Audio => AudioExtensions.Contains(ext),
                    FileTypeCategory.Documents => DocumentExtensions.Contains(ext),
                    FileTypeCategory.Archives => ArchiveExtensions.Contains(ext),
                    FileTypeCategory.Images => ImageExtensions.Contains(ext),
                    _ => true
                };
            }).ToList();
        }
    }
}
