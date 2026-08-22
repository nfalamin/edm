using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;

namespace EDM.Domain.Protocols
{
    public sealed class DynamicChunk
    {
        private readonly object _splitLock = new();
        public int Id { get; init; }
        public long StartOffset { get; set; }
        public long EndOffset { get; set; }
        private long _bytesDownloaded;
        private string _state = "Idle";
        private double _currentSpeedBps;
        private long _lastSpeedTimestamp;
        private long _lastSpeedBytes;
        private DateTime _stateStartTime = DateTime.UtcNow;

        public DynamicChunk(int id, long startOffset, long endOffset, long bytesDownloaded = 0)
        {
            Id = id;
            StartOffset = startOffset;
            EndOffset = endOffset;
            _bytesDownloaded = bytesDownloaded;
            _lastSpeedTimestamp = Stopwatch.GetTimestamp();
            _lastSpeedBytes = bytesDownloaded;
        }

        public long BytesDownloaded => Interlocked.Read(ref _bytesDownloaded);
        public string State { get => Volatile.Read(ref _state); set { Volatile.Write(ref _state, value); _stateStartTime = DateTime.UtcNow; } }
        public double CurrentSpeedBps => Volatile.Read(ref _currentSpeedBps);
        public DateTime StateStartTime => _stateStartTime;

        public void AddDownloadedBytes(long count)
        {
            Interlocked.Add(ref _bytesDownloaded, count);
        }

        public void UpdateSpeed()
        {
            long now = Stopwatch.GetTimestamp();
            long prevTime = Interlocked.Exchange(ref _lastSpeedTimestamp, now);
            double dt = (now - prevTime) / (double)Stopwatch.Frequency;
            if (dt >= 0.05)
            {
                long curBytes = BytesDownloaded;
                long prevBytes = Interlocked.Exchange(ref _lastSpeedBytes, curBytes);
                double instSpeed = Math.Max(0, (curBytes - prevBytes) / dt);
                double oldSpeed = _currentSpeedBps;
                double smooth = oldSpeed <= 0 ? instSpeed : (0.35 * instSpeed + 0.65 * oldSpeed);
                Volatile.Write(ref _currentSpeedBps, smooth);
            }
        }

        public long CurrentOffset => StartOffset + BytesDownloaded;
        
        public long RemainingBytes
        {
            get
            {
                lock (_splitLock)
                {
                    return Math.Max(0, (EndOffset - StartOffset + 1) - BytesDownloaded);
                }
            }
        }

        public bool IsComplete
        {
            get
            {
                lock (_splitLock)
                {
                    return EndOffset > 0 && RemainingBytes == 0;
                }
            }
        }

        /// <summary>
        /// Work-stealing: thread-safely splits this chunk into two if it has enough remaining bytes.
        /// </summary>
        public DynamicChunk? TrySplit(int newChunkId, long minSplitThreshold = 512 * 1024)
        {
            lock (_splitLock)
            {
                long remaining = Math.Max(0, (EndOffset - StartOffset + 1) - BytesDownloaded);
                if (remaining < minSplitThreshold * 2) return null;

                long midPoint = CurrentOffset + (remaining / 2);
                if (midPoint <= CurrentOffset || midPoint >= EndOffset) return null;

                long oldEnd = EndOffset;

                // Shorten this chunk
                EndOffset = midPoint - 1;

                // Create new chunk for stolen upper half
                return new DynamicChunk(newChunkId, midPoint, oldEnd, 0);
            }
        }
    }

    /// <summary>
    /// High-Performance Ultra Multi-Thread HTTP/HTTPS Engine.
    /// Features:
    /// - 32-64 parallel connection multiplexing with SocketsHttpHandler (DecompressionMethods.None, MaxConnectionsPerServer=128).
    /// - Zero-lock Direct RandomAccess.WriteAsync concurrent disk writing with pre-allocated file size.
    /// - Zero-Allocation ArrayPool 128KB memory buffer recycling.
    /// - Dynamic Work-Stealing & 10th-percentile slow-worker detection and connection renegotiation.
    /// - Real-time per-thread diagnostics (state, throughput, downloaded/remaining bytes).
    /// </summary>
    public sealed class HttpMultiPartEngine : IDownloadEngine, IDisposable
    {
        private static readonly HttpClient SharedHttpClient;
        private readonly SystemResourceOptimizerService _resourceOptimizer;
        private const int ChunkBufferSize = 128 * 1024; // 128 KB high-throughput worker buffer

