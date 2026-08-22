using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class MirrorSourceInfo
    {
        public string Url { get; set; } = string.Empty;
        public long LatencyMs { get; set; } = -1;
        public bool SupportsRange { get; set; }
        public long ContentLength { get; set; } = -1;
        public bool IsActive { get; set; } = true;
        public int FailureCount { get; set; }
        public long BytesDownloaded;
    }

    public class MirrorAggregationPlan
    {
        public List<MirrorSourceInfo> ActiveMirrors { get; set; } = new();
        public Dictionary<int, string> SegmentToMirrorMap { get; set; } = new();
    }

    public class MultiSourceMirrorAggregatorService
    {
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, List<MirrorSourceInfo>> _mirrorPools = new(StringComparer.OrdinalIgnoreCase);

        public MultiSourceMirrorAggregatorService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        public void RegisterMirrors(string fileKey, IEnumerable<string> mirrorUrls)
        {
            if (string.IsNullOrWhiteSpace(fileKey)) return;
            var list = mirrorUrls
                .Where(u => Uri.TryCreate(u, UriKind.Absolute, out _))
                .Select(u => new MirrorSourceInfo { Url = u })
                .ToList();

            _mirrorPools[fileKey] = list;
        }

        public async Task<List<MirrorSourceInfo>> ProbeAndRankMirrorsAsync(string fileKey, CancellationToken ct = default)
        {
            if (!_mirrorPools.TryGetValue(fileKey, out var mirrors) || mirrors.Count == 0)
            {
                return new List<MirrorSourceInfo>();
            }

            var tasks = mirrors.Select(async mirror =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Head, mirror.Url);
                    req.Headers.Add("User-Agent", "EDM/2.0-Aggregator");
                    using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                    sw.Stop();
                    mirror.LatencyMs = sw.ElapsedMilliseconds;

                    if (resp.IsSuccessStatusCode)
                    {
                        mirror.SupportsRange = resp.Headers.AcceptRanges.Contains("bytes") ||
                                              resp.Headers.Contains("Accept-Ranges");
                        if (resp.Content.Headers.ContentLength.HasValue)
                        {
                            mirror.ContentLength = resp.Content.Headers.ContentLength.Value;
                        }
                        mirror.IsActive = true;
                        mirror.FailureCount = 0;
                    }
                    else
                    {
                        mirror.IsActive = false;
                        mirror.FailureCount++;
                    }
                }
                catch
                {
                    sw.Stop();
                    mirror.LatencyMs = sw.ElapsedMilliseconds;
                    mirror.IsActive = false;
                    mirror.FailureCount++;
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);

            return mirrors.Where(m => m.IsActive).OrderBy(m => m.LatencyMs).ToList();
        }

        public MirrorAggregationPlan BuildAggregationPlan(string fileKey, int totalSegments)
        {
            var plan = new MirrorAggregationPlan();
            if (!_mirrorPools.TryGetValue(fileKey, out var mirrors) || mirrors.Count == 0)
            {
                return plan;
            }

            var active = mirrors.Where(m => m.IsActive).OrderBy(m => m.LatencyMs).ToList();
            if (active.Count == 0)
            {
                active = mirrors; // fallback to all
            }

            plan.ActiveMirrors = active;

            // Distribute segments across available active mirrors
            for (int i = 0; i < totalSegments; i++)
            {
                var assignedMirror = active[i % active.Count];
                plan.SegmentToMirrorMap[i] = assignedMirror.Url;
            }

            return plan;
        }

        public string GetFailoverMirror(string fileKey, string failedMirrorUrl)
        {
            if (!_mirrorPools.TryGetValue(fileKey, out var mirrors))
            {
                return failedMirrorUrl;
            }

            var failed = mirrors.FirstOrDefault(m => string.Equals(m.Url, failedMirrorUrl, StringComparison.OrdinalIgnoreCase));
            if (failed != null)
            {
                failed.FailureCount++;
                if (failed.FailureCount >= 3)
                {
                    failed.IsActive = false;
                }
            }

            var available = mirrors.Where(m => m.IsActive && !string.Equals(m.Url, failedMirrorUrl, StringComparison.OrdinalIgnoreCase))
                                   .OrderBy(m => m.LatencyMs)
                                   .FirstOrDefault();

            return available?.Url ?? failedMirrorUrl;
        }

        public void RecordSegmentProgress(string fileKey, string mirrorUrl, long bytes)
        {
            if (!_mirrorPools.TryGetValue(fileKey, out var mirrors)) return;
            var match = mirrors.FirstOrDefault(m => string.Equals(m.Url, mirrorUrl, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                Interlocked.Add(ref match.BytesDownloaded, bytes);
            }
        }

        public IReadOnlyList<MirrorSourceInfo> GetMirrorStatus(string fileKey)
        {
            if (_mirrorPools.TryGetValue(fileKey, out var list))
            {
                return list.AsReadOnly();
            }
            return Array.Empty<MirrorSourceInfo>();
        }
    }
}
