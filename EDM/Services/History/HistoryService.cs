using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDM.Helpers;
using EDM.Models;
using EDM.Services.Data;
using EDM.Services.Interfaces;
using Microsoft.Data.Sqlite;

namespace EDM.Services.History
{
    public class DownloadHistoryEntry
    {
        public long Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public long TotalBytes { get; set; }
        public long BytesDownloaded { get; set; }
        public double LastSpeedBytesPerSecond { get; set; }
        public double AverageSpeedBytesPerSecond { get; set; }
        public int Status { get; set; } // 0=Incomplete/Paused/Queued, 1=Completed, 2=Error
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public int VerificationState { get; set; }
        public string? VerificationAlgorithm { get; set; }
        public string? TrustedHash { get; set; }
        public string? ComputedHash { get; set; }
        public string? VerificationMessage { get; set; }
        public DateTime? VerificationTime { get; set; }
    }

    public class HistoryService : IDisposable, IHistoryProvider
    {
        private readonly string _dbPath;
        private readonly SqliteConnectionManager _connManager;
        private readonly DbMigrationService _migrationService;
        private readonly DbBackupService _backupService;
        private readonly DatabaseAuditLog _auditLog;

        private readonly SemaphoreSlim _initSemaphore = new(1, 1);
        private volatile bool _isInitialized;

        public HistoryService(string? dbPath = null)
        {
            // Default to %LOCALAPPDATA%\EDM\edm_history.db
            try
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var folder = Path.Combine(local ?? AppContext.BaseDirectory, "EDM");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                _dbPath = string.IsNullOrWhiteSpace(dbPath) ? Path.Combine(folder, "edm_history.db") : dbPath!;
            }
            catch
            {
                _dbPath = string.IsNullOrWhiteSpace(dbPath) ? Path.Combine(AppContext.BaseDirectory, "edm_history.db") : dbPath!;
            }

            // Initialize connection manager with thread-safe pooling
            _connManager = new SqliteConnectionManager(_dbPath, maxPoolSize: 5);
            _migrationService = new DbMigrationService(_connManager);
            _backupService = new DbBackupService(_dbPath);
            _auditLog = new DatabaseAuditLog(maxEntriesInMemory: 500);

