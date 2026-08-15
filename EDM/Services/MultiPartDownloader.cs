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
    public record ChunkProgressInfo(int Index, long Downloaded, long TotalBytes, bool IsActive);

    public record DownloadProgress(long TotalBytes, long BytesDownloaded, ConcurrentDictionary<int, ChunkProgressInfo>? ChunkStats = null)
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

        private static readonly object _sharedHeaderLock = new();
        private static HttpClient? _sharedHeadersConfiguredFor;

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
            var controller = new AdaptiveConnectionController(chunkCount, 2, maxConcurrency);

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

                        var segment = scheduler.GetNextWorkItem(workerId);
                        if (segment == null)
                        {
                            await Task.Delay(100, fallbackCts.Token).ConfigureAwait(false);
                            continue;
                        }

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
                                progress != null ? new Progress<DownloadProgressInfo>(_ => { }) : null,
                                () => ThrottleKbps > 0 ? ThrottleKbps * 1024.0 : 0,
                                Credentials,
                                Cookies,
                                fallbackCts.Token).ConfigureAwait(false);

                            chunkStatsMap[segment.Id] = new ChunkProgressInfo(segment.Id, segment.TotalBytes, segment.TotalBytes, false);

                            long downloadedAll = scheduler.GetTotalBytesDownloaded();
                            progress?.Report(new DownloadProgress(totalBytes, downloadedAll, chunkStatsMap));
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

            // Runtime Telemetry & Adaptive Controller Feedback Loop
            var adaptiveLoop = Task.Run(async () =>
            {
                long lastDownloadedBytes = scheduler.GetTotalBytesDownloaded();
                DateTime lastTime = DateTime.UtcNow;

                while (!scheduler.IsFullyCompleted() && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);

                    long currentDownloadedBytes = scheduler.GetTotalBytesDownloaded();
                    DateTime currentTime = DateTime.UtcNow;
                    double elapsedSec = (currentTime - lastTime).TotalSeconds;

                    if (elapsedSec > 0)
                    {
                        double currentBps = (currentDownloadedBytes - lastDownloadedBytes) / elapsedSec;
                        int recentErrors = Interlocked.Exchange(ref errorCounter, 0);

                        controller.RecordTelemetry(currentBps, 50.0, recentErrors);
                        int evaluatedCount = controller.EvaluateConnectionCount(totalBytes, false);

                        lock (workerTasks)
                        {
                            while (workerTasks.Count < evaluatedCount && !scheduler.IsFullyCompleted())
                            {
                                int newId = workerTasks.Count;
                                workerTasks.Add(createWorkerTask($"Worker_{newId}"));
                            }
                        }

                        lastDownloadedBytes = currentDownloadedBytes;
                        lastTime = currentTime;
                    }
                }
            }, cancellationToken);

            // Wait for all workers (they use fallbackCts.Token so they stop on range fallback)
            try { await Task.WhenAll(workerTasks).ConfigureAwait(false); }
            catch (OperationCanceledException) when (rangeFallbackTriggered) { /* expected — fallback triggered */ }
            try { await adaptiveLoop.ConfigureAwait(false); } catch { }

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

            progress?.Report(new DownloadProgress(totalBytes, totalBytes, chunkStatsMap));
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
            byte[] mergeBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(128 * 1024);
            try
            {
                using (var destStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    foreach (var chunk in chunkFiles)
                    {
                        if (File.Exists(chunk))
                        {
                            using var partStream = new FileStream(chunk, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                            int bytesRead;
                            while ((bytesRead = await partStream.ReadAsync(mergeBuffer.AsMemory(0, mergeBuffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                            {
                                await destStream.WriteAsync(mergeBuffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                    await destStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(mergeBuffer);
            }

            // Run verification using IntegrityVerificationService
            var verifier = new IntegrityVerificationService();
            var mergeVerification = await verifier.VerifyAsync(destinationFilePath, metaState, expectedHash, metaState?.TotalBytes, cancellationToken).ConfigureAwait(false);

            if (mergeVerification.State == Models.VerificationState.VerificationFailed)
            {
                // Preserve old behavior for expectedHash mismatch by throwing
                if (!string.IsNullOrEmpty(expectedHash))
                {
                    throw new InvalidDataException($"Merged file verification failed: {mergeVerification.Message}");
                }
            }

            return mergeVerification;
        }

        private async Task<EDM.Models.VerificationResult> DownloadSingleAsync(Uri uri, string destinationPath, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken, long? expectedSize = null)
        {
            var result = await _pipeline.ExecuteWithRetryAsync(
                requestFactory: () => _pipeline.CreateFreshRequest(HttpMethod.Get, uri, credentials: Credentials, cookies: Cookies),
                completionOption: HttpCompletionOption.ResponseHeadersRead,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            using var resp = result.Response;
            long? total = resp.Content.Headers.ContentLength;
            long downloaded = 0;

            using var contentStream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);

            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                int read;
                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
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

                    progress?.Report(new DownloadProgress(total ?? -1, downloaded));
                }
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }

            await fs.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Run final verification for single-file downloads
            var verifier = new IntegrityVerificationService();
            var verification = await verifier.VerifyAsync(destinationPath, null, null, expectedSize ?? total, cancellationToken).ConfigureAwait(false);
            if (verification.State == Models.VerificationState.VerificationFailed)
            {
                throw new InvalidDataException($"Final file verification failed: {verification.Message}");
            }
            return verification;
        }
    }
}