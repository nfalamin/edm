using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EDM.ControlPlane.Api.Services.Storage
{
    public class LocalFileStorageProvider : IStorageProvider
    {
        private readonly string _workspaceRoot;
        private readonly ILogger<LocalFileStorageProvider> _logger;

        public string ProviderName => "LocalFileStorage";
        public bool IsConfigured => Directory.Exists(_workspaceRoot);

        public LocalFileStorageProvider(IConfiguration configuration, ILogger<LocalFileStorageProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Resolve workspace root (defaults to solution root containing Update/ and Content/)
            string? configuredRoot = configuration["Storage:LocalWorkspaceRoot"];
            if (string.IsNullOrWhiteSpace(configuredRoot))
            {
                // Fall back to solution directory or current directory parent
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                DirectoryInfo? dir = new DirectoryInfo(baseDir);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "EDM.slnx")) && !Directory.Exists(Path.Combine(dir.FullName, "Update")))
                {
                    dir = dir.Parent;
                }
                _workspaceRoot = dir?.FullName ?? Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            }
            else
            {
                _workspaceRoot = Path.GetFullPath(configuredRoot);
            }

            _logger.LogInformation("LocalFileStorageProvider initialized with Workspace Root: {Root}", _workspaceRoot);
        }

        private string ResolveAndValidatePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Relative path cannot be empty.", nameof(relativePath));
            }

            // Normalize path separators
            string cleanRel = relativePath.Replace('\\', '/').TrimStart('/');
            string combined = Path.Combine(_workspaceRoot, cleanRel);
            string fullPath = Path.GetFullPath(combined);

            // Strict sandbox validation: must be within _workspaceRoot
            if (!fullPath.StartsWith(_workspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Security violation: Attempted path traversal outside workspace: {Path}", relativePath);
                throw new UnauthorizedAccessException($"Access to path outside workspace sandbox is denied: {relativePath}");
            }

            return fullPath;
        }

        public Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default)
        {
            string fullPath = ResolveAndValidatePath(relativePath);
            return Task.FromResult(File.Exists(fullPath) || Directory.Exists(fullPath));
        }

        public async Task<StorageFileInfo?> GetFileInfoAsync(string relativePath, CancellationToken ct = default)
        {
            string fullPath = ResolveAndValidatePath(relativePath);
            if (!File.Exists(fullPath))
            {
                if (Directory.Exists(fullPath))
                {
                    var dirInfo = new DirectoryInfo(fullPath);
                    return new StorageFileInfo(dirInfo.Name, relativePath, 0, dirInfo.LastWriteTimeUtc, string.Empty, true);
                }
                return null;
            }

            var fi = new FileInfo(fullPath);
            string hash = await CalculateSha256Async(relativePath, ct).ConfigureAwait(false);
            return new StorageFileInfo(fi.Name, relativePath, fi.Length, fi.LastWriteTimeUtc, hash, false);
        }

        public Task<IEnumerable<StorageFileInfo>> ListFilesAsync(string relativeDirectory = "", string searchPattern = "*", bool recursive = false, CancellationToken ct = default)
        {
            string dirPath = string.IsNullOrWhiteSpace(relativeDirectory) ? _workspaceRoot : ResolveAndValidatePath(relativeDirectory);
            if (!Directory.Exists(dirPath))
            {
                return Task.FromResult<IEnumerable<StorageFileInfo>>(Array.Empty<StorageFileInfo>());
            }

            var result = new List<StorageFileInfo>();
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            foreach (var filePath in Directory.EnumerateFiles(dirPath, searchPattern, searchOption))
            {
                var fi = new FileInfo(filePath);
                string rel = Path.GetRelativePath(_workspaceRoot, filePath).Replace('\\', '/');
                result.Add(new StorageFileInfo(fi.Name, rel, fi.Length, fi.LastWriteTimeUtc, string.Empty, false));
            }

            return Task.FromResult<IEnumerable<StorageFileInfo>>(result);
        }

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default)
        {
            string fullPath = ResolveAndValidatePath(relativePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Storage file not found: {relativePath}", fullPath);
            }
            Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            return Task.FromResult(stream);
        }

        public async Task<string> ReadAllTextAsync(string relativePath, CancellationToken ct = default)
        {
            string fullPath = ResolveAndValidatePath(relativePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Storage file not found: {relativePath}", fullPath);
            }
            return await File.ReadAllTextAsync(fullPath, Encoding.UTF8, ct).ConfigureAwait(false);
        }

        public async Task WriteAllTextAsync(string relativePath, string content, CancellationToken ct = default)
        {
            string fullPath = ResolveAndValidatePath(relativePath);
            string? dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8, ct).ConfigureAwait(false);
            _logger.LogInformation("Saved text content to storage file: {Path}", relativePath);
        }

        public async Task SaveFileAsync(string relativePath, Stream contentStream, bool overwrite = true, CancellationToken ct = default)
        {
            if (contentStream == null) throw new ArgumentNullException(nameof(contentStream));

            string fullPath = ResolveAndValidatePath(relativePath);
            string? dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            FileMode mode = overwrite ? FileMode.Create : FileMode.CreateNew;
            using (var fileStream = new FileStream(fullPath, mode, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await contentStream.CopyToAsync(fileStream, ct).ConfigureAwait(false);
            }
            _logger.LogInformation("Saved binary payload to storage file: {Path}", relativePath);
        }

        public Task<string> CreateBackupRevisionAsync(string relativePath, CancellationToken ct = default)
        {
            string fullPath = ResolveAndValidatePath(relativePath);
            if (!File.Exists(fullPath))
            {
                return Task.FromResult(string.Empty);
            }

            string dir = Path.GetDirectoryName(fullPath) ?? _workspaceRoot;
            string backupDir = Path.Combine(dir, ".revisions");
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            string fileName = Path.GetFileNameWithoutExtension(fullPath);
            string ext = Path.GetExtension(fullPath);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string backupFileName = $"{fileName}_{timestamp}{ext}";
            string backupFullPath = Path.Combine(backupDir, backupFileName);

            File.Copy(fullPath, backupFullPath, overwrite: true);
            string relBackup = Path.GetRelativePath(_workspaceRoot, backupFullPath).Replace('\\', '/');
            _logger.LogInformation("Created revision backup: {BackupPath} for {Original}", relBackup, relativePath);
            return Task.FromResult(relBackup);
        }

        public Task DeleteFileAsync(string relativePath, CancellationToken ct = default)
        {
            string fullPath = ResolveAndValidatePath(relativePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("Deleted storage file: {Path}", relativePath);
            }
            return Task.CompletedTask;
        }

        public Task CreateDirectoryAsync(string relativeDirectory, CancellationToken ct = default)
        {
            string fullPath = ResolveAndValidatePath(relativeDirectory);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                _logger.LogInformation("Created storage directory: {Path}", relativeDirectory);
            }
            return Task.CompletedTask;
        }

        public async Task<string> CalculateSha256Async(string relativePath, CancellationToken ct = default)
        {
            string fullPath = ResolveAndValidatePath(relativePath);
            if (!File.Exists(fullPath)) return string.Empty;

            using var sha256 = SHA256.Create();
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            byte[] hashBytes = await sha256.ComputeHashAsync(stream, ct).ConfigureAwait(false);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
