using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services.Helpers;

namespace EDM.Services
{
    public record ChunkProgressInfo(int Index, long Downloaded, long TotalBytes, bool IsActive, double SpeedBytesPerSec = 0);

    public record DownloadProgress(
        long TotalBytes, 
        long BytesDownloaded, 
        ConcurrentDictionary<int, ChunkProgressInfo>? ChunkStats = null,
        ConnectionTelemetrySnapshot? Telemetry = null)
    {
        public double Percentage => TotalBytes > 0 ? (BytesDownloaded / (double)TotalBytes) * 100.0 : 0.0;
    }

    internal class ChunkMetadata
    {
        public int Index { get; set; }
        public long Start { get; set; }
        public long End { get; set; }
        public string TempPath { get; set; } = string.Empty;
        public long Downloaded { get; set; }
        public bool Completed { get; set; }
    }

    internal class DownloadMetadata
    {
        public string Url { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public long TotalBytes { get; set; }
        public int ChunkCount { get; set; }
        public List<ChunkMetadata> Chunks { get; set; } = new List<ChunkMetadata>();
    }

    public class MultiPartDownloader : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly bool _disposeClient;
        private readonly HttpRequestPipeline _pipeline;
        private readonly DurableMetadataManager _metaManager = new();

        private volatile bool _isPaused;
        private TaskCompletionSource<bool>? _resumeTcs;

        public int ThrottleKbps { get; set; } = 0;

        public DownloadCredentials? Credentials { get; set; }
        public string? Cookies { get; set; }

        public MultiPartDownloader(HttpClient? httpClient = null)
        {
            if (httpClient is null)
            {
                _httpClient = Services.SharedHttpClient.Instance;
                _disposeClient = false;
            }
            else
            {
                _httpClient = httpClient;
                _disposeClient = false;
            }

            _pipeline = new HttpRequestPipeline(_httpClient);
        }

        public void Dispose()
        {
            if (_disposeClient) _httpClient.Dispose();
        }


        public void Pause()
        {
            if (_isPaused) return;
            _isPaused = true;
            _resumeTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void Resume()
        {
            if (!_isPaused) return;
            _isPaused = false;
            try { _resumeTcs?.TrySetResult(true); }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"[MultiPartDownloader.Resume] Failed to set resume result: {ex.Message}");
            }
            _resumeTcs = null;
        }

        private async Task WaitIfPausedAsync(CancellationToken ct)
        {
            while (_isPaused)
            {
                var tcs = _resumeTcs;
                if (tcs != null)
                {
                    using (ct.Register(() => tcs.TrySetCanceled()))
                    {
                        await tcs.Task.ConfigureAwait(false);
                    }
                }
                else
                {
                    await Task.Delay(200, ct).ConfigureAwait(false);
                }
            }
        }

