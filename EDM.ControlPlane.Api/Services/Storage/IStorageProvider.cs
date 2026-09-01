using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.ControlPlane.Api.Services.Storage
{
    public record StorageFileInfo(
        string Name,
        string RelativePath,
        long SizeBytes,
        DateTime LastModifiedUtc,
        string Sha256Hash,
        bool IsDirectory = false);

    public record StorageConflictInfo(
        bool HasConflict,
        string FilePath,
        DateTime LocalModifiedUtc,
        DateTime RemoteModifiedUtc,
        string LocalHash,
        string RemoteHash);

    public interface IStorageProvider
    {
        string ProviderName { get; }
        bool IsConfigured { get; }
        
        Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default);
        Task<StorageFileInfo?> GetFileInfoAsync(string relativePath, CancellationToken ct = default);
        Task<IEnumerable<StorageFileInfo>> ListFilesAsync(string relativeDirectory = "", string searchPattern = "*", bool recursive = false, CancellationToken ct = default);
        Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default);
        Task<string> ReadAllTextAsync(string relativePath, CancellationToken ct = default);
        Task WriteAllTextAsync(string relativePath, string content, CancellationToken ct = default);
        Task SaveFileAsync(string relativePath, Stream contentStream, bool overwrite = true, CancellationToken ct = default);
        Task<string> CreateBackupRevisionAsync(string relativePath, CancellationToken ct = default);
        Task DeleteFileAsync(string relativePath, CancellationToken ct = default);
        Task CreateDirectoryAsync(string relativeDirectory, CancellationToken ct = default);
        Task<string> CalculateSha256Async(string relativePath, CancellationToken ct = default);
    }
}
