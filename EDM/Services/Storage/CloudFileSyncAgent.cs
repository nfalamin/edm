using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services.Interfaces;

namespace EDM.Services.Storage
{
    public record CloudFileRecordDto(
        Guid Id,
        Guid OwnerId,
        Guid? DeviceId,
        string FileName,
        string RelativePath,
        string Category,
        long FileSizeBytes,
        string Sha256Hash,
        int Version,
        string SyncState,
        DateTime CreatedAtUtc,
        DateTime ModifiedAtUtc,
        bool IsDeleted = false,
        DateTime? DeletedAtUtc = null);

    public record SyncResult(
        bool Success,
        string Message,
        LocalSyncState State,
        CloudFileRecordDto? CloudRecord = null,
        bool IsConflict = false,
        string? ConflictDetails = null);

    public record CloudSyncDeltasResult(
        DateTime? SinceUtc,
        int? SinceVersion,
        DateTime ServerTimeUtc,
        List<CloudFileRecordDto> Changes);

    /// <summary>
    /// Desktop-to-Cloud File Synchronization Agent.
    /// Bridges the Local HDD with the Control Plane API, handling metadata sync,
    /// conflict resolution, remote download instructions, delta synchronization, and offline recovery.
    /// </summary>
    public class CloudFileSyncAgent
    {
        private static readonly Lazy<CloudFileSyncAgent> _instance = new(() => new CloudFileSyncAgent());
        public static CloudFileSyncAgent Instance => _instance.Value;

        private readonly HttpClient _httpClient;
        private readonly LocalHddStorageEngine _storageEngine;
        private readonly ISettingsService _settingsService;
        private readonly ControlPlaneClient _controlPlaneClient;

        public CloudFileSyncAgent(
            HttpClient? httpClient = null, 
            LocalHddStorageEngine? storageEngine = null,
            ISettingsService? settingsService = null,
            ControlPlaneClient? controlPlaneClient = null)
        {
            _settingsService = settingsService ?? new SettingsService();
            _httpClient = httpClient ?? SharedHttpClient.Instance;
            _storageEngine = storageEngine ?? LocalHddStorageEngine.Instance;
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

        public async Task<SyncResult> SyncLocalFileAsync(string relativePath, string? category = null, CancellationToken ct = default)
        {
            string fullPath = Path.Combine(_storageEngine.StorageRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                return new SyncResult(false, "Local file not found.", LocalSyncState.Error);
            }

            var fi = new FileInfo(fullPath);
            string hash = await _storageEngine.CalculateFileSha256Async(fullPath, ct).ConfigureAwait(false);
            Guid installationId = _controlPlaneClient.EnsureInstallationId();

            var payload = new
            {
                FileName = fi.Name,
                RelativePath = relativePath.Replace('\\', '/'),
                Category = category ?? "General",
                FileSizeBytes = fi.Length,
                Sha256Hash = hash,
                Version = 1,
                DeviceId = installationId,
                ModifiedAtUtc = fi.LastWriteTimeUtc
            };

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{GetApiBaseUrl()}/api/v1/storage/files");
                ApplyAuthHeader(req);
                req.Content = JsonContent.Create(payload);

                using var response = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
                    var fileProp = doc.GetProperty("file");
                    var cloudRec = JsonSerializer.Deserialize<CloudFileRecordDto>(fileProp.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    return new SyncResult(true, "File metadata synced with cloud.", LocalSyncState.Synced, cloudRec);
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    var conflictDoc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
                    string msg = conflictDoc.TryGetProperty("message", out var m) ? m.GetString() ?? "Conflict" : "Conflict";

                    return new SyncResult(
                        Success: false,
                        Message: msg,
                        State: LocalSyncState.Conflict,
                        IsConflict: true,
                        ConflictDetails: conflictDoc.GetRawText());
                }
                else
                {
                    return new SyncResult(false, $"Cloud sync returned status {(int)response.StatusCode}", LocalSyncState.Error);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[CloudFileSyncAgent] Cloud sync failed, queueing offline: {ex.Message}");
                _storageEngine.EnqueueOfflineSyncOperation(new OfflineSyncOperation(
                    OperationId: Guid.NewGuid(),
                    OperationType: "REGISTER",
                    FileId: Guid.NewGuid(),
                    RelativePath: relativePath,
                    Sha256Hash: hash,
                    FileSizeBytes: fi.Length,
                    Version: 1,
                    QueuedAtUtc: DateTime.UtcNow,
                    PayloadJson: JsonSerializer.Serialize(payload)));

                return new SyncResult(true, "Offline mode: Sync operation queued locally.", LocalSyncState.Offline);
            }
        }

        public async Task<CloudSyncDeltasResult?> FetchDeltasAsync(DateTime? sinceUtc = null, int? sinceVersion = null, CancellationToken ct = default)
        {
            try
            {
                string url = $"{GetApiBaseUrl()}/api/v1/storage/sync/deltas?";
                if (sinceUtc.HasValue) url += $"sinceUtc={Uri.EscapeDataString(sinceUtc.Value.ToString("O"))}&";
                if (sinceVersion.HasValue) url += $"sinceVersion={sinceVersion.Value}&";

                using var req = new HttpRequestMessage(HttpMethod.Get, url.TrimEnd('&', '?'));
                ApplyAuthHeader(req);

                using var response = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
                    var changesArray = doc.GetProperty("changes");
                    var changes = JsonSerializer.Deserialize<List<CloudFileRecordDto>>(changesArray.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    DateTime serverTime = doc.TryGetProperty("serverTimeUtc", out var st) && st.TryGetDateTime(out var dt) ? dt : DateTime.UtcNow;

                    return new CloudSyncDeltasResult(sinceUtc, sinceVersion, serverTime, changes);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[CloudFileSyncAgent] Fetch deltas failed: {ex.Message}");
            }
            return null;
        }

        public async Task<bool> DownloadFileStreamAsync(
            Guid fileId, 
            string targetRelativePath, 
            IProgress<double>? progress = null, 
            CancellationToken ct = default)
        {
            string url = $"{GetApiBaseUrl()}/api/v1/storage/files/{fileId}/download";

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                ApplyAuthHeader(req);

                using var response = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return false;

                long contentLength = response.Content.Headers.ContentLength ?? -1;
                using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

                await _storageEngine.StreamWriteAtomicAsync(stream, targetRelativePath, contentLength, progress, ct).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[CloudFileSyncAgent] Failed to download file {fileId}", ex);
                return false;
            }
        }

        public async Task<bool> DeleteRemoteFileByPathAsync(string relativePath, CancellationToken ct = default)
        {
            string url = $"{GetApiBaseUrl()}/api/v1/storage/files/by-path?path={Uri.EscapeDataString(relativePath)}";

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Delete, url);
                ApplyAuthHeader(req);

                using var response = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[CloudFileSyncAgent] Delete remote file failed, queueing offline: {ex.Message}");
                _storageEngine.EnqueueOfflineSyncOperation(new OfflineSyncOperation(
                    OperationId: Guid.NewGuid(),
                    OperationType: "DELETE",
                    FileId: Guid.Empty,
                    RelativePath: relativePath,
                    Sha256Hash: string.Empty,
                    FileSizeBytes: 0,
                    Version: 1,
                    QueuedAtUtc: DateTime.UtcNow));
                return false;
            }
        }

