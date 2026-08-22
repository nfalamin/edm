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

        /// <summary>Default starting connection count for new downloads — high-speed aggressive start.</summary>
        public int InitialConnections { get; set; } = 32;

        /// <summary>Absolute minimum connection count allowed.</summary>
        public int MinConnections { get; set; } = 1;

        /// <summary>Maximum connection count allowed per download — 32 threads.</summary>
        public int MaxConnections { get; set; } = 32;

        /// <summary>
        /// Minimum relative throughput gain required to continue scaling up concurrency (e.g. 0.02 = 2%).
        /// Lowered from 5% to 2% to scale up more aggressively.
        /// </summary>
        public double MinimumUsefulGainPercent { get; set; } = 0.02;

        /// <summary>
        /// Relative throughput degradation threshold that triggers scaling down concurrency (e.g. -0.15 = -15%).
        /// Widened to -15% to avoid premature scale-down.
        /// </summary>
        public double DegradationThresholdPercent { get; set; } = -0.15;

        /// <summary>
        /// Per-connection efficiency degradation threshold. If throughput per connection drops by more than
        /// this ratio compared to baseline, scaling is halted to avoid connection waste.
        /// </summary>
        public double PerConnectionEfficiencyThreshold { get; set; } = 0.65;

        /// <summary>
        /// Latency spike multiplier (e.g. 1.80 = 80% increase over baseline RTT) that triggers concurrency hold or backoff.
        /// Raised to 1.80 to tolerate transient latency spikes without premature backoff.
        /// </summary>
        public double LatencySpikeThresholdMultiplier { get; set; } = 1.80;

        /// <summary>
        /// Minimum duration between concurrency scaling decisions. Reduced to 800ms for faster adaptation.
        /// </summary>
        public TimeSpan CooldownInterval { get; set; } = TimeSpan.FromMilliseconds(800);

        /// <summary>
        /// Number of consecutive telemetry samples required to confirm sustained improvement or degradation.
        /// Reduced to 2 for faster scale-up decisions.
        /// </summary>
        public int ConsecutiveSamplesRequired { get; set; } = 2;

        /// <summary>
        /// Ratio of median cluster throughput below which an individual worker is flagged as slow (e.g. 0.25 = 25%).
        /// </summary>
        public double SlowWorkerRatioThreshold { get; set; } = 0.25;

        /// <summary>
        /// Minimum byte size required to split a downloading segment during dynamic work stealing (default 1 MB).
        /// Reduced from 2 MB to 1 MB for finer-grained work-stealing at high thread counts.
        /// </summary>
        public long MinSplitThresholdBytes { get; set; } = 1 * 1024 * 1024;

        /// <summary>
        /// Segment byte boundary alignment for dynamic splitting (default 64 KB).
        /// </summary>
        public long SplitAlignmentBytes { get; set; } = 64 * 1024;

        /// <summary>
        /// Global maximum active connections across all simultaneous downloads.
        /// </summary>
        public int GlobalMaximumConnections { get; set; } = 64;
    }
}
