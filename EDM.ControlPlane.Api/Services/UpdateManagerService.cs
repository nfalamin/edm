using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services.Storage;

namespace EDM.ControlPlane.Api.Services
{
    public class UpdateManagerService : IUpdateManagerService
    {
        private readonly ControlPlaneDbContext _dbContext;
        private readonly IStorageProvider _storageProvider;
        private readonly IAuditLoggingService _auditLogger;
        private readonly ILogger<UpdateManagerService> _logger;

        public UpdateManagerService(
            ControlPlaneDbContext dbContext,
            IStorageProvider storageProvider,
            IAuditLoggingService auditLogger,
            ILogger<UpdateManagerService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private static string ComputeStatus(Release r)
        {
            if (r.IsWithdrawn) return "Archived";
            if (r.IsDraft) return "Draft";
            if (r.IsPublished)
            {
                if (r.IsWebsiteDownloadEnabled || r.IsAutoUpdateEnabled) return "Active";
                return "Disabled";
            }
            return "Draft";
        }

        private static UpdateSummaryDto MapToDto(Release r)
        {
            return new UpdateSummaryDto(
                Id: r.Id,
                Component: r.Component ?? "App",
                Version: r.Version,
                Title: r.Title,
                Channel: r.Channel,
                Severity: r.Severity.ToString(),
                MinimumSupportedVersion: r.MinimumSupportedVersion,
                IsMandatory: r.IsMandatory,
                IsDraft: r.IsDraft,
                IsPublished: r.IsPublished,
                IsWithdrawn: r.IsWithdrawn,
                IsWebsiteDownloadEnabled: r.IsWebsiteDownloadEnabled,
                IsAutoUpdateEnabled: r.IsAutoUpdateEnabled,
                IsLatest: r.IsLatest,
                Status: ComputeStatus(r),
                CreatedAtUtc: r.CreatedAtUtc,
                PublishedAtUtc: r.IsPublished ? r.PublishedAtUtc : null,
                UpdatedAtUtc: r.UpdatedAtUtc,
                Artifacts: r.Artifacts.Select(a => new ArtifactSummaryDto(
                    Id: a.Id,
                    FileName: a.ArtifactName,
                    RelativePath: a.DownloadUrl ?? string.Empty,
                    Architecture: a.Architecture,
                    FileSizeBytes: a.FileSizeBytes,
                    Sha256Hash: a.Sha256Hash,
                    DownloadUrl: a.DownloadUrl ?? string.Empty
                )).ToList()
            );
        }

        public async Task<IEnumerable<UpdateSummaryDto>> GetAllUpdatesAsync(string? component = null, bool includeDrafts = true, CancellationToken ct = default)
        {
            var query = _dbContext.Releases.Include(r => r.Artifacts).AsQueryable();

            if (!string.IsNullOrWhiteSpace(component))
            {
                query = query.Where(r => r.Component.ToLower() == component.ToLower());
            }

            if (!includeDrafts)
            {
                query = query.Where(r => r.IsPublished && !r.IsWithdrawn);
            }

            var list = await query.OrderByDescending(r => r.CreatedAtUtc).ToListAsync(ct).ConfigureAwait(false);
            return list.Select(MapToDto);
        }

        public async Task<UpdateSummaryDto?> GetPublishedLatestAsync(string component = "App", CancellationToken ct = default)
        {
            var release = await _dbContext.Releases
                .Include(r => r.Artifacts)
                .Where(r => r.Component.ToLower() == component.ToLower() && r.IsPublished && !r.IsWithdrawn && r.IsWebsiteDownloadEnabled)
                .OrderByDescending(r => r.IsLatest)
                .ThenByDescending(r => r.PublishedAtUtc)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);

            return release != null ? MapToDto(release) : null;
        }

        public async Task<UpdateSummaryDto?> GetUpdateByIdAsync(Guid id, CancellationToken ct = default)
        {
            var release = await _dbContext.Releases
                .Include(r => r.Artifacts)
                .FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);

            return release != null ? MapToDto(release) : null;
        }

