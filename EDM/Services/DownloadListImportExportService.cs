using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EDM.Models;

namespace EDM.Services
{
    public class ExportedDownloadItem
    {
        public string Url { get; set; } = string.Empty;
        public string? FileName { get; set; }
        public string? DestinationDirectory { get; set; }
        public string? Referer { get; set; }
        public string? UserAgent { get; set; }
        public string? Cookies { get; set; }
        public string? Category { get; set; }
        public string? Size { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Service for importing and exporting download lists across standard formats:
    /// 1. Plain Text (.txt / .urls) - Line-delimited URLs
    /// 2. JSON (.json) - Full EDM download metadata & state
    /// 3. Standard Batch Exchange Format (.ef2) - Universal download list exchange format
    /// </summary>
    public class DownloadListImportExportService
    {
        private static readonly Lazy<DownloadListImportExportService> _instance = new(() => new DownloadListImportExportService());
        public static DownloadListImportExportService Instance => _instance.Value;

        public async Task<string> ExportToJsonAsync(IEnumerable<DownloadItem> items, string filePath)
        {
            var exportedItems = items.Select(t => new ExportedDownloadItem
            {
                Url = t.Url,
                FileName = t.FileName,
                DestinationDirectory = t.SavePath,
                Cookies = t.Cookies,
                Category = t.Category,
                Size = t.Size,
                Status = t.Status
            }).ToList();

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(exportedItems, options);
            await File.WriteAllTextAsync(filePath, json, Encoding.UTF8).ConfigureAwait(false);
            return json;
        }

        public async Task<List<ExportedDownloadItem>> ImportFromJsonAsync(string filePath)
        {
            if (!File.Exists(filePath)) return new List<ExportedDownloadItem>();

            string json = await File.ReadAllTextAsync(filePath, Encoding.UTF8).ConfigureAwait(false);
            var items = JsonSerializer.Deserialize<List<ExportedDownloadItem>>(json);
            return items ?? new List<ExportedDownloadItem>();
        }

        public async Task ExportToPlainTextUrlsAsync(IEnumerable<DownloadItem> items, string filePath)
        {
            var lines = items.Select(t => t.Url).Where(u => !string.IsNullOrWhiteSpace(u));
            await File.WriteAllLinesAsync(filePath, lines, Encoding.UTF8).ConfigureAwait(false);
        }

        public async Task<List<ExportedDownloadItem>> ImportFromPlainTextUrlsAsync(string filePath)
        {
            var list = new List<ExportedDownloadItem>();
            if (!File.Exists(filePath)) return list;

            var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8).ConfigureAwait(false);
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed) && (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)))
                {
                    string name = string.Empty;
                    try { name = Path.GetFileName(new Uri(trimmed).LocalPath); } catch { }
                    list.Add(new ExportedDownloadItem
                    {
                        Url = trimmed,
                        FileName = name
                    });
                }
            }
            return list;
        }

        /// <summary>
        /// Exports download queue to standard batch exchange file format (.ef2)
        /// </summary>
        public async Task ExportToEf2Async(IEnumerable<DownloadItem> items, string filePath)
        {
            var sb = new StringBuilder();
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Url)) continue;

                sb.AppendLine("<");
                sb.AppendLine(item.Url);
                if (!string.IsNullOrWhiteSpace(item.Cookies))
                {
                    sb.AppendLine($"cookie: {item.Cookies}");
                }
                if (!string.IsNullOrWhiteSpace(item.SavePath))
                {
                    sb.AppendLine($"filepath: {item.SavePath}");
                }
                if (!string.IsNullOrWhiteSpace(item.FileName))
                {
                    sb.AppendLine($"file: {item.FileName}");
                }
                sb.AppendLine(">");
            }

            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.ASCII).ConfigureAwait(false);
        }

        /// <summary>
        /// Imports download list from standard batch exchange file format (.ef2)
        /// </summary>
        public async Task<List<ExportedDownloadItem>> ImportFromEf2Async(string filePath)
        {
            var items = new List<ExportedDownloadItem>();
            if (!File.Exists(filePath)) return items;

            var lines = await File.ReadAllLinesAsync(filePath, Encoding.ASCII).ConfigureAwait(false);
            ExportedDownloadItem? current = null;

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line == "<")
                {
                    current = new ExportedDownloadItem();
                }
                else if (line == ">")
                {
                    if (current != null && !string.IsNullOrWhiteSpace(current.Url))
                    {
                        items.Add(current);
                    }
                    current = null;
                }
                else if (current != null)
                {
                    if (string.IsNullOrEmpty(current.Url))
                    {
                        current.Url = line;
                    }
                    else if (line.StartsWith("referer:", StringComparison.OrdinalIgnoreCase))
                    {
                        current.Referer = line.Substring(8).Trim();
                    }
                    else if (line.StartsWith("cookie:", StringComparison.OrdinalIgnoreCase))
                    {
                        current.Cookies = line.Substring(7).Trim();
                    }
                    else if (line.StartsWith("User-Agent:", StringComparison.OrdinalIgnoreCase))
                    {
                        current.UserAgent = line.Substring(11).Trim();
                    }
                    else if (line.StartsWith("filepath:", StringComparison.OrdinalIgnoreCase))
                    {
                        current.DestinationDirectory = line.Substring(9).Trim();
                    }
                    else if (line.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                    {
                        current.FileName = line.Substring(5).Trim();
                    }
                }
            }

            return items;
        }

        // Backward compatibility aliases
        public Task ExportToIdmEf2Async(IEnumerable<DownloadItem> items, string filePath) => ExportToEf2Async(items, filePath);
        public Task<List<ExportedDownloadItem>> ImportFromIdmEf2Async(string filePath) => ImportFromEf2Async(filePath);
    }
}
