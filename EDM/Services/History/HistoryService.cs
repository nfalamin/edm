using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Microsoft.Data.Sqlite;
using EDM.Models;
using EDM.Services.Interfaces;
using EDM.Services.Data;
using System.Diagnostics;

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
        public int Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        // Verification fields
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
        private static readonly object _initLock = new object();
        private static bool _initialized;

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

            // Initialize migrations asynchronously (non-blocking)
            _ = InitializeMigrationsAsync();
        }

        /// <summary>
        /// Ensures database migrations are applied. Called asynchronously to avoid blocking the UI thread.
        /// </summary>
        private async System.Threading.Tasks.Task InitializeMigrationsAsync()
        {
            lock (_initLock)
            {
                if (_initialized) return;
            }

            try
            {
                await _migrationService.RunMigrationsAsync().ConfigureAwait(false);
                lock (_initLock)
                {
                    _initialized = true;
                }
                LoggingService.Log("[HistoryService] Database initialization complete with migrations");
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[HistoryService] Migration failed", ex);
            }
        }

        public long CreateEntry(string url, string destination, long totalBytes)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var conn = _connManager.GetConnection();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO downloads (url, destination, total_bytes, created_at, status) VALUES (@url, @dest, @totalBytes, @createdAt, 0); SELECT last_insert_rowid();";
                    cmd.CommandTimeout = 5;

                    // Use prepared statement parame ters
                    cmd.Parameters.Add("@url", SqliteType.Text).Value = url ?? string.Empty;
                    cmd.Parameters.Add("@dest", SqliteType.Text).Value = destination ?? string.Empty;
                    cmd.Parameters.Add("@totalBytes", SqliteType.Integer).Value = totalBytes;
                    cmd.Parameters.Add("@createdAt", SqliteType.Text).Value = DateTime.UtcNow.ToString("o");

                    cmd.Prepare(); // Prepare statement for optimal performance
                    var result = cmd.ExecuteScalar();

                    sw.Stop();
                    _auditLog.LogQuery("INSERT", "CreateEntry", sw.ElapsedMilliseconds, success: true);
                    LoggingService.Log($"[HistoryService.CreateEntry] Created entry id={result} for {url}");

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
                    int rowsAffected = cmd.ExecuteNonQuery();

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
                    int rowsAffected = cmd.ExecuteNonQuery();

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
                            CreatedAt = DateTime.Parse(rdr.GetString(8)),
                            CompletedAt = rdr.IsDBNull(9) ? null : DateTime.Parse(rdr.GetString(9)),

                            VerificationState = rdr.IsDBNull(10) ? 0 : rdr.GetInt32(10),
                            VerificationAlgorithm = rdr.IsDBNull(11) ? null : rdr.GetString(11),
                            TrustedHash = rdr.IsDBNull(12) ? null : rdr.GetString(12),
                            ComputedHash = rdr.IsDBNull(13) ? null : rdr.GetString(13),
                            VerificationMessage = rdr.IsDBNull(14) ? null : rdr.GetString(14),
                            VerificationTime = rdr.IsDBNull(15) ? null : DateTime.Parse(rdr.GetString(15))
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
            try
            {
                var list = new List<DownloadItem>();
                foreach (var entry in ListRecent(100))
                {
                    var di = new DownloadItem
                                    {
                                        FileName = Path.GetFileName(entry.Destination ?? string.Empty),
                                        Url = entry.Url ?? string.Empty,
                                        SavePath = entry.Destination ?? string.Empty,
                                        Status = entry.Status == 1 ? "Completed" : string.Empty,
                                        Size = entry.TotalBytes.ToString(),
                                        VerificationState = (Models.VerificationState)entry.VerificationState,
                                        VerificationAlgorithm = entry.VerificationAlgorithm,
                                        TrustedVerificationHash = entry.TrustedHash,
                                        ComputedVerificationHash = entry.ComputedHash,
                                        VerificationTimestamp = entry.VerificationTime
                                    };
                    list.Add(di);
                }
                return await Task.FromResult(new ObservableCollection<DownloadItem>(list)).ConfigureAwait(false);
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
            return await Task.Run(() =>
            {
                long totalBytes = -1;
                if (long.TryParse(item.Size, out var parsed)) totalBytes = parsed;
                return CreateEntry(item.Url ?? string.Empty, item.SavePath ?? string.Empty, totalBytes);
            }).ConfigureAwait(false);
        }

        public async Task SaveHistoryAsync(ObservableCollection<DownloadItem> downloads)
        {
            if (downloads == null) return;

            await Task.Run(() =>
            {
                try
                {
                    foreach (var d in downloads)
                    {
                        var conn = _connManager.GetConnection();
                        try
                        {
                            // Check if entry exists
                            using var checkCmd = conn.CreateCommand();
                            checkCmd.CommandText = "SELECT id FROM downloads WHERE url=@url AND destination=@dest LIMIT 1";
                            checkCmd.Parameters.Add("@url", SqliteType.Text).Value = d.Url ?? string.Empty;
                            checkCmd.Parameters.Add("@dest", SqliteType.Text).Value = d.SavePath ?? string.Empty;
                            checkCmd.Prepare();
                            var res = checkCmd.ExecuteScalar();

                            if (res == null || res is DBNull)
                            {
                                // Insert new entry
                                long total = 0;
                                long.TryParse(d.Size, out total);

                                using var insCmd = conn.CreateCommand();
                                insCmd.CommandText = "INSERT INTO downloads (url, destination, total_bytes, status, created_at, completed_at, verification_state, verification_algorithm, trusted_hash, computed_hash, verification_message, verification_time) VALUES (@url, @dest, @total, @status, @createdAt, @completedAt, @verification_state, @verification_algorithm, @trusted_hash, @computed_hash, @verification_message, @verification_time)";
                                insCmd.Parameters.Add("@url", SqliteType.Text).Value = d.Url ?? string.Empty;
                                insCmd.Parameters.Add("@dest", SqliteType.Text).Value = d.SavePath ?? string.Empty;
                                insCmd.Parameters.Add("@total", SqliteType.Integer).Value = total;
                                insCmd.Parameters.Add("@status", SqliteType.Integer).Value = (string.Equals(d.Status, "Completed") ? 1 : 0);
                                insCmd.Parameters.Add("@createdAt", SqliteType.Text).Value = DateTime.UtcNow.ToString("o");
                                insCmd.Parameters.Add("@completedAt", SqliteType.Text).Value = (string.Equals(d.Status, "Completed") ? DateTime.UtcNow.ToString("o") : (object)DBNull.Value);
                                insCmd.Parameters.Add("@verification_state", SqliteType.Integer).Value = (int)d.VerificationState;
                                insCmd.Parameters.Add("@verification_algorithm", SqliteType.Text).Value = (object?)d.VerificationAlgorithm ?? DBNull.Value;
                                insCmd.Parameters.Add("@trusted_hash", SqliteType.Text).Value = (object?)d.TrustedVerificationHash ?? DBNull.Value;
                                insCmd.Parameters.Add("@computed_hash", SqliteType.Text).Value = (object?)d.ComputedVerificationHash ?? DBNull.Value;
                                insCmd.Parameters.Add("@verification_message", SqliteType.Text).Value = (object?)d.VerificationTimestamp?.ToString("o") ?? DBNull.Value; /* placeholder: message empty on insert */
                                insCmd.Parameters.Add("@verification_time", SqliteType.Text).Value = (object?)d.VerificationTimestamp?.ToString("o") ?? DBNull.Value;
                                insCmd.Prepare();
                                insCmd.ExecuteNonQuery();
                            }
                            else
                            {
                                // Update existing entry
                                long id = Convert.ToInt64(res);
                                using var updCmd = conn.CreateCommand();
                                updCmd.CommandText = "UPDATE downloads SET status=@status, completed_at=@completedAt, verification_state=@verification_state, verification_algorithm=@verification_algorithm, trusted_hash=@trusted_hash, computed_hash=@computed_hash, verification_message=@verification_message, verification_time=@verification_time WHERE id=@id";
                                updCmd.Parameters.Add("@status", SqliteType.Integer).Value = (string.Equals(d.Status, "Completed") ? 1 : 0);
                                updCmd.Parameters.Add("@completedAt", SqliteType.Text).Value = (string.Equals(d.Status, "Completed") ? DateTime.UtcNow.ToString("o") : (object)DBNull.Value);
                                updCmd.Parameters.Add("@verification_state", SqliteType.Integer).Value = (int)d.VerificationState;
                                updCmd.Parameters.Add("@verification_algorithm", SqliteType.Text).Value = (object?)d.VerificationAlgorithm ?? DBNull.Value;
                                updCmd.Parameters.Add("@trusted_hash", SqliteType.Text).Value = (object?)d.TrustedVerificationHash ?? DBNull.Value;
                                updCmd.Parameters.Add("@computed_hash", SqliteType.Text).Value = (object?)d.ComputedVerificationHash ?? DBNull.Value;
                                updCmd.Parameters.Add("@verification_message", SqliteType.Text).Value = (object?)string.Empty ?? DBNull.Value;
                                updCmd.Parameters.Add("@verification_time", SqliteType.Text).Value = (object?)d.VerificationTimestamp?.ToString("o") ?? DBNull.Value;
                                updCmd.Parameters.Add("@id", SqliteType.Integer).Value = id;
                                updCmd.Prepare();
                                updCmd.ExecuteNonQuery();
                            }
                        }
                        finally
                        {
                            _connManager.ReturnConnection(conn);
                        }
                    }

                    // Perform a checkpoint to ensure data is written
                    _connManager.Checkpoint();

                    LoggingService.Log("[HistoryService.SaveHistoryAsync] History saved successfully");
                }
                catch (Exception ex)
                {
                    LoggingService.Log($"[HistoryService.SaveHistoryAsync] Failed: {ex.Message}");
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Clears all entries from the downloads database.
        /// </summary>
        public async Task ClearHistoryAsync()
        {
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
                    }
                    finally
                    {
                        _connManager.ReturnConnection(conn);
                    }
                    _connManager.Checkpoint();
                    LoggingService.Log("[HistoryService.ClearHistoryAsync] All history cleared.");
                }
                catch (Exception ex)
                {
                    LoggingService.Log($"[HistoryService.ClearHistoryAsync] Failed: {ex.Message}");
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a backup of the database.
        /// </summary>
        public async Task<string?> CreateBackupAsync()
        {
            return await _backupService.CreateBackupAsync();
        }

        /// <summary>
        /// Gets audit log statistics for diagnostics.
        /// </summary>
        public DatabaseAuditLog.AuditStatistics GetAuditStatistics()
        {
            return _auditLog.GetStatistics();
        }

        /// <summary>
        /// Gets recent audit entries for diagnostics.
        /// </summary>
        public DatabaseAuditLog.AuditEntry[] GetRecentAuditEntries(int count = 50)
        {
            return _auditLog.GetRecentEntries(count);
        }

        public void Dispose()
        {
            try
            {
                LoggingService.Log("[HistoryService.Dispose] Shutting down database services...");

                // Checkpoint before closing
                _connManager?.Checkpoint();

                // Close all connections
                _connManager?.Dispose();

                LoggingService.Log("[HistoryService.Dispose] Database services shut down successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[HistoryService.Dispose] Error during cleanup: {ex.Message}");
            }
        }
    }
}
