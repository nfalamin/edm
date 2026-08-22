using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services.Helpers
{
    /// <summary>
    /// Production-grade fail-safe file deletion infrastructure.
    /// Provides bounded retries (up to 3 attempts with 100ms delay) for transient file locks (antivirus scans, handle releases),
    /// symlink/reparse point protection, and diagnostic reporting.
    /// </summary>
    public static class FileDeleteHelper
    {
        public const int MaxRetryAttempts = 3;
        public const int RetryDelayMs = 100;

        /// <summary>
        /// Asynchronously and safely deletes a target file with bounded retries and validation.
        /// </summary>
        /// <param name="filePath">Target file path to delete.</param>
        /// <param name="maxAttempts">Maximum deletion attempts (default 3).</param>
        /// <param name="delayMs">Delay between retries in milliseconds (default 100ms).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if file was deleted or did not exist; false if deletion failed after max attempts.</returns>
        public static async Task<bool> DeleteFileSafeAsync(
            string? filePath,
            int maxAttempts = MaxRetryAttempts,
            int delayMs = RetryDelayMs,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            try
            {
                if (Directory.Exists(filePath))
                {
                    LoggingService.Log($"[FileDeleteHelper] Target path '{filePath}' is a directory, not a file. Aborting file deletion.");
                    return false;
                }

                if (!File.Exists(filePath))
                {
                    return true; // Already deleted or non-existent
                }

                // Check for symbolic links or reparse points to prevent accidental external deletion
                var attr = File.GetAttributes(filePath);
                if (attr.HasFlag(FileAttributes.ReparsePoint))
                {
                    LoggingService.Log($"[FileDeleteHelper] Target path '{filePath}' is a reparse point / symlink. Aborting deletion for safety.");
                    return false;
                }

                // Clear read-only attribute if present
                if (attr.HasFlag(FileAttributes.ReadOnly))
                {
                    try
                    {
                        File.SetAttributes(filePath, attr & ~FileAttributes.ReadOnly);
                    }
                    catch { }
                }

                int attempts = Math.Max(1, maxAttempts);

                for (int attempt = 1; attempt <= attempts; attempt++)
                {
                    if (cancellationToken.IsCancellationRequested) return false;

                    try
                    {
                        File.Delete(filePath);

                        // Double check that file is gone
                        if (!File.Exists(filePath))
                        {
                            return true;
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        return true;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        return true;
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        LoggingService.Log($"[FileDeleteHelper] Delete attempt {attempt}/{attempts} for '{filePath}' failed: {ex.Message}");

                        if (attempt == attempts)
                        {
                            LoggingService.Log($"[FileDeleteHelper] Exceeded maximum deletion retries ({attempts}) for '{filePath}'.");
                            return false;
                        }

                        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogException($"[FileDeleteHelper] Permanent error attempting to delete '{filePath}'", ex);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[FileDeleteHelper] Unexpected error deleting file '{filePath}'", ex);
            }

            return false;
        }

        /// <summary>
        /// Synchronously and safely deletes a target file with bounded retries and validation.
        /// </summary>
        public static bool DeleteFileSafe(string? filePath, int maxAttempts = MaxRetryAttempts, int delayMs = RetryDelayMs)
        {
            try
            {
                return DeleteFileSafeAsync(filePath, maxAttempts, delayMs, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch
            {
                return false;
            }
        }
    }
}
