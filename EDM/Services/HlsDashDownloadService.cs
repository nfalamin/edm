using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;

namespace EDM.Services
{
    public class HlsDashDownloadService
    {
        private readonly HttpRequestPipeline _pipeline;
        private readonly ConcurrentDictionary<string, byte[]> _keyCache = new(StringComparer.OrdinalIgnoreCase);

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
            var dlProgress = progress != null ? new Progress<DownloadProgressInfo>(info => progress.Report(info.ProgressPercentage)) : null;
            await DownloadHlsStreamAsync(manifestUrl, targetFilePath, null, cookies, dlProgress, null, cancellationToken).ConfigureAwait(false);
        }

        public async Task DownloadHlsStreamAsync(
            string manifestUrl,
            string targetFilePath,
            string? qualityPreference,
            string? cookies,
            IProgress<DownloadProgressInfo>? progress,
            PauseTokenSource? pauseToken,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(manifestUrl)) throw new ArgumentException("Manifest URL is required", nameof(manifestUrl));
            if (string.IsNullOrWhiteSpace(targetFilePath)) throw new ArgumentException("Target file path is required", nameof(targetFilePath));

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath) ?? ".");

            var baseUri = new Uri(manifestUrl);
            string manifestText = await FetchTextAsync(baseUri, cookies, cancellationToken).ConfigureAwait(false);

            var playlist = HlsParser.Parse(manifestText, baseUri);

            if (playlist.IsDrmProtected)
            {
                string sys = !string.IsNullOrEmpty(playlist.DrmSystem) ? playlist.DrmSystem : "DRM";
                throw new InvalidOperationException($"This HLS stream is protected by {sys} and cannot be downloaded.");
            }

            var segments = playlist.Segments;
            HlsAudioTrack? selectedAudioTrack = null;

            // 1. If Master playlist, select variant according to quality preference
            if (playlist.IsMaster && playlist.Variants.Any())
            {
                var chosenVariant = SelectHlsVariant(playlist.Variants, qualityPreference);
                var variantUri = new Uri(chosenVariant.Uri);
                string variantText = await FetchTextAsync(variantUri, cookies, cancellationToken).ConfigureAwait(false);
                var variantPlaylist = HlsParser.Parse(variantText, variantUri);

                if (variantPlaylist.IsDrmProtected)
                {
                    string sys = !string.IsNullOrEmpty(variantPlaylist.DrmSystem) ? variantPlaylist.DrmSystem : "DRM";
                    throw new InvalidOperationException($"This HLS stream is protected by {sys} and cannot be downloaded.");
                }

                segments = variantPlaylist.Segments;

                // Match audio group
                if (!string.IsNullOrEmpty(chosenVariant.AudioGroupId) && playlist.AudioTracks.Any())
                {
                    selectedAudioTrack = playlist.AudioTracks.FirstOrDefault(a => a.GroupId == chosenVariant.AudioGroupId && a.IsDefault) ??
                                         playlist.AudioTracks.FirstOrDefault(a => a.GroupId == chosenVariant.AudioGroupId) ??
                                         playlist.AudioTracks.FirstOrDefault(a => a.IsDefault) ??
                                         playlist.AudioTracks.First();
                }
                else if (playlist.AudioTracks.Any(a => !string.IsNullOrEmpty(a.Uri)))
                {
                    selectedAudioTrack = playlist.AudioTracks.FirstOrDefault(a => a.IsDefault) ?? playlist.AudioTracks.First();
                }
            }

            if (!segments.Any())
            {
                throw new InvalidOperationException("No media segments found in HLS playlist.");
            }

            // 2. Separate Audio Track Muxing (Dual-Stream Adaptive HLS)
            if (selectedAudioTrack != null && !string.IsNullOrEmpty(selectedAudioTrack.Uri))
            {
                string stagingBase = targetFilePath + ".edm_hls_dual";
                string tempVideo = targetFilePath + ".vid.tmp";
                string tempAudio = targetFilePath + ".aud.tmp";

                try
                {
                    LoggingService.Log($"[HlsDashDownloadService] Downloading separate video and audio streams for HLS: audio={selectedAudioTrack.Name}");

                    // Download video segments
                    await DownloadAndConcatHlsSegmentsAsync(segments, tempVideo, cookies, progress, pauseToken, cancellationToken, "Video").ConfigureAwait(false);

                    // Resolve audio segments
                    var audioUri = new Uri(selectedAudioTrack.Uri);
                    string audioM3u8 = await FetchTextAsync(audioUri, cookies, cancellationToken).ConfigureAwait(false);
                    var audioPlaylist = HlsParser.Parse(audioM3u8, audioUri);

                    if (audioPlaylist.Segments.Any())
                    {
                        // Download audio segments
                        await DownloadAndConcatHlsSegmentsAsync(audioPlaylist.Segments, tempAudio, cookies, progress, pauseToken, cancellationToken, "Audio").ConfigureAwait(false);

                        // Merge video + audio using FFmpeg
                        progress?.Report(new DownloadProgressInfo
                        {
                            Status = "Merging HLS Audio & Video (FFmpeg)...",
                            ProgressPercentage = 99.0,
                            ServerSupportsResume = true,
                            ActiveConnections = 1,
                            IsCompleted = false
                        });

                        var mergeService = new MediaMergeService(SharedHttpClient.Instance);
                        string ffmpegPath = new SettingsService().GetFfmpegPath();
                        await mergeService.MergeAudioVideoAsync(tempVideo, tempAudio, targetFilePath, ffmpegPath, cancellationToken).ConfigureAwait(false);

                        progress?.Report(new DownloadProgressInfo
                        {
                            Status = "Finished",
                            ProgressPercentage = 100.0,
                            ServerSupportsResume = true,
                            ActiveConnections = 0,
                            IsCompleted = true
                        });
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
                    throw;
                }
                finally
                {
                    try { if (File.Exists(tempVideo)) File.Delete(tempVideo); } catch { }
                    try { if (File.Exists(tempAudio)) File.Delete(tempAudio); } catch { }
                }
            }

            // 3. Single stream (Muxed or Video-only) download
            await DownloadAndConcatHlsSegmentsAsync(segments, targetFilePath, cookies, progress, pauseToken, cancellationToken, "HLS").ConfigureAwait(false);
        }

        public async Task DownloadDashStreamAsync(
            string manifestUrl,
            string targetFilePath,
            string? cookies = null,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var dlProgress = progress != null ? new Progress<DownloadProgressInfo>(info => progress.Report(info.ProgressPercentage)) : null;
            await DownloadDashStreamAsync(manifestUrl, targetFilePath, null, cookies, dlProgress, null, cancellationToken).ConfigureAwait(false);
        }

        public async Task DownloadDashStreamAsync(
            string manifestUrl,
            string targetFilePath,
            string? qualityPreference,
            string? cookies,
            IProgress<DownloadProgressInfo>? progress,
            PauseTokenSource? pauseToken,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(manifestUrl)) throw new ArgumentException("Manifest URL is required", nameof(manifestUrl));
            if (string.IsNullOrWhiteSpace(targetFilePath)) throw new ArgumentException("Target file path is required", nameof(targetFilePath));

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath) ?? ".");

            var baseUri = new Uri(manifestUrl);
            string manifestXml = await FetchTextAsync(baseUri, cookies, cancellationToken).ConfigureAwait(false);

            var manifest = DashParser.Parse(manifestXml, baseUri);

            if (manifest.IsDrmProtected)
            {
                string sys = !string.IsNullOrEmpty(manifest.DrmSystem) ? manifest.DrmSystem : "DRM";
                throw new InvalidOperationException($"This DASH stream is protected by {sys} and cannot be downloaded.");
            }

            var chosenVideo = SelectDashRepresentation(manifest.VideoRepresentations, qualityPreference);
            var chosenAudio = manifest.AudioRepresentations.OrderByDescending(a => a.Bandwidth).FirstOrDefault();

            if (chosenVideo == null && chosenAudio == null)
            {
                throw new InvalidOperationException("No video or audio representations found in DASH manifest.");
            }

            // Dual stream DASH (Separate video & audio representations)
            if (chosenVideo != null && chosenAudio != null && chosenVideo.SegmentUrls.Any() && chosenAudio.SegmentUrls.Any())
            {
                string tempVideo = targetFilePath + ".dash_vid.tmp";
                string tempAudio = targetFilePath + ".dash_aud.tmp";

                try
                {
                    await DownloadAndConcatDashSegmentsAsync(chosenVideo, tempVideo, cookies, progress, pauseToken, cancellationToken, "Video").ConfigureAwait(false);
                    await DownloadAndConcatDashSegmentsAsync(chosenAudio, tempAudio, cookies, progress, pauseToken, cancellationToken, "Audio").ConfigureAwait(false);

                    progress?.Report(new DownloadProgressInfo
                    {
                        Status = "Merging DASH Audio & Video (FFmpeg)...",
                        ProgressPercentage = 99.0,
                        ServerSupportsResume = true,
                        ActiveConnections = 1,
                        IsCompleted = false
                    });

                    var mergeService = new MediaMergeService(SharedHttpClient.Instance);
                    string ffmpegPath = new SettingsService().GetFfmpegPath();
                    await mergeService.MergeAudioVideoAsync(tempVideo, tempAudio, targetFilePath, ffmpegPath, cancellationToken).ConfigureAwait(false);

                    progress?.Report(new DownloadProgressInfo
                    {
                        Status = "Finished",
                        ProgressPercentage = 100.0,
                        ServerSupportsResume = true,
                        ActiveConnections = 0,
                        IsCompleted = true
                    });
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
                    throw;
                }
                finally
                {
                    try { if (File.Exists(tempVideo)) File.Delete(tempVideo); } catch { }
                    try { if (File.Exists(tempAudio)) File.Delete(tempAudio); } catch { }
                }
            }

            var targetRep = chosenVideo ?? chosenAudio!;
            if (!targetRep.SegmentUrls.Any())
            {
                throw new InvalidOperationException("No media segments found in selected DASH representation.");
            }

            await DownloadAndConcatDashSegmentsAsync(targetRep, targetFilePath, cookies, progress, pauseToken, cancellationToken, "DASH").ConfigureAwait(false);
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

        private async Task<byte[]> GetOrDownloadKeyAsync(string keyUri, string? cookies, CancellationToken ct)
        {
            if (_keyCache.TryGetValue(keyUri, out var cachedKey))
            {
                return cachedKey;
            }

            var result = await _pipeline.ExecuteWithRetryAsync(() =>
            {
                var req = new HttpRequestMessage(HttpMethod.Get, new Uri(keyUri));
                if (!string.IsNullOrEmpty(cookies)) req.Headers.Add("Cookie", cookies);
                return req;
            }, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

            using var resp = result.Response;
            resp.EnsureSuccessStatusCode();
            byte[] keyBytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

            _keyCache[keyUri] = keyBytes;
            return keyBytes;
        }

        private async Task DownloadAndConcatHlsSegmentsAsync(
            List<HlsSegment> segments,
            string targetFilePath,
            string? cookies,
            IProgress<DownloadProgressInfo>? progress,
            PauseTokenSource? pauseToken,
            CancellationToken cancellationToken,
            string streamLabel)
        {
            // Deterministic staging directory for resume support
            string stagingDir = Path.Combine(Path.GetDirectoryName(targetFilePath) ?? Path.GetTempPath(), "." + Path.GetFileName(targetFilePath) + ".hls_segments");
            Directory.CreateDirectory(stagingDir);

            int total = segments.Count;
            int completed = 0;
            long totalBytesDownloaded = 0;
            var speedTracker = new SpeedTracker();

            var downloadedParts = new ConcurrentDictionary<int, string>();

            // 1. Scan for already completed segments to resume instantly
            for (int i = 0; i < total; i++)
            {
                string partPath = Path.Combine(stagingDir, $"seg_{i:D6}.part");
                if (File.Exists(partPath))
                {
                    long len = new FileInfo(partPath).Length;
                    if (len > 0)
                    {
                        downloadedParts[i] = partPath;
                        completed++;
                        totalBytesDownloaded += len;
                    }
                }
            }

            ReportHlsProgress(progress, completed, total, totalBytesDownloaded, speedTracker, streamLabel);

            try
            {
                // 2. Download fMP4 Initialization Segment if present
                string? firstInitUri = segments.FirstOrDefault(s => !string.IsNullOrEmpty(s.InitSegmentUri))?.InitSegmentUri;
                string? initPartPath = null;

                if (!string.IsNullOrEmpty(firstInitUri))
                {
                    initPartPath = Path.Combine(stagingDir, "init_header.part");
                    if (!File.Exists(initPartPath) || new FileInfo(initPartPath).Length == 0)
                    {
                        var initResult = await _pipeline.ExecuteWithRetryAsync(() =>
                        {
                            var req = new HttpRequestMessage(HttpMethod.Get, new Uri(firstInitUri));
                            if (!string.IsNullOrEmpty(cookies)) req.Headers.Add("Cookie", cookies);
                            return req;
                        }, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                        using (var resp = initResult.Response)
                        {
                            resp.EnsureSuccessStatusCode();
                            await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                            await using var fs = new FileStream(initPartPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                            await stream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }

                // 3. Download missing media segments concurrently with bounded parallelism
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 8,
                    CancellationToken = cancellationToken
                };

                var missingIndices = Enumerable.Range(0, total).Where(i => !downloadedParts.ContainsKey(i)).ToList();

                await Parallel.ForEachAsync(missingIndices, parallelOptions, async (index, ct) =>
                {
                    if (pauseToken != null && pauseToken.IsPaused)
                    {
                        await pauseToken.WaitIfPausedAsync().ConfigureAwait(false);
                    }

                    var seg = segments[index];
                    string partPath = Path.Combine(stagingDir, $"seg_{index:D6}.part");
                    string tmpPartPath = partPath + ".tmp";

                    var result = await _pipeline.ExecuteWithRetryAsync(() =>
                    {
                        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(seg.Uri));
                        if (!string.IsNullOrEmpty(cookies)) req.Headers.Add("Cookie", cookies);

                        if (seg.ByteRangeLength.HasValue && seg.ByteRangeOffset.HasValue)
                        {
                            long from = seg.ByteRangeOffset.Value;
                            long to = from + seg.ByteRangeLength.Value - 1;
                            req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(from, to);
                        }

                        return req;
                    }, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

                    using (var resp = result.Response)
                    {
                        resp.EnsureSuccessStatusCode();

                        if (seg.KeyMethod.Equals("AES-128", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(seg.KeyUri))
                        {
                            byte[] segmentBytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                            byte[] key = await GetOrDownloadKeyAsync(seg.KeyUri, cookies, ct).ConfigureAwait(false);
                            byte[] iv = seg.KeyIv ?? GenerateSequenceIv(seg.SequenceNumber);
                            byte[] decrypted = DecryptAes128(segmentBytes, key, iv);

                            await File.WriteAllBytesAsync(tmpPartPath, decrypted, ct).ConfigureAwait(false);
                            File.Move(tmpPartPath, partPath, true);
                            Interlocked.Add(ref totalBytesDownloaded, decrypted.Length);
                        }
                        else
                        {
                            // Direct streaming write for unencrypted segments
                            await using var netStream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                            await using (var fs = new FileStream(tmpPartPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                            {
                                await netStream.CopyToAsync(fs, ct).ConfigureAwait(false);
                            }
                            File.Move(tmpPartPath, partPath, true);
                            long len = new FileInfo(partPath).Length;
                            Interlocked.Add(ref totalBytesDownloaded, len);
                        }
                    }

                    downloadedParts[index] = partPath;
                    int cur = Interlocked.Increment(ref completed);

                    ReportHlsProgress(progress, cur, total, totalBytesDownloaded, speedTracker, streamLabel);
                }).ConfigureAwait(false);

                // 4. Concatenate initialization segment + all media segments into target file
                await using var outFs = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                if (!string.IsNullOrEmpty(initPartPath) && File.Exists(initPartPath))
                {
                    await using var initFs = File.OpenRead(initPartPath);
                    await initFs.CopyToAsync(outFs, cancellationToken).ConfigureAwait(false);
                }

                for (int i = 0; i < total; i++)
                {
                    if (downloadedParts.TryGetValue(i, out var partFile) && File.Exists(partFile))
                    {
                        await using var partFs = File.OpenRead(partFile);
                        await partFs.CopyToAsync(outFs, cancellationToken).ConfigureAwait(false);
                    }
                }
                await outFs.FlushAsync(cancellationToken).ConfigureAwait(false);

                // 5. Clean up staging directory on successful completion
                try
                {
                    if (Directory.Exists(stagingDir))
                    {
                        Directory.Delete(stagingDir, true);
                    }
                }
                catch { }

                ReportHlsProgress(progress, total, total, totalBytesDownloaded, speedTracker, streamLabel, isFinished: true);
            }
            catch (OperationCanceledException)
            {
                LoggingService.Log($"[HlsDashDownloadService] HLS download paused/cancelled. {completed}/{total} segments saved for resume.");
                throw;
            }
        }

        private async Task DownloadAndConcatDashSegmentsAsync(
            DashRepresentation rep,
            string targetFilePath,
            string? cookies,
            IProgress<DownloadProgressInfo>? progress,
            PauseTokenSource? pauseToken,
            CancellationToken cancellationToken,
            string streamLabel)
        {
            string stagingDir = Path.Combine(Path.GetDirectoryName(targetFilePath) ?? Path.GetTempPath(), "." + Path.GetFileName(targetFilePath) + ".dash_segments");
            Directory.CreateDirectory(stagingDir);

            var downloadedParts = new ConcurrentDictionary<int, string>();
            var segmentUrls = rep.SegmentUrls;
            int total = segmentUrls.Count;
            int completed = 0;
            long totalBytesDownloaded = 0;
            var speedTracker = new SpeedTracker();

            // 1. Scan for existing segments for instant resume
            for (int i = 0; i < total; i++)
            {
                string partPath = Path.Combine(stagingDir, $"seg_{i:D6}.part");
                if (File.Exists(partPath))
                {
                    long len = new FileInfo(partPath).Length;
                    if (len > 0)
                    {
                        downloadedParts[i] = partPath;
                        completed++;
                        totalBytesDownloaded += len;
                    }
                }
            }

            ReportHlsProgress(progress, completed, total, totalBytesDownloaded, speedTracker, streamLabel);

            try
            {
                // 2. Download DASH Initialization Segment if present
                string? initPartPath = null;
                if (!string.IsNullOrEmpty(rep.InitializationUrl))
                {
                    initPartPath = Path.Combine(stagingDir, "dash_init.part");
                    string tmpInitPath = initPartPath + ".tmp";
                    if (!File.Exists(initPartPath) || new FileInfo(initPartPath).Length == 0)
                    {
                        var initResult = await _pipeline.ExecuteWithRetryAsync(() =>
                        {
                            var req = new HttpRequestMessage(HttpMethod.Get, new Uri(rep.InitializationUrl));
                            if (!string.IsNullOrEmpty(cookies)) req.Headers.Add("Cookie", cookies);
                            return req;
                        }, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                        using (var resp = initResult.Response)
                        {
                            resp.EnsureSuccessStatusCode();
                            await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                            await using (var fs = new FileStream(tmpInitPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                            {
                                await stream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
                            }
                            File.Move(tmpInitPath, initPartPath, true);
                        }
                    }
                }

                // 3. Download missing segments concurrently with direct streaming writes
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 8,
                    CancellationToken = cancellationToken
                };

                var missingIndices = Enumerable.Range(0, total).Where(i => !downloadedParts.ContainsKey(i)).ToList();

                await Parallel.ForEachAsync(missingIndices, parallelOptions, async (index, ct) =>
                {
                    if (pauseToken != null && pauseToken.IsPaused)
                    {
                        await pauseToken.WaitIfPausedAsync().ConfigureAwait(false);
                    }

                    string segUrl = segmentUrls[index];
                    string partPath = Path.Combine(stagingDir, $"seg_{index:D6}.part");
                    string tmpPartPath = partPath + ".tmp";

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
                        await using (var fs = new FileStream(tmpPartPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                        {
                            await stream.CopyToAsync(fs, ct).ConfigureAwait(false);
                        }
                        File.Move(tmpPartPath, partPath, true);

                        long len = new FileInfo(partPath).Length;
                        Interlocked.Add(ref totalBytesDownloaded, len);
                    }

                    downloadedParts[index] = partPath;
                    int cur = Interlocked.Increment(ref completed);

                    ReportHlsProgress(progress, cur, total, totalBytesDownloaded, speedTracker, streamLabel);
                }).ConfigureAwait(false);

                // 4. Concatenate initialization + segments into target file
                await using var outFs = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                if (!string.IsNullOrEmpty(initPartPath) && File.Exists(initPartPath))
                {
                    await using var initFs = File.OpenRead(initPartPath);
                    await initFs.CopyToAsync(outFs, cancellationToken).ConfigureAwait(false);
                }

                for (int i = 0; i < total; i++)
                {
                    if (downloadedParts.TryGetValue(i, out var partFile) && File.Exists(partFile))
                    {
                        await using var partFs = File.OpenRead(partFile);
                        await partFs.CopyToAsync(outFs, cancellationToken).ConfigureAwait(false);
                    }
                }
                await outFs.FlushAsync(cancellationToken).ConfigureAwait(false);

                // 5. Clean up staging directory
                try
                {
                    if (Directory.Exists(stagingDir))
                    {
                        Directory.Delete(stagingDir, true);
                    }
                }
                catch { }

                ReportHlsProgress(progress, total, total, totalBytesDownloaded, speedTracker, streamLabel, isFinished: true);
            }
            catch (OperationCanceledException)
            {
                LoggingService.Log($"[HlsDashDownloadService] DASH download paused/cancelled. {completed}/{total} segments saved for resume.");
                throw;
            }
        }

        private static void ReportHlsProgress(
            IProgress<DownloadProgressInfo>? progress,
            int completed,
            int total,
            long bytesDownloaded,
            SpeedTracker speedTracker,
            string label,
            bool isFinished = false)
        {
            if (progress == null) return;

            double pct = total > 0 ? Math.Clamp(((double)completed / total) * 100.0, 0.0, 100.0) : 0.0;
            double speed = speedTracker.UpdateAndGetSpeed(bytesDownloaded);
            double remainingSeconds = (total > completed && speed > 0 && completed > 0)
                ? ((double)(total - completed) / completed) * ((double)bytesDownloaded / speed)
                : -1;

            string status = isFinished
                ? "Finished"
                : $"Downloading {label} Segments ({completed}/{total} - {pct:F1}%)";

            progress.Report(new DownloadProgressInfo
            {
                Status = status,
                ProgressPercentage = pct,
                BytesReceived = bytesDownloaded,
                TotalBytes = null,
                SpeedBytesPerSecond = isFinished ? 0 : speed,
                RemainingSeconds = isFinished ? 0 : remainingSeconds,
                ActiveConnections = isFinished ? 0 : 8,
                ServerSupportsResume = true,
                IsCompleted = isFinished || completed >= total
            });
        }

        private static HlsVariant SelectHlsVariant(List<HlsVariant> variants, string? qualityPreference)
        {
            if (!variants.Any()) throw new InvalidOperationException("No variants available.");

            if (string.IsNullOrWhiteSpace(qualityPreference) || qualityPreference.Equals("best", StringComparison.OrdinalIgnoreCase))
            {
                return variants.OrderByDescending(v => v.Height).ThenByDescending(v => v.Bandwidth).First();
            }

            if (int.TryParse(qualityPreference.Replace("p", "", StringComparison.OrdinalIgnoreCase), out int targetHeight))
            {
                var match = variants.Where(v => v.Height == targetHeight).OrderByDescending(v => v.Bandwidth).FirstOrDefault();
                if (match != null) return match;

                // Nearest resolution fallback
                return variants.OrderBy(v => Math.Abs(v.Height - targetHeight)).ThenByDescending(v => v.Bandwidth).First();
            }

            return variants.OrderByDescending(v => v.Height).ThenByDescending(v => v.Bandwidth).First();
        }

        private static DashRepresentation? SelectDashRepresentation(List<DashRepresentation> representations, string? qualityPreference)
        {
            if (!representations.Any()) return null;

            if (string.IsNullOrWhiteSpace(qualityPreference) || qualityPreference.Equals("best", StringComparison.OrdinalIgnoreCase))
            {
                return representations.OrderByDescending(v => v.Height).ThenByDescending(v => v.Bandwidth).First();
            }

            if (int.TryParse(qualityPreference.Replace("p", "", StringComparison.OrdinalIgnoreCase), out int targetHeight))
            {
                var match = representations.Where(v => v.Height == targetHeight).OrderByDescending(v => v.Bandwidth).FirstOrDefault();
                if (match != null) return match;

                return representations.OrderBy(v => Math.Abs(v.Height - targetHeight)).ThenByDescending(v => v.Bandwidth).First();
            }

            return representations.OrderByDescending(v => v.Height).ThenByDescending(v => v.Bandwidth).First();
        }

        private static byte[] DecryptAes128(byte[] cipherText, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
        }

        private static byte[] GenerateSequenceIv(long sequenceNumber)
        {
            byte[] iv = new byte[16];
            byte[] seqBytes = BitConverter.GetBytes(sequenceNumber);
            if (BitConverter.IsLittleEndian) Array.Reverse(seqBytes);
            Array.Copy(seqBytes, 0, iv, 16 - seqBytes.Length, seqBytes.Length);
            return iv;
        }
    }
}


