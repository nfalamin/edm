using System;

namespace EDM.ControlPlane.Api.Models
{
    public class ExtensionRelease
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ClientType Browser { get; set; } = ClientType.ChromeExtension;
        public string ExtensionVersion { get; set; } = "1.0.0";
        public string MinBrowserVersion { get; set; } = "88.0";
        public int ManifestVersion { get; set; } = 3;
        public string StoreUrl { get; set; } = string.Empty;
        public string DirectZipUrl { get; set; } = string.Empty;
        public string Sha256Hash { get; set; } = string.Empty;
        public bool IsMandatory { get; set; } = false;
        public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
