using System;
using System.Collections.Concurrent;

namespace EDM.Services
{
    public class DownloadDiagnosticsMetrics
    {
        public string DownloadId { get; set; } = string.Empty;
        public long TotalBytes { get; set; }
        public long CompletedBytes { get; set; }
        public double InstantaneousSpeedBps { get; set; }
        public double AverageSpeedBps { get; set; }
        public int ActiveConnections { get; set; }
        public int TotalRetries { get; set; }
        public double LatencyMs { get; set; }
        public int ErrorCount { get; set; }
        public TimeSpan RemainingEta { get; set; }
    }

    public class DownloadDiagnosticsTracker
    {
        private readonly ConcurrentDictionary<string, DownloadDiagnosticsMetrics> _metrics = new(StringComparer.OrdinalIgnoreCase);

        public void RecordMetrics(string downloadId, long totalBytes, long completedBytes, double instSpeed, double avgSpeed, int activeConns, int retries, double latencyMs, int errors)
        {
            if (string.IsNullOrWhiteSpace(downloadId)) return;

            long remainingBytes = Math.Max(0, totalBytes - completedBytes);
            TimeSpan eta = instSpeed > 0 ? TimeSpan.FromSeconds(remainingBytes / instSpeed) : TimeSpan.Zero;

            if (_metrics.TryGetValue(downloadId, out var existing))
            {
                existing.TotalBytes = totalBytes;
                existing.CompletedBytes = completedBytes;
                existing.InstantaneousSpeedBps = instSpeed;
                existing.AverageSpeedBps = avgSpeed;
                existing.ActiveConnections = activeConns;
                existing.TotalRetries = retries;
                existing.LatencyMs = latencyMs;
                existing.ErrorCount = errors;
                existing.RemainingEta = eta;
            }
            else
            {
                var metrics = new DownloadDiagnosticsMetrics
                {
                    DownloadId = downloadId,
                    TotalBytes = totalBytes,
                    CompletedBytes = completedBytes,
                    InstantaneousSpeedBps = instSpeed,
                    AverageSpeedBps = avgSpeed,
                    ActiveConnections = activeConns,
                    TotalRetries = retries,
                    LatencyMs = latencyMs,
                    ErrorCount = errors,
                    RemainingEta = eta
                };
                _metrics[downloadId] = metrics;
            }
        }

        public DownloadDiagnosticsMetrics? GetMetrics(string downloadId)
        {
            return _metrics.TryGetValue(downloadId, out var m) ? m : null;
        }
    }
}
