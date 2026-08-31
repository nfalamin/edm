using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Services
{
    public record CreateReleaseModel(
        ClientType Platform,
        string Version,
        string Channel,
        string MinimumSupportedVersion,
        string Title,
        string ReleaseNotes,
        bool IsMandatory,
        ReleaseSeverity Severity,
        List<CreateArtifactModel>? Artifacts = null);

    public record UpdateReleaseModel(
        string? Version,
        string? Channel,
        string? MinimumSupportedVersion,
        string? Title,
        string? ReleaseNotes,
        bool? IsMandatory,
        ReleaseSeverity? Severity);

    public record CreateArtifactModel(
        string ArtifactName,
        string Architecture,
        string DownloadUrl,
        string Sha256Hash,
        long FileSizeBytes,
        string? SignatureBase64);

    public interface IReleaseService
    {
        Task<Release> CreateReleaseAsync(CreateReleaseModel model, Guid? adminActorId = null);
        Task<Release?> UpdateReleaseAsync(Guid releaseId, UpdateReleaseModel model, Guid? adminActorId = null);
        Task<ReleaseArtifact> UploadArtifactAsync(Guid releaseId, Stream fileStream, string originalFileName, string architecture, string? expectedSha256 = null, Guid? adminActorId = null);
        Task<bool> DeleteArtifactAsync(Guid releaseId, Guid artifactId, Guid? adminActorId = null);
        Task<bool> PublishReleaseAsync(Guid releaseId, Guid? adminActorId = null);
        Task<bool> UnpublishReleaseAsync(Guid releaseId, Guid? adminActorId = null);
        Task<bool> RollbackReleaseAsync(Guid releaseId, string rollbackTargetVersion, string reason, Guid? adminActorId = null);
        Task<bool> WithdrawReleaseAsync(Guid releaseId, string reason, Guid? adminActorId = null);
        Task<List<Release>> GetReleasesAsync(ClientType? platform = null, string? channel = null, bool includeWithdrawn = false);
        Task<Release?> GetReleaseByIdAsync(Guid releaseId);
        Task<Release?> GetLatestActiveReleaseAsync(ClientType platform, string channel = "stable");
        Task<(Stream Stream, string ContentType, string FileName, long FileLength)> GetArtifactFileStreamAsync(Guid artifactId);
        Task RecordArtifactDownloadAsync(Guid artifactId, string? clientIp, string? userAgent, string? countryCode = null);
    }

    public class ReleaseService : IReleaseService
    {
        private readonly ControlPlaneDbContext _dbContext;
        private readonly IAuditLoggingService _auditLogger;
        private readonly string _storageBasePath;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".msi", ".zip", ".dmg", ".pkg", ".deb", ".rpm", ".appimage", ".tar.gz", ".tgz", ".tar.bz2"
        };

        private static readonly HashSet<string> ProhibitedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".dll", ".aspx", ".asp", ".php", ".jsp", ".bat", ".cmd", ".ps1", ".sh", ".vbs", ".html", ".htm", ".js", ".py"
        };

        private const long MaxFileSizeBytes = 524_288_000; // 500 MB Limit

        public ReleaseService(ControlPlaneDbContext dbContext, IAuditLoggingService? auditLogger = null)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _auditLogger = auditLogger ?? new AuditLoggingService(_dbContext);

            _storageBasePath = Path.Combine(AppContext.BaseDirectory, "App_Data", "Storage", "Artifacts");
            if (!Directory.Exists(_storageBasePath))
            {
                Directory.CreateDirectory(_storageBasePath);
            }
        }

        public async Task<Release> CreateReleaseAsync(CreateReleaseModel model, Guid? adminActorId = null)
        {
            if (string.IsNullOrWhiteSpace(model.Version))
            {
                throw new ArgumentException("Version string cannot be empty.", nameof(model.Version));
            }

            bool exists = await _dbContext.Releases.AnyAsync(r => r.Platform == model.Platform && r.Version == model.Version.Trim());
            if (exists)
            {
                throw new InvalidOperationException($"Release version '{model.Version}' already exists for platform '{model.Platform}'.");
            }

            var release = new Release
            {
                Id = Guid.NewGuid(),
                Platform = model.Platform,
                Version = model.Version.Trim(),
                Channel = string.IsNullOrWhiteSpace(model.Channel) ? "stable" : model.Channel.ToLowerInvariant().Trim(),
                MinimumSupportedVersion = string.IsNullOrWhiteSpace(model.MinimumSupportedVersion) ? "1.0.0" : model.MinimumSupportedVersion.Trim(),
                Title = string.IsNullOrWhiteSpace(model.Title) ? $"EDM {model.Version}" : model.Title.Trim(),
                ReleaseNotes = model.ReleaseNotes ?? string.Empty,
                IsMandatory = model.IsMandatory,
                IsPublished = true,
                IsWithdrawn = false,
                Severity = model.Severity,
                CreatedByUserId = adminActorId,
                PublishedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            if (model.Artifacts != null)
            {
                foreach (var art in model.Artifacts)
                {
                    release.Artifacts.Add(new ReleaseArtifact
                    {
                        Id = Guid.NewGuid(),
                        ReleaseId = release.Id,
                        ArtifactName = art.ArtifactName,
                        Architecture = string.IsNullOrWhiteSpace(art.Architecture) ? "x64" : art.Architecture,
                        DownloadUrl = art.DownloadUrl,
                        Sha256Hash = art.Sha256Hash,
                        FileSizeBytes = art.FileSizeBytes,
                        SignatureBase64 = art.SignatureBase64,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }
            }

            _dbContext.Releases.Add(release);
            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: adminActorId,
                actorUsername: "RELEASE_MANAGER",
                action: "RELEASE_CREATED",
                targetEntity: "Release",
                targetId: release.Id.ToString(),
                detailsJson: $"{{\"version\":\"{release.Version}\",\"platform\":\"{release.Platform}\",\"channel\":\"{release.Channel}\"}}",
                correlationId: Guid.NewGuid().ToString("N"));

            return release;
        }

        public async Task<Release?> UpdateReleaseAsync(Guid releaseId, UpdateReleaseModel model, Guid? adminActorId = null)
        {
            var release = await _dbContext.Releases.Include(r => r.Artifacts).FirstOrDefaultAsync(r => r.Id == releaseId);
            if (release == null) return null;

            if (!string.IsNullOrWhiteSpace(model.Version)) release.Version = model.Version.Trim();
            if (!string.IsNullOrWhiteSpace(model.Channel)) release.Channel = model.Channel.ToLowerInvariant().Trim();
            if (!string.IsNullOrWhiteSpace(model.MinimumSupportedVersion)) release.MinimumSupportedVersion = model.MinimumSupportedVersion.Trim();
            if (!string.IsNullOrWhiteSpace(model.Title)) release.Title = model.Title.Trim();
            if (model.ReleaseNotes != null) release.ReleaseNotes = model.ReleaseNotes;
            if (model.IsMandatory.HasValue) release.IsMandatory = model.IsMandatory.Value;
            if (model.Severity.HasValue) release.Severity = model.Severity.Value;

            release.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: adminActorId,
                actorUsername: "RELEASE_MANAGER",
                action: "RELEASE_UPDATED",
                targetEntity: "Release",
                targetId: release.Id.ToString(),
                detailsJson: $"{{\"version\":\"{release.Version}\"}}",
                correlationId: Guid.NewGuid().ToString("N"));

            return release;
        }

        public async Task<ReleaseArtifact> UploadArtifactAsync(
            Guid releaseId,
            Stream fileStream,
            string originalFileName,
            string architecture,
            string? expectedSha256 = null,
            Guid? adminActorId = null)
        {
            if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));
            if (string.IsNullOrWhiteSpace(originalFileName)) throw new ArgumentException("Filename cannot be empty.", nameof(originalFileName));

            var release = await _dbContext.Releases.FindAsync(releaseId);
            if (release == null)
            {
                throw new InvalidOperationException($"Release {releaseId} not found.");
            }

            string safeFileName = Path.GetFileName(originalFileName).Trim();
            string extension = Path.GetExtension(safeFileName).ToLowerInvariant();

            if (ProhibitedExtensions.Contains(extension))
            {
                throw new InvalidOperationException($"File extension '{extension}' is strictly prohibited for security.");
            }

            if (!AllowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException($"File extension '{extension}' is not in the allowed installer binary formats ({string.Join(", ", AllowedExtensions)}).");
            }

            Guid artifactId = Guid.NewGuid();
            string releaseStorageFolder = Path.Combine(_storageBasePath, releaseId.ToString());
            if (!Directory.Exists(releaseStorageFolder))
            {
                Directory.CreateDirectory(releaseStorageFolder);
            }

            string diskFileName = $"{artifactId:N}_{safeFileName}";
            string diskFilePath = Path.Combine(releaseStorageFolder, diskFileName);

            long totalBytes = 0;
            string calculatedSha256;

            using (var destinationStream = new FileStream(diskFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            using (var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                byte[] buffer = new byte[81920];
                int bytesRead;

                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    totalBytes += bytesRead;
                    if (totalBytes > MaxFileSizeBytes)
                    {
                        destinationStream.Close();
                        try { File.Delete(diskFilePath); } catch { }
                        throw new InvalidOperationException($"Upload exceeds maximum allowed size of {MaxFileSizeBytes / 1024 / 1024} MB.");
                    }

                    await destinationStream.WriteAsync(buffer, 0, bytesRead);
                    sha256.AppendData(buffer, 0, bytesRead);
                }

                calculatedSha256 = Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant();
            }

            // Verify hash if client provided expectation
            if (!string.IsNullOrWhiteSpace(expectedSha256))
            {
                string cleanExpected = expectedSha256.Trim().ToLowerInvariant();
                if (!string.Equals(cleanExpected, calculatedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(diskFilePath); } catch { }
                    throw new InvalidOperationException($"Integrity verification failed. Expected SHA-256: {cleanExpected}, Calculated: {calculatedSha256}");
                }
            }

            var artifact = new ReleaseArtifact
            {
                Id = artifactId,
                ReleaseId = releaseId,
                ArtifactName = safeFileName,
                Architecture = string.IsNullOrWhiteSpace(architecture) ? "x64" : architecture.Trim().ToLowerInvariant(),
                DownloadUrl = $"/api/v1/releases/artifacts/{artifactId}/download",
                Sha256Hash = calculatedSha256,
                FileSizeBytes = totalBytes,
                StorageProvider = "local",
                StoragePath = diskFilePath,
                DownloadCount = 0,
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.ReleaseArtifacts.Add(artifact);
            release.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: adminActorId,
                actorUsername: "RELEASE_MANAGER",
                action: "ARTIFACT_UPLOADED",
                targetEntity: "ReleaseArtifact",
                targetId: artifact.Id.ToString(),
                detailsJson: $"{{\"artifactName\":\"{artifact.ArtifactName}\",\"sizeBytes\":{artifact.FileSizeBytes},\"sha256\":\"{artifact.Sha256Hash}\"}}",
                correlationId: Guid.NewGuid().ToString("N"));

            return artifact;
        }

        public async Task<bool> DeleteArtifactAsync(Guid releaseId, Guid artifactId, Guid? adminActorId = null)
        {
            var artifact = await _dbContext.ReleaseArtifacts.FirstOrDefaultAsync(a => a.ReleaseId == releaseId && a.Id == artifactId);
            if (artifact == null) return false;

            if (!string.IsNullOrWhiteSpace(artifact.StoragePath) && File.Exists(artifact.StoragePath))
            {
                try { File.Delete(artifact.StoragePath); } catch { }
            }

            _dbContext.ReleaseArtifacts.Remove(artifact);
            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: adminActorId,
                actorUsername: "RELEASE_MANAGER",
                action: "ARTIFACT_DELETED",
                targetEntity: "ReleaseArtifact",
                targetId: artifactId.ToString(),
                detailsJson: $"{{\"releaseId\":\"{releaseId}\"}}",
                correlationId: Guid.NewGuid().ToString("N"));

            return true;
        }

        public async Task<bool> PublishReleaseAsync(Guid releaseId, Guid? adminActorId = null)
        {
            var release = await _dbContext.Releases.FindAsync(releaseId);
            if (release == null) return false;

            release.IsPublished = true;
            release.IsWithdrawn = false;
            release.PublishedAtUtc = DateTime.UtcNow;
            release.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: adminActorId,
                actorUsername: "RELEASE_MANAGER",
                action: "RELEASE_PUBLISHED",
                targetEntity: "Release",
                targetId: release.Id.ToString(),
                detailsJson: $"{{\"version\":\"{release.Version}\"}}",
                correlationId: Guid.NewGuid().ToString("N"));

            return true;
        }

        public async Task<bool> UnpublishReleaseAsync(Guid releaseId, Guid? adminActorId = null)
        {
            var release = await _dbContext.Releases.FindAsync(releaseId);
            if (release == null) return false;

            release.IsPublished = false;
            release.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: adminActorId,
                actorUsername: "RELEASE_MANAGER",
                action: "RELEASE_UNPUBLISHED",
                targetEntity: "Release",
                targetId: release.Id.ToString(),
                detailsJson: $"{{\"version\":\"{release.Version}\"}}",
                correlationId: Guid.NewGuid().ToString("N"));

            return true;
        }

        public async Task<bool> RollbackReleaseAsync(Guid releaseId, string rollbackTargetVersion, string reason, Guid? adminActorId = null)
        {
            var release = await _dbContext.Releases.FindAsync(releaseId);
            if (release == null) return false;

            var targetRelease = await _dbContext.Releases
                .FirstOrDefaultAsync(r => r.Platform == release.Platform && r.Version == rollbackTargetVersion && !r.IsWithdrawn);

            if (targetRelease == null)
            {
                throw new InvalidOperationException($"Rollback target version '{rollbackTargetVersion}' does not exist or is withdrawn.");
            }

            release.IsWithdrawn = true;
            release.RollbackTargetVersion = rollbackTargetVersion;
            release.RollbackReason = reason;
            release.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: adminActorId,
                actorUsername: "RELEASE_MANAGER",
                action: "RELEASE_ROLLBACK",
                targetEntity: "Release",
                targetId: release.Id.ToString(),
                detailsJson: $"{{\"rolledBackVersion\":\"{release.Version}\",\"targetVersion\":\"{rollbackTargetVersion}\",\"reason\":\"{reason}\"}}",
                correlationId: Guid.NewGuid().ToString("N"),
                resultStatus: "SUCCESS");

            return true;
        }

        public async Task<bool> WithdrawReleaseAsync(Guid releaseId, string reason, Guid? adminActorId = null)
        {
            var release = await _dbContext.Releases.FindAsync(releaseId);
            if (release == null) return false;

            release.IsWithdrawn = true;
            release.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: adminActorId,
                actorUsername: "RELEASE_MANAGER",
                action: "RELEASE_WITHDRAWN",
                targetEntity: "Release",
                targetId: release.Id.ToString(),
                detailsJson: $"{{\"version\":\"{release.Version}\",\"reason\":\"{reason}\"}}",
                correlationId: Guid.NewGuid().ToString("N"));

            return true;
        }

        public async Task<List<Release>> GetReleasesAsync(ClientType? platform = null, string? channel = null, bool includeWithdrawn = false)
        {
            var query = _dbContext.Releases.Include(r => r.Artifacts).AsQueryable();

            if (platform.HasValue) query = query.Where(r => r.Platform == platform.Value);
            if (!string.IsNullOrWhiteSpace(channel)) query = query.Where(r => r.Channel == channel.ToLowerInvariant());
            if (!includeWithdrawn) query = query.Where(r => !r.IsWithdrawn);

            return await query.OrderByDescending(r => r.PublishedAtUtc).ToListAsync();
        }

        public async Task<Release?> GetReleaseByIdAsync(Guid releaseId)
        {
            return await _dbContext.Releases
                .Include(r => r.Artifacts)
                .FirstOrDefaultAsync(r => r.Id == releaseId);
        }

        public async Task<Release?> GetLatestActiveReleaseAsync(ClientType platform, string channel = "stable")
        {
            string cleanChannel = (channel ?? "stable").Trim().ToLowerInvariant();
            return await _dbContext.Releases
                .Include(r => r.Artifacts)
                .Where(r => r.Platform == platform && (r.Channel == null || r.Channel.ToLower() == cleanChannel) && r.IsPublished && !r.IsWithdrawn)
                .OrderByDescending(r => r.PublishedAtUtc)
                .FirstOrDefaultAsync();
        }

        public async Task<(Stream Stream, string ContentType, string FileName, long FileLength)> GetArtifactFileStreamAsync(Guid artifactId)
        {
            var artifact = await _dbContext.ReleaseArtifacts.Include(a => a.Release).FirstOrDefaultAsync(a => a.Id == artifactId);
            if (artifact == null)
            {
                throw new FileNotFoundException("Release artifact metadata not found in database.");
            }

            string safeFileName = Path.GetFileName(artifact.ArtifactName);
            string? resolvedPath = null;

            if (!string.IsNullOrWhiteSpace(artifact.StoragePath) && File.Exists(artifact.StoragePath))
            {
                resolvedPath = artifact.StoragePath;
            }
            else
            {
                // Fallback to local distribution and downloads folders
                var searchDirs = new[]
                {
                    _storageBasePath,
                    Path.Combine(AppContext.BaseDirectory, "downloads"),
                    Path.Combine(Directory.GetCurrentDirectory(), "website", "downloads"),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "website", "downloads"),
                    Path.Combine(AppContext.BaseDirectory, "..", "website", "downloads"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Dist", "EDM_v1.0_Complete_Distribution"),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Dist", "EDM_v1.0_Complete_Distribution"),
                    Path.Combine(AppContext.BaseDirectory, "..", "Dist", "EDM_v1.0_Complete_Distribution")
                };

                var fileCandidates = new List<string> { safeFileName };
                if (safeFileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    fileCandidates.Add("EDM-Setup-v2.1.0.exe");
                    fileCandidates.Add("EDM_Setup_v1.0.exe");
                    fileCandidates.Add("EDM-Setup-v2.0.0.exe");
                    fileCandidates.Add("EDM-Setup-v1.0.0.exe");
                }

                foreach (var dir in searchDirs)
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    try
                    {
                        foreach (var cand in fileCandidates)
                        {
                            var candidatePath = Path.GetFullPath(Path.Combine(dir, cand));
                            if (File.Exists(candidatePath))
                            {
                                resolvedPath = candidatePath;
                                break;
                            }
                        }
                        if (resolvedPath != null) break;
                    }
                    catch { }
                }
            }

            if (resolvedPath == null || !File.Exists(resolvedPath))
            {
                throw new FileNotFoundException($"Artifact binary file '{safeFileName}' not found on disk at specified storage location.");
            }

            var fileInfo = new FileInfo(resolvedPath);
            var fileStream = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

            string contentType = GetContentType(safeFileName);
            return (fileStream, contentType, safeFileName, fileInfo.Length);
        }

        public async Task RecordArtifactDownloadAsync(Guid artifactId, string? clientIp, string? userAgent, string? countryCode = null)
        {
            var artifact = await _dbContext.ReleaseArtifacts.FindAsync(artifactId);
            if (artifact != null)
            {
                artifact.DownloadCount++;

                var record = new DownloadRecord
                {
                    Id = Guid.NewGuid(),
                    ReleaseArtifactId = artifactId,
                    ClientIpCoarse = clientIp,
                    UserAgent = userAgent,
                    CountryCode = countryCode,
                    BytesTransferred = artifact.FileSizeBytes,
                    Status = DownloadStatus.Completed,
                    DownloadedAtUtc = DateTime.UtcNow
                };

                _dbContext.DownloadRecords.Add(record);
                await _dbContext.SaveChangesAsync();
            }
        }

        private static string GetContentType(string filename)
        {
            string ext = Path.GetExtension(filename).ToLowerInvariant();
            return ext switch
            {
                ".exe" => "application/vnd.microsoft.portable-executable",
                ".msi" => "application/x-msdownload",
                ".zip" => "application/zip",
                ".dmg" => "application/x-apple-diskimage",
                ".pkg" => "application/octet-stream",
                ".deb" => "application/vnd.debian.binary-package",
                ".rpm" => "application/x-rpm",
                ".appimage" => "application/x-executable",
                ".tar.gz" or ".tgz" => "application/gzip",
                _ => "application/octet-stream"
            };
        }
    }
}