            // Trigger non-blocking async initialization in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await EnsureInitializedAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[HistoryService] Background initialization failed", ex);
                }
            });
        }

        /// <summary>
        /// Ensures database migrations and startup deduplication are executed exactly once before dependent operations.
        /// Thread-safe, re-entrant, and non-blocking for concurrent callers.
        /// </summary>
        public async Task EnsureInitializedAsync(CancellationToken ct = default)
        {
            if (_isInitialized) return;

            await _initSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_isInitialized) return;

                await _migrationService.RunMigrationsAsync().ConfigureAwait(false);
                await DeduplicateDatabaseCoreAsync().ConfigureAwait(false);

                _isInitialized = true;
                LoggingService.Log("[HistoryService] Database initialization & migrations complete");
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[HistoryService] Migration / Initialization failed", ex);
                throw;
            }
            finally
            {
                _initSemaphore.Release();
            }
        }

        private void EnsureInitializedSync()
        {
            if (_isInitialized) return;
            try
            {
                EnsureInitializedAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[HistoryService.EnsureInitializedSync] Failed", ex);
            }
        }

        public static string CanonicalizeUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            string trimmed = url.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                var builder = new UriBuilder(uri);
                builder.Host = builder.Host.ToLowerInvariant();
                if ((builder.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && builder.Port == 80) ||
                    (builder.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) && builder.Port == 443))
                {
                    builder.Port = -1;
                }
                return builder.Uri.ToString();
            }
            return trimmed;
        }

        public static string CanonicalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return path.Trim();
            }
        }

        public long CreateEntry(string url, string destination, long totalBytes)
        {
            EnsureInitializedSync();
            var sw = Stopwatch.StartNew();
            try
            {
                var conn = _connManager.GetConnection();
                try
                {
                    string canonicalUrl = CanonicalizeUrl(url);
                    string canonicalDest = CanonicalizePath(destination);

                    using var checkCmd = conn.CreateCommand();
                    checkCmd.CommandText = "SELECT id FROM downloads WHERE (url=@url OR url=@rawUrl) AND (destination=@dest OR destination=@rawDest) LIMIT 1";
                    checkCmd.Parameters.Add("@url", SqliteType.Text).Value = canonicalUrl;
                    checkCmd.Parameters.Add("@rawUrl", SqliteType.Text).Value = url ?? string.Empty;
                    checkCmd.Parameters.Add("@dest", SqliteType.Text).Value = canonicalDest;
                    checkCmd.Parameters.Add("@rawDest", SqliteType.Text).Value = destination ?? string.Empty;
                    checkCmd.Prepare();

                    var existing = checkCmd.ExecuteScalar();
                    if (existing != null && existing != DBNull.Value)
                    {
                        long existingId = Convert.ToInt64(existing);
                        if (totalBytes > 0)
                        {
                            using var updCmd = conn.CreateCommand();
                            updCmd.CommandText = "UPDATE downloads SET total_bytes=@totalBytes WHERE id=@id AND (total_bytes <= 0 OR total_bytes IS NULL)";
                            updCmd.Parameters.Add("@totalBytes", SqliteType.Integer).Value = totalBytes;
                            updCmd.Parameters.Add("@id", SqliteType.Integer).Value = existingId;
                            updCmd.Prepare();
                            updCmd.ExecuteNonQuery();
                        }
                        return existingId;
                    }

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO downloads (url, destination, total_bytes, created_at, status) VALUES (@url, @dest, @totalBytes, @createdAt, 0); SELECT last_insert_rowid();";
                    cmd.CommandTimeout = 5;

                    cmd.Parameters.Add("@url", SqliteType.Text).Value = canonicalUrl;
                    cmd.Parameters.Add("@dest", SqliteType.Text).Value = canonicalDest;
                    cmd.Parameters.Add("@totalBytes", SqliteType.Integer).Value = totalBytes;
                    cmd.Parameters.Add("@createdAt", SqliteType.Text).Value = DateTime.UtcNow.ToString("o");

                    cmd.Prepare();
                    var result = cmd.ExecuteScalar();

                    sw.Stop();
                    _auditLog.LogQuery("INSERT", "CreateEntry", sw.ElapsedMilliseconds, success: true);
                    LoggingService.Log($"[HistoryService.CreateEntry] Created entry id={result} for {canonicalUrl}");

                    return result != null ? Convert.ToInt64(result) : -1;
                }
                finally
                {
                    _connManager.ReturnConnection(conn);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                _auditLog.LogQuery("INSERT", "CreateEntry", sw.ElapsedMilliseconds, success: false, errorMessage: ex.Message);
                LoggingService.Log($"[HistoryService.CreateEntry] Failed: {ex.Message}");
                return -1;
            }
        }

        public void UpdateProgress(long id, long bytesDownloaded, double lastSpeed, double avgSpeed)
        {
            EnsureInitializedSync();
            var sw = Stopwatch.StartNew();
            try
            {
                var conn = _connManager.GetConnection();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "UPDATE downloads SET bytes_downloaded=@bytes, last_speed=@lastSpeed, avg_speed=@avgSpeed WHERE id=@id";
                    cmd.CommandTimeout = 5;

                    cmd.Parameters.Add("@bytes", SqliteType.Integer).Value = bytesDownloaded;
                    cmd.Parameters.Add("@lastSpeed", SqliteType.Real).Value = lastSpeed;
                    cmd.Parameters.Add("@avgSpeed", SqliteType.Real).Value = avgSpeed;
                    cmd.Parameters.Add("@id", SqliteType.Integer).Value = id;

                    cmd.Prepare();
                    cmd.ExecuteNonQuery();

                    sw.Stop();
                    _auditLog.LogQuery("UPDATE", "UpdateProgress", sw.ElapsedMilliseconds, success: true);
                }
                finally
                {
                    _connManager.ReturnConnection(conn);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                _auditLog.LogQuery("UPDATE", "UpdateProgress", sw.ElapsedMilliseconds, success: false, errorMessage: ex.Message);
                LoggingService.Log($"[HistoryService.UpdateProgress] Failed: {ex.Message}");
            }
        }

        public void MarkCompleted(long id)
        {
            EnsureInitializedSync();
            var sw = Stopwatch.StartNew();
            try
            {
                var conn = _connManager.GetConnection();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "UPDATE downloads SET status=1, completed_at=@completedAt WHERE id=@id";
                    cmd.CommandTimeout = 5;

                    cmd.Parameters.Add("@completedAt", SqliteType.Text).Value = DateTime.UtcNow.ToString("o");
                    cmd.Parameters.Add("@id", SqliteType.Integer).Value = id;

                    cmd.Prepare();
                    cmd.ExecuteNonQuery();

                    sw.Stop();
                    _auditLog.LogQuery("UPDATE", "MarkCompleted", sw.ElapsedMilliseconds, success: true);
                }
                finally
                {
                    _connManager.ReturnConnection(conn);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                _auditLog.LogQuery("UPDATE", "MarkCompleted", sw.ElapsedMilliseconds, success: false, errorMessage: ex.Message);
                LoggingService.Log($"[HistoryService.MarkCompleted] Failed: {ex.Message}");
            }
        }

        public IEnumerable<DownloadHistoryEntry> ListRecent(int limit = 100)
        {
            EnsureInitializedSync();
            var sw = Stopwatch.StartNew();
            try
            {
                var conn = _connManager.GetConnection();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT id, url, destination, total_bytes, bytes_downloaded, last_speed, avg_speed, status, created_at, completed_at, verification_state, verification_algorithm, trusted_hash, computed_hash, verification_message, verification_time FROM downloads ORDER BY created_at DESC LIMIT @limit";
                    cmd.CommandTimeout = 10;

                    cmd.Parameters.Add("@limit", SqliteType.Integer).Value = Math.Max(1, limit);
                    cmd.Prepare();

                    using var rdr = cmd.ExecuteReader();
                    var results = new List<DownloadHistoryEntry>();

                    while (rdr.Read())
                    {
                        results.Add(new DownloadHistoryEntry
                        {
                            Id = rdr.GetInt64(0),
                            Url = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1),
                            Destination = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2),
                            TotalBytes = rdr.GetInt64(3),
                            BytesDownloaded = rdr.GetInt64(4),
                            LastSpeedBytesPerSecond = rdr.GetDouble(5),
                            AverageSpeedBytesPerSecond = rdr.GetDouble(6),
                            Status = rdr.GetInt32(7),
                            CreatedAt = DateTime.Parse(rdr.GetString(8), null, System.Globalization.DateTimeStyles.RoundtripKind),
                            CompletedAt = rdr.IsDBNull(9) ? null : DateTime.Parse(rdr.GetString(9), null, System.Globalization.DateTimeStyles.RoundtripKind),

                            VerificationState = rdr.IsDBNull(10) ? 0 : rdr.GetInt32(10),
                            VerificationAlgorithm = rdr.IsDBNull(11) ? null : rdr.GetString(11),
                            TrustedHash = rdr.IsDBNull(12) ? null : rdr.GetString(12),
                            ComputedHash = rdr.IsDBNull(13) ? null : rdr.GetString(13),
                            VerificationMessage = rdr.IsDBNull(14) ? null : rdr.GetString(14),
                            VerificationTime = rdr.IsDBNull(15) ? null : DateTime.Parse(rdr.GetString(15), null, System.Globalization.DateTimeStyles.RoundtripKind)
                        });
                    }

                    sw.Stop();
                    _auditLog.LogQuery("SELECT", "ListRecent", sw.ElapsedMilliseconds, success: true);

                    return results;
                }
                finally
                {
                    _connManager.ReturnConnection(conn);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                _auditLog.LogQuery("SELECT", "ListRecent", sw.ElapsedMilliseconds, success: false, errorMessage: ex.Message);
                LoggingService.Log($"[HistoryService.ListRecent] Failed: {ex.Message}");
                return new List<DownloadHistoryEntry>();
            }
        }

        // Implement IHistoryProvider for compatibility with DownloadHistoryService
        public async Task<ObservableCollection<DownloadItem>> LoadHistoryAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            try
            {
                var list = new List<DownloadItem>();
                foreach (var entry in ListRecent(100))
                {
                    bool isCompleted = entry.Status == 1;
                    double progress = isCompleted ? 100.0 :
                        (entry.TotalBytes > 0 && entry.BytesDownloaded > 0
                            ? Math.Clamp(Math.Round((double)entry.BytesDownloaded / entry.TotalBytes * 100.0, 1), 0.0, 100.0)
                            : 0.0);

                    string status = isCompleted ? "Completed" : (entry.BytesDownloaded > 0 ? "Paused" : "Queued");
                    string formattedSize = entry.TotalBytes > 0 ? SizeFormatter.FormatBytes(entry.TotalBytes) : "Unknown";

                    var di = new DownloadItem
                    {
                        FileName = Path.GetFileName(entry.Destination ?? string.Empty),
                        Url = entry.Url ?? string.Empty,
                        SavePath = entry.Destination ?? string.Empty,
                        Status = status,
                        Progress = progress,
                        Size = formattedSize,
                        TimeLeft = isCompleted ? "Completed" : "--",
                        VerificationState = (Models.VerificationState)entry.VerificationState,
                        VerificationAlgorithm = entry.VerificationAlgorithm,
                        TrustedVerificationHash = entry.TrustedHash,
                        ComputedVerificationHash = entry.ComputedHash,
                        VerificationMessage = entry.VerificationMessage,
                        VerificationTimestamp = entry.VerificationTime
                    };
                    list.Add(di);
                }
                return new ObservableCollection<DownloadItem>(list);
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[HistoryService.LoadHistoryAsync] Failed: {ex.Message}");
                return new ObservableCollection<DownloadItem>();
            }
        }

        public async Task<long> SaveDownloadAsync(DownloadItem item)
        {
            if (item == null) return -1;
            await EnsureInitializedAsync().ConfigureAwait(false);

            return await Task.Run(() =>
            {
                var conn = _connManager.GetConnection();
                try
                {
                    string canonicalUrl = CanonicalizeUrl(item.Url);
                    string canonicalDest = CanonicalizePath(item.SavePath);

                    using var checkCmd = conn.CreateCommand();
                    checkCmd.CommandText = "SELECT id, total_bytes FROM downloads WHERE (url=@url OR url=@rawUrl) AND (destination=@dest OR destination=@rawDest) LIMIT 1";
                    checkCmd.Parameters.Add("@url", SqliteType.Text).Value = canonicalUrl;
                    checkCmd.Parameters.Add("@rawUrl", SqliteType.Text).Value = item.Url ?? string.Empty;
                    checkCmd.Parameters.Add("@dest", SqliteType.Text).Value = canonicalDest;
                    checkCmd.Parameters.Add("@rawDest", SqliteType.Text).Value = item.SavePath ?? string.Empty;
                    checkCmd.Prepare();

                    long totalBytes = SizeFormatter.ParseToBytes(item.Size);

                    using var rdr = checkCmd.ExecuteReader();
                    if (rdr.Read())
                    {
                        long id = rdr.GetInt64(0);
                        long dbTotal = rdr.IsDBNull(1) ? -1 : rdr.GetInt64(1);
                        rdr.Close();

                        long finalTotal = totalBytes > 0 ? totalBytes : dbTotal;
                        bool isCompleted = string.Equals(item.Status, "Completed", StringComparison.OrdinalIgnoreCase);

                        long downloadedBytes = item.DownloadedBytes > 0 
                            ? item.DownloadedBytes 
                            : (isCompleted && finalTotal > 0 ? finalTotal : (long)(finalTotal * (item.Progress / 100.0)));
                        if (isCompleted && finalTotal > 0) downloadedBytes = finalTotal;

                        using var updCmd = conn.CreateCommand();
                        updCmd.CommandText = @"UPDATE downloads SET 
                            status=@status, 
                            completed_at=@completedAt, 
                            total_bytes=@total,
                            bytes_downloaded=CASE WHEN @downloaded > 0 THEN @downloaded WHEN @status = 1 AND @total > 0 THEN @total ELSE bytes_downloaded END,
                            verification_state=@vState,
                            verification_algorithm=@vAlgo,
                            trusted_hash=@vTrust,
                            computed_hash=@vComp,
                            verification_message=@vMsg,
                            verification_time=@vTime
                            WHERE id=@id";

                        updCmd.Parameters.Add("@status", SqliteType.Integer).Value = isCompleted ? 1 : 0;
                        updCmd.Parameters.Add("@completedAt", SqliteType.Text).Value = isCompleted ? DateTime.UtcNow.ToString("o") : (object)DBNull.Value;
                        updCmd.Parameters.Add("@total", SqliteType.Integer).Value = finalTotal;
                        updCmd.Parameters.Add("@downloaded", SqliteType.Integer).Value = downloadedBytes;
                        updCmd.Parameters.Add("@vState", SqliteType.Integer).Value = (int)item.VerificationState;
                        updCmd.Parameters.Add("@vAlgo", SqliteType.Text).Value = (object?)item.VerificationAlgorithm ?? DBNull.Value;
                        updCmd.Parameters.Add("@vTrust", SqliteType.Text).Value = (object?)item.TrustedVerificationHash ?? DBNull.Value;
                        updCmd.Parameters.Add("@vComp", SqliteType.Text).Value = (object?)item.ComputedVerificationHash ?? DBNull.Value;
                        updCmd.Parameters.Add("@vMsg", SqliteType.Text).Value = (object?)item.VerificationMessage ?? DBNull.Value;
                        updCmd.Parameters.Add("@vTime", SqliteType.Text).Value = (object?)item.VerificationTimestamp?.ToString("o") ?? DBNull.Value;
                        updCmd.Parameters.Add("@id", SqliteType.Integer).Value = id;

                        updCmd.Prepare();
                        updCmd.ExecuteNonQuery();
                        return id;
                    }
                    else
                    {
                        rdr.Close();
                        return CreateEntry(canonicalUrl, canonicalDest, totalBytes);
                    }
                }
                finally
                {
                    _connManager.ReturnConnection(conn);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Purges any legacy duplicate history records keeping the latest entry with maximum bytes/status.
        /// </summary>
        public async Task<int> DeduplicateDatabaseAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            return await DeduplicateDatabaseCoreAsync().ConfigureAwait(false);
        }

        private async Task<int> DeduplicateDatabaseCoreAsync()
        {
            return await Task.Run(() =>
            {
                var conn = _connManager.GetConnection();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        DELETE FROM downloads 
                        WHERE id NOT IN (
                            SELECT id FROM (
                                SELECT id, 
                                       ROW_NUMBER() OVER (
                                           PARTITION BY lower(trim(url)), lower(trim(destination)) 
                                           ORDER BY status DESC, bytes_downloaded DESC, id DESC
                                       ) as rn
                                FROM downloads
                            ) t
                            WHERE t.rn = 1
                        );";
                    int count = cmd.ExecuteNonQuery();
                    if (count > 0)
                    {
                        LoggingService.Log($"[HistoryService.DeduplicateDatabaseCoreAsync] Purged {count} duplicate history entries.");
                    }
                    return count;
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[HistoryService.DeduplicateDatabaseCoreAsync] Failed", ex);
                    return 0;
                }
                finally
                {
                    _connManager.ReturnConnection(conn);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Atomically saves the entire download history within a single optimized SQLite transaction.
        /// Reuses prepared statements for maximum write throughput.
        /// </summary>
        public Task SaveHistoryAsync(ObservableCollection<DownloadItem> downloads) => SaveHistoryAsync((IEnumerable<DownloadItem>)downloads);

        public async Task SaveHistoryAsync(IEnumerable<DownloadItem> downloads)
        {
            if (downloads == null) return;
            await EnsureInitializedAsync().ConfigureAwait(false);

            await Task.Run(() =>
            {
                var conn = _connManager.GetConnection();
                SqliteTransaction? tx = null;
                try
                {
                    tx = conn.BeginTransaction();

                    using var checkCmd = conn.CreateCommand();
                    checkCmd.Transaction = tx;
                    checkCmd.CommandText = "SELECT id FROM downloads WHERE (url=@url OR url=@rawUrl) AND (destination=@dest OR destination=@rawDest) LIMIT 1";
                    var pCheckUrl = checkCmd.Parameters.Add("@url", SqliteType.Text);
                    var pCheckRawUrl = checkCmd.Parameters.Add("@rawUrl", SqliteType.Text);
                    var pCheckDest = checkCmd.Parameters.Add("@dest", SqliteType.Text);
                    var pCheckRawDest = checkCmd.Parameters.Add("@rawDest", SqliteType.Text);
                    checkCmd.Prepare();

                    using var insCmd = conn.CreateCommand();
                    insCmd.Transaction = tx;
                    insCmd.CommandText = @"INSERT INTO downloads 
                        (url, destination, total_bytes, bytes_downloaded, status, created_at, completed_at, verification_state, verification_algorithm, trusted_hash, computed_hash, verification_message, verification_time) 
                        VALUES (@url, @dest, @total, @downloaded, @status, @createdAt, @completedAt, @verification_state, @verification_algorithm, @trusted_hash, @computed_hash, @verification_message, @verification_time)";
                    var pInsUrl = insCmd.Parameters.Add("@url", SqliteType.Text);
                    var pInsDest = insCmd.Parameters.Add("@dest", SqliteType.Text);
                    var pInsTotal = insCmd.Parameters.Add("@total", SqliteType.Integer);
                    var pInsDownloaded = insCmd.Parameters.Add("@downloaded", SqliteType.Integer);
                    var pInsStatus = insCmd.Parameters.Add("@status", SqliteType.Integer);
                    var pInsCreatedAt = insCmd.Parameters.Add("@createdAt", SqliteType.Text);
                    var pInsCompletedAt = insCmd.Parameters.Add("@completedAt", SqliteType.Text);
                    var pInsVState = insCmd.Parameters.Add("@verification_state", SqliteType.Integer);
                    var pInsVAlgo = insCmd.Parameters.Add("@verification_algorithm", SqliteType.Text);
                    var pInsVTrust = insCmd.Parameters.Add("@trusted_hash", SqliteType.Text);
                    var pInsVComp = insCmd.Parameters.Add("@computed_hash", SqliteType.Text);
                    var pInsVMsg = insCmd.Parameters.Add("@verification_message", SqliteType.Text);
                    var pInsVTime = insCmd.Parameters.Add("@verification_time", SqliteType.Text);
                    insCmd.Prepare();

                    using var updCmd = conn.CreateCommand();
                    updCmd.Transaction = tx;
                    updCmd.CommandText = @"UPDATE downloads SET 
                        status=@status, 
                        total_bytes=CASE WHEN @total > 0 THEN @total ELSE total_bytes END,
                        bytes_downloaded=CASE WHEN @downloaded > 0 THEN @downloaded WHEN @status = 1 AND @total > 0 THEN @total ELSE bytes_downloaded END,
                        completed_at=@completedAt, 
                        verification_state=@verification_state, 
                        verification_algorithm=@verification_algorithm, 
                        trusted_hash=@trusted_hash, 
                        computed_hash=@computed_hash, 
                        verification_message=@verification_message, 
                        verification_time=@verification_time 
                        WHERE id=@id";
                    var pUpdStatus = updCmd.Parameters.Add("@status", SqliteType.Integer);
                    var pUpdTotal = updCmd.Parameters.Add("@total", SqliteType.Integer);
                    var pUpdDownloaded = updCmd.Parameters.Add("@downloaded", SqliteType.Integer);
                    var pUpdCompletedAt = updCmd.Parameters.Add("@completedAt", SqliteType.Text);
                    var pUpdVState = updCmd.Parameters.Add("@verification_state", SqliteType.Integer);
                    var pUpdVAlgo = updCmd.Parameters.Add("@verification_algorithm", SqliteType.Text);
                    var pUpdVTrust = updCmd.Parameters.Add("@trusted_hash", SqliteType.Text);
                    var pUpdVComp = updCmd.Parameters.Add("@computed_hash", SqliteType.Text);
                    var pUpdVMsg = updCmd.Parameters.Add("@verification_message", SqliteType.Text);
                    var pUpdVTime = updCmd.Parameters.Add("@verification_time", SqliteType.Text);
                    var pUpdId = updCmd.Parameters.Add("@id", SqliteType.Integer);
                    updCmd.Prepare();

                    foreach (var d in downloads)
                    {
                        if (d == null) continue;
                        string canonicalUrl = CanonicalizeUrl(d.Url);
                        string canonicalDest = CanonicalizePath(d.SavePath);

                        pCheckUrl.Value = canonicalUrl;
                        pCheckRawUrl.Value = d.Url ?? string.Empty;
                        pCheckDest.Value = canonicalDest;
                        pCheckRawDest.Value = d.SavePath ?? string.Empty;

                        var res = checkCmd.ExecuteScalar();
                        long totalBytes = d.TotalBytes > 0 ? d.TotalBytes : SizeFormatter.ParseToBytes(d.Size);
                        bool isCompleted = string.Equals(d.Status, "Completed", StringComparison.OrdinalIgnoreCase);

                        long downloadedBytes = d.DownloadedBytes > 0 
                            ? d.DownloadedBytes 
                            : (isCompleted && totalBytes > 0 ? totalBytes : (long)(totalBytes * (d.Progress / 100.0)));
                        if (isCompleted && totalBytes > 0) downloadedBytes = totalBytes;

                        if (res == null || res is DBNull)
                        {
                            pInsUrl.Value = canonicalUrl;
                            pInsDest.Value = canonicalDest;
                            pInsTotal.Value = totalBytes;
                            pInsDownloaded.Value = downloadedBytes;
                            pInsStatus.Value = isCompleted ? 1 : 0;
                            pInsCreatedAt.Value = DateTime.UtcNow.ToString("o");
                            pInsCompletedAt.Value = isCompleted ? DateTime.UtcNow.ToString("o") : (object)DBNull.Value;
                            pInsVState.Value = (int)d.VerificationState;
                            pInsVAlgo.Value = (object?)d.VerificationAlgorithm ?? DBNull.Value;
                            pInsVTrust.Value = (object?)d.TrustedVerificationHash ?? DBNull.Value;
                            pInsVComp.Value = (object?)d.ComputedVerificationHash ?? DBNull.Value;
                            pInsVMsg.Value = (object?)d.VerificationMessage ?? DBNull.Value;
                            pInsVTime.Value = (object?)d.VerificationTimestamp?.ToString("o") ?? DBNull.Value;

                            insCmd.ExecuteNonQuery();
                        }
                        else
                        {
                            long id = Convert.ToInt64(res);
                            pUpdStatus.Value = isCompleted ? 1 : 0;
                            pUpdTotal.Value = totalBytes;
                            pUpdDownloaded.Value = downloadedBytes;
                            pUpdCompletedAt.Value = isCompleted ? DateTime.UtcNow.ToString("o") : (object)DBNull.Value;
                            pUpdVState.Value = (int)d.VerificationState;
                            pUpdVAlgo.Value = (object?)d.VerificationAlgorithm ?? DBNull.Value;
                            pUpdVTrust.Value = (object?)d.TrustedVerificationHash ?? DBNull.Value;
                            pUpdVComp.Value = (object?)d.ComputedVerificationHash ?? DBNull.Value;
                            pUpdVMsg.Value = (object?)d.VerificationMessage ?? DBNull.Value;
                            pUpdVTime.Value = (object?)d.VerificationTimestamp?.ToString("o") ?? DBNull.Value;
                            pUpdId.Value = id;

                            updCmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                    _connManager.Checkpoint();
                    LoggingService.Log("[HistoryService.SaveHistoryAsync] History batched transaction saved successfully");
                }
                catch (Exception ex)
                {
                    try { tx?.Rollback(); } catch { }
                    LoggingService.Log($"[HistoryService.SaveHistoryAsync] Failed: {ex.Message}");
                    throw;
                }
                finally
                {
                    _connManager.ReturnConnection(conn);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Clears all entries from the downloads database.
        /// </summary>
        public async Task ClearHistoryAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            await Task.Run(() =>
            {
                try
                {
                    var conn = _connManager.GetConnection();
                    try
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "DELETE FROM downloads";
                        cmd.ExecuteNonQuery();
                        _connManager.Checkpoint();
                        LoggingService.Log("[HistoryService.ClearHistoryAsync] Database cleared");
                    }
                    finally
                    {
                        _connManager.ReturnConnection(conn);
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[HistoryService.ClearHistoryAsync] Failed", ex);
                }
            }).ConfigureAwait(false);
        }

        public async Task<int> GetTotalCountAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            return await Task.Run(() =>
            {
                var conn = _connManager.GetConnection();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM downloads";
                    var res = cmd.ExecuteScalar();
                    return res != null ? Convert.ToInt32(res) : 0;
                }
                finally
                {
                    _connManager.ReturnConnection(conn);
                }
            }).ConfigureAwait(false);
        }

        public async Task<(int TotalCount, int CompletedCount, long TotalDownloadedBytes)> GetMetricsSnapshotAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            return await Task.Run(() =>
            {
                var conn = _connManager.GetConnection();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"SELECT 
                        COUNT(*),
                        COUNT(CASE WHEN status = 1 THEN 1 END),
                        COALESCE(SUM(bytes_downloaded), 0)
                    FROM downloads";
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        int total = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetInt64(0));
                        int completed = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetInt64(1));
                        long downloaded = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
                        return (total, completed, downloaded);
                    }
                    return (0, 0, 0L);
                }
                finally
                {
                    _connManager.ReturnConnection(conn);
                }
            }).ConfigureAwait(false);
        }

        public async Task<bool> DeleteHistoryItemAsync(string url, string savePath)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            return await Task.Run(() =>
            {
                string canonicalUrl = CanonicalizeUrl(url);
                string canonicalDest = CanonicalizePath(savePath);
                var conn = _connManager.GetConnection();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "DELETE FROM downloads WHERE (url = @url OR url = @rawUrl) AND (destination = @dest OR destination = @rawDest)";
                    cmd.Parameters.AddWithValue("@url", canonicalUrl);
                    cmd.Parameters.AddWithValue("@rawUrl", url ?? string.Empty);
                    cmd.Parameters.AddWithValue("@dest", canonicalDest);
                    cmd.Parameters.AddWithValue("@rawDest", savePath ?? string.Empty);

                    int rows = cmd.ExecuteNonQuery();
                    _connManager.Checkpoint();
                    return rows > 0;
                }
                finally
                {
                    _connManager.ReturnConnection(conn);
                }
            }).ConfigureAwait(false);
        }

        public async Task<string?> CreateBackupAsync() => await _backupService.CreateBackupAsync().ConfigureAwait(false);
        public string GetAuditStatistics() => _auditLog.GetStatistics().ToString();
        public IEnumerable<object> GetRecentAuditEntries(int count) => _auditLog.GetRecentEntries(count);

        public DatabaseAuditLog AuditLog => _auditLog;
        public DbBackupService BackupService => _backupService;

        public void Dispose()
        {
            _initSemaphore?.Dispose();
            _connManager?.Dispose();
        }
    }
}
