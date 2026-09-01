using System;

namespace EDM.ControlPlane.Api.Models
{
    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy
    }

    public class SystemHealthSnapshot
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ComponentName { get; set; } = string.Empty; // e.g. "ControlPlaneApi", "Database", "StorageEngine", "LicenseValidator"
        public HealthStatus Status { get; set; } = HealthStatus.Healthy;
        public long LatencyMs { get; set; } = 0;
        public string DetailsJson { get; set; } = "{}";
        public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class SystemMetric
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string MetricName { get; set; } = string.Empty; // e.g. "active_sessions", "downloads_24h", "api_latency_ms", "db_size_bytes"
        public double MetricValue { get; set; } = 0.0;
        public string DimensionsJson { get; set; } = "{}";
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
