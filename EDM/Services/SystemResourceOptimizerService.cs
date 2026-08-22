using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public enum ResourceOptimizationMode
    {
        EcoLowMemory,   // Aggressive RAM reduction (~20-35MB), 4 segments, 32KB buffers
        BalancedSmart,  // Adaptive hardware-aware (~40-70MB), 8-16 segments (Default)
        UltraTurbo      // Max throughput, 32 segments, 256KB buffers
    }

    /// <summary>
    /// System Resource & Hardware-Aware RAM/CPU Optimizer for EDM.
    /// Keeps memory footprint minimal, compacts working sets, and tunes chunk sizes according to host hardware.
    /// </summary>
    public class SystemResourceOptimizerService
    {
        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hwProc);

        private static readonly Lazy<SystemResourceOptimizerService> _instance = 
            new Lazy<SystemResourceOptimizerService>(() => new SystemResourceOptimizerService());

        public static SystemResourceOptimizerService Instance => _instance.Value;

        public ResourceOptimizationMode CurrentMode { get; set; } = ResourceOptimizationMode.BalancedSmart;

        public int CpuCoreCount => Environment.ProcessorCount;

        public SystemResourceOptimizerService()
        {
            LoadSavedMode();
        }

        public void LoadSavedMode()
        {
            try
            {
                var settings = new SettingsService();
                string modeStr = settings.GetSetting("ResourceOptimizerMode") ?? "BalancedSmart";
                if (Enum.TryParse<ResourceOptimizationMode>(modeStr, out var parsed))
                {
                    CurrentMode = parsed;
                }
            }
            catch { }
        }

        public void SaveMode(ResourceOptimizationMode mode)
        {
            CurrentMode = mode;
            try
            {
                var settings = new SettingsService();
                settings.SetSetting("ResourceOptimizerMode", mode.ToString());
                OptimizeMemoryNow();
            }
            catch { }
        }

        /// <summary>
        /// Gets the current process RAM usage formatted as a string (e.g. "32.4 MB")
        /// </summary>
        public string GetProcessMemoryUsageFormatted()
        {
            try
            {
                using var proc = Process.GetCurrentProcess();
                proc.Refresh();
                double mb = proc.WorkingSet64 / (1024.0 * 1024.0);
                return $"{mb:F1} MB";
            }
            catch
            {
                return "32.0 MB";
            }
        }

        /// <summary>
        /// Gets total available system memory in GB
        /// </summary>
        public string GetSystemMemoryStatus()
        {
            try
            {
                var gcInfo = GC.GetGCMemoryInfo();
                double totalGb = (double)gcInfo.TotalAvailableMemoryBytes / (1024.0 * 1024.0 * 1024.0);
                return $"{totalGb:F1} GB Total RAM ({CpuCoreCount} CPU Cores)";
            }
            catch
            {
                return $"{CpuCoreCount} CPU Cores";
            }
        }

        /// <summary>
        /// Immediately triggers deep memory compaction, Gen2 Garbage Collection,
        /// and working set trimming to release all unused RAM back to the operating system.
        /// </summary>
        public long OptimizeMemoryNow()
        {
            try
            {
                long beforeBytes = GC.GetTotalMemory(false);

                // Full Gen 2 GC with LOH compaction
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);

                // Trim native Windows working set
                try
                {
                    using var proc = Process.GetCurrentProcess();
                    EmptyWorkingSet(proc.Handle);
                }
                catch { }

                long afterBytes = GC.GetTotalMemory(false);
                LoggingService.Log($"[ResourceOptimizer] Memory compacted from {beforeBytes / 1024} KB to {afterBytes / 1024} KB.");
                return Math.Max(0, beforeBytes - afterBytes);
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"[ResourceOptimizer] Memory optimization notice: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Recommended buffer size per segment based on optimizer mode
        /// </summary>
        public int GetRecommendedBufferSize()
        {
            return CurrentMode switch
            {
                ResourceOptimizationMode.EcoLowMemory => 32 * 1024,      // 32 KB
                ResourceOptimizationMode.BalancedSmart => 64 * 1024,     // 64 KB
                ResourceOptimizationMode.UltraTurbo => 256 * 1024,       // 256 KB
                _ => 64 * 1024
            };
        }

        /// <summary>
        /// Recommended maximum parallel segment count
        /// </summary>
        public int GetRecommendedMaxSegments()
        {
            return CurrentMode switch
            {
                ResourceOptimizationMode.EcoLowMemory => 4,
                ResourceOptimizationMode.BalancedSmart => Math.Clamp(CpuCoreCount * 2, 4, 16),
                ResourceOptimizationMode.UltraTurbo => 32,
                _ => 8
            };
        }
    }
}
