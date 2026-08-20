using System;

namespace EDM.Services
{
    public enum BottleneckType
    {
        NetworkLimited,     // Network bandwidth or link is saturated
        ServerLimited,      // Remote server rate-limiting, slow upstream or concurrency capped
        DiskLimited,        // Local storage write throughput / queue latency bottleneck
        CpuLimited,         // CPU saturation (e.g. high thread contention or decryption/merging)
        ApplicationLimited, // Global resource governor or bandwidth throttle limit
        Unknown             // Insufficient runtime telemetry
    }

    public class BottleneckAnalysisResult
    {
        public BottleneckType Type { get; set; } = BottleneckType.Unknown;
        public string Reason { get; set; } = string.Empty;
        public double Confidence { get; set; } = 1.0;
        public bool ShouldStopScalingUp { get; set; }
    }

    /// <summary>
    /// ResourceBottleneckClassifier — Identifies the true real-time bottleneck constraining download speed.
    /// Distinguishes between network capacity, server saturation, disk write latency, CPU load, and retry pressure.
    /// </summary>
    public static class ResourceBottleneckClassifier
    {
        public static BottleneckAnalysisResult Classify(
            double currentThroughputBps,
            double networkEstimatedBps,
            double rttMs,
            double diskWriteLatencyMs = 2.0,
            double cpuUsagePercent = 5.0,
            double retryRatePercent = 0.0,
            bool isThrottledByApp = false)
        {
            if (isThrottledByApp)
            {
                return new BottleneckAnalysisResult
                {
                    Type = BottleneckType.ApplicationLimited,
                    Reason = "Download speed is restricted by configured bandwidth limit.",
                    ShouldStopScalingUp = true
                };
            }

            if (cpuUsagePercent >= 90.0)
            {
                return new BottleneckAnalysisResult
                {
                    Type = BottleneckType.CpuLimited,
                    Reason = $"High CPU utilization ({cpuUsagePercent:F0}%) is throttling download processing.",
                    ShouldStopScalingUp = true
                };
            }

            if (diskWriteLatencyMs >= 50.0)
            {
                return new BottleneckAnalysisResult
                {
                    Type = BottleneckType.DiskLimited,
                    Reason = $"Disk write latency is high ({diskWriteLatencyMs:F1}ms). Local storage I/O is saturated.",
                    ShouldStopScalingUp = true
                };
            }

            if (retryRatePercent >= 5.0)
            {
                return new BottleneckAnalysisResult
                {
                    Type = BottleneckType.ServerLimited,
                    Reason = $"High request retry/failure rate ({retryRatePercent:F1}%). Server is rejecting rapid concurrent connections.",
                    ShouldStopScalingUp = true
                };
            }

            if (networkEstimatedBps > 0 && currentThroughputBps >= networkEstimatedBps * 0.85)
            {
                return new BottleneckAnalysisResult
                {
                    Type = BottleneckType.NetworkLimited,
                    Reason = $"Current throughput ({currentThroughputBps / (1024 * 1024):F1} MB/s) is near local network capacity ({networkEstimatedBps / (1024 * 1024):F1} MB/s).",
                    ShouldStopScalingUp = true
                };
            }

            if (currentThroughputBps > 0 && (networkEstimatedBps <= 0 || currentThroughputBps < networkEstimatedBps * 0.50))
            {
                return new BottleneckAnalysisResult
                {
                    Type = BottleneckType.ServerLimited,
                    Reason = "Throughput is constrained by remote server bandwidth or per-IP connection throttling.",
                    ShouldStopScalingUp = false
                };
            }

            return new BottleneckAnalysisResult
            {
                Type = BottleneckType.Unknown,
                Reason = "Engine is operating with balanced resource headroom.",
                ShouldStopScalingUp = false
            };
        }
    }
}
