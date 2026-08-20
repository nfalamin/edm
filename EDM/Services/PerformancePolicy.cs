using System;

namespace EDM.Services
{
    /// <summary>
    /// PerformancePolicy — Centralized, configurable performance policy settings
    /// for adaptive concurrency control, hysteresis, segment sizing, and worker optimization.
    /// Eliminates hardcoded magic numbers across the download engine.
    /// </summary>
    public class PerformancePolicy
    {
        public static PerformancePolicy Default { get; } = new PerformancePolicy();

        /// <summary>Default starting connection count for new downloads (conservative default).</summary>
        public int InitialConnections { get; set; } = 4;

        /// <summary>Absolute minimum connection count allowed.</summary>
        public int MinConnections { get; set; } = 1;

        /// <summary>Default maximum connection count allowed.</summary>
        public int MaxConnections { get; set; } = 32;

        /// <summary>
        /// Minimum relative throughput gain required to continue scaling up concurrency (e.g. 0.05 = 5%).
        /// If increasing connections produces less than 5% gain, the engine will halt scaling.
        /// </summary>
        public double MinimumUsefulGainPercent { get; set; } = 0.05;

        /// <summary>
        /// Relative throughput degradation threshold that triggers scaling down concurrency (e.g. -0.10 = -10%).
        /// </summary>
        public double DegradationThresholdPercent { get; set; } = -0.10;

        /// <summary>
        /// Per-connection efficiency degradation threshold. If throughput per connection drops by more than
        /// this ratio compared to baseline, scaling is halted to avoid connection waste.
        /// </summary>
        public double PerConnectionEfficiencyThreshold { get; set; } = 0.65;

        /// <summary>
        /// Latency spike multiplier (e.g. 1.40 = 40% increase over baseline RTT) that triggers concurrency hold or backoff.
        /// </summary>
        public double LatencySpikeThresholdMultiplier { get; set; } = 1.40;

        /// <summary>
        /// Minimum duration between concurrency scaling decisions to prevent rapid oscillation.
        /// </summary>
        public TimeSpan CooldownInterval { get; set; } = TimeSpan.FromMilliseconds(2000);

        /// <summary>
        /// Number of consecutive telemetry samples required to confirm sustained improvement or degradation.
        /// </summary>
        public int ConsecutiveSamplesRequired { get; set; } = 3;

        /// <summary>
        /// Ratio of median cluster throughput below which an individual worker is flagged as slow (e.g. 0.25 = 25%).
        /// </summary>
        public double SlowWorkerRatioThreshold { get; set; } = 0.25;

        /// <summary>
        /// Minimum byte size required to split a downloading segment during dynamic work stealing (default 2 MB).
        /// </summary>
        public long MinSplitThresholdBytes { get; set; } = 2 * 1024 * 1024;

        /// <summary>
        /// Segment byte boundary alignment for dynamic splitting (default 64 KB).
        /// </summary>
        public long SplitAlignmentBytes { get; set; } = 64 * 1024;

        /// <summary>
        /// Global maximum active connections across all simultaneous downloads.
        /// </summary>
        public int GlobalMaximumConnections { get; set; } = 32;
    }
}
