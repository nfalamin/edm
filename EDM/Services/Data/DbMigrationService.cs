using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using EDM.Models;

namespace EDM.Services.Data
{
    /// <summary>
    /// Manages SQLite schema migrations using PRAGMA user_version for version tracking.
    /// Runs idempotent, ordered migration scripts at startup.
    /// </summary>
    public class DbMigrationService
    {
        private readonly SqliteConnectionManager _connectionManager;
        private readonly List<Migration> _migrations;
        private const int CURRENT_SCHEMA_VERSION = 2;

        public DbMigrationService(SqliteConnectionManager connectionManager)
        {
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _migrations = new List<Migration>();
            RegisterMigrations();
        }

        /// <summary>
        /// Registers all schema migrations in order.
        /// </summary>
        private void RegisterMigrations()
        {
            // Version 1: Initial schema with downloads table and indexes
            _migrations.Add(new Migration(1, "Create initial downloads table and indexes", new[]
            {
                @"CREATE TABLE IF NOT EXISTS downloads (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    url TEXT NOT NULL,
                    destination TEXT NOT NULL,
                    total_bytes INTEGER DEFAULT 0,
                    bytes_downloaded INTEGER DEFAULT 0,
                    last_speed REAL DEFAULT 0,
                    avg_speed REAL DEFAULT 0,
                    status INTEGER DEFAULT 0,
                    created_at TEXT NOT NULL,
                    completed_at TEXT
                );",

                @"CREATE INDEX IF NOT EXISTS idx_downloads_url ON downloads(url);",
                @"CREATE INDEX IF NOT EXISTS idx_downloads_status ON downloads(status);",
                @"CREATE INDEX IF NOT EXISTS idx_downloads_created_at ON downloads(created_at);",
                @"CREATE INDEX IF NOT EXISTS idx_downloads_status_created_at ON downloads(status, created_at);",
                @"CREATE INDEX IF NOT EXISTS idx_downloads_destination ON downloads(destination);"
            }));

            // Migration 2: Add verification columns for post-download integrity state
            _migrations.Add(new Migration(2, "Add verification columns for integrity checks", new[]
            {
                @"ALTER TABLE downloads ADD COLUMN verification_state INTEGER DEFAULT 0;",
                @"ALTER TABLE downloads ADD COLUMN verification_algorithm TEXT;",
                @"ALTER TABLE downloads ADD COLUMN trusted_hash TEXT;",
                @"ALTER TABLE downloads ADD COLUMN computed_hash TEXT;",
                @"ALTER TABLE downloads ADD COLUMN verification_message TEXT;",
                @"ALTER TABLE downloads ADD COLUMN verification_time TEXT;"
            }));

            // Future migrations can be added here:
            // _migrations.Add(new Migration(3, "Add new column", new[] { "ALTER TABLE downloads ADD COLUMN..." }));
        }

