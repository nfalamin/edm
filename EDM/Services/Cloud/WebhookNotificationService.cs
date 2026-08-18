using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EDM.Models;

namespace EDM.Services.Cloud
{
    public class WebhookConfig
    {
        public bool IsEnabled { get; set; } = false;
        public string WebhookUrl { get; set; } = string.Empty;
        public bool NotifyOnComplete { get; set; } = true;
        public bool NotifyOnFailure { get; set; } = true;
        public string ServiceType { get; set; } = "Discord"; // "Discord", "Telegram", "CustomJson"
    }

    /// <summary>
    /// Webhook Notification Dispatcher.
    /// Pushes instant completion and failure alerts to Discord, Telegram, or custom REST webhooks.
    /// </summary>
    public class WebhookNotificationService
    {
        private static readonly Lazy<WebhookNotificationService> _instance = new(() => new WebhookNotificationService());
        public static WebhookNotificationService Instance => _instance.Value;

        private readonly HttpClient _httpClient;
        public WebhookConfig Config { get; set; } = new();

        public WebhookNotificationService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? SharedHttpClient.Instance;
        }

        public async Task<bool> SendDownloadNotificationAsync(DownloadItem item, bool isSuccess, string? errorReason = null)
        {
            if (!Config.IsEnabled || string.IsNullOrWhiteSpace(Config.WebhookUrl)) return false;
            if (isSuccess && !Config.NotifyOnComplete) return false;
            if (!isSuccess && !Config.NotifyOnFailure) return false;

            try
            {
                string jsonPayload = BuildPayload(item, isSuccess, errorReason);
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var resp = await _httpClient.PostAsync(Config.WebhookUrl, content).ConfigureAwait(false);
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[WebhookNotificationService] Webhook post failed", ex);
                return false;
            }
        }

        public string BuildPayload(DownloadItem item, bool isSuccess, string? errorReason)
        {
            if (Config.ServiceType == "Discord")
            {
                var discord = new
                {
                    username = "EDM Download Manager",
                    embeds = new[]
                    {
                        new
                        {
                            title = isSuccess ? "✅ Download Completed" : "❌ Download Failed",
                            description = $"**File:** `{item.FileName}`\n**Size:** `{item.Size}`\n**Category:** `{item.Category}`",
                            color = isSuccess ? 0x10B981 : 0xEF4444,
                            timestamp = DateTime.UtcNow.ToString("o")
                        }
                    }
                };
                return JsonSerializer.Serialize(discord);
            }
            else
            {
                var generic = new
                {
                    Event = isSuccess ? "DOWNLOAD_COMPLETED" : "DOWNLOAD_FAILED",
                    FileName = item.FileName,
                    Size = item.Size,
                    Url = item.Url,
                    Category = item.Category,
                    Error = errorReason ?? string.Empty,
                    Timestamp = DateTime.UtcNow
                };
                return JsonSerializer.Serialize(generic);
            }
        }
    }
}