        public async Task<SyncResult> ResolveConflictAsync(Guid fileId, string strategy, string? relativePath = null, CancellationToken ct = default)
        {
            try
            {
                string? localHash = null;
                long? localSize = null;

                if (!string.IsNullOrEmpty(relativePath))
                {
                    string fullPath = Path.Combine(_storageEngine.StorageRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(fullPath))
                    {
                        var fi = new FileInfo(fullPath);
                        localSize = fi.Length;
                        localHash = await _storageEngine.CalculateFileSha256Async(fullPath, ct).ConfigureAwait(false);
                    }
                }

                var payload = new
                {
                    Strategy = strategy,
                    ResolvedHash = localHash,
                    ResolvedSize = localSize,
                    NewVersion = 2
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, $"{GetApiBaseUrl()}/api/v1/storage/files/{fileId}/resolve-conflict");
                ApplyAuthHeader(req);
                req.Content = JsonContent.Create(payload);

                using var response = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
                    var fileProp = doc.GetProperty("file");
                    var cloudRec = JsonSerializer.Deserialize<CloudFileRecordDto>(fileProp.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    return new SyncResult(true, "Conflict resolved.", LocalSyncState.Synced, cloudRec);
                }

                return new SyncResult(false, $"Conflict resolution failed with status {(int)response.StatusCode}", LocalSyncState.Error);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[CloudFileSyncAgent] Conflict resolution error", ex);
                return new SyncResult(false, ex.Message, LocalSyncState.Error);
            }
        }

        public async Task<List<CloudFileRecordDto>> FetchCloudFilesAsync(string? category = null, CancellationToken ct = default)
        {
            try
            {
                string url = $"{GetApiBaseUrl()}/api/v1/storage/files";
                if (!string.IsNullOrEmpty(category)) url += $"?category={Uri.EscapeDataString(category)}";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                ApplyAuthHeader(req);

                using var response = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var files = await response.Content.ReadFromJsonAsync<List<CloudFileRecordDto>>(cancellationToken: ct).ConfigureAwait(false);
                    return files ?? new();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[CloudFileSyncAgent] Failed to fetch cloud files: {ex.Message}");
            }
            return new();
        }

        public async Task ProcessOfflineQueueAsync(CancellationToken ct = default)
        {
            var pending = _storageEngine.GetPendingOfflineOperations();
            if (pending.Count == 0) return;

            LoggingService.Log($"[CloudFileSyncAgent] Processing {pending.Count} pending offline sync operations...");

            foreach (var op in pending)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    if (op.OperationType == "DELETE")
                    {
                        bool success = await DeleteRemoteFileByPathAsync(op.RelativePath, ct).ConfigureAwait(false);
                        if (success) _storageEngine.ClearProcessedOfflineOperation(op.OperationId);
                    }
                    else
                    {
                        var syncRes = await SyncLocalFileAsync(op.RelativePath, ct: ct).ConfigureAwait(false);
                        if (syncRes.Success && syncRes.State != LocalSyncState.Offline)
                        {
                            _storageEngine.ClearProcessedOfflineOperation(op.OperationId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Log($"[CloudFileSyncAgent] Replaying offline sync op {op.OperationId} failed: {ex.Message}");
                }
            }
        }
    }
}
