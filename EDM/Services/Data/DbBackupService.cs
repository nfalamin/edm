using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services.Data
{
    /// <summary>
    /// Automated SQLite backup service using online backup API.
    /// Creates point-in-time backups without locking the database.
    /// </summary>
    public class DbBackupService
    {
        private readonly string _dbPath;
        private readonly string _backupDir;

        public DbBackupService(string dbPath)
        {
            _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));

            // Create backup directory next to database
            _backupDir = Path.Combine(
                Path.GetDirectoryName(_dbPath) ?? AppContext.BaseDirectory,
                "backups"
            );

            if (!Directory.Exists(_backupDir))
                Directory.CreateDirectory(_backupDir);
        }

        /// <summary>
        /// Creates an automatic timestamped backup of the database.
        /// Uses SQLite's online backup API to avoid locking.
        /// </summary>
        public async Task<string?> CreateBackupAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(_dbPath))
                        {
                            LoggingService.Log("[DbBackupService] Database file does not exist, skipping backup");
                            return null;
                        }

                    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                    var backupFile = Path.Combine(_backupDir, $"edm_history_backup_{timestamp}.db");

                    // Use online backup API to avoid locking
                    using (var sourceConn = new SqliteConnection($"Data Source={_dbPath};"))
                    {
                        sourceConn.Open();

                        using (var backupConn = new SqliteConnection($"Data Source={backupFile};"))
                        {
                            backupConn.Open();

                            // Copy database without locking
                            using (var sourceCmd = sourceConn.CreateCommand())
                            {
                                sourceCmd.CommandText = "SELECT count(*) FROM downloads;";
                                sourceCmd.CommandTimeout = 5;
                                sourceCmd.ExecuteScalar(); // Ensure DB is accessible
                            }

                            // Use backup API through raw SQL
                            BackupDatabaseUsingSql(sourceConn, backupConn);

                            backupConn.Close();
                        }

                        sourceConn.Close();
                    }

                    LoggingService.Log($"[DbBackupService] Backup created successfully: {backupFile}");

                    // Cleanup old backups (keep last 10)
                    CleanupOldBackups(10);

                    return backupFile;
                }
                catch (Exception ex)
                {
                    LoggingService.Log($"[DbBackupService.CreateBackupAsync] Failed: {ex.Message}");
                    return null;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Backs up using SQLite's VACUUM and direct file copy (optimal for .NET).
        /// More reliable than raw backup API for managed code.
        /// </summary>
        private void BackupDatabaseUsingSql(SqliteConnection sourceConn, SqliteConnection backupConn)
        {
            // Use VACUUM INTO which creates a backup without locking the source
            using (var cmd = sourceConn.CreateCommand())
            {
                cmd.CommandText = $"VACUUM INTO '{backupConn.DataSource.Replace("'", "''")}';";
                cmd.CommandTimeout = 30;
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Removes old backups, keeping only the most recent N backups.
        /// </summary>
        private void CleanupOldBackups(int keepCount)
        {
            try
            {
                var backupFiles = Directory.GetFiles(_backupDir, "edm_history_backup_*.db");
                if (backupFiles.Length <= keepCount)
                    return;

                // Sort by creation time (descending) and delete oldest
                var fileInfos = new System.Collections.Generic.List<FileInfo>();
                foreach (var file in backupFiles)
                    fileInfos.Add(new FileInfo(file));

                fileInfos.Sort((a, b) => b.CreationTimeUtc.CompareTo(a.CreationTimeUtc));

                for (int i = keepCount; i < fileInfos.Count; i++)
                {
                    try
                    {
                        fileInfos[i].Delete();
                        LoggingService.Log($"[DbBackupService] Deleted old backup: {fileInfos[i].Name}");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Log($"[DbBackupService] Failed to delete {fileInfos[i].Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DbBackupService.CleanupOldBackups] Failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns the path to the most recent backup file, or null if no backups exist.
        /// </summary>
        public string? GetLatestBackupPath()
        {
            try
            {
                var backupFiles = Directory.GetFiles(_backupDir, "edm_history_backup_*.db");
                if (backupFiles.Length == 0)
                    return null;

                var latest = new FileInfo(backupFiles[0]);
                foreach (var file in backupFiles)
                {
                    var fi = new FileInfo(file);
                    if (fi.CreationTimeUtc > latest.CreationTimeUtc)
                        latest = fi;
                }

                return latest.FullName;
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DbBackupService.GetLatestBackupPath] Failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lists all available backups with their creation times.
        /// </summary>
        public System.Collections.Generic.List<(string Path, DateTime CreatedAt)> ListBackups()
        {
            var result = new System.Collections.Generic.List<(string Path, DateTime CreatedAt)>();
            try
            {
                var backupFiles = Directory.GetFiles(_backupDir, "edm_history_backup_*.db");
                foreach (var file in backupFiles)
                {
                    var fi = new FileInfo(file);
                    result.Add((Path: file, CreatedAt: fi.CreationTimeUtc));
                }

                result.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DbBackupService.ListBackups] Failed: {ex.Message}");
            }

            return result;
        }
    }
}
