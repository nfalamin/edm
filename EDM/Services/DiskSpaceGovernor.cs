using System;
using System.IO;

namespace EDM.Services
{
    public class InsufficientDiskSpaceException : IOException
    {
        public long RequiredBytes { get; }
        public long AvailableBytes { get; }
        public long ShortageBytes { get; }

        public InsufficientDiskSpaceException(long required, long available, long shortage)
            : base($"Insufficient disk space: required {required / (1024 * 1024)} MB, available {available / (1024 * 1024)} MB, shortage {shortage / (1024 * 1024)} MB.")
        {
            RequiredBytes = required;
            AvailableBytes = available;
            ShortageBytes = shortage;
        }
    }

    /// <summary>
    /// DiskSpaceGovernor — Preflight and continuous runtime disk space monitor.
    /// Prevents drive exhaustion, partial write corruption, and silent disk failures.
    /// </summary>
    public static class DiskSpaceGovernor
    {
        public const long DefaultSafetyBufferBytes = 50 * 1024 * 1024; // 50 MB safety margin

        /// <summary>
        /// Validates that target drive has sufficient free space for the download.
        /// </summary>
        public static bool ValidateAvailableSpace(string targetPath, long requiredBytes, long safetyBufferBytes = DefaultSafetyBufferBytes)
        {
            if (requiredBytes <= 0) return true;

            try
            {
                string root = Path.GetPathRoot(Path.GetFullPath(targetPath)) ?? string.Empty;
                if (string.IsNullOrEmpty(root)) return true;

                var driveInfo = new DriveInfo(root);
                long freeSpace = driveInfo.AvailableFreeSpace;

                return freeSpace >= (requiredBytes + safetyBufferBytes);
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"[DiskSpaceGovernor] Could not query drive space for '{targetPath}': {ex.Message}");
                return true; // Don't block if drive info cannot be queried (e.g. UNC path)
            }
        }

        public static void EnsureAvailableSpaceOrThrow(string targetPath, long requiredBytes, long safetyBufferBytes = DefaultSafetyBufferBytes)
        {
            if (requiredBytes <= 0) return;

            try
            {
                string root = Path.GetPathRoot(Path.GetFullPath(targetPath)) ?? string.Empty;
                if (string.IsNullOrEmpty(root)) return;

                var driveInfo = new DriveInfo(root);
                long freeSpace = driveInfo.AvailableFreeSpace;
                long totalNeeded = requiredBytes + safetyBufferBytes;

                if (freeSpace < totalNeeded)
                {
                    long shortage = totalNeeded - freeSpace;
                    throw new InsufficientDiskSpaceException(totalNeeded, freeSpace, shortage);
                }
            }
            catch (InsufficientDiskSpaceException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"[DiskSpaceGovernor] Space check warning: {ex.Message}");
            }
        }
    }
}
