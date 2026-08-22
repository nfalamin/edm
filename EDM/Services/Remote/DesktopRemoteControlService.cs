using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services.Interfaces;

namespace EDM.Services.Remote
{
    public record DesktopLiveDownloadSnapshot(
        string DownloadId,
        string FileName,
        string Url,
        string? Category,
        long TotalBytes,
        long DownloadedBytes,
        double ProgressPercentage,
        double SpeedBytesPerSecond,
        long? EtaSeconds,
        string Status,
        string? ErrorMessage = null);

    public record RemoteCommandDto(
        Guid Id,
        Guid DeviceId,
        string CommandType,
        string? TargetDownloadId,
        string? PayloadJson,
        string Status,
        DateTime CreatedAtUtc);

    /// <summary>
    /// Desktop Remote Control Service for EDM.
    /// Bridges the Desktop Download Engine with the Central Control Plane Dashboard:
    /// 1. Continuously streams live download progress, speed, ETA, and queue status to the Cloud API.
    /// 2. Polls and executes authenticated remote commands (Start, Pause, Resume, Cancel, Retry, Delete, Add URL, Queue Control).
    /// 3. Confirms command state transitions (Received -> Executing -> Completed / Failed) back to Dashboard.
    /// </summary>
    public class DesktopRemoteControlService : IDisposable
    {
        private static readonly Lazy<DesktopRemoteControlService> _instance = new(() => new DesktopRemoteControlService());
        public static DesktopRemoteControlService Instance => _instance.Value;

        private readonly HttpClient _httpClient;
        private readonly ISettingsService _settingsService;
        private readonly ControlPlaneClient _controlPlaneClient;

        private readonly ConcurrentDictionary<string, DesktopLiveDownloadSnapshot> _activeDownloads = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();
        private CancellationTokenSource? _cts;
        private Task? _workerTask;
        private bool _isDisposed;

        // Command Execution Hooks (Injected or wired by MainViewModel / DownloadManager)
        public Func<string, string?, Task<bool>>? AddDownloadHandler { get; set; }
        public Func<string, Task<bool>>? PauseDownloadHandler { get; set; }
        public Func<string, Task<bool>>? ResumeDownloadHandler { get; set; }
        public Func<string, Task<bool>>? CancelDownloadHandler { get; set; }
        public Func<string, Task<bool>>? RetryDownloadHandler { get; set; }
        public Func<string, Task<bool>>? DeleteDownloadHandler { get; set; }
        public Func<string, Task<bool>>? QueueControlHandler { get; set; }
        public Func<string, Task<bool>>? OpenFileHandler { get; set; }
        public Func<string, Task<bool>>? OpenFolderHandler { get; set; }

        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

        public DesktopRemoteControlService(
            HttpClient? httpClient = null,
            ISettingsService? settingsService = null,
            ControlPlaneClient? controlPlaneClient = null)
        {
            _settingsService = settingsService ?? new SettingsService();
            _httpClient = httpClient ?? SharedHttpClient.Instance;
            _controlPlaneClient = controlPlaneClient ?? new ControlPlaneClient(_httpClient, _settingsService);
        }

        private string GetApiBaseUrl()
        {
            if (_httpClient.BaseAddress != null)
            {
                return _httpClient.BaseAddress.ToString().TrimEnd('/');
            }

            string? url = _settingsService.GetSetting("ControlPlaneApiUrl");
            return string.IsNullOrWhiteSpace(url) ? "http://localhost:5000" : url.TrimEnd('/');
        }

        private void ApplyAuthHeader(HttpRequestMessage request)
        {
            string? token = _settingsService.GetSetting("EncryptedAccessToken");
            if (!string.IsNullOrEmpty(token))
            {
                string decrypted = SecureCredentialVault.DecryptSecret(token);
                string tokenToUse = !string.IsNullOrEmpty(decrypted) ? decrypted : token;
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenToUse);
            }
        }

