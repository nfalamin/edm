using System;
using System.Collections.Generic;

namespace EDM.Services
{
    /// <summary>
    /// Authoritative progress snapshot representing a physical download's state at a point in time.
    /// Thread-safe, 64-bit byte accurate, and merge-aware.
    /// </summary>
    public class DownloadProgressInfo
    {
        public string? DownloadIdentity { get; set; }
        public string? State { get; set; }
        public string? FileName { get; set; }
        public string? SelectedQuality { get; set; }

        public long BytesReceived { get; set; }
        public long? TotalBytes { get; set; }
        public bool HasKnownTotal => TotalBytes.HasValue && TotalBytes.Value > 0;

        public double ProgressPercentage { get; set; }
        public double SmoothedProgressPercentage { get; set; }

        public double SpeedBytesPerSecond { get; set; }
        public double AverageSpeedBytesPerSecond { get; set; }
        public double PeakSpeedBytesPerSecond { get; set; }

        public double RemainingSeconds { get; set; }
        public TimeSpan? Elapsed { get; set; }
        public string? Eta => RemainingSeconds > 0 && !double.IsInfinity(RemainingSeconds)
            ? TimeSpan.FromSeconds(RemainingSeconds).ToString(@"mm\:ss")
            : "Calculating...";

        public bool IsAdaptive { get; set; }
        public long VideoDownloadedBytes { get; set; }
        public long VideoTotalBytes { get; set; }
        public long AudioDownloadedBytes { get; set; }
        public long AudioTotalBytes { get; set; }

        public string? Status { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsCompleted { get; set; }
        public bool ServerSupportsResume { get; set; }

        public int ActiveConnections { get; set; }
        public int PeersCount { get; set; }
        public int SeedsCount { get; set; }
        public double UploadSpeedBytesPerSecond { get; set; }
        public int SegmentCount { get; set; }
        public long[] SegmentBytes { get; set; } = Array.Empty<long>();
        public IReadOnlyDictionary<int, ChunkProgressInfo>? ChunkStats { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Backwards-compatible alias
        public long BytesDownloaded
        {
            get => BytesReceived;
            set => BytesReceived = value;
        }
    }
}
