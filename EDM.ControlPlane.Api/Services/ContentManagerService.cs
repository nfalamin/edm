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
    public class ContentManagerService : IContentManagerService
    {
        private readonly ControlPlaneDbContext _dbContext;
        private readonly IStorageProvider _storageProvider;
        private readonly IAuditLoggingService _auditLogger;
        private readonly ILogger<ContentManagerService> _logger;

        private static readonly Dictionary<string, (string DocType, string DefaultTitle, string DefaultPath)> StandardDocumentMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["about"] = ("About", "About Exclusive Download Manager", "Content/About/about.md"),
            ["privacy-policy"] = ("Privacy", "Privacy Policy", "Content/Privacy/privacy-policy.md"),
            ["privacy"] = ("Privacy", "Privacy Policy", "Content/Privacy/privacy-policy.md"),
            ["terms-and-conditions"] = ("Terms", "Terms & Conditions", "Content/Terms/terms-and-conditions.md"),
            ["terms"] = ("Terms", "Terms & Conditions", "Content/Terms/terms-and-conditions.md"),
            ["faq"] = ("FAQ", "Frequently Asked Questions", "Content/FAQ/faq.md"),
            ["help"] = ("Help", "Help & Support Guide", "Content/Help/help.md"),
            ["documentation"] = ("Documentation", "EDM Architecture & API Documentation", "Content/Documentation/documentation.md"),
            ["release-notes"] = ("ReleaseNotes", "Production Release Notes & Changelog", "Content/ReleaseNotes/release-notes.md"),
            ["announcements"] = ("Announcements", "Product Announcements & Security Advisories", "Content/Announcements/announcements.md")
        };

        public ContentManagerService(
            ControlPlaneDbContext dbContext,
            IStorageProvider storageProvider,
            IAuditLoggingService auditLogger,
            ILogger<ContentManagerService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private static ContentDocumentDto MapToDto(ContentDocument doc)
        {
            List<DocumentRevisionDto> revisions = new();
            try
            {
                if (!string.IsNullOrWhiteSpace(doc.RevisionsJson))
                {
                    revisions = JsonSerializer.Deserialize<List<DocumentRevisionDto>>(doc.RevisionsJson) ?? new();
                }
            }
            catch
            {
                revisions = new();
            }

            return new ContentDocumentDto(
                Id: doc.Id,
                DocType: doc.DocType,
                Slug: doc.Slug,
                Title: doc.Title,
                RelativeFilePath: doc.RelativeFilePath,
                MarkdownContent: doc.MarkdownContent,
                Summary: doc.Summary,
                IsPublished: doc.IsPublished,
                IsDraft: doc.IsDraft,
                Version: doc.Version,
                LastEditor: doc.LastEditor,
                Sha256Hash: doc.Sha256Hash,
                FileSizeBytes: doc.FileSizeBytes,
                LastModifiedUtc: doc.LastModifiedUtc,
                PublishedAtUtc: doc.PublishedAtUtc,
                CreatedAtUtc: doc.CreatedAtUtc,
                Revisions: revisions
            );
        }

        public async Task<IEnumerable<ContentDocumentDto>> GetAllDocumentsAsync(bool includeDrafts = true, CancellationToken ct = default)
        {
            var query = _dbContext.ContentDocuments.AsQueryable();
            if (!includeDrafts)
            {
                query = query.Where(d => d.IsPublished);
            }

            var list = await query.OrderBy(d => d.DocType).ToListAsync(ct).ConfigureAwait(false);
            return list.Select(MapToDto);
        }

        public async Task<ContentDocumentDto?> GetDocumentBySlugAsync(string slug, bool publishedOnly = true, CancellationToken ct = default)
        {
            var query = _dbContext.ContentDocuments.AsQueryable();
            string cleanSlug = slug.Trim().ToLowerInvariant();

            query = query.Where(d => d.Slug.ToLower() == cleanSlug || d.DocType.ToLower() == cleanSlug);
            if (publishedOnly)
            {
                query = query.Where(d => d.IsPublished);
            }

            var doc = await query.FirstOrDefaultAsync(ct).ConfigureAwait(false);
            return doc != null ? MapToDto(doc) : null;
        }

        public async Task<ContentDocumentDto?> GetDocumentByIdAsync(Guid id, CancellationToken ct = default)
        {
            var doc = await _dbContext.ContentDocuments.FirstOrDefaultAsync(d => d.Id == id, ct).ConfigureAwait(false);
            return doc != null ? MapToDto(doc) : null;
        }

        public async Task<ContentDocumentDto> SaveDraftAsync(Guid id, SaveDocumentDraftDto dto, string editorName = "Admin", Guid? adminUserId = null, CancellationToken ct = default)
        {
            var doc = await _dbContext.ContentDocuments.FirstOrDefaultAsync(d => d.Id == id, ct).ConfigureAwait(false);
            if (doc == null) throw new KeyNotFoundException($"Content document with ID {id} not found.");

            // Create backup of current state
            string backupPath = string.Empty;
            if (!string.IsNullOrWhiteSpace(doc.RelativeFilePath) && await _storageProvider.ExistsAsync(doc.RelativeFilePath, ct).ConfigureAwait(false))
            {
                backupPath = await _storageProvider.CreateBackupRevisionAsync(doc.RelativeFilePath, ct).ConfigureAwait(false);
            }

            // Append to revision history
            var revisions = new List<DocumentRevisionDto>();
            try
            {
                if (!string.IsNullOrWhiteSpace(doc.RevisionsJson))
                {
                    revisions = JsonSerializer.Deserialize<List<DocumentRevisionDto>>(doc.RevisionsJson) ?? new();
                }
            }
            catch { }

            revisions.Add(new DocumentRevisionDto(
                Version: doc.Version,
                Title: doc.Title,
                MarkdownContent: doc.MarkdownContent,
                SavedBy: doc.LastEditor,
                SavedAtUtc: doc.LastModifiedUtc,
                BackupFilePath: backupPath,
                WasPublished: doc.IsPublished
            ));

            doc.Title = dto.Title.Trim();
            doc.MarkdownContent = dto.MarkdownContent;
            doc.Summary = dto.Summary ?? string.Empty;
            doc.IsDraft = true; // Changes remain in draft until published
            doc.Version++;
            doc.LastEditor = editorName;
            doc.LastModifiedUtc = DateTime.UtcNow;
            doc.RevisionsJson = JsonSerializer.Serialize(revisions);

            // Write updated text to storage file
            if (!string.IsNullOrWhiteSpace(doc.RelativeFilePath))
            {
                await _storageProvider.WriteAllTextAsync(doc.RelativeFilePath, dto.MarkdownContent, ct).ConfigureAwait(false);
                var fi = await _storageProvider.GetFileInfoAsync(doc.RelativeFilePath, ct).ConfigureAwait(false);
                doc.FileSizeBytes = fi?.SizeBytes ?? 0;
                doc.Sha256Hash = fi?.Sha256Hash ?? string.Empty;
            }

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await _auditLogger.LogActionAsync(adminUserId, editorName, "SaveDocumentDraft", "ContentDocument", id.ToString(), $"Saved draft revision v{doc.Version} for {doc.DocType} ({doc.Title})", Guid.NewGuid().ToString("N")).ConfigureAwait(false);

            return MapToDto(doc);
        }

        public async Task<ContentDocumentDto> PublishDocumentAsync(Guid id, string editorName = "Admin", Guid? adminUserId = null, CancellationToken ct = default)
        {
            var doc = await _dbContext.ContentDocuments.FirstOrDefaultAsync(d => d.Id == id, ct).ConfigureAwait(false);
            if (doc == null) throw new KeyNotFoundException($"Content document with ID {id} not found.");

            doc.IsPublished = true;
            doc.IsDraft = false;
            doc.PublishedAtUtc = DateTime.UtcNow;
            doc.LastModifiedUtc = DateTime.UtcNow;
            doc.LastEditor = editorName;

            // Ensure storage file matches
            if (!string.IsNullOrWhiteSpace(doc.RelativeFilePath))
            {
                await _storageProvider.WriteAllTextAsync(doc.RelativeFilePath, doc.MarkdownContent, ct).ConfigureAwait(false);
            }

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await _auditLogger.LogActionAsync(adminUserId, editorName, "PublishDocument", "ContentDocument", id.ToString(), $"Published {doc.DocType} ({doc.Title}) v{doc.Version}", Guid.NewGuid().ToString("N")).ConfigureAwait(false);

            return MapToDto(doc);
        }

        public async Task<ContentDocumentDto> UnpublishDocumentAsync(Guid id, string editorName = "Admin", Guid? adminUserId = null, CancellationToken ct = default)
        {
            var doc = await _dbContext.ContentDocuments.FirstOrDefaultAsync(d => d.Id == id, ct).ConfigureAwait(false);
            if (doc == null) throw new KeyNotFoundException($"Content document with ID {id} not found.");

            doc.IsPublished = false;
            doc.IsDraft = true;
            doc.LastModifiedUtc = DateTime.UtcNow;
            doc.LastEditor = editorName;

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await _auditLogger.LogActionAsync(adminUserId, editorName, "UnpublishDocument", "ContentDocument", id.ToString(), $"Unpublished {doc.DocType} ({doc.Title})", Guid.NewGuid().ToString("N")).ConfigureAwait(false);

            return MapToDto(doc);
        }

        public async Task<ContentDocumentDto> ReplaceFileAsync(Guid id, Stream fileStream, string fileName, string editorName = "Admin", Guid? adminUserId = null, CancellationToken ct = default)
        {
            var doc = await _dbContext.ContentDocuments.FirstOrDefaultAsync(d => d.Id == id, ct).ConfigureAwait(false);
            if (doc == null) throw new KeyNotFoundException($"Content document with ID {id} not found.");

            using var reader = new StreamReader(fileStream);
            string newContent = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

            return await SaveDraftAsync(id, new SaveDocumentDraftDto(doc.Title, newContent, doc.Summary), editorName, adminUserId, ct).ConfigureAwait(false);
        }

        public async Task<IEnumerable<DocumentRevisionDto>> GetRevisionHistoryAsync(Guid id, CancellationToken ct = default)
        {
            var doc = await _dbContext.ContentDocuments.FirstOrDefaultAsync(d => d.Id == id, ct).ConfigureAwait(false);
            if (doc == null) return Array.Empty<DocumentRevisionDto>();

            try
            {
                if (!string.IsNullOrWhiteSpace(doc.RevisionsJson))
                {
                    return JsonSerializer.Deserialize<List<DocumentRevisionDto>>(doc.RevisionsJson) ?? new();
                }
            }
            catch { }

            return Array.Empty<DocumentRevisionDto>();
        }

        public async Task<ContentDocumentDto> RestoreRevisionAsync(Guid id, int version, string editorName = "Admin", Guid? adminUserId = null, CancellationToken ct = default)
        {
            var doc = await _dbContext.ContentDocuments.FirstOrDefaultAsync(d => d.Id == id, ct).ConfigureAwait(false);
            if (doc == null) throw new KeyNotFoundException($"Content document with ID {id} not found.");

            var revisions = await GetRevisionHistoryAsync(id, ct).ConfigureAwait(false);
            var target = revisions.FirstOrDefault(r => r.Version == version);
            if (target == null) throw new InvalidOperationException($"Revision version {version} not found for document {doc.DocType}.");

            return await SaveDraftAsync(id, new SaveDocumentDraftDto(target.Title, target.MarkdownContent, doc.Summary), editorName, adminUserId, ct).ConfigureAwait(false);
        }

        public async Task<int> ScanLocalContentWorkspaceAsync(CancellationToken ct = default)
        {
            int discoveredCount = 0;
            string contentDir = "Content";

            var files = await _storageProvider.ListFilesAsync(contentDir, "*.md", recursive: true, ct).ConfigureAwait(false);

            foreach (var f in files)
            {
                if (f.IsDirectory || f.RelativePath.Contains("/.revisions/")) continue;

                // Extract folder name as doc type: Content/<DocType>/<file.md>
                var parts = f.RelativePath.Split('/');
                string folder = parts.Length >= 2 ? parts[1] : "General";
                string slug = folder.ToLowerInvariant();

                var existing = await _dbContext.ContentDocuments.FirstOrDefaultAsync(d => d.RelativeFilePath.ToLower() == f.RelativePath.ToLower() || d.Slug.ToLower() == slug, ct).ConfigureAwait(false);
                string text = await _storageProvider.ReadAllTextAsync(f.RelativePath, ct).ConfigureAwait(false);
                string hash = string.IsNullOrWhiteSpace(f.Sha256Hash) ? await _storageProvider.CalculateSha256Async(f.RelativePath, ct).ConfigureAwait(false) : f.Sha256Hash;

                if (existing == null)
                {
                    string title = folder;
                    if (StandardDocumentMap.TryGetValue(slug, out var std))
                    {
                        title = std.DefaultTitle;
                    }

                    existing = new ContentDocument
                    {
                        DocType = folder,
                        Slug = slug,
                        Title = title,
                        RelativeFilePath = f.RelativePath,
                        MarkdownContent = text,
                        IsPublished = false, // SCANNED FILES ALWAYS START AS DRAFT
                        IsDraft = true,
                        Version = 1,
                        LastEditor = "Local Scanner",
                        Sha256Hash = hash,
                        FileSizeBytes = f.SizeBytes,
                        LastModifiedUtc = f.LastModifiedUtc,
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    _dbContext.ContentDocuments.Add(existing);
                    discoveredCount++;
                }
                else
                {
                    // Update metadata if file on disk was modified
                    if (existing.Sha256Hash != hash)
                    {
                        existing.MarkdownContent = text;
                        existing.Sha256Hash = hash;
                        existing.FileSizeBytes = f.SizeBytes;
                        existing.LastModifiedUtc = f.LastModifiedUtc;
                        existing.IsDraft = true; // Mark as draft upon external file change
                    }
                }
            }

            if (discoveredCount > 0)
            {
                await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                _logger.LogInformation("Scanned local content workspace and registered {Count} new documents.", discoveredCount);
            }

            return discoveredCount;
        }
    }
}