        static HttpMultiPartEngine()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 128,
                EnableMultipleHttp2Connections = true,
                InitialHttp2StreamWindowSize = 16 * 1024 * 1024,
                AutomaticDecompression = DecompressionMethods.None, // Zero decompression CPU overhead on range chunks
                ConnectTimeout = TimeSpan.FromSeconds(15),
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests
            };

            SharedHttpClient = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            SharedHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("EDM-TurboEngine/6.0 (+https://edm.download)");
        }

        public HttpMultiPartEngine(SystemResourceOptimizerService? resourceOptimizer = null)
        {
            _resourceOptimizer = resourceOptimizer ?? SystemResourceOptimizerService.Instance;
        }

        public EngineProtocolType SupportedProtocol => EngineProtocolType.HttpMultiPart;

        public bool CanHandle(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<long?> ProbeContentLengthAsync(EngineDownloadRequest request, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, request.SourceUrl);
            ApplyAuthenticationAndCookies(req, request);

            try
            {
                using var response = await SharedHttpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new DownloadAuthenticationException(
                        AuthenticationErrorType.AuthenticationRequired,
                        $"Authentication required (401) during probe for '{request.SourceUrl}'.",
                        HttpStatusCode.Unauthorized,
                        new Uri(request.SourceUrl));
                }
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new DownloadAuthenticationException(
                        AuthenticationErrorType.Forbidden,
                        $"Access forbidden (403) during probe for '{request.SourceUrl}'.",
                        HttpStatusCode.Forbidden,
                        new Uri(request.SourceUrl));
                }

                if (response.IsSuccessStatusCode && response.Content.Headers.ContentLength.HasValue)
                {
                    return response.Content.Headers.ContentLength.Value;
                }
            }
            catch (DownloadAuthenticationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"[HttpMultiPartEngine] HEAD probe failed: {ex.Message}. Falling back to Range probe...");
            }

            // Fallback: Range probe with 1 byte
            using var rangeReq = new HttpRequestMessage(HttpMethod.Get, request.SourceUrl);
            rangeReq.Headers.Range = new RangeHeaderValue(0, 0);
            ApplyAuthenticationAndCookies(rangeReq, request);

            try
            {
                using var response = await SharedHttpClient.SendAsync(rangeReq, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new DownloadAuthenticationException(
                        AuthenticationErrorType.AuthenticationRequired,
                        $"Authentication required (401) during range probe for '{request.SourceUrl}'.",
                        HttpStatusCode.Unauthorized,
                        new Uri(request.SourceUrl));
                }
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new DownloadAuthenticationException(
                        AuthenticationErrorType.Forbidden,
                        $"Access forbidden (403) during range probe for '{request.SourceUrl}'.",
                        HttpStatusCode.Forbidden,
                        new Uri(request.SourceUrl));
                }

                if (response.Content.Headers.ContentRange?.Length.HasValue == true)
                {
                    return response.Content.Headers.ContentRange.Length.Value;
                }
            }
            catch (DownloadAuthenticationException)
            {
                throw;
            }
            catch { }

            return null;
        }

        public async Task DownloadAsync(
            EngineDownloadRequest request,
            IProgress<EngineProgressReport> progress,
            IPauseToken pauseToken,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(progress);

            // Ensure destination directory exists
            string? dir = Path.GetDirectoryName(request.DestinationFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // 1. Probe total length and resume capability
            long? totalLength = await ProbeContentLengthAsync(request, ct).ConfigureAwait(false);
            bool serverSupportsRange = totalLength.HasValue && totalLength.Value > 0;

            // 2. Determine target stream count (saturate up to 32 streams)
            int targetStreams = serverSupportsRange 
                ? Math.Clamp(request.DesiredParallelStreams > 0 ? request.DesiredParallelStreams : 32, 2, 32)
                : 1;

            var governor = new AdaptiveThroughputGovernor(targetStreams, 2, 32);
            governor.SetRateLimit(request.SpeedLimitBytesPerSecond);

            // 3. Pre-allocate sparse disk file to prevent fragmentation and lock contention
            string targetFile = request.DestinationFilePath;
            await using (var preallocStream = new FileStream(
                targetFile, 
                FileMode.OpenOrCreate, 
                FileAccess.ReadWrite, 
                FileShare.ReadWrite, 
                4096, 
                FileOptions.Asynchronous))
            {
                if (totalLength.HasValue && preallocStream.Length < totalLength.Value)
                {
                    preallocStream.SetLength(totalLength.Value);
                }
            }

            // Open shared direct asynchronous file handle for lock-free RandomAccess writing across all 32 threads
            using var fileHandle = File.OpenHandle(
                targetFile, 
                FileMode.Open, 
                FileAccess.Write, 
                FileShare.ReadWrite, 
                FileOptions.Asynchronous);

            // 4. Initial chunk partition
            var chunkQueue = new ConcurrentQueue<DynamicChunk>();
            var allChunks = new ConcurrentBag<DynamicChunk>();
            var workerActiveChunks = new ConcurrentDictionary<int, DynamicChunk>();
            var workerCtsMap = new ConcurrentDictionary<int, CancellationTokenSource>();
            int nextChunkId = 0;

            if (totalLength.HasValue && totalLength.Value > 0 && targetStreams > 1)
            {
                long chunkSize = totalLength.Value / targetStreams;
                for (int i = 0; i < targetStreams; i++)
                {
                    long start = i * chunkSize;
                    long end = (i == targetStreams - 1) ? totalLength.Value - 1 : (start + chunkSize - 1);
                    var c = new DynamicChunk(Interlocked.Increment(ref nextChunkId), start, end, 0);
                    chunkQueue.Enqueue(c);
                    allChunks.Add(c);
                }
            }
            else
            {
                var c = new DynamicChunk(Interlocked.Increment(ref nextChunkId), 0, totalLength ?? 0, 0);
                chunkQueue.Enqueue(c);
                allChunks.Add(c);
            }

            using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            // 5. Background Metrics Reporter Loop & Slow-Worker Detection
            var metricsTask = Task.Run(async () =>
            {
                int evalCycle = 0;
                while (!loopCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(200, loopCts.Token).ConfigureAwait(false);
                        evalCycle++;

                        // Build per-thread diagnostics
                        var threadDiagnostics = new List<WorkerThreadDiagnostic>();
                        for (int w = 0; w < targetStreams; w++)
                        {
                            if (workerActiveChunks.TryGetValue(w, out var activeChunk))
                            {
                                activeChunk.UpdateSpeed();
                                threadDiagnostics.Add(new WorkerThreadDiagnostic
                                {
                                    ThreadId = w + 1,
                                    State = activeChunk.IsComplete ? "Completed" : activeChunk.State,
                                    ThroughputBytesPerSec = activeChunk.CurrentSpeedBps,
                                    BytesDownloaded = activeChunk.BytesDownloaded,
                                    RemainingBytes = activeChunk.RemainingBytes,
                                    StartOffset = activeChunk.StartOffset,
                                    EndOffset = activeChunk.EndOffset
                                });
                            }
                            else
                            {
                                threadDiagnostics.Add(new WorkerThreadDiagnostic
                                {
                                    ThreadId = w + 1,
                                    State = "Idle",
                                    ThroughputBytesPerSec = 0,
                                    BytesDownloaded = 0,
                                    RemainingBytes = 0,
                                    StartOffset = 0,
                                    EndOffset = 0
                                });
                            }
                        }

                        int activeCount = allChunks.Count(c => !c.IsComplete);
                        var baseReport = governor.SampleMetrics(totalLength, activeCount, serverSupportsRange, "Downloading");
                        
                        var enrichedReport = new EngineProgressReport
                        {
                            BytesReceived = baseReport.BytesReceived,
                            TotalBytes = baseReport.TotalBytes,
                            CurrentSpeedBytesPerSec = baseReport.CurrentSpeedBytesPerSec,
                            AverageSpeedBytesPerSec = baseReport.AverageSpeedBytesPerSec,
                            PeakSpeedBytesPerSec = baseReport.PeakSpeedBytesPerSec,
                            ActiveConnections = activeCount,
                            CanResume = baseReport.CanResume,
                            StatusText = baseReport.StatusText,
                            WorkerDiagnostics = threadDiagnostics
                        };
                        progress.Report(enrichedReport);

                        // Slow-Worker Detection & Renegotiation (every ~1s)
                        if (evalCycle % 5 == 0 && targetStreams > 2)
                        {
                            DetectAndRenegotiateSlowWorkers(workerActiveChunks, workerCtsMap);
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex) { LoggingService.LogWarning($"[HttpMultiPartEngine] Metrics error: {ex.Message}"); }
                }
            }, loopCts.Token);

            try
            {
                // 6. Spawn Multi-Threaded Workers with Work-Stealing Engine
                var workerTasks = new List<Task>();
                for (int w = 0; w < targetStreams; w++)
                {
                    int workerId = w;
                    workerTasks.Add(Task.Run(async () =>
                    {
                        while (!loopCts.Token.IsCancellationRequested)
                        {
                            if (chunkQueue.TryDequeue(out var chunk))
                            {
                                workerActiveChunks[workerId] = chunk;
                                await RunWorkerChunkWithRenegotiationAsync(workerId, request, chunk, fileHandle, governor, pauseToken, workerCtsMap, loopCts.Token).ConfigureAwait(false);
                            }
                            else
                            {
                                // Work-Stealing: Find the largest incomplete chunk and steal half of it
                                var largestChunk = allChunks
                                    .Where(c => !c.IsComplete)
                                    .OrderByDescending(c => c.RemainingBytes)
                                    .FirstOrDefault();

                                if (largestChunk != null)
                                {
                                    var stolenChunk = largestChunk.TrySplit(Interlocked.Increment(ref nextChunkId));
                                    if (stolenChunk != null)
                                    {
                                        allChunks.Add(stolenChunk);
                                        workerActiveChunks[workerId] = stolenChunk;
                                        await RunWorkerChunkWithRenegotiationAsync(workerId, request, stolenChunk, fileHandle, governor, pauseToken, workerCtsMap, loopCts.Token).ConfigureAwait(false);
                                        continue;
                                    }
                                }

                                // No more work available for this worker
                                if (workerActiveChunks.TryGetValue(workerId, out var finishedChunk))
                                {
                                    finishedChunk.State = "Completed";
                                }
                                break;
                            }
                        }
                    }, loopCts.Token));
                }

                await Task.WhenAll(workerTasks).ConfigureAwait(false);
            }
            finally
            {
                loopCts.Cancel();
                try { await metricsTask.ConfigureAwait(false); } catch { }
            }

            // Final 100% progress report
            var finalReport = governor.SampleMetrics(totalLength, 0, serverSupportsRange, "Completed");
            progress.Report(finalReport);
        }

        private static void DetectAndRenegotiateSlowWorkers(
            ConcurrentDictionary<int, DynamicChunk> activeChunks,
            ConcurrentDictionary<int, CancellationTokenSource> workerCtsMap)
        {
            try
            {
                var downloadingWorkers = activeChunks
                    .Where(kv => kv.Value.State == "Downloading" && !kv.Value.IsComplete && (DateTime.UtcNow - kv.Value.StateStartTime).TotalSeconds > 2.5)
                    .ToList();

                if (downloadingWorkers.Count < 3) return;

                var speeds = downloadingWorkers.Select(w => w.Value.CurrentSpeedBps).OrderBy(s => s).ToList();
                double medianSpeed = speeds[speeds.Count / 2];
                if (medianSpeed < 50 * 1024) return; // Pool is slow overall, skip

                // 10th percentile speed calculation
                int p10Index = Math.Max(0, (int)(speeds.Count * 0.10));
                double p10Speed = speeds[p10Index];
                double slowThreshold = Math.Min(p10Speed, medianSpeed * 0.25);

                foreach (var worker in downloadingWorkers)
                {
                    if (worker.Value.CurrentSpeedBps < slowThreshold && worker.Value.RemainingBytes > 256 * 1024)
                    {
                        LoggingService.LogWarning($"[HttpMultiPartEngine] Worker {worker.Key} detected below 10th-percentile throughput ({worker.Value.CurrentSpeedBps / 1024.0:F1} KB/s vs median {medianSpeed / 1024.0:F1} KB/s). Triggering connection renegotiation...");
                        if (workerCtsMap.TryGetValue(worker.Key, out var workerCts))
                        {
                            try { workerCts.Cancel(); } catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"[HttpMultiPartEngine] Slow worker detection error: {ex.Message}");
            }
        }

        private async Task RunWorkerChunkWithRenegotiationAsync(
            int workerId,
            EngineDownloadRequest request,
            DynamicChunk chunk,
            Microsoft.Win32.SafeHandles.SafeFileHandle fileHandle,
            AdaptiveThroughputGovernor governor,
            IPauseToken pauseToken,
            ConcurrentDictionary<int, CancellationTokenSource> workerCtsMap,
            CancellationToken masterCt)
        {
            const int maxRetries = 5;
            int attempt = 0;

            while (!chunk.IsComplete && attempt < maxRetries)
            {
                masterCt.ThrowIfCancellationRequested();
                attempt++;

                using var workerCts = CancellationTokenSource.CreateLinkedTokenSource(masterCt);
                workerCtsMap[workerId] = workerCts;

                try
                {
                    await DownloadChunkStreamDirectAsync(request, chunk, fileHandle, governor, pauseToken, workerCts.Token).ConfigureAwait(false);
                    return; // Success
                }
                catch (OperationCanceledException) when (masterCt.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException) when (!masterCt.IsCancellationRequested && workerCts.IsCancellationRequested)
                {
                    // Renegotiation triggered by slow-worker detector
                    LoggingService.Log($"[HttpMultiPartEngine] Worker {workerId} renegotiating connection at offset {chunk.CurrentOffset}...");
                    await Task.Delay(50, masterCt).ConfigureAwait(false);
                    continue;
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"[HttpMultiPartEngine] Chunk {chunk.Id} attempt {attempt} error: {ex.Message}. Backing off...");
                    
                    if (attempt >= maxRetries)
                    {
                        throw new IOException($"Failed to download chunk {chunk.Id} after {maxRetries} attempts: {ex.Message}", ex);
                    }

                    int delayMs = (int)(Math.Pow(2, attempt) * 150 + Random.Shared.Next(50, 150));
                    await Task.Delay(delayMs, masterCt).ConfigureAwait(false);
                }
                finally
                {
                    workerCtsMap.TryRemove(workerId, out _);
                }
            }
        }

        private async Task DownloadChunkStreamDirectAsync(
            EngineDownloadRequest request,
            DynamicChunk chunk,
            Microsoft.Win32.SafeHandles.SafeFileHandle fileHandle,
            AdaptiveThroughputGovernor governor,
            IPauseToken pauseToken,
            CancellationToken ct)
        {
            if (chunk.IsComplete) return;

            byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(ChunkBufferSize);

            try
            {
                chunk.State = "Connecting";
                long currentOffset = chunk.CurrentOffset;
                long endOffset = chunk.EndOffset;

                using var req = new HttpRequestMessage(HttpMethod.Get, request.SourceUrl);
                if (endOffset > 0 && endOffset >= currentOffset)
                {
                    req.Headers.Range = new RangeHeaderValue(currentOffset, endOffset);
                }
                ApplyAuthenticationAndCookies(req, request);

                using var response = await SharedHttpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                
                // Handle 401 Unauthorized / 403 Forbidden / 429 / 503 / 504 gracefully
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        throw new DownloadAuthenticationException(
                            AuthenticationErrorType.AuthenticationRequired,
                            $"Authentication required (401) during chunk download for '{request.SourceUrl}'.",
                            HttpStatusCode.Unauthorized,
                            new Uri(request.SourceUrl));
                    }
                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        throw new DownloadAuthenticationException(
                            AuthenticationErrorType.AuthenticationExpired,
                            $"Authentication expired or access forbidden (403) during active chunk download.",
                            HttpStatusCode.Forbidden,
                            new Uri(request.SourceUrl));
                    }

                    if (response.StatusCode == (HttpStatusCode)429 || response.StatusCode == HttpStatusCode.ServiceUnavailable)
                    {
                        var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
                        await Task.Delay(retryAfter, ct).ConfigureAwait(false);
                    }
                    throw new HttpRequestException($"Server responded with HTTP {(int)response.StatusCode} ({response.ReasonPhrase})", null, response.StatusCode);
                }

                // Strict Range validation for multi-segment downloads
                if (endOffset > 0 && response.StatusCode == HttpStatusCode.OK)
                {
                    throw new InvalidOperationException($"Server returned HTTP 200 OK instead of 206 Partial Content for range {currentOffset}-{endOffset}. Server does not support parallel range streams.");
                }

                if (endOffset > 0 && response.StatusCode == HttpStatusCode.PartialContent)
                {
                    var cr = response.Content.Headers.ContentRange;
                    if (cr != null && cr.From.HasValue && cr.From.Value != currentOffset)
                    {
                        throw new InvalidDataException($"Content-Range start mismatch. Expected {currentOffset}, received {cr.From.Value}.");
                    }
                }

                await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                chunk.State = "Downloading";

                while (chunk.CurrentOffset <= chunk.EndOffset || chunk.EndOffset <= 0)
                {
                    ct.ThrowIfCancellationRequested();

                    if (pauseToken != null && pauseToken.IsPaused)
                    {
                        chunk.State = "Idle";
                        await pauseToken.WaitWhilePausedAsync(ct).ConfigureAwait(false);
                        chunk.State = "Downloading";
                    }

                    int toRead = chunk.EndOffset > 0 
                        ? (int)Math.Min(rentedBuffer.Length, (chunk.EndOffset - chunk.CurrentOffset + 1))
                        : rentedBuffer.Length;

                    if (toRead <= 0) break;

                    int bytesRead = await contentStream.ReadAsync(rentedBuffer.AsMemory(0, toRead), ct).ConfigureAwait(false);
                    if (bytesRead <= 0) break;

                    // Direct lock-free asynchronous random-access writing at current chunk offset
                    long writePosition = chunk.CurrentOffset;
                    chunk.State = "Writing";
                    await RandomAccess.WriteAsync(fileHandle, rentedBuffer.AsMemory(0, bytesRead), writePosition, ct).ConfigureAwait(false);
                    chunk.State = "Downloading";

                    chunk.AddDownloadedBytes(bytesRead);
                    chunk.UpdateSpeed();
                    governor.RecordBytes(bytesRead);
                    governor.ApplyRateLimiting(bytesRead);
                }

                if (chunk.IsComplete)
                {
                    chunk.State = "Completed";
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }

        private static void ApplyAuthenticationAndCookies(HttpRequestMessage req, EngineDownloadRequest downloadRequest)
        {
            if (!string.IsNullOrEmpty(downloadRequest.AuthCredentials))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Basic", downloadRequest.AuthCredentials);
            }
            else if (!string.IsNullOrEmpty(downloadRequest.AuthHeader))
            {
                string cleanAuth = HttpHeaderSecuritySanitizer.SanitizeHeaderValue(downloadRequest.AuthHeader);
                var parts = cleanAuth.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    req.Headers.Authorization = new AuthenticationHeaderValue(parts[0], parts[1]);
                }
                else
                {
                    req.Headers.TryAddWithoutValidation("Authorization", cleanAuth);
                }
            }

            if (!string.IsNullOrEmpty(downloadRequest.Cookies))
            {
                string cleanCookies = HttpHeaderSecuritySanitizer.SanitizeHeaderValue(downloadRequest.Cookies);
                if (cleanCookies.Length > 16384) cleanCookies = cleanCookies.Substring(0, 16384);
                req.Headers.TryAddWithoutValidation("Cookie", cleanCookies);
            }

            if (!string.IsNullOrEmpty(downloadRequest.UserAgent))
            {
                string cleanUa = HttpHeaderSecuritySanitizer.SanitizeHeaderValue(downloadRequest.UserAgent);
                req.Headers.Remove("User-Agent");
                req.Headers.TryAddWithoutValidation("User-Agent", cleanUa);
            }

            if (!string.IsNullOrEmpty(downloadRequest.Referer))
            {
                string cleanRef = HttpHeaderSecuritySanitizer.SanitizeHeaderValue(downloadRequest.Referer);
                if (Uri.TryCreate(cleanRef, UriKind.Absolute, out var refUri))
                {
                    req.Headers.Referrer = refUri;
                }
            }
        }

        public void Dispose()
        {
            // SharedHttpClient is static and pooled for high-concurrency reuse
        }
    }
}
