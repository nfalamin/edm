using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services.Interfaces;
namespace EDM.Services
{
    // Scans for .tmp_<filename> metadata folders and returns resumable DownloadItem entries
    public class ResumeScannerService
    {
        // Search common roots for partial metadata. Currently scans the user's Downloads folder and application working directory.
        private readonly string[] _roots;

        public ResumeScannerService(ISettingsService settings)
        {
            // configurable roots from settings; include default download path and application dir as fallback
            var configured = new System.Collections.Generic.List<string>();
            try { configured.Add(settings.GetDefaultDownloadPath()); } catch (Exception ex) { LoggingService.Log($"[ResumeScannerService] Failed to add configured root: {ex.Message}"); }
            try { configured.Add(AppContext.BaseDirectory ?? "."); } catch (Exception ex) { LoggingService.Log($"[ResumeScannerService] Failed to add AppContext.BaseDirectory: {ex.Message}"); }
            _roots = configured.ToArray();
        }

        public async Task<List<DownloadItem>> FindResumableDownloadsAsync()
        {
            var result = new List<DownloadItem>();

            try
            {
                foreach (var root in _roots)
                {
                    if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;

                    // enumerate immediate .tmp_* directories
                    IEnumerable<string> dirs = Array.Empty<string>();
                    try { dirs = Directory.EnumerateDirectories(root, ".tmp_*", SearchOption.TopDirectoryOnly); } catch (Exception ex) { LoggingService.Log($"[ResumeScannerService] EnumerateDirectories failed for root {root}: {ex.Message}"); }

                    foreach (var dir in dirs)
                    {
                        try
                        {
                            var metaPath = Path.Combine(dir, "metadata.json");
                            if (!File.Exists(metaPath)) continue;

                            // 1. Try reading via DurableMetadataManager (Schema v1-v3)
                            var metaManager = new DurableMetadataManager();
                            var state = await metaManager.ReadStateAsync(metaPath, CancellationToken.None).ConfigureAwait(false);

                            if (state != null && !string.IsNullOrWhiteSpace(state.DestinationPath))
                            {
                                long downloaded = state.Segments?.Sum(s => s.BytesDownloaded) ?? 0;
                                double progress = state.TotalBytes > 0 ? (downloaded * 100.0 / state.TotalBytes) : 0;

                                var item = new DownloadItem
                                {
                                    Id = Guid.TryParse(state.DownloadId, out var gid) ? gid : Guid.NewGuid(),
                                    Url = !string.IsNullOrWhiteSpace(state.Url) ? state.Url : state.OriginalUrl,
                                    SavePath = state.DestinationPath,
                                    FileName = !string.IsNullOrWhiteSpace(state.Filename) ? state.Filename : (Path.GetFileName(state.DestinationPath) ?? "download.bin"),
                                    Status = "Paused",
                                    DownloadedBytes = downloaded,
                                    TotalBytes = state.TotalBytes,
                                    Progress = progress,
                                    Size = FormatBytes(state.TotalBytes),
                                    Description = $"Recoverable partial download ({progress:F1}%)",
                                    LastTryDate = state.LastUpdatedTimeUtc.ToLocalTime().ToString("MMM dd, yyyy")
                                };
                                result.Add(item);
                                continue;
                            }

                            // 2. Legacy fallback
                            var raw = await File.ReadAllTextAsync(metaPath).ConfigureAwait(false);
                            var meta = JsonSerializer.Deserialize<DownloadMetadata>(raw);
                            if (meta != null && !string.IsNullOrWhiteSpace(meta.Destination))
                            {
                                var item = new DownloadItem
                                {
                                    Url = meta.Url ?? string.Empty,
                                    SavePath = meta.Destination,
                                    FileName = Path.GetFileName(meta.Destination) ?? string.Empty,
                                    Status = "Paused",
                                    Description = "Found partial download; resume available",
                                    LastTryDate = DateTime.Now.ToString("MMM dd, yyyy")
                                };
                                result.Add(item);
                            }
                        }
                        catch (Exception ex) { LoggingService.Log($"[ResumeScannerService] Processing meta dir {dir} failed: {ex.Message}"); }
                    }
                }
            }
            catch (Exception ex) { LoggingService.Log($"[ResumeScannerService] FindResumableDownloadsAsync failed: {ex.Message}"); }

            return result;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double dbl = bytes;
            while (dbl >= 1024 && i < suffixes.Length - 1)
            {
                dbl /= 1024.0;
                i++;
            }
            return $"{dbl:0.##} {suffixes[i]}";
        }
    }
}
