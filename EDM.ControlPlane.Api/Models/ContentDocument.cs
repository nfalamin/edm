using System;
using System.Collections.Generic;

namespace EDM.ControlPlane.Api.Models
{
    public record DocumentRevisionDto(
        int Version,
        string Title,
        string MarkdownContent,
        string SavedBy,
        DateTime SavedAtUtc,
        string BackupFilePath,
        bool WasPublished);

    public class ContentDocument
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DocType { get; set; } = string.Empty; // "About", "Privacy", "Terms", "FAQ", "Help", "Documentation", "ReleaseNotes", "Announcements"
        public string Slug { get; set; } = string.Empty; // "about", "privacy", "terms", "faq", "help", "documentation", "release-notes", "announcements"
        public string Title { get; set; } = string.Empty;
        public string RelativeFilePath { get; set; } = string.Empty; // e.g. "Content/Privacy/privacy-policy.md"
        public string MarkdownContent { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public bool IsPublished { get; set; } = false;
        public bool IsDraft { get; set; } = true;
        public int Version { get; set; } = 1;
        public string LastEditor { get; set; } = "Admin";
        public string Sha256Hash { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; } = 0;
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string RevisionsJson { get; set; } = "[]";
    }
}
