using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace EDM.Services
{
    public class DiskVolumeInfo
    {
        public string DriveName { get; set; } = string.Empty;
        public long TotalFreeSpaceBytes { get; set; }
        public long TotalSizeBytes { get; set; }
        public string CacheDirectoryPath { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
    }

    /// <summary>
    /// Phase 19: Per-Disk / Multi-Volume Temporary Cache Manager.
    /// Dynamically selects optimal temporary storage drives based on available disk space thresholds
    /// to prevent system drive saturation and partition bottlenecks.
    /// </summary>
    public class PerDiskTempStorageManager
    {
        private static readonly Lazy<PerDiskTempStorageManager> _instance = new(() => new PerDiskTempStorageManager());
        public static PerDiskTempStorageManager Instance => _instance.Value;

        public long MinimumFreeSpaceThresholdBytes { get; set; } = 524_288_000; // 500 MB default safeguard
        private readonly ConcurrentDictionary<string, string> _customDriveCachePaths = new(StringComparer.OrdinalIgnoreCase);

        public void SetCustomCachePathForDrive(string driveLetter, string customPath)
        {
            if (string.IsNullOrWhiteSpace(driveLetter) || string.IsNullOrWhiteSpace(customPath)) return;
            string key = Path.GetPathRoot(driveLetter) ?? driveLetter;
            _customDriveCachePaths[key] = customPath;
        }

        public string GetOptimalCacheDirectory(long estimatedDownloadSizeBytes = 0)
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .OrderByDescending(d => d.AvailableFreeSpace)
                .ToList();

            foreach (var drive in drives)
            {
                if (drive.AvailableFreeSpace > (estimatedDownloadSizeBytes + MinimumFreeSpaceThresholdBytes))
                {
                    string root = drive.RootDirectory.FullName;
                    if (_customDriveCachePaths.TryGetValue(root, out var customPath) && Directory.Exists(customPath))
                    {
                        return customPath;
                    }

                    string defaultPath = Path.Combine(root, "EDM_Temp_Cache");
                    try
                    {
                        Directory.CreateDirectory(defaultPath);
                        return defaultPath;
                    }
                    catch { }
                }
            }

            // Fallback to user temp path
            string fallback = Path.Combine(Path.GetTempPath(), "EDM_Temp_Cache");
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        public bool IsDiskSpaceSufficient(string destinationPath, long requiredBytes)
        {
            try
            {
                string? root = Path.GetPathRoot(Path.GetFullPath(destinationPath));
                if (string.IsNullOrEmpty(root)) return true;

                var drive = new DriveInfo(root);
                if (!drive.IsReady) return false;

                return drive.AvailableFreeSpace >= (requiredBytes + MinimumFreeSpaceThresholdBytes);
            }
            catch
            {
                return true;
            }
        }
    }
}
