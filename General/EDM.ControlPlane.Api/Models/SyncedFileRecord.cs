using System;

namespace EDM.ControlPlane.Api.Models
{
    public enum FileSyncState
    {
        Synced,
        Uploading,
        Downloading,
        Syncing,
        ModifiedLocally,
        ModifiedRemotely,
        Conflict,
        Offline,
        Error
    }

    public class SyncedFileRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid OwnerId { get; set; }
        public Guid? DeviceId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public long FileSizeBytes { get; set; }
        public string Sha256Hash { get; set; } = string.Empty;
        public int Version { get; set; } = 1;
        public FileSyncState SyncState { get; set; } = FileSyncState.Synced;
        public string? ConflictResolution { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? DeletedAtUtc { get; set; }
        public bool IsDeleted { get; set; } = false;

        // Navigation
        public User? Owner { get; set; }
        public Device? Device { get; set; }
    }
}
