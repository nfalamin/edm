using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Services
{
    public record UpdateSummaryDto(
        Guid Id,
        string Component,
        string Version,
        string Title,
        string Channel,
        string Severity,
        string MinimumSupportedVersion,
        bool IsMandatory,
        bool IsDraft,
        bool IsPublished,
        bool IsWithdrawn,
        bool IsWebsiteDownloadEnabled,
        bool IsAutoUpdateEnabled,
        bool IsLatest,
        string Status,
        DateTime CreatedAtUtc,
        DateTime? PublishedAtUtc,
        DateTime UpdatedAtUtc,
        List<ArtifactSummaryDto> Artifacts);

    public record ArtifactSummaryDto(
        Guid Id,
        string FileName,
        string RelativePath,
        string Architecture,
        long FileSizeBytes,
        string Sha256Hash,
        string DownloadUrl);

    public record CreateUpdateDto(
        string Component,
        string Version,
        string Title,
        string ReleaseNotes,
        string Severity,
        string MinimumSupportedVersion,
        bool IsMandatory,
        string Channel = "stable");

    public record UpdateMetadataDto(
        string Title,
        string ReleaseNotes,
        string Severity,
        string MinimumSupportedVersion,
        bool IsMandatory);

    public interface IUpdateManagerService
    {
        Task<IEnumerable<UpdateSummaryDto>> GetAllUpdatesAsync(string? component = null, bool includeDrafts = true, CancellationToken ct = default);
        Task<UpdateSummaryDto?> GetPublishedLatestAsync(string component = "App", CancellationToken ct = default);
        Task<UpdateSummaryDto?> GetUpdateByIdAsync(Guid id, CancellationToken ct = default);
        Task<UpdateSummaryDto> CreateUpdateDraftAsync(CreateUpdateDto dto, Guid? adminUserId = null, CancellationToken ct = default);
        Task<UpdateSummaryDto> UpdateMetadataAsync(Guid id, UpdateMetadataDto dto, Guid? adminUserId = null, CancellationToken ct = default);
        Task<UpdateSummaryDto> PublishUpdateAsync(Guid id, bool setAsLatest = true, Guid? adminUserId = null, CancellationToken ct = default);
        Task<UpdateSummaryDto> UnpublishUpdateAsync(Guid id, Guid? adminUserId = null, CancellationToken ct = default);
        Task<UpdateSummaryDto> ToggleWebsiteDownloadAsync(Guid id, bool enabled, Guid? adminUserId = null, CancellationToken ct = default);
        Task<UpdateSummaryDto> ToggleAutoUpdateAsync(Guid id, bool enabled, Guid? adminUserId = null, CancellationToken ct = default);
        Task<UpdateSummaryDto> SetAsLatestAsync(Guid id, Guid? adminUserId = null, CancellationToken ct = default);
        Task<UpdateSummaryDto> ArchiveUpdateAsync(Guid id, string? reason = null, Guid? adminUserId = null, CancellationToken ct = default);
        Task<bool> DeleteUpdateAsync(Guid id, Guid? adminUserId = null, CancellationToken ct = default);
        Task<ArtifactSummaryDto> UploadOrReplaceArtifactAsync(Guid releaseId, string fileName, Stream contentStream, string architecture = "x64", Guid? adminUserId = null, CancellationToken ct = default);
        Task<int> ScanLocalUpdateWorkspaceAsync(CancellationToken ct = default);
    }
}
