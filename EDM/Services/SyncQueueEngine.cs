using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;

namespace EDM.Services
{
    public class SyncQueueItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Url { get; set; } = string.Empty;
        public string LocalFilePath { get; set; } = string.Empty;
        public string? LastETag { get; set; }
        public DateTimeOffset? LastModified { get; set; }
        public long LastContentLength { get; set; }
        public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(1);
        public DateTime LastCheckedAt { get; set; } = DateTime.MinValue;
        public bool KeepBackupOfOldVersion { get; set; } = true;
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// EDM Native Periodic Synchronization Queue Engine.
    /// Periodically probes remote resources with HTTP HEAD requests, inspects ETag / Last-Modified / Content-Length headers,
    /// and automatically redownloads and performs atomic file swaps when remote contents change.
    /// </summary>
    public class SyncQueueEngine : IDisposable
    {
        private static readonly Lazy<SyncQueueEngine> _instance = new(() => new SyncQueueEngine());
        public static SyncQueueEngine Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, SyncQueueItem> _items = new(StringComparer.OrdinalIgnoreCase);
        private readonly HttpClient _httpClient;
        private System.Threading.Timer? _timer;
        private bool _isChecking;
        private readonly object _lock = new();

        public event Action<SyncQueueItem>? FileUpdated;
        public event Action<SyncQueueItem, string>? SyncError;

        public SyncQueueEngine(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? SharedHttpClient.Instance;
            // Run check loop every 60 seconds
            _timer = new System.Threading.Timer(async _ => await ProcessSyncLoopAsync().ConfigureAwait(false), null, 5000, 60000);
        }

        public void AddOrUpdateItem(SyncQueueItem item)
        {
            _items[item.Id] = item;
            LoggingService.Log($"[SyncQueueEngine] Registered sync item: {item.Url} -> {item.LocalFilePath}");
        }

        public bool RemoveItem(string id)
        {
            return _items.TryRemove(id, out _);
        }

        public IReadOnlyCollection<SyncQueueItem> GetItems() => new List<SyncQueueItem>(_items.Values);

        public async Task ProcessSyncLoopAsync(CancellationToken ct = default)
        {
            lock (_lock)
            {
                if (_isChecking) return;
                _isChecking = true;
            }

            try
            {
                var now = DateTime.UtcNow;
                foreach (var item in _items.Values)
                {
                    if (ct.IsCancellationRequested) break;
                    if (!item.IsEnabled) continue;

                    if (now - item.LastCheckedAt >= item.CheckInterval)
                    {
                        await CheckAndSyncItemAsync(item, ct).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                lock (_lock)
                {
                    _isChecking = false;
                }
            }
        }

        public async Task<bool> CheckAndSyncItemAsync(SyncQueueItem item, CancellationToken ct = default)
        {
            item.LastCheckedAt = DateTime.UtcNow;

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, item.Url);
                if (!string.IsNullOrEmpty(item.LastETag))
                {
                    req.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(item.LastETag));
                }
                if (item.LastModified.HasValue)
                {
                    req.Headers.IfModifiedSince = item.LastModified;
                }

                using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                if (resp.StatusCode == System.Net.HttpStatusCode.NotModified)
                {
                    LoggingService.Log($"[SyncQueueEngine] {item.Url} is unchanged (HTTP 304 Not Modified).");
                    return false;
                }

                if (!resp.IsSuccessStatusCode)
                {
                    SyncError?.Invoke(item, $"Probe returned HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}");
                    return false;
                }

                string? newETag = resp.Headers.ETag?.Tag;
                DateTimeOffset? newLastModified = resp.Content.Headers.LastModified;
                long newLength = resp.Content.Headers.ContentLength ?? 0;

                bool hasChanged = (newETag != null && !string.Equals(newETag, item.LastETag, StringComparison.OrdinalIgnoreCase)) ||
                                  (newLastModified.HasValue && newLastModified != item.LastModified) ||
                                  (newLength > 0 && newLength != item.LastContentLength);

                if (hasChanged || !File.Exists(item.LocalFilePath))
                {
                    LoggingService.Log($"[SyncQueueEngine] Remote file changed for {item.Url}. Downloading updated payload...");

                    string tempDest = item.LocalFilePath + ".synctmp";
                    using (var getReq = new HttpRequestMessage(HttpMethod.Get, item.Url))
                    using (var getResp = await _httpClient.SendAsync(getReq, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                    {
                        getResp.EnsureSuccessStatusCode();
                        string? dir = Path.GetDirectoryName(item.LocalFilePath);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                        await using (var fs = new FileStream(tempDest, FileMode.Create, FileAccess.Write, FileShare.None))
                        await using (var netStream = await getResp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
                        {
                            await netStream.CopyToAsync(fs, ct).ConfigureAwait(false);
                        }
                    }

                    // Atomic swap
                    if (File.Exists(item.LocalFilePath))
                    {
                        if (item.KeepBackupOfOldVersion)
                        {
                            string backupPath = item.LocalFilePath + $".bak_{DateTime.UtcNow:yyyyMMddHHmmss}";
                            File.Move(item.LocalFilePath, backupPath, true);
                        }
                        else
                        {
                            File.Delete(item.LocalFilePath);
                        }
                    }

                    File.Move(tempDest, item.LocalFilePath, true);

                    item.LastETag = newETag;
                    item.LastModified = newLastModified;
                    item.LastContentLength = newLength;

                    FileUpdated?.Invoke(item);
                    LoggingService.Log($"[SyncQueueEngine] Successfully synchronized {item.LocalFilePath}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[SyncQueueEngine] Error syncing {item.Url}", ex);
                SyncError?.Invoke(item, ex.Message);
                return false;
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}
