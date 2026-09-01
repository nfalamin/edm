using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Services
{
    public record ContentDocumentDto(
        Guid Id,
        string DocType,
        string Slug,
        string Title,
        string RelativeFilePath,
        string MarkdownContent,
        string Summary,
        bool IsPublished,
        bool IsDraft,
        int Version,
        string LastEditor,
        string Sha256Hash,
        long FileSizeBytes,
        DateTime LastModifiedUtc,
        DateTime? PublishedAtUtc,
        DateTime CreatedAtUtc,
        List<DocumentRevisionDto> Revisions);

    public record SaveDocumentDraftDto(
        string Title,
        string MarkdownContent,
        string? Summary = null);

    public interface IContentManagerService
    {
        Task<IEnumerable<ContentDocumentDto>> GetAllDocumentsAsync(bool includeDrafts = true, CancellationToken ct = default);
        Task<ContentDocumentDto?> GetDocumentBySlugAsync(string slug, bool publishedOnly = true, CancellationToken ct = default);
        Task<ContentDocumentDto?> GetDocumentByIdAsync(Guid id, CancellationToken ct = default);
        Task<ContentDocumentDto> SaveDraftAsync(Guid id, SaveDocumentDraftDto dto, string editorName = "Admin", Guid? adminUserId = null, CancellationToken ct = default);
        Task<ContentDocumentDto> PublishDocumentAsync(Guid id, string editorName = "Admin", Guid? adminUserId = null, CancellationToken ct = default);
        Task<ContentDocumentDto> UnpublishDocumentAsync(Guid id, string editorName = "Admin", Guid? adminUserId = null, CancellationToken ct = default);
        Task<ContentDocumentDto> ReplaceFileAsync(Guid id, Stream fileStream, string fileName, string editorName = "Admin", Guid? adminUserId = null, CancellationToken ct = default);
        Task<IEnumerable<DocumentRevisionDto>> GetRevisionHistoryAsync(Guid id, CancellationToken ct = default);
        Task<ContentDocumentDto> RestoreRevisionAsync(Guid id, int version, string editorName = "Admin", Guid? adminUserId = null, CancellationToken ct = default);
        Task<int> ScanLocalContentWorkspaceAsync(CancellationToken ct = default);
    }
}
