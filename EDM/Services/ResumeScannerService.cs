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

                            var raw = await File.ReadAllTextAsync(metaPath).ConfigureAwait(false);
                            // DownloadMetadata is internal in MultiPartDownloader; same namespace allows access
                            var meta = JsonSerializer.Deserialize<DownloadMetadata>(raw);
                            if (meta == null) continue;

                            // basic sanity checks
                            if (string.IsNullOrWhiteSpace(meta.Destination)) continue;

                            var item = new DownloadItem
                            {
                                Url = meta.Url ?? string.Empty,
                                SavePath = meta.Destination,
                                FileName = Path.GetFileName(meta.Destination) ?? string.Empty,
                                Status = "Resumable",
                                Description = "Found partial download; resume available",
                                LastTryDate = DateTime.Now.ToString("MMM dd, yyyy")
                            };

                            // attach a metadata helper path so the UI can find meta dir for cleanup or resume
                            try { item.Description += $" (meta: {Path.GetFileName(dir)})"; } catch (Exception ex) { LoggingService.Log($"[ResumeScannerService] Failed to append meta description for {dir}: {ex.Message}"); }

                            result.Add(item);
                        }
                        catch (Exception ex) { LoggingService.Log($"[ResumeScannerService] Processing meta dir {dir} failed: {ex.Message}"); }
                    }
                }
            }
            catch (Exception ex) { LoggingService.Log($"[ResumeScannerService] FindResumableDownloadsAsync failed: {ex.Message}"); }

            return result;
        }
    }
}