        public async Task DownloadFileAsync(
            Uri fileUrl,
            string destinationFilePath,
            int chunkCount = 16,
            int maxConcurrency = 16,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            long historyId = -1; // history entry id if created

            if (chunkCount <= 0) throw new ArgumentOutOfRangeException(nameof(chunkCount));
            if (maxConcurrency <= 0) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));

            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath) ?? ".");
            string tempDir = Path.Combine(Path.GetDirectoryName(destinationFilePath) ?? ".", ".tmp_" + Path.GetFileName(destinationFilePath));
            Directory.CreateDirectory(tempDir);
            string metaPath = Path.Combine(tempDir, "metadata.json");

            // Execute HEAD request using fresh request pipeline
            var headResult = await _pipeline.ExecuteWithRetryAsync(
                requestFactory: () => _pipeline.CreateFreshRequest(HttpMethod.Head, fileUrl, credentials: Credentials, cookies: Cookies),
                completionOption: HttpCompletionOption.ResponseHeadersRead,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            using var headResponse = headResult.Response;
            if (headResponse.RequestMessage?.RequestUri != null)
            {
                fileUrl = headResponse.RequestMessage.RequestUri;
            }
            long? contentLength = headResponse.Content.Headers.ContentLength;
            string? remoteETag = headResponse.Headers.ETag?.Tag;
            string? remoteLastModified = headResponse.Content.Headers.LastModified?.ToString();


            bool supportsRanges = false;
            if (headResponse.Headers.TryGetValues("Accept-Ranges", out var acceptRanges))
            {
                supportsRanges = acceptRanges.Any(v => v.Contains("bytes", StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                // Test range probe using fresh request
                try
                {
                    var probeResult = await _pipeline.ExecuteWithRetryAsync(
                        requestFactory: () => _pipeline.CreateFreshRequest(HttpMethod.Get, fileUrl, rangeStart: 0, rangeEnd: 0, credentials: Credentials, cookies: Cookies),
                        completionOption: HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken: cancellationToken,
                        maxRetries: 1
                    ).ConfigureAwait(false);

                    using var testResp = probeResult.Response;
                    supportsRanges = testResp.StatusCode == HttpStatusCode.PartialContent;
                }
                catch
                {
                    supportsRanges = false;
                }
            }

            if (!contentLength.HasValue || !supportsRanges || chunkCount == 1)
            {
                try { historyId = Services.History.DownloadHistoryRecorder.CreateEntry(fileUrl.ToString(), destinationFilePath, contentLength ?? -1); } catch { }
                var singleVerification = await DownloadSingleAsync(fileUrl, destinationFilePath, progress, cancellationToken, expectedSize: contentLength).ConfigureAwait(false);
                if (historyId > 0)
                {
                    Services.History.DownloadHistoryRecorder.RecordVerification(historyId, singleVerification.State, singleVerification.Algorithm, singleVerification.ExpectedHashHex, singleVerification.ComputedHashHex, singleVerification.Message);
                    if (singleVerification.State == Models.VerificationState.Verified)
                    {
                        Services.History.DownloadHistoryRecorder.MarkCompleted(historyId);
                    }
                }
                if (singleVerification.State == Models.VerificationState.VerificationFailed)
                {
                    throw new InvalidDataException($"Final file verification failed: {singleVerification.Message}");
                }
                return;
            }

            long totalBytes = contentLength.Value;
            var scheduler = new SegmentScheduler(totalBytes);
            int recommendedStart = ServerCapabilityCache.Instance.GetRecommendedInitialConnections(fileUrl, totalBytes, maxConcurrency);
            int initialConns = Math.Min(chunkCount, Math.Max(2, recommendedStart));
            var controller = new AdaptiveConnectionController(initialConns, 2, maxConcurrency);
            var speedTracker = new MonotonicSpeedTracker();

            // Durable Metadata Recovery
            var metaState = await _metaManager.ReadStateAsync(metaPath, cancellationToken).ConfigureAwait(false);
            bool isResumeValid = false;

            if (metaState != null && metaState.TotalBytes == totalBytes)
            {
                isResumeValid = _metaManager.ReconcileAndValidate(metaState, remoteETag ?? "", remoteLastModified ?? "");
            }

            if (isResumeValid && metaState != null && metaState.Segments.Count > 0)
            {
                scheduler.InitializeFromState(metaState.Segments);
            }
            else
            {
                scheduler.InitializeDefault(chunkCount);
                metaState = new DurableDownloadState
                {
                    Url = fileUrl.ToString(),
                    DestinationPath = destinationFilePath,
                    TotalBytes = totalBytes,
                    ServerSupportsRanges = true,
                    ETag = remoteETag,
                    LastModified = remoteLastModified,
                    Segments = scheduler.GetSegmentsSnapshot()
                };

                for (int i = 0; i < metaState.Segments.Count; i++)
                {
                    metaState.Segments[i].TempPath = Path.Combine(tempDir, $"segment_{i}.part");
                }
                scheduler.InitializeFromState(metaState.Segments);
                await _metaManager.WriteStateAtomicAsync(metaPath, metaState, cancellationToken).ConfigureAwait(false);
            }

            // Preflight Disk Space Verification
            DiskSpaceGovernor.EnsureAvailableSpaceOrThrow(destinationFilePath, totalBytes);

            // High-Speed Disk Space Pre-Allocation: Reserve file clusters beforehand to avoid disk fragmentation
            try
            {
                if (totalBytes > 0)
                {
                    string? destDir = Path.GetDirectoryName(destinationFilePath);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    using var preAllocFs = new FileStream(destinationFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.None);
                    if (preAllocFs.Length < totalBytes)
                    {
                        preAllocFs.SetLength(totalBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"[MultiPartDownloader] Disk pre-allocation skipped: {ex.Message}");
            }

            var chunkStatsMap = new ConcurrentDictionary<int, ChunkProgressInfo>();
            var accountant = new ConnectionAccountant(maxConcurrency);
            accountant.SetRequestedConnections(chunkCount);

            // Active runtime adaptive connection scaling pool
            int targetConnections = controller.CurrentConnections;
            var workerTasks = new List<Task>();
            var exceptions = new List<Exception>();
            int activeWorkerCounter = 0;
            int errorCounter = 0;

            // BUG-FIX 3: Use a CancellationTokenSource to signal all workers when any one of them
            // receives a RangeFallbackRequiredException. This ensures the 200 fallback decision is
            // made ONCE and workers don't race to write full-file downloads into segment files.
            using var fallbackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            bool rangeFallbackTriggered = false;

            Func<string, Task> createWorkerTask = (workerId) => Task.Run(async () =>
            {
                Interlocked.Increment(ref activeWorkerCounter);
                try
                {
                    var worker = new SegmentWorker(workerId, _httpClient);

                    while (!scheduler.IsFullyCompleted())
                    {
                        fallbackCts.Token.ThrowIfCancellationRequested();
                        await WaitIfPausedAsync(fallbackCts.Token).ConfigureAwait(false);

                        // Check if active worker pool exceeds target controller connections
                        if (activeWorkerCounter > controller.CurrentConnections && scheduler.GetSegmentsSnapshot().Count(s => s.State == SegmentState.Downloading) > controller.CurrentConnections)
                        {
                            break; // Controlled worker removal
                        }

                        accountant.OnWorkerIdle();
                        var segment = scheduler.GetNextWorkItem(workerId);
                        if (segment == null)
                        {
                            await Task.Delay(100, fallbackCts.Token).ConfigureAwait(false);
                            continue;
                        }
                        accountant.OnWorkerBusy();

                        if (string.IsNullOrEmpty(segment.TempPath))
                        {
                            segment.TempPath = Path.Combine(tempDir, $"segment_{segment.Id}.part");
                        }
                        scheduler.UpdateTempPath(segment.Id, segment.TempPath);

                        // Atomically persist snapshot so metadata file on disk reflects dynamic split ranges
                        metaState.Segments = scheduler.GetSegmentsSnapshot();
                        await _metaManager.WriteStateAtomicAsync(metaPath, metaState, fallbackCts.Token).ConfigureAwait(false);

                        try
                        {
                            chunkStatsMap[segment.Id] = new ChunkProgressInfo(segment.Id, segment.BytesDownloaded, segment.TotalBytes, true);

                            await worker.ExecuteSegmentDownloadAsync(
                                fileUrl,
                                segment,
                                metaPath,
                                _metaManager,
                                metaState,
                                scheduler,
                                null,
                                () => ThrottleKbps > 0 ? ThrottleKbps * 1024.0 : 0,
                                Credentials,
                                Cookies,
                                fallbackCts.Token,
                                accountant).ConfigureAwait(false);

                            chunkStatsMap[segment.Id] = new ChunkProgressInfo(segment.Id, segment.TotalBytes, segment.TotalBytes, false);

                            long downloadedAll = scheduler.GetTotalBytesDownloaded();
                            var segSnap = scheduler.GetSegmentsSnapshot();
                            int qSegs = segSnap.Count(s => s.State == SegmentState.Pending);
                            int rSegs = segSnap.Count(s => s.State == SegmentState.Downloading);
                            int cSegs = segSnap.Count(s => s.State == SegmentState.Completed);
                            progress?.Report(new DownloadProgress(totalBytes, downloadedAll, chunkStatsMap, accountant.GetSnapshot(qSegs, rSegs, cSegs)));
                        }
                        catch (RangeFallbackRequiredException rfx)
                        {
                            // BUG-FIX 3: Server does not support ranges. Signal ALL other workers to stop
                            // before falling back to a single-stream download. This prevents multiple
                            // workers from independently writing full-file content into segment files.
                            lock (exceptions)
                            {
                                if (!rangeFallbackTriggered)
                                {
                                    rangeFallbackTriggered = true;
                                    LoggingService.LogWarning($"[MultiPartDownloader] Range fallback triggered by {workerId}: {rfx.Message}");
                                    try { fallbackCts.Cancel(); } catch { }
                                }
                            }
                            return;
                        }
                        catch (OperationCanceledException)
                        {
                            // Preserve cancellation semantics: do not convert cancellation into generic failure
                            throw;
                        }
                        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            throw;
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref errorCounter);
                            segment.RetryCount++;
                            if (segment.RetryCount <= 8 && !fallbackCts.IsCancellationRequested)
                            {
                                scheduler.MarkFailed(segment.Id, requeue: true);
                                await Task.Delay(Math.Min(1000, 100 * segment.RetryCount), fallbackCts.Token).ConfigureAwait(false);
                                continue;
                            }

                            scheduler.MarkFailed(segment.Id, requeue: false);
                            lock (exceptions) { exceptions.Add(ex); }
                            break;
                        }
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref activeWorkerCounter);
                    accountant.OnWorkerBusy();
                }
            }, fallbackCts.Token);

            // Spawn initial worker pool
            lock (workerTasks)
            {
                for (int w = 0; w < targetConnections; w++)
                {
                    workerTasks.Add(createWorkerTask($"Worker_{w}"));
                }
            }

            // High-frequency live segment telemetry reporting heartbeat loop (100ms interval)
            var lastSegSamples = new Dictionary<int, (long Bytes, DateTime Time)>();
            var telemetryReportingLoop = Task.Run(async () =>
            {
                while (!scheduler.IsFullyCompleted() && !fallbackCts.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(100, fallbackCts.Token).ConfigureAwait(false);

                        var now = DateTime.UtcNow;
                        var segments = scheduler.GetSegmentsSnapshot();
                        foreach (var seg in segments)
                        {
                            bool isActive = seg.State == SegmentState.Downloading;
                            double segSpeed = 0;
                            if (lastSegSamples.TryGetValue(seg.Id, out var lastSample))
                            {
                                double dt = (now - lastSample.Time).TotalSeconds;
                                if (dt > 0.04)
                                {
                                    segSpeed = Math.Max(0, (seg.BytesDownloaded - lastSample.Bytes) / dt);
                                }
                            }
                            lastSegSamples[seg.Id] = (seg.BytesDownloaded, now);

                            chunkStatsMap[seg.Id] = new ChunkProgressInfo(seg.Id, seg.BytesDownloaded, seg.TotalBytes, isActive, segSpeed);
                        }

                        long downloadedAll = scheduler.GetTotalBytesDownloaded();
                        int qSegs = segments.Count(s => s.State == SegmentState.Pending);
                        int rSegs = segments.Count(s => s.State == SegmentState.Downloading);
                        int cSegs = segments.Count(s => s.State == SegmentState.Completed);
                        progress?.Report(new DownloadProgress(totalBytes, downloadedAll, chunkStatsMap, accountant.GetSnapshot(qSegs, rSegs, cSegs)));
                    }
                    catch (OperationCanceledException) { break; }
                    catch { }
                }
            }, fallbackCts.Token);

            // Runtime Telemetry & Adaptive Controller Feedback Loop with REAL measured RTT & Monotonic Speed
            var adaptiveLoop = Task.Run(async () =>
            {
                while (!scheduler.IsFullyCompleted() && !cancellationToken.IsCancellationRequested && !fallbackCts.IsCancellationRequested)
                {
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);

                    long currentDownloadedBytes = scheduler.GetTotalBytesDownloaded();
                    speedTracker.RecordProgress(currentDownloadedBytes);

                    double currentBps = speedTracker.RollingSpeedBps;
                    int recentErrors = Interlocked.Exchange(ref errorCounter, 0);

                    // Use REAL authoritative measured RTT from actual HTTP connections (never fake 50ms)
                    double measuredRtt = accountant.MeasuredRttMs > 0 ? accountant.MeasuredRttMs : 0;
                    double ttfb = accountant.TimeToFirstByteMs > 0 ? accountant.TimeToFirstByteMs : 0;
                    controller.RecordTelemetry(currentBps, measuredRtt, recentErrors, ttfbMs: ttfb);
                    int evaluatedCount = controller.EvaluateConnectionCount(totalBytes, false);

                    accountant.SetRequestedConnections(evaluatedCount);

                    lock (workerTasks)
                    {
                        while (workerTasks.Count < evaluatedCount && !scheduler.IsFullyCompleted())
                        {
                            int newId = workerTasks.Count;
                            workerTasks.Add(createWorkerTask($"Worker_{newId}"));
                        }
                    }
                }
            }, cancellationToken);

            // Wait for all workers (they use fallbackCts.Token so they stop on range fallback)
            try { await Task.WhenAll(workerTasks).ConfigureAwait(false); }
            catch (OperationCanceledException) when (rangeFallbackTriggered) { /* expected — fallback triggered */ }
            try { await adaptiveLoop.ConfigureAwait(false); } catch { }
            try { await telemetryReportingLoop.ConfigureAwait(false); } catch { }

            // Final 100% authoritative progress report & server capability learning
            if (scheduler.IsFullyCompleted())
            {
                long finalDownloaded = scheduler.GetTotalBytesDownloaded();
                speedTracker.RecordProgress(finalDownloaded);
                var finalSegments = scheduler.GetSegmentsSnapshot();
                foreach (var seg in finalSegments)
                {
                    chunkStatsMap[seg.Id] = new ChunkProgressInfo(seg.Id, seg.TotalBytes, seg.TotalBytes, false);
                }
                progress?.Report(new DownloadProgress(totalBytes, finalDownloaded, chunkStatsMap, accountant.GetSnapshot(0, 0, finalSegments.Count)));

                // Learn optimal host performance in ServerCapabilityCache
                ServerCapabilityCache.Instance.RecordResponse(
                    fileUrl,
                    HttpStatusCode.OK,
                    accountant.MeasuredRttMs,
                    speedTracker.AverageSpeedBps,
                    supportsRange: true,
                    activeConnections: controller.OptimalObservedConnections);
            }

            // BUG-FIX 3: Safe 200 fallback — all workers have now stopped cleanly.
            // Run single-stream download only after the multi-worker pool has fully exited.
            if (rangeFallbackTriggered)
            {
                LoggingService.Log("[MultiPartDownloader] All workers stopped. Falling back to single-stream download.");
                await DownloadSingleAsync(fileUrl, destinationFilePath, progress, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (exceptions.Any() && !scheduler.IsFullyCompleted())
            {
                throw new AggregateException("One or more segment workers failed.", exceptions);
            }

            // Verify 100% byte range coverage before merging
            if (!scheduler.ValidateCoverage())
            {
                var snap = scheduler.GetSegmentsSnapshot();
                string dump = string.Join("; ", snap.Select(s => $"[Id={s.Id}: {s.Start}-{s.End}]"));
                throw new InvalidDataException($"Segment scheduler coverage validation failed. Segments: {dump}");
            }


            // Merge & Cleanup
            var sortedSegments = scheduler.GetSegmentsSnapshot().OrderBy(s => s.Start).Select(s => s.TempPath);
            var verification = await MergeFilesAsync(sortedSegments, destinationFilePath, cancellationToken, metaState).ConfigureAwait(false);

                        // Record verification into history if available
                        try
                        {
                            // If verification failed, surface as exception to caller
                            if (verification.State == Models.VerificationState.VerificationFailed)
                            {
                                throw new InvalidDataException($"Final file verification failed: {verification.Message}");
                            }

                            // Map verification metadata to DownloadHistory if a history entry exists
                            // History recording is performed by the orchestrator at a higher level; callers may hook into DownloadHistoryRecorder as needed.
                        }
                        catch
                        {
                            throw;
                        }

            // Cleanup temp files using fail-safe helper
            await FileDeleteHelper.DeleteFileSafeAsync(metaPath, cancellationToken: cancellationToken).ConfigureAwait(false);
            foreach (var segPath in sortedSegments)
            {
                await FileDeleteHelper.DeleteFileSafeAsync(segPath, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            try { Directory.Delete(tempDir, true); } catch { }

            progress?.Report(new DownloadProgress(totalBytes, totalBytes, chunkStatsMap, accountant.GetSnapshot(0, 0, sortedSegments.Count())));
        }

        internal static IEnumerable<(long Start, long End)> CalculateRanges(long totalBytes, int chunkCount)
        {
            var ranges = new List<(long Start, long End)>();
            long baseSize = totalBytes / chunkCount;
            long remainder = totalBytes % chunkCount;
            long offset = 0;

            for (int i = 0; i < chunkCount; i++)
            {
                long thisSize = baseSize + (i < remainder ? 1 : 0);
                long start = offset;
                long end = offset + thisSize - 1;
                ranges.Add((start, end));
                offset += thisSize;
            }

            return ranges;
        }

        internal async Task<EDM.Models.VerificationResult> MergeFilesAsync(IEnumerable<string> chunkFiles, string destinationFilePath, CancellationToken cancellationToken, DurableDownloadState? metaState = null, string? expectedHash = null)
        {
            string tempMergePath = destinationFilePath + ".merging";
            byte[] mergeBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(256 * 1024);
            try
            {
                using (var destStream = new FileStream(tempMergePath, FileMode.Create, FileAccess.Write, FileShare.None, 256 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    foreach (var chunk in chunkFiles)
                    {
                        if (File.Exists(chunk))
                        {
                            using var partStream = new FileStream(chunk, FileMode.Open, FileAccess.Read, FileShare.Read, 256 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                            int bytesRead;
                            while ((bytesRead = await partStream.ReadAsync(mergeBuffer.AsMemory(0, mergeBuffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                            {
                                await destStream.WriteAsync(mergeBuffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                    await destStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                // Run verification on assembled temporary file before atomic rename
                var verifier = new IntegrityVerificationService();
                var mergeVerification = await verifier.VerifyAsync(tempMergePath, metaState, expectedHash, metaState?.TotalBytes, cancellationToken).ConfigureAwait(false);

                if (mergeVerification.State == Models.VerificationState.VerificationFailed)
                {
                    try { File.Delete(tempMergePath); } catch { }
                    if (!string.IsNullOrEmpty(expectedHash))
                    {
                        throw new InvalidDataException($"Merged file verification failed: {mergeVerification.Message}");
                    }
                }

                // Atomic Finalization: Atomically replace the destination file
                File.Move(tempMergePath, destinationFilePath, overwrite: true);
                return mergeVerification;
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(mergeBuffer);
                if (File.Exists(tempMergePath))
                {
                    try { File.Delete(tempMergePath); } catch { }
                }
            }
        }

        private async Task<EDM.Models.VerificationResult> DownloadSingleAsync(Uri uri, string destinationPath, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken, long? expectedSize = null)
        {
            string tempSinglePath = destinationPath + ".tmpdl";
            long downloaded = 0;
            long? total = expectedSize;
            int maxAttempts = 5;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    long startByte = downloaded;
                    bool isResume = startByte > 0;

                    var result = await _pipeline.ExecuteWithRetryAsync(
                        requestFactory: () => _pipeline.CreateFreshRequest(
                            HttpMethod.Get, 
                            uri, 
                            rangeStart: isResume ? startByte : null, 
                            credentials: Credentials, 
                            cookies: Cookies),
                        completionOption: HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken: cancellationToken
                    ).ConfigureAwait(false);

                    using var resp = result.Response;
                    if (!total.HasValue)
                    {
                        total = resp.Content.Headers.ContentLength;
                        if (isResume && resp.StatusCode == HttpStatusCode.PartialContent && resp.Content.Headers.ContentRange?.Length.HasValue == true)
                        {
                            total = resp.Content.Headers.ContentRange.Length.Value;
                        }
                    }

                    // If server returned 200 OK when we requested a resume range, server restarted from 0
                    if (isResume && resp.StatusCode == HttpStatusCode.OK)
                    {
                        downloaded = 0;
                    }

                    FileMode fileMode = (downloaded > 0) ? FileMode.Append : FileMode.Create;
                    using (var contentStream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                    using (var fs = new FileStream(tempSinglePath, fileMode, FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                    {
                        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(128 * 1024);
                        long lastReportTicks = 0;
                        try
                        {
                            int read;
                            while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                            {
                                await WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
                                await fs.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                                downloaded += read;

                                if (ThrottleKbps > 0)
                                {
                                    double delayMs = (read / 1024.0) / ThrottleKbps * 1000;
                                    if (delayMs > 0)
                                    {
                                        await Task.Delay((int)Math.Max(1, delayMs), cancellationToken).ConfigureAwait(false);
                                    }
                                }

                                long nowTicks = Environment.TickCount64;
                                if (nowTicks - lastReportTicks >= 100 || (total.HasValue && downloaded >= total.Value))
                                {
                                    progress?.Report(new DownloadProgress(total ?? -1, downloaded));
                                    lastReportTicks = nowTicks;
                                }
                            }

                            progress?.Report(new DownloadProgress(total ?? -1, downloaded));
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                        }

                        await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }

                    // Successfully completed stream without error
                    break;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested && (ex is IOException or HttpRequestException or System.Net.Sockets.SocketException) && attempt < maxAttempts)
                {
                    LoggingService.LogWarning($"[MultiPartDownloader.DownloadSingleAsync] Transient socket/IO exception on attempt {attempt}/{maxAttempts}: {ex.Message}. Retrying...");
                    await Task.Delay(100 * attempt, cancellationToken).ConfigureAwait(false);
                }
            }

            // Run final verification for single-file downloads before atomic rename
            var verifier = new IntegrityVerificationService();
            var verification = await verifier.VerifyAsync(tempSinglePath, null, null, expectedSize ?? total, cancellationToken).ConfigureAwait(false);
            if (verification.State != Models.VerificationState.VerificationFailed)
            {
                File.Move(tempSinglePath, destinationPath, overwrite: true);
            }
            else
            {
                try { File.Delete(tempSinglePath); } catch { }
                throw new InvalidDataException($"Final file verification failed: {verification.Message}");
            }
            return verification;
        }
    }
}