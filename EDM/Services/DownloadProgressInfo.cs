namespace EDM.Services
{
    public class DownloadProgressInfo
    {
        public long BytesReceived { get; set; }
        // Per-segment bytes downloaded; length equals SegmentCount when segmented download
        public long[] SegmentBytes { get; set; } = System.Array.Empty<long>();
        // Number of segments used for segmented download
        public int SegmentCount { get; set; }
        public long? TotalBytes { get; set; }
        public double ProgressPercentage { get; set; }
        public double SpeedBytesPerSecond { get; set; }
        // Smoothed average speed (bytes/sec) computed by smoothing service
        public double AverageSpeedBytesPerSecond { get; set; }
        // Peak observed speed (bytes/sec)
        public double PeakSpeedBytesPerSecond { get; set; }
        // Smoothed progress percentage (for smooth UI animation). If zero, UI may use ProgressPercentage.
        public double SmoothedProgressPercentage { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsCompleted { get; set; }

        // Additional status fields expected by DownloadService
        public string? Status { get; set; }
        public bool ServerSupportsResume { get; set; }
        public double RemainingSeconds { get; set; }
        public int ActiveConnections { get; set; }
        public System.Collections.Generic.IReadOnlyDictionary<int, ChunkProgressInfo>? ChunkStats { get; set; }
        // Backwards-compatible alias used in some code paths
        public long BytesDownloaded
        {
            get => BytesReceived;
            set => BytesReceived = value;
        }
    }
}
