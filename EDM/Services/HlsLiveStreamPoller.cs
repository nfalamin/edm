using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class HlsSegmentDiscoveredEventArgs : EventArgs
    {
        public HlsSegment Segment { get; }
        public int DiscoveredIndex { get; }

        public HlsSegmentDiscoveredEventArgs(HlsSegment segment, int index)
        {
            Segment = segment;
            DiscoveredIndex = index;
        }
    }

    /// <summary>
    /// Production-grade resilient HLS Live Stream Polling & Segment Deduplication Controller.
    /// Safely polls live media playlists, dynamically deduplicates appended segments, detects stream termination,
    /// and provides cancellation and network reconnect resilience.
    /// </summary>
    public sealed class HlsLiveStreamPoller
    {
        private readonly HttpRequestPipeline _pipeline;
        private readonly HashSet<string> _seenSegmentKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        public event EventHandler<HlsSegmentDiscoveredEventArgs>? SegmentDiscovered;
        public event EventHandler? StreamEnded;

        public HlsLiveStreamPoller(HttpRequestPipeline? pipeline = null)
        {
            _pipeline = pipeline ?? new HttpRequestPipeline(SharedHttpClient.Instance);
        }

        public async Task PollLiveStreamAsync(
            Uri manifestUri,
            string? cookies = null,
            int maxConsecutiveErrors = 10,
            CancellationToken cancellationToken = default)
        {
            int consecutiveErrors = 0;
            int discoveredCount = 0;
            long lastMediaSequence = -1;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _pipeline.ExecuteWithRetryAsync(() =>
                    {
                        var req = new HttpRequestMessage(HttpMethod.Get, manifestUri);
                        if (!string.IsNullOrEmpty(cookies)) req.Headers.Add("Cookie", cookies);
                        return req;
                    }, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);

                    using var resp = result.Response;
                    resp.EnsureSuccessStatusCode();

                    string m3u8Text = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var playlist = HlsParser.Parse(m3u8Text, manifestUri);

                    consecutiveErrors = 0;

                    // If stream is DRM protected, stop immediately
                    if (playlist.IsDrmProtected)
                    {
                        LoggingService.LogWarning($"[HlsLiveStreamPoller] DRM detected on live stream: {playlist.DrmSystem}. Halting live polling.");
                        break;
                    }

                    // Enumerate new segments
                    var newSegments = new List<HlsSegment>();
                    lock (_lock)
                    {
                        foreach (var seg in playlist.Segments)
                        {
                            string key = BuildSegmentKey(manifestUri.ToString(), seg);
                            if (_seenSegmentKeys.Add(key))
                            {
                                newSegments.Add(seg);
                            }
                        }
                    }

                    foreach (var seg in newSegments)
                    {
                        SegmentDiscovered?.Invoke(this, new HlsSegmentDiscoveredEventArgs(seg, discoveredCount++));
                    }

                    // Detect stream termination (#EXT-X-ENDLIST)
                    if (!playlist.IsLive)
                    {
                        LoggingService.Log("[HlsLiveStreamPoller] #EXT-X-ENDLIST encountered. Live stream finished.");
                        StreamEnded?.Invoke(this, EventArgs.Empty);
                        break;
                    }

                    lastMediaSequence = playlist.MediaSequence;

                    // Compute adaptive sleep duration (half of target duration or default 2s)
                    double targetSec = playlist.TargetDurationSeconds > 0 ? playlist.TargetDurationSeconds : 2.0;
                    int delayMs = (int)Math.Max(500, (targetSec * 1000.0) / 2.0);

                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    consecutiveErrors++;
                    LoggingService.LogWarning($"[HlsLiveStreamPoller] Network error polling live stream ({consecutiveErrors}/{maxConsecutiveErrors}): {ex.Message}");

                    if (consecutiveErrors >= maxConsecutiveErrors)
                    {
                        LoggingService.LogWarning($"[HlsLiveStreamPoller] Max consecutive polling errors exceeded for {manifestUri}. Terminating poller.");
                        break;
                    }

                    int backoffMs = Math.Min(15000, 1000 * (int)Math.Pow(2, Math.Min(4, consecutiveErrors)));
                    await Task.Delay(backoffMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public static string BuildSegmentKey(string manifestUrl, HlsSegment seg)
        {
            return $"{manifestUrl}#seq={seg.SequenceNumber}#disc={seg.DiscontinuitySequence}#uri={seg.Uri}#range={seg.ByteRangeOffset}-{seg.ByteRangeLength}";
        }
    }
}
