using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Controllers
{
    public record RegisterFileMetadataDto(
        string FileName,
        string RelativePath,
        string? Category,
        long FileSizeBytes,
        string Sha256Hash,
        int Version,
        Guid? DeviceId,
        DateTime? ModifiedAtUtc);

    public record ResolveConflictDto(string Strategy, string? ResolvedHash, long? ResolvedSize, int? NewVersion);
    public record RenameFileDto(string NewFileName);
    public record MoveFileDto(string TargetFolder);

    [ApiController]
    [Authorize]
    [Route("api/v1/storage")]
    public class FileStorageController : ControllerBase
    {
        private readonly ControlPlaneDbContext _dbContext;
        private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public FileStorageController(ControlPlaneDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        private Guid? GetCurrentUserId()
        {
            var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;

            return Guid.TryParse(sub, out var id) ? id : null;
        }

        private string GetUserStorageRoot(Guid userId)
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string baseDir = Path.Combine(userProfile, "EDM", "Users", userId.ToString("N"));
            Directory.CreateDirectory(baseDir);
            return baseDir;
        }

        private static bool IsValidFileName(string fileName, out string? errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                errorMessage = "File name cannot be empty.";
                return false;
            }

            if (fileName.Length > 255)
            {
                errorMessage = "File name exceeds maximum length of 255 characters.";
                return false;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            if (fileName.IndexOfAny(invalidChars) >= 0)
            {
                errorMessage = "File name contains invalid characters.";
                return false;
            }

            string baseName = Path.GetFileNameWithoutExtension(fileName).Trim();
            if (ReservedWindowsNames.Contains(baseName))
            {
                errorMessage = $"'{baseName}' is a reserved operating system filename.";
                return false;
            }

            return true;
        }

        private static string SanitizeRelativePath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return string.Empty;
            string normalized = rawPath.Replace('\\', '/').Trim('/');
            var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p != "." && p != "..")
                .ToArray();

            return string.Join('/', parts);
        }

        [HttpGet("files")]
        public async Task<IActionResult> GetFilesAsync(
            [FromQuery] string? folder = null,
            [FromQuery] string? search = null,
            [FromQuery] string? category = null,
            [FromQuery] bool includeDeleted = false)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            var query = _dbContext.SyncedFiles.Where(f => f.OwnerId == userId.Value);

            if (!includeDeleted)
            {
                query = query.Where(f => !f.IsDeleted);
            }
            else
            {
                query = query.Where(f => f.IsDeleted);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(f => f.Category == category.Trim());
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string searchLower = search.Trim().ToLowerInvariant();
                query = query.Where(f => f.FileName.ToLower().Contains(searchLower) || f.RelativePath.ToLower().Contains(searchLower));
            }

            var allFiles = await query
                .OrderByDescending(f => f.ModifiedAtUtc)
                .ToListAsync();

            if ((Request.Query.ContainsKey("folder") || folder != null) && string.IsNullOrWhiteSpace(search))
            {
                string sanitizedFolder = SanitizeRelativePath(folder ?? string.Empty);
                var folderFiles = new List<SyncedFileRecord>();
                var subFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var f in allFiles)
                {
                    string fDir = Path.GetDirectoryName(f.RelativePath)?.Replace('\\', '/').Trim('/') ?? string.Empty;

                    if (string.Equals(fDir, sanitizedFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        folderFiles.Add(f);
                    }
                    else
                    {
                        string prefix = string.IsNullOrEmpty(sanitizedFolder) ? "" : sanitizedFolder + "/";
                        if (string.IsNullOrEmpty(prefix) || f.RelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            string remainder = string.IsNullOrEmpty(prefix) ? f.RelativePath : f.RelativePath.Substring(prefix.Length);
                            int slashIndex = remainder.IndexOf('/');
                            if (slashIndex > 0)
                            {
                                subFolders.Add(remainder.Substring(0, slashIndex));
                            }
                            else if (slashIndex == -1 && string.IsNullOrEmpty(sanitizedFolder))
                            {
                                folderFiles.Add(f);
                            }
                        }
                    }
                }

                return Ok(new
                {
                    currentFolder = sanitizedFolder,
                    subFolders = subFolders.OrderBy(s => s).ToList(),
                    files = folderFiles
                });
            }

            return Ok(allFiles);
        }

        [HttpGet("sync/deltas")]
        public async Task<IActionResult> GetSyncDeltasAsync(
            [FromQuery] DateTime? sinceUtc = null,
            [FromQuery] int? sinceVersion = null,
            [FromQuery] Guid? deviceId = null)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            var query = _dbContext.SyncedFiles.Where(f => f.OwnerId == userId.Value);

            if (sinceUtc.HasValue)
            {
                query = query.Where(f => f.ModifiedAtUtc > sinceUtc.Value || (f.DeletedAtUtc.HasValue && f.DeletedAtUtc.Value > sinceUtc.Value));
            }

            if (sinceVersion.HasValue)
            {
                query = query.Where(f => f.Version > sinceVersion.Value);
            }

            var changes = await query
                .OrderBy(f => f.ModifiedAtUtc)
                .ToListAsync();

            return Ok(new
            {
                sinceUtc,
                sinceVersion,
                serverTimeUtc = DateTime.UtcNow,
                changes
            });
        }

        [HttpDelete("files/by-path")]
        public async Task<IActionResult> DeleteFileByPathAsync([FromQuery] string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return BadRequest(new { error = "INVALID_PATH" });

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            string sanitized = SanitizeRelativePath(path);
            var file = await _dbContext.SyncedFiles
                .FirstOrDefaultAsync(f => f.OwnerId == userId.Value && f.RelativePath == sanitized && !f.IsDeleted);

            if (file == null) return NotFound(new { error = "FILE_NOT_FOUND" });

            file.IsDeleted = true;
            file.DeletedAtUtc = DateTime.UtcNow;
            file.ModifiedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, message = "File marked as deleted.", file });
        }

        [HttpGet("files/{id}")]
        public async Task<IActionResult> GetFileByIdAsync([FromRoute] Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            var file = await _dbContext.SyncedFiles
                .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == userId.Value);

            if (file == null) return NotFound(new { error = "FILE_NOT_FOUND" });

            return Ok(file);
        }

        [HttpPost("files")]
        public async Task<IActionResult> RegisterFileMetadataAsync([FromBody] RegisterFileMetadataDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.Sha256Hash))
            {
                return BadRequest(new { error = "INVALID_PAYLOAD", message = "File name and SHA-256 hash are required." });
            }

            if (!IsValidFileName(request.FileName, out var err))
            {
                return BadRequest(new { error = "INVALID_FILENAME", message = err });
            }

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            Guid? validDeviceId = null;
            if (request.DeviceId.HasValue)
            {
                bool deviceExists = await _dbContext.Devices.AnyAsync(d => d.Id == request.DeviceId.Value);
                if (deviceExists)
                {
                    validDeviceId = request.DeviceId.Value;
                }
            }

            string relPath = SanitizeRelativePath(request.RelativePath ?? request.FileName);
            var existing = await _dbContext.SyncedFiles
                .FirstOrDefaultAsync(f => f.OwnerId == userId.Value && f.RelativePath == relPath && !f.IsDeleted);

            if (existing != null)
            {
                if (string.Equals(existing.Sha256Hash, request.Sha256Hash, StringComparison.OrdinalIgnoreCase))
                {
                    existing.SyncState = FileSyncState.Synced;
                    existing.ModifiedAtUtc = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    return Ok(new { success = true, action = "UNCHANGED", file = existing });
                }

                if (request.Version <= existing.Version)
                {
                    existing.SyncState = FileSyncState.Conflict;
                    await _dbContext.SaveChangesAsync();

                    return Conflict(new
                    {
                        error = "SYNC_CONFLICT",
                        message = "File version conflict detected. Local and cloud copies have diverged.",
                        serverFile = existing,
                        incomingHash = request.Sha256Hash,
                        incomingVersion = request.Version
                    });
                }

                existing.Sha256Hash = request.Sha256Hash.ToLowerInvariant().Trim();
                existing.FileSizeBytes = request.FileSizeBytes;
                existing.Version = existing.Version + 1;
                existing.Category = request.Category ?? existing.Category;
                existing.DeviceId = validDeviceId ?? existing.DeviceId;
                existing.SyncState = FileSyncState.Synced;
                existing.ModifiedAtUtc = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                return Ok(new { success = true, action = "UPDATED", file = existing });
            }

            var newFile = new SyncedFileRecord
            {
                Id = Guid.NewGuid(),
                OwnerId = userId.Value,
                DeviceId = validDeviceId,
                FileName = request.FileName.Trim(),
                RelativePath = relPath,
                Category = request.Category ?? "General",
                FileSizeBytes = request.FileSizeBytes,
                Sha256Hash = request.Sha256Hash.ToLowerInvariant().Trim(),
                Version = Math.Max(1, request.Version),
                SyncState = FileSyncState.Synced,
                CreatedAtUtc = DateTime.UtcNow,
                ModifiedAtUtc = request.ModifiedAtUtc ?? DateTime.UtcNow
            };

            _dbContext.SyncedFiles.Add(newFile);
            await _dbContext.SaveChangesAsync();

            return Created($"/api/v1/storage/files/{newFile.Id}", new { success = true, action = "CREATED", file = newFile });
        }

        [HttpPost("upload")]
        [RequestSizeLimit(100L * 1024 * 1024 * 1024)] // 100 GB max
        public async Task<IActionResult> UploadFileAsync(
            [FromForm] IFormFile? file,
            [FromForm] string? targetFolder = null,
            [FromForm] string? category = null)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "NO_FILE_PROVIDED", message = "Please select a valid file to upload." });
            }

            if (!IsValidFileName(file.FileName, out var nameErr))
            {
                return BadRequest(new { error = "INVALID_FILENAME", message = nameErr });
            }

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            string sanitizedFolder = SanitizeRelativePath(targetFolder ?? string.Empty);
            string relativePath = string.IsNullOrEmpty(sanitizedFolder) ? file.FileName : $"{sanitizedFolder}/{file.FileName}";

            string userStorageRoot = GetUserStorageRoot(userId.Value);
            string destinationFullPath = Path.Combine(userStorageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string? destDir = Path.GetDirectoryName(destinationFullPath);
            if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);

            string calculatedHash;
            long totalBytes = 0;

            using (var sha256 = SHA256.Create())
            using (var fileStream = new FileStream(destinationFullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024 * 1024, useAsync: true))
            using (var uploadStream = file.OpenReadStream())
            {
                byte[] buffer = new byte[4 * 1024 * 1024];
                int read;
                while ((read = await uploadStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read);
                    sha256.TransformBlock(buffer, 0, read, null, 0);
                    totalBytes += read;
                }
                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                calculatedHash = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
            }

            var existing = await _dbContext.SyncedFiles
                .FirstOrDefaultAsync(f => f.OwnerId == userId.Value && f.RelativePath == relativePath && !f.IsDeleted);

            if (existing != null)
            {
                existing.Sha256Hash = calculatedHash;
                existing.FileSizeBytes = totalBytes;
                existing.Version = existing.Version + 1;
                existing.Category = category ?? existing.Category;
                existing.SyncState = FileSyncState.Synced;
                existing.ModifiedAtUtc = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                return Ok(new { success = true, action = "UPDATED", file = existing });
            }

            var newRecord = new SyncedFileRecord
            {
                Id = Guid.NewGuid(),
                OwnerId = userId.Value,
                FileName = file.FileName,
                RelativePath = relativePath,
                Category = category ?? "Uploads",
                FileSizeBytes = totalBytes,
                Sha256Hash = calculatedHash,
                Version = 1,
                SyncState = FileSyncState.Synced,
                CreatedAtUtc = DateTime.UtcNow,
                ModifiedAtUtc = DateTime.UtcNow
            };

            _dbContext.SyncedFiles.Add(newRecord);
            await _dbContext.SaveChangesAsync();

            return Created($"/api/v1/storage/files/{newRecord.Id}", new { success = true, action = "CREATED", file = newRecord });
        }

        [HttpGet("files/{id}/download")]
        public async Task<IActionResult> DownloadFileAsync([FromRoute] Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            var record = await _dbContext.SyncedFiles
                .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == userId.Value && !f.IsDeleted);

            if (record == null) return NotFound(new { error = "FILE_NOT_FOUND" });

            string userStorageRoot = GetUserStorageRoot(userId.Value);
            string fullPath = Path.Combine(userStorageRoot, record.RelativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound(new { error = "PHYSICAL_FILE_NOT_FOUND", message = "Physical file not present on storage volume." });
            }

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4 * 1024 * 1024, useAsync: true);
            return this.File(stream, "application/octet-stream", record.FileName, enableRangeProcessing: true);
        }

        [HttpGet("files/{id}/preview")]
        public async Task<IActionResult> PreviewFileAsync([FromRoute] Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            var record = await _dbContext.SyncedFiles
                .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == userId.Value && !f.IsDeleted);

            if (record == null) return NotFound(new { error = "FILE_NOT_FOUND" });

            string userStorageRoot = GetUserStorageRoot(userId.Value);
            string fullPath = Path.Combine(userStorageRoot, record.RelativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(fullPath))
            {
                return Ok(new
                {
                    previewType = "metadata",
                    file = record,
                    message = "Physical preview not cached on cloud; metadata available."
                });
            }

            string ext = Path.GetExtension(record.FileName).ToLowerInvariant();
            string contentType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                ".json" => "application/json",
                ".cs" or ".js" or ".html" or ".css" or ".xml" or ".md" => "text/plain",
                _ => "application/octet-stream"
            };

            if (contentType.StartsWith("text/") || contentType == "application/json")
            {
                if (record.FileSizeBytes <= 2 * 1024 * 1024)
                {
                    string textContent = await System.IO.File.ReadAllTextAsync(fullPath);
                    return Ok(new
                    {
                        previewType = "text",
                        contentType,
                        fileName = record.FileName,
                        content = textContent
                    });
                }
            }

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, useAsync: true);
            return this.File(stream, contentType);
        }

        [HttpPost("files/{id}/rename")]
        public async Task<IActionResult> RenameFileAsync([FromRoute] Guid id, [FromBody] RenameFileDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.NewFileName))
            {
                return BadRequest(new { error = "INVALID_FILENAME", message = "New file name is required." });
            }

            if (!IsValidFileName(request.NewFileName, out var err))
            {
                return BadRequest(new { error = "INVALID_FILENAME", message = err ?? "Invalid file name." });
            }

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            var file = await _dbContext.SyncedFiles
                .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == userId.Value && !f.IsDeleted);

            if (file == null) return NotFound(new { error = "FILE_NOT_FOUND" });

            string dir = Path.GetDirectoryName(file.RelativePath)?.Replace('\\', '/').Trim('/') ?? string.Empty;
            string newRelPath = string.IsNullOrEmpty(dir) ? request.NewFileName.Trim() : $"{dir}/{request.NewFileName.Trim()}";

            string userStorageRoot = GetUserStorageRoot(userId.Value);
            string oldPhysical = Path.Combine(userStorageRoot, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            string newPhysical = Path.Combine(userStorageRoot, newRelPath.Replace('/', Path.DirectorySeparatorChar));

            if (System.IO.File.Exists(oldPhysical))
            {
                System.IO.File.Move(oldPhysical, newPhysical, overwrite: true);
            }

            file.FileName = request.NewFileName.Trim();
            file.RelativePath = newRelPath;
            file.ModifiedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            return Ok(new { success = true, message = "File renamed successfully.", file });
        }

        [HttpPost("files/{id}/move")]
        public async Task<IActionResult> MoveFileAsync([FromRoute] Guid id, [FromBody] MoveFileDto request)
        {
            if (request == null) return BadRequest(new { error = "INVALID_PAYLOAD" });

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            var file = await _dbContext.SyncedFiles
                .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == userId.Value && !f.IsDeleted);

            if (file == null) return NotFound(new { error = "FILE_NOT_FOUND" });

            string targetFolder = SanitizeRelativePath(request.TargetFolder);
            string newRelPath = string.IsNullOrEmpty(targetFolder) ? file.FileName : $"{targetFolder}/{file.FileName}";

            string userStorageRoot = GetUserStorageRoot(userId.Value);
            string oldPhysical = Path.Combine(userStorageRoot, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            string newPhysical = Path.Combine(userStorageRoot, newRelPath.Replace('/', Path.DirectorySeparatorChar));

            if (System.IO.File.Exists(oldPhysical))
            {
                string? destDir = Path.GetDirectoryName(newPhysical);
                if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                System.IO.File.Move(oldPhysical, newPhysical, overwrite: true);
            }

            file.RelativePath = newRelPath;
            file.ModifiedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            return Ok(new { success = true, message = "File moved successfully.", file });
        }

        [HttpDelete("files/{id}")]
        public async Task<IActionResult> DeleteFileAsync([FromRoute] Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            var file = await _dbContext.SyncedFiles
                .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == userId.Value);

            if (file == null) return NotFound(new { error = "FILE_NOT_FOUND" });

            file.IsDeleted = true;
            file.DeletedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, message = "File moved to Trash (soft deleted)." });
        }

        [HttpPost("files/{id}/restore")]
        public async Task<IActionResult> RestoreFileAsync([FromRoute] Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            var file = await _dbContext.SyncedFiles
                .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == userId.Value && f.IsDeleted);

            if (file == null) return NotFound(new { error = "FILE_NOT_FOUND_IN_TRASH" });

            file.IsDeleted = false;
            file.DeletedAtUtc = null;
            file.ModifiedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, message = "File restored from Trash.", file });
        }

        [HttpDelete("files/{id}/permanent")]
        public async Task<IActionResult> PermanentlyDeleteFileAsync([FromRoute] Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            var file = await _dbContext.SyncedFiles
                .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == userId.Value);

            if (file == null) return NotFound(new { error = "FILE_NOT_FOUND" });

            string userStorageRoot = GetUserStorageRoot(userId.Value);
            string physical = Path.Combine(userStorageRoot, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(physical))
            {
                try { System.IO.File.Delete(physical); } catch { /* Ignore */ }
            }

            _dbContext.SyncedFiles.Remove(file);
            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, message = "File permanently deleted." });
        }

        [HttpPost("files/{id}/resolve-conflict")]
        public async Task<IActionResult> ResolveConflictAsync([FromRoute] Guid id, [FromBody] ResolveConflictDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Strategy))
            {
                return BadRequest(new { error = "INVALID_STRATEGY", message = "Resolution strategy is required." });
            }

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            var file = await _dbContext.SyncedFiles
                .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == userId.Value && !f.IsDeleted);

            if (file == null) return NotFound(new { error = "FILE_NOT_FOUND" });

            switch (request.Strategy.ToUpperInvariant())
            {
                case "KEEPLOCAL":
                    if (!string.IsNullOrEmpty(request.ResolvedHash)) file.Sha256Hash = request.ResolvedHash.ToLowerInvariant();
                    if (request.ResolvedSize.HasValue) file.FileSizeBytes = request.ResolvedSize.Value;
                    file.Version = (request.NewVersion ?? file.Version) + 1;
                    file.SyncState = FileSyncState.Synced;
                    file.ConflictResolution = "KeepLocal";
                    file.ModifiedAtUtc = DateTime.UtcNow;
                    break;

                case "KEEPREMOTE":
                    file.SyncState = FileSyncState.Synced;
                    file.ConflictResolution = "KeepRemote";
                    file.ModifiedAtUtc = DateTime.UtcNow;
                    break;

                case "KEEPBOTH":
                    file.SyncState = FileSyncState.Synced;
                    file.ConflictResolution = "KeepBoth";
                    var forked = new SyncedFileRecord
                    {
                        Id = Guid.NewGuid(),
                        OwnerId = userId.Value,
                        DeviceId = file.DeviceId,
                        FileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_ConflictCopy_{DateTime.UtcNow:yyyyMMdd_HHmmss}{Path.GetExtension(file.FileName)}",
                        RelativePath = $"{Path.GetDirectoryName(file.RelativePath)}/{Path.GetFileNameWithoutExtension(file.FileName)}_ConflictCopy_{DateTime.UtcNow:yyyyMMdd_HHmmss}{Path.GetExtension(file.FileName)}".TrimStart('/'),
                        Category = file.Category,
                        FileSizeBytes = request.ResolvedSize ?? file.FileSizeBytes,
                        Sha256Hash = request.ResolvedHash ?? file.Sha256Hash,
                        Version = 1,
                        SyncState = FileSyncState.Synced,
                        CreatedAtUtc = DateTime.UtcNow,
                        ModifiedAtUtc = DateTime.UtcNow
                    };
                    _dbContext.SyncedFiles.Add(forked);
                    break;

                default:
                    return BadRequest(new { error = "UNKNOWN_STRATEGY", message = "Supported strategies: KeepLocal, KeepRemote, KeepBoth" });
            }

            await _dbContext.SaveChangesAsync();
            return Ok(new { success = true, message = "Conflict resolved successfully.", file });
        }

        [HttpGet("quota")]
        public async Task<IActionResult> GetStorageQuotaAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "UNAUTHORIZED" });

            var userFiles = await _dbContext.SyncedFiles
                .Where(f => f.OwnerId == userId.Value && !f.IsDeleted)
                .ToListAsync();

            long usedBytes = userFiles.Sum(f => f.FileSizeBytes);
            int totalFiles = userFiles.Count;
            long maxQuotaBytes = 50L * 1024 * 1024 * 1024; // 50 GB

            return Ok(new
            {
                usedBytes,
                maxQuotaBytes,
                usedPercentage = maxQuotaBytes > 0 ? Math.Round((double)usedBytes / maxQuotaBytes * 100, 2) : 0,
                totalFiles,
                categories = userFiles.GroupBy(f => f.Category).Select(g => new { Category = g.Key, Count = g.Count(), SizeBytes = g.Sum(x => x.FileSizeBytes) })
            });
        }
    }
}
