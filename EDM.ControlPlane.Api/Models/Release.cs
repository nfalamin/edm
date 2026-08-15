using System;
using System.Collections.Generic;

namespace EDM.ControlPlane.Api.Models
{
    public enum ReleaseSeverity
    {
        Standard,
        Recommended,
        Critical,
        SecurityHotfix
    }

    public class Release
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ClientType Platform { get; set; } = ClientType.DesktopWindows;
        public string Version { get; set; } = string.Empty; // e.g. "2.0.0"
        public string MinimumSupportedVersion { get; set; } = "1.0.0";
        public string Title { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public bool IsMandatory { get; set; } = false;
        public bool IsWithdrawn { get; set; } = false;
        public ReleaseSeverity Severity { get; set; } = ReleaseSeverity.Standard;
        public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<ReleaseArtifact> Artifacts { get; set; } = new List<ReleaseArtifact>();
    }
}
