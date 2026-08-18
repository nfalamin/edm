using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;

namespace EDM.Domain.Protocols
{
    public sealed class DynamicChunk
    {
        public int Id { get; init; }
        public long StartOffset { get; set; }
        public long EndOffset { get; set; }
        private long _bytesDownloaded;

        public DynamicChunk(int id, long startOffset, long endOffset, long bytesDownloaded = 0)
        {
            Id = id;
            StartOffset = startOffset;
            EndOffset = endOffset;
            _bytesDownloaded = bytesDownloaded;
        }

        public long BytesDownloaded => Interlocked.Read(ref _bytesDownloaded);

        public void AddDownloadedBytes(long count)
        {
            Interlocked.Add(ref _bytesDownloaded, count);
        }

        public long CurrentOffset => StartOffset + BytesDownloaded;
        public long RemainingBytes => Math.Max(0, (EndOffset - StartOffset + 1) - BytesDownloaded);
        public bool IsComplete => EndOffset > 0 && RemainingBytes == 0;

        /// <summary>
        /// Work-stealing: safely splits this chunk into two if it has enough remaining bytes.
        /// </summary>
        public DynamicChunk? TrySplit(int newChunkId, long minSplitThreshold = 2 * 1024 * 1024)
        {
            long remaining = RemainingBytes;
            if (remaining < minSplitThreshold) return null;

            long midPoint = CurrentOffset + (remaining / 2);
            long oldEnd = EndOffset;

            // Shorten this chunk
            EndOffset = midPoint - 1;

            // Create new chunk for stolen upper half
            return new DynamicChunk(newChunkId, midPoint, oldEnd, 0);
        }
    }

    /// <summary>
    /// High-Performance Ultra Multi-Thread HTTP/HTTPS Engine.
    /// Features:
    /// - 32-64 parallel connection multiplexing.
    /// - Dynamic Work-Stealing: Idle threads split remaining chunks so 100% of bandwidth is utilized until the last byte.
    /// - Fault-tolerant HTTP Error Recovery (403, 408, 429, 5xx, ECONNRESET, SocketDrops) with exponential jitter backoff.
    /// - Zero-Allocation ArrayPool byte recycling and sparse disk pre-allocation.
    /// </summary>
    public sealed class HttpMultiPartEngine : IDownloadEngine, IDisposable
    {
        private static readonly HttpClient SharedHttpClient;
        private readonly SystemResourceOptimizerService _resourceOptimizer;

        static HttpMultiPartEngine()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 128,
                EnableMultipleHttp2Connections = true,
                AutomaticDecompression = DecompressionMethods.All,
                ConnectTimeout = TimeSpan.FromSeconds(15)
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
                if (response.IsSuccessStatusCode && response.Content.Headers.ContentLength.HasValue)
                {
                    return response.Content.Headers.ContentLength.Value;
                }
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
                if (response.Content.Headers.ContentRange?.Length.HasValue == true)
                {
                    return response.Content.Headers.ContentRange.Length.Value;
                }
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

            // 2. Determine target stream count
            int targetStreams = serverSupportsRange 
                ? Math.Clamp(request.DesiredParallelStreams, 2, _resourceOptimizer.GetRecommendedMaxSegments())
                : 1;

            var governor = new AdaptiveThroughputGovernor(targetStreams, 2, 32);
            governor.SetRateLimit(request.SpeedLimitBytesPerSecond);

            // 3. Pre-allocate sparse disk file to prevent fragmentation
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

            // 4. Initial chunk partition
            var chunkQueue = new ConcurrentQueue<DynamicChunk>();
            var allChunks = new ConcurrentBag<DynamicChunk>();
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

            // 5. Background Metrics Reporter Loop
            var metricsTask = Task.Run(async () =>
            {
                while (!loopCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(200, loopCts.Token).ConfigureAwait(false);
                        int activeCount = allChunks.Count(c => !c.IsComplete);
                        var report = governor.SampleMetrics(totalLength, activeCount, serverSupportsRange, "Downloading");
                        progress.Report(report);
                    }
                    catch (OperationCanceledException) { break; }
                }
            }, loopCts.Token);

