using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class HlsDashDownloadService
    {
        private readonly HttpRequestPipeline _pipeline;

        public HlsDashDownloadService()
        {
            _pipeline = new HttpRequestPipeline(SharedHttpClient.Instance);
        }

        public async Task DownloadHlsStreamAsync(
            string manifestUrl,
            string targetFilePath,
            string? cookies = null,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var baseUri = new Uri(manifestUrl);
            string manifestText = await FetchTextAsync(baseUri, cookies, cancellationToken).ConfigureAwait(false);

            var playlist = HlsParser.Parse(manifestText, baseUri);

            if (playlist.IsDrmProtected)
            {
                throw new InvalidOperationException("This stream is DRM-protected and cannot be downloaded.");
            }

            List<string> segmentUrls = playlist.SegmentUrls;

            // If Master playlist, pick highest resolution variant
            if (playlist.IsMaster && playlist.Variants.Any())
            {
                var highestVariant = playlist.Variants.OrderByDescending(v => v.Bandwidth).First();
                var variantUri = new Uri(highestVariant.Uri);
                string variantText = await FetchTextAsync(variantUri, cookies, cancellationToken).ConfigureAwait(false);
                var variantPlaylist = HlsParser.Parse(variantText, variantUri);

                if (variantPlaylist.IsDrmProtected)
                {
                    throw new InvalidOperationException("This stream is DRM-protected and cannot be downloaded.");
                }

                segmentUrls = variantPlaylist.SegmentUrls;
            }

            if (!segmentUrls.Any())
            {
                throw new InvalidOperationException("No media segments found in HLS playlist.");
            }

            // Check if separate audio track needs downloading & muxing
            if (playlist.IsMaster && playlist.AudioTracks.Any(a => !string.IsNullOrEmpty(a.Uri)))
            {
                var audioTrack = playlist.AudioTracks.FirstOrDefault(a => a.IsDefault) ?? playlist.AudioTracks.First();
                string tempVideo = targetFilePath + ".vid.tmp";
                string tempAudio = targetFilePath + ".aud.tmp";

                try
                {
                    // 1. Download video segments
                    await DownloadAndConcatSegmentsAsync(segmentUrls, tempVideo, cookies, progress, cancellationToken).ConfigureAwait(false);

                    // 2. Resolve and download audio segments
                    var audioUri = new Uri(audioTrack.Uri);
                    string audioM3u8 = await FetchTextAsync(audioUri, cookies, cancellationToken).ConfigureAwait(false);
                    var audioPlaylist = HlsParser.Parse(audioM3u8, audioUri);

                    if (audioPlaylist.SegmentUrls.Any())
                    {
                        await DownloadAndConcatSegmentsAsync(audioPlaylist.SegmentUrls, tempAudio, cookies, null, cancellationToken).ConfigureAwait(false);

                        // 3. Merge video + audio using MediaMergeService / FFmpeg
                        var mergeService = new MediaMergeService(SharedHttpClient.Instance);
                        string ffmpegPath = new SettingsService().GetFfmpegPath();
                        await mergeService.MergeAudioVideoAsync(tempVideo, tempAudio, targetFilePath, ffmpegPath, cancellationToken).ConfigureAwait(false);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Log($"[HlsDashDownloadService] Separate audio mux failed, falling back to video only: {ex.Message}");
                    if (File.Exists(tempVideo))
                    {
                        File.Move(tempVideo, targetFilePath, true);
                        return;
                    }
                }
                finally
                {
                    try { if (File.Exists(tempVideo)) File.Delete(tempVideo); } catch { }
                    try { if (File.Exists(tempAudio)) File.Delete(tempAudio); } catch { }
                }
            }

            await DownloadAndConcatSegmentsAsync(segmentUrls, targetFilePath, cookies, progress, cancellationToken).ConfigureAwait(false);
        }

        public async Task DownloadDashStreamAsync(
            string manifestUrl,
            string targetFilePath,
            string? cookies = null,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var baseUri = new Uri(manifestUrl);
            string manifestXml = await FetchTextAsync(baseUri, cookies, cancellationToken).ConfigureAwait(false);

            var manifest = DashParser.Parse(manifestXml, baseUri);

            if (manifest.IsDrmProtected)
            {
                throw new InvalidOperationException("This stream is DRM-protected and cannot be downloaded.");
            }

            var bestVideo = manifest.VideoRepresentations.OrderByDescending(v => v.Height).ThenByDescending(v => v.Bandwidth).FirstOrDefault();
            var bestAudio = manifest.AudioRepresentations.OrderByDescending(a => a.Bandwidth).FirstOrDefault();

            if (bestVideo == null && bestAudio == null)
            {
                throw new InvalidOperationException("No video or audio representations found in DASH manifest.");
            }

            if (bestVideo != null && bestAudio != null && bestVideo.SegmentUrls.Any() && bestAudio.SegmentUrls.Any())
            {
                string tempVideo = targetFilePath + ".dash_vid.tmp";
                string tempAudio = targetFilePath + ".dash_aud.tmp";

                try
                {
                    await DownloadAndConcatSegmentsAsync(bestVideo.SegmentUrls, tempVideo, cookies, progress, cancellationToken).ConfigureAwait(false);
                    await DownloadAndConcatSegmentsAsync(bestAudio.SegmentUrls, tempAudio, cookies, null, cancellationToken).ConfigureAwait(false);

                    var mergeService = new MediaMergeService(SharedHttpClient.Instance);
                    string ffmpegPath = new SettingsService().GetFfmpegPath();
                    await mergeService.MergeAudioVideoAsync(tempVideo, tempAudio, targetFilePath, ffmpegPath, cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    LoggingService.Log($"[HlsDashDownloadService] DASH audio+video merge failed, falling back: {ex.Message}");
                    if (File.Exists(tempVideo))
                    {
                        File.Move(tempVideo, targetFilePath, true);
                        return;
                    }
                }
                finally
                {
                    try { if (File.Exists(tempVideo)) File.Delete(tempVideo); } catch { }
                    try { if (File.Exists(tempAudio)) File.Delete(tempAudio); } catch { }
                }
            }

            var targetRep = bestVideo ?? bestAudio!;
            if (!targetRep.SegmentUrls.Any())
            {
                throw new InvalidOperationException("No media segments found in selected DASH representation.");
            }

            await DownloadAndConcatSegmentsAsync(targetRep.SegmentUrls, targetFilePath, cookies, progress, cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> FetchTextAsync(Uri uri, string? cookies, CancellationToken ct)
        {
            var result = await _pipeline.ExecuteWithRetryAsync(() =>
            {
                var req = new HttpRequestMessage(HttpMethod.Get, uri);
                if (!string.IsNullOrEmpty(cookies))
                {
                    req.Headers.Add("Cookie", cookies);
                }
                return req;
            }, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

            using var resp = result.Response;
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }

        private async Task DownloadAndConcatSegmentsAsync(
            List<string> segmentUrls,
            string targetFilePath,
            string? cookies,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            string tempDir = Path.Combine(Path.GetDirectoryName(targetFilePath) ?? Path.GetTempPath(), "edm_hls_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var downloadedParts = new ConcurrentDictionary<int, string>();
            int total = segmentUrls.Count;
            int completed = 0;

            try
            {
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 8,
                    CancellationToken = cancellationToken
                };

                await Parallel.ForEachAsync(Enumerable.Range(0, total), parallelOptions, async (index, ct) =>
                {
                    string segUrl = segmentUrls[index];
                    string partPath = Path.Combine(tempDir, $"seg_{index:D6}.part");

                    var result = await _pipeline.ExecuteWithRetryAsync(() =>
                    {
                        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(segUrl));
                        if (!string.IsNullOrEmpty(cookies)) req.Headers.Add("Cookie", cookies);
                        return req;
                    }, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                    using (var resp = result.Response)
                    {
                        resp.EnsureSuccessStatusCode();
                        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                        await using var fs = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                        await stream.CopyToAsync(fs, ct).ConfigureAwait(false);
                    }

                    downloadedParts[index] = partPath;
                    int cur = Interlocked.Increment(ref completed);
                    progress?.Report((double)cur / total * 100.0);
                }).ConfigureAwait(false);

                // Concatenate in order into target file
                await using var outFs = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                for (int i = 0; i < total; i++)
                {
                    if (downloadedParts.TryGetValue(i, out var partFile) && File.Exists(partFile))
                    {
                        await using var partFs = File.OpenRead(partFile);
                        await partFs.CopyToAsync(outFs, cancellationToken).ConfigureAwait(false);
                    }
                }
                await outFs.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch { }
            }
        }
    }
}
