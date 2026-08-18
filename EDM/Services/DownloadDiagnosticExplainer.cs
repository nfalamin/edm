using System;

namespace EDM.Services
{
    /// <summary>
    /// DownloadDiagnosticExplainer — Self-diagnostic engine providing clear human-readable explanations
    /// for download performance, bottlenecks, and adaptive scaling decisions.
    /// </summary>
    public static class DownloadDiagnosticExplainer
    {
        public static string ExplainSpeed(
            double currentSpeedBps,
            double peakSpeedBps,
            BottleneckAnalysisResult bottleneck,
            int activeConnections,
            int retryCount)
        {
            double currentMbps = currentSpeedBps / (1024.0 * 1024.0);
            double peakMbps = peakSpeedBps / (1024.0 * 1024.0);

            string diagnosis = bottleneck.Type switch
            {
                BottleneckType.ServerLimited => "Remote server is limiting transfer throughput or per-connection rate.",
                BottleneckType.NetworkLimited => "Local network connection bandwidth is fully saturated.",
                BottleneckType.DiskLimited => "Local storage disk write speed is currently constraining throughput.",
                BottleneckType.CpuLimited => "High CPU usage is throttling download stream processing.",
                BottleneckType.ApplicationLimited => "Download speed is being capped by the user-configured speed limit.",
                _ => "Download is progressing normally with balanced resource headroom."
            };

            string retryNote = retryCount > 0 ? $" ({retryCount} network retries recorded)" : "";
            return $"Speed: {currentMbps:F2} MB/s (Peak: {peakMbps:F2} MB/s) using {activeConnections} active connections{retryNote}. Diagnosis: {diagnosis}";
        }

        public static string ExplainScalingDecision(
            int currentConns,
            int targetConns,
            double gainPercent,
            string reason)
        {
            if (targetConns > currentConns)
            {
                return $"Scaled connections UP ({currentConns} → {targetConns}) due to +{gainPercent:F1}% throughput gain. {reason}";
            }
            if (targetConns < currentConns)
            {
                return $"Scaled connections DOWN ({currentConns} → {targetConns}). {reason}";
            }
            return $"Maintained {currentConns} connections. {reason}";
        }
    }
}