            try
            {
                // 6. Spawn Multi-Threaded Workers with Work-Stealing Engine
                var workerTasks = new List<Task>();
                for (int w = 0; w < targetStreams; w++)
                {
                    workerTasks.Add(Task.Run(async () =>
                    {
                        while (!loopCts.Token.IsCancellationRequested)
                        {
                            if (chunkQueue.TryDequeue(out var chunk))
                            {
                                await DownloadChunkWithRetryAsync(request, chunk, targetFile, governor, pauseToken, loopCts.Token).ConfigureAwait(false);
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
                                        await DownloadChunkWithRetryAsync(request, stolenChunk, targetFile, governor, pauseToken, loopCts.Token).ConfigureAwait(false);
                                        continue;
                                    }
                                }

                                // No more work available
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

        private async Task DownloadChunkWithRetryAsync(
            EngineDownloadRequest request,
            DynamicChunk chunk,
            string targetFilePath,
            AdaptiveThroughputGovernor governor,
            IPauseToken pauseToken,
            CancellationToken ct)
        {
            const int maxRetries = 5;
            int attempt = 0;

            while (!chunk.IsComplete && attempt < maxRetries)
            {
                ct.ThrowIfCancellationRequested();
                attempt++;

                try
                {
                    await DownloadChunkStreamAsync(request, chunk, targetFilePath, governor, pauseToken, ct).ConfigureAwait(false);
                    return; // Success
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"[HttpMultiPartEngine] Chunk {chunk.Id} attempt {attempt} error: {ex.Message}. Backing off...");
                    
                    if (attempt >= maxRetries)
                    {
                        throw new IOException($"Failed to download chunk {chunk.Id} after {maxRetries} attempts: {ex.Message}", ex);
                    }

                    // Exponential backoff with jitter
                    int delayMs = (int)(Math.Pow(2, attempt) * 200 + Random.Shared.Next(50, 150));
                    await Task.Delay(delayMs, ct).ConfigureAwait(false);
                }
            }
        }

        private async Task DownloadChunkStreamAsync(
            EngineDownloadRequest request,
            DynamicChunk chunk,
            string targetFilePath,
            AdaptiveThroughputGovernor governor,
            IPauseToken pauseToken,
            CancellationToken ct)
        {
            if (chunk.IsComplete) return;

            int bufferSize = _resourceOptimizer.GetRecommendedBufferSize();
            byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);

            try
            {
                long currentOffset = chunk.CurrentOffset;
                long endOffset = chunk.EndOffset;

                using var req = new HttpRequestMessage(HttpMethod.Get, request.SourceUrl);
                if (endOffset > 0 && endOffset >= currentOffset)
                {
                    req.Headers.Range = new RangeHeaderValue(currentOffset, endOffset);
                }
                ApplyAuthenticationAndCookies(req, request);

                using var response = await SharedHttpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                
                // Handle 429 / 503 / 504 / 403 gracefully
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Server responded with HTTP {(int)response.StatusCode} ({response.ReasonPhrase})", null, response.StatusCode);
                }

                await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var fileStream = new FileStream(
                    targetFilePath,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.ReadWrite,
                    bufferSize,
                    FileOptions.Asynchronous);

                fileStream.Seek(currentOffset, SeekOrigin.Begin);

                while (chunk.CurrentOffset <= chunk.EndOffset || chunk.EndOffset <= 0)
                {
                    ct.ThrowIfCancellationRequested();

                    if (pauseToken != null && pauseToken.IsPaused)
                    {
                        await pauseToken.WaitWhilePausedAsync(ct).ConfigureAwait(false);
                    }

                    int toRead = chunk.EndOffset > 0 
                        ? (int)Math.Min(rentedBuffer.Length, (chunk.EndOffset - chunk.CurrentOffset + 1))
                        : rentedBuffer.Length;

                    if (toRead <= 0) break;

                    int bytesRead = await contentStream.ReadAsync(rentedBuffer.AsMemory(0, toRead), ct).ConfigureAwait(false);
                    if (bytesRead <= 0) break;

                    await fileStream.WriteAsync(rentedBuffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);

                    chunk.AddDownloadedBytes(bytesRead);
                    governor.RecordBytes(bytesRead);
                    governor.ApplyRateLimiting(bytesRead);
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
            if (!string.IsNullOrEmpty(downloadRequest.Cookies))
            {
                req.Headers.TryAddWithoutValidation("Cookie", downloadRequest.Cookies);
            }
        }

        public void Dispose()
        {
            // SharedHttpClient is static and pooled for high-concurrency reuse
        }
    }
}
