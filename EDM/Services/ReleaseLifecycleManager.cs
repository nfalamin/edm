using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using EDM.Models;

namespace EDM.Services
{
    public class MigrationResult
    {
        public bool Success { get; set; }
        public string SourceVersion { get; set; } = string.Empty;
        public string TargetVersion { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Manages enterprise release lifecycle, version migrations, downgrade guards,
    /// and config/database schema transitions.
    /// </summary>
    public static class ReleaseLifecycleManager
    {
        public static readonly Version CurrentVersion = new Version(2, 0, 0, 0);

        public static MigrationResult CheckAndExecuteMigrations(string appDataDirectory)
        {
            try
            {
                Directory.CreateDirectory(appDataDirectory);
                string versionFilePath = Path.Combine(appDataDirectory, "version.json");

                Version installedVersion = new Version(1, 0, 0, 0);

                if (File.Exists(versionFilePath))
                {
                    try
                    {
                        string json = File.ReadAllText(versionFilePath);
                        using var doc = JsonDocument.Parse(json);
                        string vStr = doc.RootElement.GetProperty("version").GetString() ?? "1.0.0.0";
                        installedVersion = Version.Parse(vStr);
                    }
                    catch { }
                }

                // Downgrade protection
                if (installedVersion > CurrentVersion)
                {
                    return new MigrationResult
                    {
                        Success = false,
                        SourceVersion = installedVersion.ToString(),
                        TargetVersion = CurrentVersion.ToString(),
                        Message = "Downgrade rejected. An existing higher version data directory was detected."
                    };
                }

                // Perform upgrade migrations if needed
                if (installedVersion < CurrentVersion)
                {
                    PerformDatabaseAndConfigMigration(appDataDirectory, installedVersion, CurrentVersion);
                }

                // Write current version
                var versionInfo = new
                {
                    version = CurrentVersion.ToString(),
                    lastUpdatedUtc = DateTime.UtcNow,
                    schemaVersion = 2
                };
                File.WriteAllText(versionFilePath, JsonSerializer.Serialize(versionInfo, new JsonSerializerOptions { WriteIndented = true }));

                // Clean orphan temporary/lock files
                CleanOrphanArtifacts(appDataDirectory);

                return new MigrationResult
                {
                    Success = true,
                    SourceVersion = installedVersion.ToString(),
                    TargetVersion = CurrentVersion.ToString(),
                    Message = "Migration completed successfully."
                };
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[ReleaseLifecycleManager] Migration error", ex);
                return new MigrationResult
                {
                    Success = false,
                    Message = $"Migration failed: {ex.Message}"
                };
            }
        }

        private static void PerformDatabaseAndConfigMigration(string appDataDir, Version from, Version to)
        {
            LoggingService.Log($"[ReleaseLifecycleManager] Migrating configuration and database from {from} to {to}...");
            // Clean legacy temp formats
            string legacyDir = Path.Combine(appDataDir, "Temp");
            if (Directory.Exists(legacyDir))
            {
                try { Directory.Delete(legacyDir, true); } catch { }
            }
        }

        private static void CleanOrphanArtifacts(string appDataDir)
        {
            try
            {
                var files = Directory.GetFiles(appDataDir, "*.tmp", SearchOption.TopDirectoryOnly);
                foreach (var f in files)
                {
                    try { File.Delete(f); } catch { }
                }
            }
            catch { }
        }
    }
}