        public async Task<UpdateSummaryDto> CreateUpdateDraftAsync(CreateUpdateDto dto, Guid? adminUserId = null, CancellationToken ct = default)
        {
            var release = new Release
            {
                Component = dto.Component ?? "App",
                Version = dto.Version.Trim(),
                Title = dto.Title.Trim(),
                ReleaseNotes = dto.ReleaseNotes ?? string.Empty,
                Channel = dto.Channel ?? "stable",
                MinimumSupportedVersion = dto.MinimumSupportedVersion ?? "1.0.0",
                IsMandatory = dto.IsMandatory,
                IsDraft = true, // ALWAYS starts as DRAFT
                IsPublished = false,
                IsWebsiteDownloadEnabled = false,
                IsAutoUpdateEnabled = false,
                IsLatest = false,
                CreatedByUserId = adminUserId,
                Severity = Enum.TryParse<ReleaseSeverity>(dto.Severity, true, out var sev) ? sev : ReleaseSeverity.Standard,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _dbContext.Releases.Add(release);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            // Ensure directory exists in local workspace: Update/<Component>/<Version>/
            string relDir = $"Update/{release.Component}/{release.Version}";
            await _storageProvider.CreateDirectoryAsync(relDir, ct).ConfigureAwait(false);

            await _auditLogger.LogActionAsync(adminUserId, "Admin", "CreateUpdateDraft", "Release", release.Id.ToString(), $"Created draft update {release.Component} v{release.Version}", Guid.NewGuid().ToString("N")).ConfigureAwait(false);
            return MapToDto(release);
        }

        public async Task<UpdateSummaryDto> UpdateMetadataAsync(Guid id, UpdateMetadataDto dto, Guid? adminUserId = null, CancellationToken ct = default)
        {
            var release = await _dbContext.Releases.Include(r => r.Artifacts).FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
            if (release == null) throw new KeyNotFoundException($"Release with ID {id} not found.");

            release.Title = dto.Title.Trim();
            release.ReleaseNotes = dto.ReleaseNotes;
            release.MinimumSupportedVersion = dto.MinimumSupportedVersion;
            release.IsMandatory = dto.IsMandatory;
            if (Enum.TryParse<ReleaseSeverity>(dto.Severity, true, out var sev)) release.Severity = sev;
            release.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await _auditLogger.LogActionAsync(adminUserId, "Admin", "UpdateMetadata", "Release", id.ToString(), $"Updated metadata for {release.Component} v{release.Version}", Guid.NewGuid().ToString("N")).ConfigureAwait(false);
            return MapToDto(release);
        }

        public async Task<UpdateSummaryDto> PublishUpdateAsync(Guid id, bool setAsLatest = true, Guid? adminUserId = null, CancellationToken ct = default)
        {
            var release = await _dbContext.Releases.Include(r => r.Artifacts).FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
            if (release == null) throw new KeyNotFoundException($"Release with ID {id} not found.");

            release.IsDraft = false;
            release.IsPublished = true;
            release.IsWithdrawn = false;
            release.IsWebsiteDownloadEnabled = true;
            release.IsAutoUpdateEnabled = true;
            release.PublishedAtUtc = DateTime.UtcNow;
            release.UpdatedAtUtc = DateTime.UtcNow;

            if (setAsLatest)
            {
                // Clear latest flag for others in the same component
                var others = await _dbContext.Releases.Where(r => r.Component == release.Component && r.Id != release.Id).ToListAsync(ct).ConfigureAwait(false);
                foreach (var other in others)
                {
                    other.IsLatest = false;
                }
                release.IsLatest = true;
            }

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await _auditLogger.LogActionAsync(adminUserId, "Admin", "PublishUpdate", "Release", id.ToString(), $"Published {release.Component} v{release.Version} (Latest: {release.IsLatest})", Guid.NewGuid().ToString("N")).ConfigureAwait(false);
            return MapToDto(release);
        }

        public async Task<UpdateSummaryDto> UnpublishUpdateAsync(Guid id, Guid? adminUserId = null, CancellationToken ct = default)
        {
            var release = await _dbContext.Releases.Include(r => r.Artifacts).FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
            if (release == null) throw new KeyNotFoundException($"Release with ID {id} not found.");

            release.IsPublished = false;
            release.IsDraft = true;
            release.IsLatest = false;
            release.IsWebsiteDownloadEnabled = false;
            release.IsAutoUpdateEnabled = false;
            release.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await _auditLogger.LogActionAsync(adminUserId, "Admin", "UnpublishUpdate", "Release", id.ToString(), $"Unpublished {release.Component} v{release.Version}", Guid.NewGuid().ToString("N")).ConfigureAwait(false);
            return MapToDto(release);
        }

        public async Task<UpdateSummaryDto> ToggleWebsiteDownloadAsync(Guid id, bool enabled, Guid? adminUserId = null, CancellationToken ct = default)
        {
            var release = await _dbContext.Releases.Include(r => r.Artifacts).FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
            if (release == null) throw new KeyNotFoundException($"Release with ID {id} not found.");

            release.IsWebsiteDownloadEnabled = enabled;
            release.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await _auditLogger.LogActionAsync(adminUserId, "Admin", "ToggleWebsiteDownload", "Release", id.ToString(), $"Website download set to {enabled} for {release.Component} v{release.Version}", Guid.NewGuid().ToString("N")).ConfigureAwait(false);
            return MapToDto(release);
        }

        public async Task<UpdateSummaryDto> ToggleAutoUpdateAsync(Guid id, bool enabled, Guid? adminUserId = null, CancellationToken ct = default)
        {
            var release = await _dbContext.Releases.Include(r => r.Artifacts).FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
            if (release == null) throw new KeyNotFoundException($"Release with ID {id} not found.");

            release.IsAutoUpdateEnabled = enabled;
            release.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await _auditLogger.LogActionAsync(adminUserId, "Admin", "ToggleAutoUpdate", "Release", id.ToString(), $"Auto-update set to {enabled} for {release.Component} v{release.Version}", Guid.NewGuid().ToString("N")).ConfigureAwait(false);
            return MapToDto(release);
        }

        public async Task<UpdateSummaryDto> SetAsLatestAsync(Guid id, Guid? adminUserId = null, CancellationToken ct = default)
        {
            var release = await _dbContext.Releases.Include(r => r.Artifacts).FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
            if (release == null) throw new KeyNotFoundException($"Release with ID {id} not found.");

            var others = await _dbContext.Releases.Where(r => r.Component == release.Component && r.Id != release.Id).ToListAsync(ct).ConfigureAwait(false);
            foreach (var other in others)
            {
                other.IsLatest = false;
            }
            release.IsLatest = true;
            release.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await _auditLogger.LogActionAsync(adminUserId, "Admin", "SetAsLatest", "Release", id.ToString(), $"Marked {release.Component} v{release.Version} as Latest", Guid.NewGuid().ToString("N")).ConfigureAwait(false);
            return MapToDto(release);
        }

        public async Task<UpdateSummaryDto> ArchiveUpdateAsync(Guid id, string? reason = null, Guid? adminUserId = null, CancellationToken ct = default)
        {
            var release = await _dbContext.Releases.Include(r => r.Artifacts).FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
            if (release == null) throw new KeyNotFoundException($"Release with ID {id} not found.");

            release.IsWithdrawn = true;
            release.IsPublished = false;
            release.IsLatest = false;
            release.IsWebsiteDownloadEnabled = false;
            release.IsAutoUpdateEnabled = false;
            release.RollbackReason = reason;
            release.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await _auditLogger.LogActionAsync(adminUserId, "Admin", "ArchiveUpdate", "Release", id.ToString(), $"Archived {release.Component} v{release.Version}. Reason: {reason}", Guid.NewGuid().ToString("N")).ConfigureAwait(false);
            return MapToDto(release);
        }

        public async Task<bool> DeleteUpdateAsync(Guid id, Guid? adminUserId = null, CancellationToken ct = default)
        {
            var release = await _dbContext.Releases.Include(r => r.Artifacts).FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
            if (release == null) return false;

            _dbContext.Releases.Remove(release);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await _auditLogger.LogActionAsync(adminUserId, "Admin", "DeleteUpdate", "Release", id.ToString(), $"Deleted {release.Component} v{release.Version}", Guid.NewGuid().ToString("N")).ConfigureAwait(false);
            return true;
        }

        public async Task<ArtifactSummaryDto> UploadOrReplaceArtifactAsync(Guid releaseId, string fileName, Stream contentStream, string architecture = "x64", Guid? adminUserId = null, CancellationToken ct = default)
        {
            var release = await _dbContext.Releases.Include(r => r.Artifacts).FirstOrDefaultAsync(r => r.Id == releaseId, ct).ConfigureAwait(false);
            if (release == null) throw new KeyNotFoundException($"Release with ID {releaseId} not found.");

            string relPath = $"Update/{release.Component}/{release.Version}/{fileName}";
            
            // Backup previous version if it exists
            if (await _storageProvider.ExistsAsync(relPath, ct).ConfigureAwait(false))
            {
                await _storageProvider.CreateBackupRevisionAsync(relPath, ct).ConfigureAwait(false);
            }

            // Save new file
            await _storageProvider.SaveFileAsync(relPath, contentStream, overwrite: true, ct).ConfigureAwait(false);
            var fileInfo = await _storageProvider.GetFileInfoAsync(relPath, ct).ConfigureAwait(false);

            // Update database artifact record
            var artifact = release.Artifacts.FirstOrDefault(a => a.ArtifactName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            if (artifact == null)
            {
                artifact = new ReleaseArtifact
                {
                    ReleaseId = release.Id,
                    ArtifactName = fileName,
                    Architecture = architecture,
                    FileSizeBytes = fileInfo?.SizeBytes ?? 0,
                    Sha256Hash = fileInfo?.Sha256Hash ?? string.Empty,
                    DownloadUrl = relPath
                };
                release.Artifacts.Add(artifact);
            }
            else
            {
                artifact.FileSizeBytes = fileInfo?.SizeBytes ?? 0;
                artifact.Sha256Hash = fileInfo?.Sha256Hash ?? string.Empty;
                artifact.DownloadUrl = relPath;
            }

            release.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await _auditLogger.LogActionAsync(adminUserId, "Admin", "UploadArtifact", "ReleaseArtifact", artifact.Id.ToString(), $"Uploaded/Replaced artifact {fileName} for {release.Component} v{release.Version}", Guid.NewGuid().ToString("N")).ConfigureAwait(false);

            return new ArtifactSummaryDto(
                Id: artifact.Id,
                FileName: artifact.ArtifactName,
                RelativePath: artifact.DownloadUrl ?? string.Empty,
                Architecture: artifact.Architecture,
                FileSizeBytes: artifact.FileSizeBytes,
                Sha256Hash: artifact.Sha256Hash,
                DownloadUrl: artifact.DownloadUrl ?? string.Empty
            );
        }

        public async Task<int> ScanLocalUpdateWorkspaceAsync(CancellationToken ct = default)
        {
            int discoveredCount = 0;
            string[] components = new[] { "App", "Extension", "NativeHost", "Other" };

            foreach (var comp in components)
            {
                string compDir = $"Update/{comp}";
                var files = await _storageProvider.ListFilesAsync(compDir, "*", recursive: true, ct).ConfigureAwait(false);

                // Group by version folder: Update/<Component>/<Version>/<File>
                var versionGroups = files
                    .Where(f => !f.IsDirectory && !f.RelativePath.Contains("/.revisions/"))
                    .GroupBy(f => {
                        var parts = f.RelativePath.Split('/');
                        return parts.Length >= 3 ? parts[2] : "1.0.0";
                    });

                foreach (var group in versionGroups)
                {
                    string version = group.Key;
                    var existingRelease = await _dbContext.Releases
                        .Include(r => r.Artifacts)
                        .FirstOrDefaultAsync(r => (r.Component == null || r.Component.ToLower() == comp.ToLower()) && r.Version == version, ct).ConfigureAwait(false);

                    if (existingRelease == null)
                    {
                        // Create DRAFT release for scanned version
                        existingRelease = new Release
                        {
                            Component = comp,
                            Version = version,
                            Title = $"EDM {comp} v{version}",
                            ReleaseNotes = $"Scanned local update package for {comp} v{version}",
                            Channel = "stable",
                            MinimumSupportedVersion = "1.0.0",
                            IsMandatory = false,
                            IsDraft = true, // NEVER AUTO-PUBLISH
                            IsPublished = false,
                            IsWebsiteDownloadEnabled = false,
                            IsAutoUpdateEnabled = false,
                            IsLatest = false,
                            CreatedAtUtc = DateTime.UtcNow,
                            UpdatedAtUtc = DateTime.UtcNow
                        };
                        _dbContext.Releases.Add(existingRelease);
                        discoveredCount++;
                    }

                    // Attach/update artifacts
                    foreach (var f in group)
                    {
                        var art = existingRelease.Artifacts.FirstOrDefault(a => a.ArtifactName.Equals(f.Name, StringComparison.OrdinalIgnoreCase));
                        string hash = string.IsNullOrWhiteSpace(f.Sha256Hash) ? await _storageProvider.CalculateSha256Async(f.RelativePath, ct).ConfigureAwait(false) : f.Sha256Hash;
                        if (art == null)
                        {
                            existingRelease.Artifacts.Add(new ReleaseArtifact
                            {
                                ReleaseId = existingRelease.Id,
                                ArtifactName = f.Name,
                                Architecture = "x64",
                                FileSizeBytes = f.SizeBytes,
                                Sha256Hash = hash,
                                DownloadUrl = f.RelativePath
                            });
                        }
                        else
                        {
                            art.FileSizeBytes = f.SizeBytes;
                            art.Sha256Hash = hash;
                            art.DownloadUrl = f.RelativePath;
                        }
                    }
                }
            }

            if (discoveredCount > 0)
            {
                await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                _logger.LogInformation("Scanned local update workspace and registered {Count} new draft releases.", discoveredCount);
            }

            return discoveredCount;
        }
    }
}
