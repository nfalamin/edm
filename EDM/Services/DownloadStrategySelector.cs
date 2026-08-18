using System;

namespace EDM.Services
{
    public enum DownloadStrategyType
    {
        SingleStream,       // Small files (< 1MB) or servers without 206 Partial Content support
        AdaptiveMultipart,  // Standard multi-part segmented downloading with dynamic concurrency
        MediaStream,        // Audio/Video streaming media requiring resolution and FFmpeg merging
        Fallback            // Single-stream recovery mode when multi-part segmented download fails
    }

    public class StrategySelectionResult
    {
        public DownloadStrategyType Strategy { get; set; } = DownloadStrategyType.AdaptiveMultipart;
        public string Rationale { get; set; } = string.Empty;
        public int RecommendedInitialConnections { get; set; } = 4;
        public bool ShouldPerformFullProbe { get; set; } = true;
    }

    /// <summary>
    /// DownloadStrategySelector — Authoritative engine for selecting the optimal download strategy
    /// based on file size, protocol capabilities, range support, and media streaming signatures.
    /// </summary>
    public static class DownloadStrategySelector
    {
        private const long SmallFileThresholdBytes = 1 * 1024 * 1024; // 1 MB

        public static StrategySelectionResult SelectStrategy(
            string url,
            long? totalBytes,
            bool serverSupportsRanges,
            bool isMediaStreaming = false)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return new StrategySelectionResult
                {
                    Strategy = DownloadStrategyType.SingleStream,
                    Rationale = "Invalid or empty URL.",
                    RecommendedInitialConnections = 1,
                    ShouldPerformFullProbe = false
                };
            }

            // 1. Streaming Media Detection
            if (isMediaStreaming || DownloadService.IsVideoStreamingUrl(url))
            {
                return new StrategySelectionResult
                {
                    Strategy = DownloadStrategyType.MediaStream,
                    Rationale = "URL identified as media streaming service (YouTube/Vimeo/etc). Routing to Media Engine.",
                    RecommendedInitialConnections = 4,
                    ShouldPerformFullProbe = false
                };
            }

            // 2. Small File Optimization (< 1 MB)
            if (totalBytes.HasValue && totalBytes.Value > 0 && totalBytes.Value < SmallFileThresholdBytes)
            {
                return new StrategySelectionResult
                {
                    Strategy = DownloadStrategyType.SingleStream,
                    Rationale = $"Small file ({totalBytes.Value / 1024.0:F1} KB < 1 MB). Using fast single-stream without segmentation overhead.",
                    RecommendedInitialConnections = 1,
                    ShouldPerformFullProbe = false
                };
            }

            // 3. No Range Support from Server
            if (!serverSupportsRanges)
            {
                return new StrategySelectionResult
                {
                    Strategy = DownloadStrategyType.SingleStream,
                    Rationale = "Remote server does not advertise HTTP 206 Partial Content (Accept-Ranges). Using single-stream.",
                    RecommendedInitialConnections = 1,
                    ShouldPerformFullProbe = true
                };
            }

            // 4. Large File Adaptive Multipart
            int initialConns = 4;
            if (totalBytes.HasValue && totalBytes.Value > 500 * 1024 * 1024) // > 500 MB
            {
                initialConns = 8;
            }

            return new StrategySelectionResult
            {
                Strategy = DownloadStrategyType.AdaptiveMultipart,
                Rationale = "Large file with verified HTTP 206 range support. Enabling high-speed adaptive multi-part engine.",
                RecommendedInitialConnections = initialConns,
                ShouldPerformFullProbe = true
            };
        }
    }
}