        /// <summary>
        /// Runs all pending migrations. Idempotent and safe to call multiple times.
        /// </summary>
        public async Task RunMigrationsAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    using (var conn = _connectionManager.GetConnection())
                    {
                        try
                        {
                            int currentVersion = GetSchemaVersion(conn);
                            LoggingService.Log($"[DbMigrationService] Current schema version: {currentVersion}");

                            foreach (var migration in _migrations)
                            {
                                if (migration.Version > currentVersion)
                                {
                                    LoggingService.Log($"[DbMigrationService] Running migration {migration.Version}: {migration.Description}");
                                    RunMigration(conn, migration);
                                    SetSchemaVersion(conn, migration.Version);
                                    LoggingService.Log($"[DbMigrationService] Completed migration {migration.Version}");
                                }
                            }

                            // After applying migrations, perform one-time JSON history import if upgrading from 0 -> 1
                            try
                            {
                                int finalVersion = GetSchemaVersion(conn);
                                if (currentVersion < 1 && finalVersion >= 1)
                                {
                                    try
                                    {
                                        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EDM");
                                        string historyJson = Path.Combine(appData, "downloads.json");
                                        if (File.Exists(historyJson))
                                        {
                                            LoggingService.Log("[DbMigrationService] Found legacy downloads.json - attempting migration into SQLite.");
                                            var raw = File.ReadAllText(historyJson);
                                            var list = JsonSerializer.Deserialize<List<DownloadItem>>(raw);
                                            if (list != null && list.Count > 0)
                                            {
                                                LoggingService.Log($"[DbMigrationService] Migrating {list.Count} history records from JSON.");
                                                foreach (var d in list)
                                                {
                                                    using (var cmd = conn.CreateCommand())
                                                    {
                                                        cmd.CommandText = "INSERT INTO downloads (url, destination, total_bytes, bytes_downloaded, last_speed, avg_speed, status, created_at, completed_at) VALUES ($url,$dest,$total,$downloaded,$last_speed,$avg_speed,$status,$created,$completed);";
                                                        cmd.Parameters.AddWithValue("$url", d.Url ?? string.Empty);
                                                        cmd.Parameters.AddWithValue("$dest", d.SavePath ?? string.Empty);
                                                        cmd.Parameters.AddWithValue("$total", d.Size != null ? 0 : 0);
                                                        cmd.Parameters.AddWithValue("$downloaded", 0);
                                                        cmd.Parameters.AddWithValue("$last_speed", 0);
                                                        cmd.Parameters.AddWithValue("$avg_speed", 0);
                                                        cmd.Parameters.AddWithValue("$status", 0);
                                                        cmd.Parameters.AddWithValue("$created", d.LastTryDate ?? DateTime.UtcNow.ToString("o"));
                                                        cmd.Parameters.AddWithValue("$completed", DBNull.Value);
                                                        cmd.ExecuteNonQuery();
                                                    }
                                                }
                                                // rename original file to .migrated.bak to avoid data loss
                                                try { File.Move(historyJson, historyJson + ".migrated.bak", true); } catch (Exception ex) { LoggingService.Log($"[DbMigrationService] Failed to rename legacy history file: {ex.Message}"); }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LoggingService.LogException("[DbMigrationService] Legacy JSON migration failed", ex);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LoggingService.LogException("[DbMigrationService] Post-migration hook failed", ex);
                            }

                            // Checkpoint to ensure all changes are written

                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = "PRAGMA wal_checkpoint(RESTART);";
                                cmd.CommandTimeout = 10;
                                cmd.ExecuteNonQuery();
                            }

                            LoggingService.Log($"[DbMigrationService] All migrations completed. Final schema version: {GetSchemaVersion(conn)}");
                        }
                        finally
                        {
                            _connectionManager.ReturnConnection(conn);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Log($"[DbMigrationService.RunMigrationsAsync] Failed: {ex.Message}");
                    throw;
                }
            });
        }

        /// <summary>
        /// Gets the current schema version from PRAGMA user_version.
        /// </summary>
        private int GetSchemaVersion(SqliteConnection connection)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA user_version;";
                cmd.CommandTimeout = 5;
                var result = cmd.ExecuteScalar();
                return result != null && int.TryParse(result.ToString(), out int version) ? version : 0;
            }
        }

        /// <summary>
        /// Sets the schema version in PRAGMA user_version.
        /// </summary>
        private void SetSchemaVersion(SqliteConnection connection, int version)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA user_version = {version};";
                cmd.CommandTimeout = 5;
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Executes a single migration's SQL scripts.
        /// </summary>
        private void RunMigration(SqliteConnection connection, Migration migration)
        {
            foreach (var script in migration.Scripts)
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = script;
                    cmd.CommandTimeout = 30;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Represents a single migration with version, description, and scripts.
        /// </summary>
        private class Migration
        {
            public int Version { get; }
            public string Description { get; }
            public string[] Scripts { get; }

            public Migration(int version, string description, string[] scripts)
            {
                Version = version;
                Description = description;
                Scripts = scripts;
            }
        }
    }
}
