using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    // Default persistence uses JSON. If you want LiteDB, add the LiteDB NuGet package and
    // replace this implementation or update to use LiteDB APIs.
    public class DownloadHistoryService : IHistoryProvider
    {
        private readonly string _filePath;

        public DownloadHistoryService()
        {
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EDM");
            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }
            _filePath = Path.Combine(appDataFolder, "downloads.json");
        }

        public async Task<ObservableCollection<DownloadItem>> LoadHistoryAsync()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string jsonString = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
                    var list = JsonSerializer.Deserialize<List<DownloadItem>>(jsonString);
                    if (list != null)
                    {
                        return new ObservableCollection<DownloadItem>(list);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadHistoryService] LoadHistoryAsync failed", ex);
            }
            return new ObservableCollection<DownloadItem>();
        }

        public async Task SaveHistoryAsync(ObservableCollection<DownloadItem> downloads)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(downloads, options);
                var tmp = _filePath + ".tmp";
                await File.WriteAllTextAsync(tmp, jsonString).ConfigureAwait(false);
                try { File.Move(tmp, _filePath, true); }
                catch (Exception moveEx)
                {
                    try { File.Copy(tmp, _filePath, true); }
                    catch (Exception copyEx) { LoggingService.LogException("[DownloadHistoryService] Failed to move or copy tmp file", new AggregateException(moveEx, copyEx)); }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadHistoryService] SaveHistoryAsync failed", ex);
            }
        }
    }
}
