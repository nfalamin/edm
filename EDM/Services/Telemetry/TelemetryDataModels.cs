using System;
using System.Collections.Generic;

namespace EDM.Services.Telemetry
{
    public class TelemetryBatchEnvelope
    {
        public string SchemaVersion { get; set; } = "1.0.0";
        public TelemetryClientHeader ClientHeader { get; set; } = new();
        public List<TelemetryEvent> Events { get; set; } = new();
        public SystemSnapshotPayload? SystemSnapshot { get; set; }
    }

    public class TelemetryClientHeader
    {
        public string AnonymousInstallId { get; set; } = Guid.NewGuid().ToString("D");
        public string AppVersion { get; set; } = "6.0.0";
        public string BuildNumber { get; set; } = "20260816.1";
        public string Channel { get; set; } = "Stable";
        public string OsVersion { get; set; } = Environment.OSVersion.VersionString;
        public string Architecture { get; set; } = Environment.Is64BitOperatingSystem ? "X64" : "X86";
        public string DotnetRuntime { get; set; } = Environment.Version.ToString();
        public string UiCulture { get; set; } = "en-US";
        public bool IsProTier { get; set; } = true;
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }

    public class TelemetryEvent
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString("N")[..12];
        public string Type { get; set; } = "HEARTBEAT"; // "DOWNLOAD_COMPLETED", "DOWNLOAD_FAILED", "FAULT_DIAGNOSTIC", "HEARTBEAT"
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public DownloadMetricsPayload? DownloadMetrics { get; set; }
        public FaultDiagnosticPayload? Fault { get; set; }
        public Dictionary<string, object>? CustomProperties { get; set; }
    }

    public class DownloadMetricsPayload
    {
        public string Protocol { get; set; } = "HTTP_MULTIPART";
        public string FileExtension { get; set; } = ".bin";
        public long TotalBytes { get; set; }
        public double DurationSeconds { get; set; }
        public double AverageSpeedMbps { get; set; }
        public double PeakSpeedMbps { get; set; }
        public int ActiveSegments { get; set; } = 8;
        public int RetryCount { get; set; }
        public string? CdnDetected { get; set; }
        public double DiskWriteThroughputMbps { get; set; }
    }

    public class FaultDiagnosticPayload
    {
        public string Category { get; set; } = "GENERAL_FAULT";
        public string ErrorCode { get; set; } = "UNKNOWN";
        public string DomainHost { get; set; } = string.Empty;
        public string SanitizedStackTrace { get; set; } = string.Empty;
        public string? RecoveryActionTaken { get; set; }
    }

    public class SystemSnapshotPayload
    {
        public int LogicalCpuCores { get; set; } = Environment.ProcessorCount;
        public long WorkingSetMemoryMb { get; set; } = Environment.WorkingSet / (1024 * 1024);
        public int ActiveDownloadsCount { get; set; }
        public int CompletedDownloadsCount { get; set; }
        public long SqliteDbSizeBytes { get; set; }
    }
}
