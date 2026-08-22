using System;
using System.Collections.Generic;

namespace EDM.ControlPlane.Api.Models
{
    public enum ClientType
    {
        DesktopWindows,
        ChromeExtension,
        EdgeExtension,
        FirefoxExtension
    }

    public class Device
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// Privacy-Safe Installation ID: Cryptographically random GUID generated on first install.
        /// No raw MAC address or invasive hardware fingerprinting is ever stored.
        /// </summary>
        public Guid InstallationId { get; set; } = Guid.NewGuid();
        
        public ClientType ClientType { get; set; } = ClientType.DesktopWindows;
        public string OsVersion { get; set; } = string.Empty; // e.g. "Windows 11 x64"
        public string AppVersion { get; set; } = string.Empty; // e.g. "2.0.0"
        public string? CoarseCountryCode { get; set; } // Server-derived coarse geo (e.g. "US", "BD")
        public string? StorageJson { get; set; }
        public Guid? UserId { get; set; }
        public bool IsBanned { get; set; } = false;
        public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
        public ICollection<Session> Sessions { get; set; } = new List<Session>();
        public ICollection<TelemetryEvent> TelemetryEvents { get; set; } = new List<TelemetryEvent>();
    }
}
