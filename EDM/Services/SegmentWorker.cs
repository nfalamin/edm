using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;

namespace EDM.Services
{
    public class SegmentWorker
    {
        private readonly string _workerId;
        private readonly HttpClient _httpClient;
        private readonly HttpRequestPipeline _pipeline;

        // Per-read inactivity timeout: if the server sends no data for this long,
        // treat as a connection reset and allow retry.
        private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

        public string WorkerId => _workerId;

        public SegmentWorker(string workerId, HttpClient httpClient)
        {
            _workerId = workerId ?? throw new ArgumentNullException(nameof(workerId));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _pipeline = new HttpRequestPipeline(_httpClient);
        }

        public async Task ExecuteSegmentDownloadAsync(
            Uri uri,
            SegmentRange segment,
            string metaPath,
            DurableMetadataManager metaManager,
            DurableDownloadState metaState,
            SegmentScheduler scheduler,
            IProgress<DownloadProgressInfo>? progressReporter,
            Func<double>? speedLimitProvider,
            DownloadCredentials? credentials,
            string? cookies,
            CancellationToken cancellationToken,
            ConnectionAccountant? accountant = null)
        {
            long startByte = segment.Start + segment.BytesDownloaded;
            long currentAssigned = scheduler.GetAssignedEnd(segment.Id);
            if (currentAssigned < segment.End) segment.End = currentAssigned;
            long endByte = segment.End;

            if (startByte > endByte)
            {
                scheduler.MarkCompleted(segment.Id);
                return;
            }

            long expectedSegmentBytes = endByte - startByte + 1;

            accountant?.OnConnectionRequested();
            accountant?.RegisterWorker(_workerId, segment.Id);
            try
            {
                // Execute HTTP Request with full 206 validation.
                var result = await _pipeline.ExecuteWithRetryAsync(
                    requestFactory: () => _pipeline.CreateFreshRequest(HttpMethod.Get, uri, startByte, endByte, credentials, cookies),
                    completionOption: HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken: cancellationToken,
                    maxRetries: 5,
                    requirePartialContent: true,
                    expectedRangeStart: startByte,
                    expectedRangeEnd: endByte,
                    knownTotalBytes: scheduler.TotalBytes
                ).ConfigureAwait(false);

                accountant?.OnConnectionStarted();
                accountant?.RecordNetworkMetrics(result.ElapsedMilliseconds, result.TimeToFirstByteMs);

                using var response = result.Response;

                long? responseContentLength = response.Content.Headers.ContentLength;
                if (responseContentLength.HasValue && responseContentLength.Value != expectedSegmentBytes)
                {
                    throw new InvalidDataException(
                        $"[SegmentWorker:{_workerId}] Content-Length={responseContentLength.Value} does not match " +
                        $"expected segment size={expectedSegmentBytes} for range bytes={startByte}-{endByte}.");
                }

                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

                Directory.CreateDirectory(Path.GetDirectoryName(segment.TempPath) ?? ".");

                long totalBytesWritten;
                {
                    using var fs = new FileStream(
                        segment.TempPath,
                        FileMode.OpenOrCreate,
                        FileAccess.Write,
                        FileShare.ReadWrite,
                        256 * 1024,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);

                    fs.Seek(segment.BytesDownloaded, SeekOrigin.Begin);

                    byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(256 * 1024);
                    long bytesSinceLastMetaWrite = 0;
                    totalBytesWritten = 0;
                    using var incHasher = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);

                    long lastSpeedTimestamp = Stopwatch.GetTimestamp();
                    long lastSpeedBytes = segment.BytesDownloaded;
                    double rollingWorkerSpeed = 0;

                    try
                    {
                        int read;
                        while (true)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            using var readTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                            readTimeoutCts.CancelAfter(ReadTimeout);

                            try
                            {
                                read = await contentStream.ReadAsync(buffer, 0, buffer.Length, readTimeoutCts.Token).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                            {
                                throw new IOException(
                                    $"[SegmentWorker:{_workerId}] Read timed out after {ReadTimeout.TotalSeconds}s " +
                                    $"at byte offset {startByte + totalBytesWritten}. Server may have stalled.");
                            }

                            if (read == 0) break; // Stream ended

                            // Active ownership check: Query authoritative end boundary from scheduler.
                            long currentAssignedEnd = scheduler.GetAssignedEnd(segment.Id);
                            long currentPosition = segment.Start + segment.BytesDownloaded;
                            long bytesAllowed = currentAssignedEnd + 1 - currentPosition;

                            if (bytesAllowed <= 0)
                            {
                                // Dynamic work stealing reduced our boundary
                                scheduler.MarkCompleted(segment.Id);
                                break;
                            }

                            int writeCount = (int)Math.Min(read, bytesAllowed);
                            await fs.WriteAsync(buffer.AsMemory(0, writeCount), cancellationToken).ConfigureAwait(false);
                            incHasher.AppendData(buffer, 0, writeCount);

                            segment.BytesDownloaded += writeCount;
                            totalBytesWritten += writeCount;
                            bytesSinceLastMetaWrite += writeCount;

                            scheduler.ReportProgress(segment.Id, segment.BytesDownloaded);

                            // Calculate per-worker throughput and telemetry
                            long nowTs = Stopwatch.GetTimestamp();
                            double dtSec = (nowTs - lastSpeedTimestamp) / (double)Stopwatch.Frequency;
                            if (dtSec >= 0.1)
                            {
                                double instSpeed = (segment.BytesDownloaded - lastSpeedBytes) / dtSec;
                                rollingWorkerSpeed = rollingWorkerSpeed <= 0 ? instSpeed : (0.3 * instSpeed + 0.7 * rollingWorkerSpeed);
                                accountant?.RecordWorkerProgress(_workerId, segment.Id, segment.BytesDownloaded, rollingWorkerSpeed, result.ElapsedMilliseconds, result.TimeToFirstByteMs);
                                scheduler.RegisterWorkerProgress(_workerId, segment.Id, segment.BytesDownloaded, rollingWorkerSpeed);
                                lastSpeedTimestamp = nowTs;
                                lastSpeedBytes = segment.BytesDownloaded;
                            }

                            if (writeCount < read || currentPosition + writeCount > currentAssignedEnd)
                            {
                                long targetLength = currentAssignedEnd - segment.Start + 1;
                                if (fs.Length > targetLength)
                                {
                                    fs.SetLength(targetLength);
                                }

                                if (writeCount < read)
                                {
                                    LoggingService.LogWarning(
                                        $"[SegmentWorker:{_workerId}] Server sent {read - writeCount} extra bytes " +
                                        $"beyond assigned range end={currentAssignedEnd}. Excess discarded.");
                                }
                                scheduler.MarkCompleted(segment.Id);
                                break;
                            }

                            // Global throttle
                            try
                            {
                                await BandwidthThrottler.Instance.ThrottleAsync(writeCount, cancellationToken).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException) { throw; }
                            catch { }

                            // Periodically persist metadata (every 256 KB)
                            if (bytesSinceLastMetaWrite >= 256 * 1024)
                            {
                                metaState.Segments = scheduler.GetSegmentsSnapshot();
                                await metaManager.WriteStateAtomicAsync(metaPath, metaState, cancellationToken).ConfigureAwait(false);
                                bytesSinceLastMetaWrite = 0;
                            }
                        }

                        await fs.FlushAsync(cancellationToken).ConfigureAwait(false);

                        // Finalize live SHA-256 hash
                        var hashBytes = incHasher.GetHashAndReset();
                        segment.Sha256Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                        long currentAssignedEndFinal = scheduler.GetAssignedEnd(segment.Id);
                        long currentExpectedBytes = currentAssignedEndFinal - startByte + 1;
                        long remainingExpected = currentExpectedBytes - totalBytesWritten;
                        if (remainingExpected > 0)
                        {
                            throw new IOException(
                                $"[SegmentWorker:{_workerId}] Short read detected: expected {currentExpectedBytes} bytes " +
                                $"for range bytes={startByte}-{currentAssignedEndFinal}, but only received {totalBytesWritten} bytes. " +
                                $"Missing {remainingExpected} bytes.");
                        }

                        scheduler.MarkCompleted(segment.Id);
                    }
                    finally
                    {
                        System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                    }
                }

                metaState.Segments = scheduler.GetSegmentsSnapshot();
                await metaManager.WriteStateAtomicAsync(metaPath, metaState, cancellationToken).ConfigureAwait(false);
                accountant?.CompleteWorker(_workerId);
                accountant?.OnConnectionCompleted();
            }
            catch (OperationCanceledException)
            {
                accountant?.OnConnectionCancelled();
                throw;
            }
            catch (Exception ex)
            {
                accountant?.RecordWorkerError(_workerId, segment.Id, ex);
                accountant?.OnConnectionFailed(ex);
                throw;
            }
        }
    }
}