        public void Start(int pollingIntervalSeconds = 3)
        {
            lock (_lock)
            {
                if (IsRunning) return;

                _cts = new CancellationTokenSource();
                var token = _cts.Token;

                _workerTask = Task.Run(() => BackgroundLoopAsync(pollingIntervalSeconds, token), token);
                LoggingService.Log("[DesktopRemoteControlService] Remote control daemon started.");
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!IsRunning) return;

                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
                LoggingService.Log("[DesktopRemoteControlService] Remote control daemon stopped.");
            }
        }

        public void RegisterOrUpdateDownload(DesktopLiveDownloadSnapshot snapshot)
        {
            _activeDownloads[snapshot.DownloadId] = snapshot;
        }

        public void RemoveDownload(string downloadId)
        {
            _activeDownloads.TryRemove(downloadId, out _);
        }

        private async Task BackgroundLoopAsync(int intervalSeconds, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // 1. Send Heartbeat & Live Telemetry
                    await SendHeartbeatAsync(ct).ConfigureAwait(false);

                    // 2. Poll and Execute Pending Commands
                    await PollAndExecuteCommandsAsync(ct).ConfigureAwait(false);

                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(2, intervalSeconds)), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LoggingService.Log($"[DesktopRemoteControlService] Loop iteration error: {ex.Message}");
                    try { await Task.Delay(2000, ct).ConfigureAwait(false); } catch { break; }
                }
            }
        }

        public async Task<bool> SendHeartbeatAsync(CancellationToken ct = default)
        {
            try
            {
                Guid installId = _controlPlaneClient.EnsureInstallationId();
                var downloadsList = _activeDownloads.Values.ToList();
                var drivesList = GetLocalDrives();

                var payload = new
                {
                    InstallationId = installId,
                    OsVersion = Environment.OSVersion.VersionString,
                    AppVersion = "2.0.0",
                    ClientType = "DesktopWindows",
                    Downloads = downloadsList.Select(d => new
                    {
                        d.DownloadId,
                        d.FileName,
                        d.Url,
                        d.Category,
                        d.TotalBytes,
                        d.DownloadedBytes,
                        d.ProgressPercentage,
                        d.SpeedBytesPerSecond,
                        d.EtaSeconds,
                        d.Status,
                        d.ErrorMessage
                    }).ToList(),
                    StorageDrives = drivesList
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, $"{GetApiBaseUrl()}/api/v1/remote/devices/heartbeat");
                ApplyAuthHeader(req);
                req.Content = JsonContent.Create(payload);

                using var response = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DesktopRemoteControlService] Heartbeat failed: {ex.Message}");
                return false;
            }
        }

        public static List<object> GetLocalDrives()
        {
            var list = new List<object>();
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    list.Add(new
                    {
                        DriveName = drive.Name,
                        VolumeLabel = string.IsNullOrEmpty(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel,
                        DriveFormat = drive.DriveFormat,
                        TotalSizeBytes = drive.TotalSize,
                        FreeSpaceBytes = drive.TotalFreeSpace,
                        AvailableFreeSpaceBytes = drive.AvailableFreeSpace
                    });
                }
            }
            catch { }
            return list;
        }

        public async Task<bool> SyncCompletedDownloadHistoryAsync(
            string url,
            string fileName,
            string? category,
            long fileSizeBytes,
            string status = "Completed",
            string? sha256Hash = null,
            DateTime? completedAtUtc = null,
            CancellationToken ct = default)
        {
            try
            {
                Guid installId = _controlPlaneClient.EnsureInstallationId();
                var payload = new
                {
                    InstallationId = installId,
                    Records = new[]
                    {
                        new
                        {
                            Url = url,
                            FileName = fileName,
                            Category = category ?? "General",
                            FileSizeBytes = fileSizeBytes,
                            Status = status,
                            Sha256Hash = sha256Hash,
                            CompletedAtUtc = completedAtUtc ?? DateTime.UtcNow
                        }
                    }
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, $"{GetApiBaseUrl()}/api/v1/remote/history/sync");
                ApplyAuthHeader(req);
                req.Content = JsonContent.Create(payload);

                using var response = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DesktopRemoteControlService] History sync failed: {ex.Message}");
                return false;
            }
        }

        public async Task<List<RemoteCommandDto>> FetchPendingCommandsAsync(CancellationToken ct = default)
        {
            try
            {
                Guid installId = _controlPlaneClient.EnsureInstallationId();
                string url = $"{GetApiBaseUrl()}/api/v1/remote/commands/pending?installationId={installId}";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                ApplyAuthHeader(req);

                using var response = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
                    var cmdArray = doc.GetProperty("commands");
                    var list = JsonSerializer.Deserialize<List<RemoteCommandDto>>(cmdArray.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return list ?? new();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DesktopRemoteControlService] Fetch pending commands error: {ex.Message}");
            }
            return new();
        }

        public async Task PollAndExecuteCommandsAsync(CancellationToken ct = default)
        {
            var commands = await FetchPendingCommandsAsync(ct).ConfigureAwait(false);
            if (commands.Count == 0) return;

            LoggingService.Log($"[DesktopRemoteControlService] Received {commands.Count} pending remote command(s).");

            foreach (var cmd in commands)
            {
                if (ct.IsCancellationRequested) break;

                // 1. Acknowledge Received & Executing
                await AcknowledgeCommandStatusAsync(cmd.Id, "Executing", null, ct).ConfigureAwait(false);

                // 2. Dispatch command
                bool success = false;
                string? error = null;

                try
                {
                    success = await ExecuteCommandInternalAsync(cmd, ct).ConfigureAwait(false);
                    if (!success) error = "Command execution returned failure.";
                }
                catch (Exception ex)
                {
                    LoggingService.LogException($"[DesktopRemoteControlService] Command {cmd.Id} failed", ex);
                    success = false;
                    error = ex.Message;
                }

                // 3. Acknowledge Final Status
                await AcknowledgeCommandStatusAsync(cmd.Id, success ? "Completed" : "Failed", error, ct).ConfigureAwait(false);
            }
        }

        private async Task<bool> ExecuteCommandInternalAsync(RemoteCommandDto cmd, CancellationToken ct)
        {
            LoggingService.Log($"[DesktopRemoteControlService] Executing remote command '{cmd.CommandType}' (ID: {cmd.Id})");

            switch (cmd.CommandType)
            {
                case "AddUrl":
                    string? url = null;
                    string? fileName = null;
                    if (!string.IsNullOrEmpty(cmd.PayloadJson))
                    {
                        using var doc = JsonDocument.Parse(cmd.PayloadJson);
                        if (doc.RootElement.TryGetProperty("url", out var u)) url = u.GetString();
                        if (doc.RootElement.TryGetProperty("fileName", out var f)) fileName = f.GetString();
                    }
                    if (string.IsNullOrEmpty(url)) return false;

                    if (AddDownloadHandler != null)
                    {
                        return await AddDownloadHandler(url, fileName).ConfigureAwait(false);
                    }
                    else
                    {
                        var gateway = (App.ServiceProvider?.GetService(typeof(Interfaces.IDownloadRequestGateway)) as Interfaces.IDownloadRequestGateway)
                            ?? new DownloadRequestGateway(_settingsService);

                        var res = await gateway.SubmitRequestAsync(new DownloadRequest
                        {
                            Source = IngestionSource.RemoteDashboard,
                            Url = url,
                            SuggestedFileName = fileName
                        }, ct).ConfigureAwait(false);

                        return res.IsSuccess;
                    }

                case "PauseDownload":
                    if (string.IsNullOrEmpty(cmd.TargetDownloadId)) return false;
                    if (PauseDownloadHandler != null) return await PauseDownloadHandler(cmd.TargetDownloadId).ConfigureAwait(false);
                    return UpdateDownloadStatus(cmd.TargetDownloadId, "Paused");

                case "ResumeDownload":
                case "StartDownload":
                    if (string.IsNullOrEmpty(cmd.TargetDownloadId)) return false;
                    if (ResumeDownloadHandler != null) return await ResumeDownloadHandler(cmd.TargetDownloadId).ConfigureAwait(false);
                    return UpdateDownloadStatus(cmd.TargetDownloadId, "Downloading");

                case "CancelDownload":
                    if (string.IsNullOrEmpty(cmd.TargetDownloadId)) return false;
                    if (CancelDownloadHandler != null) return await CancelDownloadHandler(cmd.TargetDownloadId).ConfigureAwait(false);
                    return UpdateDownloadStatus(cmd.TargetDownloadId, "Stopped");

                case "RetryDownload":
                    if (string.IsNullOrEmpty(cmd.TargetDownloadId)) return false;
                    if (RetryDownloadHandler != null) return await RetryDownloadHandler(cmd.TargetDownloadId).ConfigureAwait(false);
                    return UpdateDownloadStatus(cmd.TargetDownloadId, "Downloading");

                case "DeleteDownload":
                    if (string.IsNullOrEmpty(cmd.TargetDownloadId)) return false;
                    if (DeleteDownloadHandler != null) return await DeleteDownloadHandler(cmd.TargetDownloadId).ConfigureAwait(false);
                    RemoveDownload(cmd.TargetDownloadId);
                    return true;

                case "QueueControl":
                    string action = "toggle";
                    if (!string.IsNullOrEmpty(cmd.PayloadJson))
                    {
                        using var doc = JsonDocument.Parse(cmd.PayloadJson);
                        if (doc.RootElement.TryGetProperty("action", out var a)) action = a.GetString() ?? "toggle";
                    }
                    if (QueueControlHandler != null) return await QueueControlHandler(action).ConfigureAwait(false);
                    return true;

                case "OpenFile":
                    if (!string.IsNullOrEmpty(cmd.TargetDownloadId) && OpenFileHandler != null)
                    {
                        return await OpenFileHandler(cmd.TargetDownloadId).ConfigureAwait(false);
                    }
                    return true;

                case "OpenFolder":
                    if (!string.IsNullOrEmpty(cmd.TargetDownloadId) && OpenFolderHandler != null)
                    {
                        return await OpenFolderHandler(cmd.TargetDownloadId).ConfigureAwait(false);
                    }
                    return true;

                default:
                    LoggingService.LogWarning($"[DesktopRemoteControlService] Unsupported command type '{cmd.CommandType}'");
                    return false;
            }
        }

        private bool UpdateDownloadStatus(string downloadId, string newStatus)
        {
            if (_activeDownloads.TryGetValue(downloadId, out var existing))
            {
                _activeDownloads[downloadId] = existing with { Status = newStatus };
                return true;
            }
            return false;
        }

        public async Task<bool> AcknowledgeCommandStatusAsync(Guid commandId, string status, string? errorMessage = null, CancellationToken ct = default)
        {
            try
            {
                string url = $"{GetApiBaseUrl()}/api/v1/remote/commands/{commandId}/ack";
                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                ApplyAuthHeader(req);
                req.Content = JsonContent.Create(new
                {
                    Status = status,
                    ErrorMessage = errorMessage
                });

                using var response = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DesktopRemoteControlService] Acknowledge command {commandId} failed: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Stop();
        }
    }
}
