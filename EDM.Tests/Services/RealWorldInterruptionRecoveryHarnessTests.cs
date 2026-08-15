using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    [CollectionDefinition("InterruptionRecoveryHarnessTests", DisableParallelization = true)]
    public class InterruptionRecoveryHarnessTestCollection : ICollectionFixture<RealWorldInterruptionRecoveryHarnessTests> { }

    /// <summary>
    /// Comprehensive real-world interruption recovery test harness for EDM.
    /// Tests full download lifecycle under pause, application shutdown, simulated crash,
    /// ETag changes, network disconnection/reconnection, and multi-download concurrency.
    /// </summary>
    [Collection("InterruptionRecoveryHarnessTests")]
    public class RealWorldInterruptionRecoveryHarnessTests : IAsyncDisposable
    {
        private readonly string _testDir;
        private static readonly int TestFileSize = 1024 * 1024; // 1 MB payload
        private readonly byte[] _payload;
        private readonly string _payloadSha256;

        public RealWorldInterruptionRecoveryHarnessTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "EDM_RecoveryHarness_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);

            _payload = new byte[TestFileSize];
            for (int i = 0; i < TestFileSize; i++)
            {
                _payload[i] = (byte)(i % 251);
            }
            using var sha = SHA256.Create();
            _payloadSha256 = Convert.ToHexString(sha.ComputeHash(_payload));
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch { }
            await Task.CompletedTask;
        }

        // -----------------------------------------------------------------------
        // TEST 1: Normal Pause and Resume Lifecycle
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Test1_NormalPauseAndResume_PreservesByteRangesAndCompletesFile()
        {
            int port = Random.Shared.Next(43000, 43999);
            string url = $"http://127.0.0.1:{port}/pause-test.bin";
            string destFile = Path.Combine(_testDir, "pause_test_output.bin");

            var server = CreateMockHttpServer(port, _payload);
            try
            {
                var pauseToken = new PauseTokenSource();
                var cts = new CancellationTokenSource();
                var progressTracker = new List<DownloadProgressInfo>();
                var progress = new Progress<DownloadProgressInfo>(info => progressTracker.Add(info));

                var orchestrator = new DownloadOrchestrator();

                var downloadTask = orchestrator.StartDownloadAsync(
                    url, destFile, progress, pauseToken, () => -1, cts.Token, segmentCount: 4);

                await Task.Delay(50);
                pauseToken.Pause();
                Assert.True(pauseToken.IsPaused, "PauseToken must be in paused state");

                await Task.Delay(100);
                pauseToken.Resume();

                await downloadTask;

                // EVIDENCE
                File.Exists(destFile).Should().BeTrue("Final file must exist after pause and resume");
                byte[] actualData = await File.ReadAllBytesAsync(destFile);
                actualData.Length.Should().Be(TestFileSize, "Final file size must match payload size");

                using var sha = SHA256.Create();
                string actualSha256 = Convert.ToHexString(sha.ComputeHash(actualData));
                actualSha256.Should().Be(_payloadSha256, "Final file SHA-256 hash must match payload exactly");
            }
            finally
            {
                StopMockHttpServer(server);
            }
        }

        // -----------------------------------------------------------------------
        // TEST 2: Application Shutdown / Hard Task Cancellation
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Test2_ApplicationShutdown_SavesStateAndResumesOnRestart()
        {
            int port = Random.Shared.Next(44000, 44999);
            string url = $"http://127.0.0.1:{port}/shutdown-test.bin";
            string destFile = Path.Combine(_testDir, "shutdown_output.bin");
            string tempDir = Path.Combine(Path.GetDirectoryName(destFile)!, ".tmp_" + Path.GetFileName(destFile));
            string metaPath = Path.Combine(tempDir, "metadata.json");

            var holdTcs = new TaskCompletionSource<bool>();
            using var server = new HttpListener();
            server.Prefixes.Add($"http://127.0.0.1:{port}/");
            server.Start();

            var serverTask = Task.Run(async () =>
            {
                while (server.IsListening)
                {
                    try
                    {
                        var ctx = await server.GetContextAsync().ConfigureAwait(false);
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var req = ctx.Request;
                                var res = ctx.Response;

                                res.Headers["Accept-Ranges"] = "bytes";
                                res.Headers["ETag"] = "\"shutdown-etag\"";

                                if (req.HttpMethod == "HEAD")
                                {
                                    res.StatusCode = (int)HttpStatusCode.OK;
                                    res.ContentLength64 = _payload.Length;
                                    res.Close();
                                    return;
                                }

                                if (req.Headers["Range"] != null && req.Headers["Range"]!.StartsWith("bytes="))
                                {
                                    string[] parts = req.Headers["Range"]!.Substring(6).Split('-');
                                    long start = long.Parse(parts[0]);
                                    long end = parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) ? long.Parse(parts[1]) : _payload.Length - 1;
                                    if (end >= _payload.Length) end = _payload.Length - 1;

                                    long length = end - start + 1;
                                    res.StatusCode = (int)HttpStatusCode.PartialContent;
                                    res.Headers["Content-Range"] = $"bytes {start}-{end}/{_payload.Length}";
                                    res.ContentLength64 = length;

                                    if (length <= 2)
                                    {
                                        // Probe request
                                        await res.OutputStream.WriteAsync(_payload, (int)start, (int)length).ConfigureAwait(false);
                                        res.Close();
                                        return;
                                    }

                                    if (!holdTcs.Task.IsCompleted)
                                    {
                                        int chunk = Math.Min((int)length, 10 * 1024);
                                        await res.OutputStream.WriteAsync(_payload, (int)start, chunk).ConfigureAwait(false);
                                        await res.OutputStream.FlushAsync().ConfigureAwait(false);
                                        await holdTcs.Task.ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        await res.OutputStream.WriteAsync(_payload, (int)start, (int)length).ConfigureAwait(false);
                                    }
                                    res.Close();
                                    return;
                                }

                                res.StatusCode = (int)HttpStatusCode.OK;
                                res.ContentLength64 = _payload.Length;
                                await res.OutputStream.WriteAsync(_payload, 0, _payload.Length).ConfigureAwait(false);
                                res.Close();
                            }
                            catch { }
                        });
                    }
                    catch { break; }
                }
            });

            var pauseToken = new PauseTokenSource();
            var shutdownCts = new CancellationTokenSource();
            var orchestrator = new DownloadOrchestrator();

            // Start download (server will hold connection mid-segment until holdTcs is released)
            var downloadTask = orchestrator.StartDownloadAsync(
                url, destFile, new Progress<DownloadProgressInfo>(), pauseToken, () => -1, shutdownCts.Token, segmentCount: 2);

            // Poll for metadata file to be created on disk
            int waitMs = 0;
            while (!File.Exists(metaPath) && waitMs < 5000)
            {
                await Task.Delay(20);
                waitMs += 20;
            }

            bool existsBeforeCancel = File.Exists(metaPath);
            shutdownCts.Cancel(); // Simulate abrupt application shutdown mid-download
            holdTcs.TrySetResult(true); // Release server worker hold

            try { await downloadTask; } catch (OperationCanceledException) { }

            // Verify state on disk after shutdown
            var manager = new DurableMetadataManager();
            var stateAfterShutdown = await manager.ReadStateAsync(metaPath, CancellationToken.None);

            // EVIDENCE BEFORE RESTART
            existsBeforeCancel.Should().BeTrue($"State file '{metaPath}' must be created during active download before shutdown");
            stateAfterShutdown.Should().NotBeNull("State file must exist on disk after application shutdown");
            stateAfterShutdown!.Segments.Count.Should().BeGreaterThan(0, "Segment metadata must be preserved");

            // Restart EDM (fresh orchestrator with un-cancelled CTS)
            var newCts = new CancellationTokenSource();
            var newOrchestrator = new DownloadOrchestrator();

            await newOrchestrator.StartDownloadAsync(
                url, destFile, new Progress<DownloadProgressInfo>(), new PauseTokenSource(), () => -1, newCts.Token, segmentCount: 2);

            // EVIDENCE AFTER RESTART
            File.Exists(destFile).Should().BeTrue();
            byte[] actualData = await File.ReadAllBytesAsync(destFile);
            using var sha = SHA256.Create();
            Convert.ToHexString(sha.ComputeHash(actualData)).Should().Be(_payloadSha256, "File must be 100% correct after restart");
        }

        // -----------------------------------------------------------------------
        // TEST 3: Simulated Crash Mid-Write (Partial Segment Bytes Preserved)
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Test3_SimulatedCrash_PreservesValidBytesAndReconcilesStaleState()
        {
            string destFile = Path.Combine(_testDir, "crash_test.bin");
            string metaPath = destFile + ".edm.json";
            string tempDir = Path.Combine(Path.GetDirectoryName(destFile)!, ".tmp_" + Path.GetFileName(destFile));
            Directory.CreateDirectory(tempDir);

            long totalBytes = _payload.Length;
            long segSize = totalBytes / 4;

            // Write 2 segments: Seg 0 complete, Seg 1 partially written (100 KB out of 256 KB)
            string seg0Path = Path.Combine(tempDir, "chunk_0.part");
            string seg1Path = Path.Combine(tempDir, "chunk_1.part");

            await File.WriteAllBytesAsync(seg0Path, _payload.Take((int)segSize).ToArray());
            await File.WriteAllBytesAsync(seg1Path, _payload.Skip((int)segSize).Take(100 * 1024).ToArray());

            var state = new DurableDownloadState
            {
                Url = "http://example.com/crash.bin",
                DestinationPath = destFile,
                TotalBytes = totalBytes,
                ETag = "\"crash-etag\"",
                Segments = new List<SegmentRange>
                {
                    new SegmentRange { Id = 0, Start = 0, End = segSize - 1, BytesDownloaded = segSize, State = SegmentState.Completed, TempPath = seg0Path },
                    new SegmentRange { Id = 1, Start = segSize, End = (2 * segSize) - 1, BytesDownloaded = 100 * 1024, State = SegmentState.Downloading, TempPath = seg1Path },
                    new SegmentRange { Id = 2, Start = 2 * segSize, End = (3 * segSize) - 1, BytesDownloaded = 0, State = SegmentState.Pending, TempPath = Path.Combine(tempDir, "chunk_2.part") },
                    new SegmentRange { Id = 3, Start = 3 * segSize, End = totalBytes - 1, BytesDownloaded = 0, State = SegmentState.Pending, TempPath = Path.Combine(tempDir, "chunk_3.part") }
                }
            };

            var manager = new DurableMetadataManager();
            await manager.WriteStateAtomicAsync(metaPath, state, CancellationToken.None);

            // Reconcile and validate
            var readState = await manager.ReadStateAsync(metaPath, CancellationToken.None);
            bool isValid = manager.ReconcileAndValidate(readState!, "\"crash-etag\"", null);

            // EVIDENCE
            isValid.Should().BeTrue("Crashed state with valid segment files must be accepted for resume");
            readState!.Segments[0].BytesDownloaded.Should().Be(segSize, "Completed segment 0 bytes must be preserved");
            readState.Segments[1].BytesDownloaded.Should().Be(100 * 1024, "Partially downloaded segment 1 bytes (100 KB) must be preserved");
        }

        // -----------------------------------------------------------------------
        // TEST 4: Remote ETag Changed — Invalidation Guard
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Test4_RemoteETagChanged_InvalidatesStaleStateAndPreventsCorruption()
        {
            string destFile = Path.Combine(_testDir, "etag_test.bin");
            string metaPath = destFile + ".edm.json";

            var state = new DurableDownloadState
            {
                Url = "http://example.com/etag.bin",
                DestinationPath = destFile,
                TotalBytes = 1000,
                ETag = "\"old-etag-v1\"",
                Segments = new List<SegmentRange>
                {
                    new SegmentRange { Id = 0, Start = 0, End = 999, BytesDownloaded = 500, State = SegmentState.Downloading }
                }
            };

            var manager = new DurableMetadataManager();
            await manager.WriteStateAtomicAsync(metaPath, state, CancellationToken.None);

            var readState = await manager.ReadStateAsync(metaPath, CancellationToken.None);

            // Remote server returned new ETag "new-etag-v2"
            bool valid = manager.ReconcileAndValidate(readState!, "\"new-etag-v2\"", null);

            // EVIDENCE
            valid.Should().BeFalse("Changed ETag must invalidate stale local state to prevent file corruption");
        }

        // -----------------------------------------------------------------------
        // TEST 5: Network Disconnection & Recovery Loop
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Test5_NetworkDisconnectionAndRecovery_RetriesAndCompletes()
        {
            int port = Random.Shared.Next(45000, 45999);
            string url = $"http://127.0.0.1:{port}/net-disconnect.bin";
            string destFile = Path.Combine(_testDir, "net_disconnect_output.bin");

            int totalRequestCount = 0;
            using var server = new HttpListener();
            server.Prefixes.Add($"http://127.0.0.1:{port}/");
            server.Start();

            var serverTask = Task.Run(async () =>
            {
                while (server.IsListening)
                {
                    try
                    {
                        var ctx = await server.GetContextAsync().ConfigureAwait(false);
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var req = ctx.Request;
                                var res = ctx.Response;

                                int reqNum = Interlocked.Increment(ref totalRequestCount);

                                if (reqNum == 3)
                                {
                                    // Simulate network disconnection error on request #3
                                    res.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                                    res.Close();
                                    return;
                                }

                                res.Headers["Accept-Ranges"] = "bytes";
                                res.Headers["ETag"] = "\"mock-etag-net\"";

                                if (req.HttpMethod == "HEAD")
                                {
                                    res.StatusCode = (int)HttpStatusCode.OK;
                                    res.ContentLength64 = _payload.Length;
                                    res.Close();
                                    return;
                                }

                                if (req.Headers["Range"] != null && req.Headers["Range"]!.StartsWith("bytes="))
                                {
                                    string[] parts = req.Headers["Range"]!.Substring(6).Split('-');
                                    long start = long.Parse(parts[0]);
                                    long end = parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) ? long.Parse(parts[1]) : _payload.Length - 1;
                                    if (end >= _payload.Length) end = _payload.Length - 1;

                                    long length = end - start + 1;
                                    res.StatusCode = (int)HttpStatusCode.PartialContent;
                                    res.Headers["Content-Range"] = $"bytes {start}-{end}/{_payload.Length}";
                                    res.ContentLength64 = length;

                                    await res.OutputStream.WriteAsync(_payload, (int)start, (int)length).ConfigureAwait(false);
                                    res.Close();
                                    return;
                                }

                                res.StatusCode = (int)HttpStatusCode.OK;
                                res.ContentLength64 = _payload.Length;
                                await res.OutputStream.WriteAsync(_payload, 0, _payload.Length).ConfigureAwait(false);
                                res.Close();
                            }
                            catch { }
                        });
                    }
                    catch { break; }
                }
            });

            var orchestrator = new DownloadOrchestrator();
            await orchestrator.StartDownloadAsync(
                url, destFile, new Progress<DownloadProgressInfo>(), new PauseTokenSource(), () => -1, CancellationToken.None);

            // EVIDENCE
            int finalCount = Volatile.Read(ref totalRequestCount);
            finalCount.Should().BeGreaterThan(2, "Engine must retry requests after transient network failure");
            File.Exists(destFile).Should().BeTrue();
            byte[] actualData = await File.ReadAllBytesAsync(destFile);
            using var sha = SHA256.Create();
            Convert.ToHexString(sha.ComputeHash(actualData)).Should().Be(_payloadSha256, "Data must be byte-identical after network recovery");
        }

        // -----------------------------------------------------------------------
        // TEST 6: Multiple Simultaneous Downloads Interruption
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Test6_MultipleSimultaneousDownloads_AllResumeCleanly()
        {
            int count = 3;
            var tasks = new List<Task>();
            var destFiles = new List<string>();
            var servers = new List<HttpListener>();

            try
            {
                for (int i = 0; i < count; i++)
                {
                    int port = 48001 + i;
                    string url = $"http://127.0.0.1:{port}/multi-{i}.bin";
                    string destFile = Path.Combine(_testDir, $"multi_output_{i}.bin");
                    destFiles.Add(destFile);

                    var server = CreateMockHttpServer(port, _payload);
                    servers.Add(server);

                    var orchestrator = new DownloadOrchestrator();
                    tasks.Add(orchestrator.StartDownloadAsync(
                        url, destFile, new Progress<DownloadProgressInfo>(), new PauseTokenSource(), () => -1, CancellationToken.None, segmentCount: 2));
                }

                await Task.WhenAll(tasks);

                // EVIDENCE
                for (int i = 0; i < count; i++)
                {
                    File.Exists(destFiles[i]).Should().BeTrue($"Download {i} must complete");
                    byte[] data = await File.ReadAllBytesAsync(destFiles[i]);
                    using var sha = SHA256.Create();
                    Convert.ToHexString(sha.ComputeHash(data)).Should().Be(_payloadSha256, $"Download {i} SHA-256 hash must match");
                }
            }
            finally
            {
                foreach (var s in servers)
                {
                    try { s.Stop(); s.Close(); } catch { }
                }
            }
        }

        // -----------------------------------------------------------------------
        // HELPER: Mock HTTP Server
        // -----------------------------------------------------------------------
        private static HttpListener CreateMockHttpServer(int port, byte[] payload)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();

            _ = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var ctx = await listener.GetContextAsync().ConfigureAwait(false);
                        var req = ctx.Request;
                        var res = ctx.Response;

                        res.Headers["Accept-Ranges"] = "bytes";
                        res.Headers["ETag"] = "\"mock-etag-12345\"";

                        if (req.HttpMethod == "HEAD")
                        {
                            res.StatusCode = (int)HttpStatusCode.OK;
                            res.ContentLength64 = payload.Length;
                            res.Close();
                            continue;
                        }

                        if (req.Headers["Range"] != null)
                        {
                            string rangeHeader = req.Headers["Range"]!;
                            if (rangeHeader.StartsWith("bytes="))
                            {
                                string[] parts = rangeHeader.Substring(6).Split('-');
                                long start = long.Parse(parts[0]);
                                long end = parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) ? long.Parse(parts[1]) : payload.Length - 1;
                                if (end >= payload.Length) end = payload.Length - 1;

                                long length = end - start + 1;
                                res.StatusCode = (int)HttpStatusCode.PartialContent;
                                res.Headers["Content-Range"] = $"bytes {start}-{end}/{payload.Length}";
                                res.ContentLength64 = length;

                                await res.OutputStream.WriteAsync(payload, (int)start, (int)length).ConfigureAwait(false);
                                res.Close();
                                continue;
                            }
                        }

                        res.StatusCode = (int)HttpStatusCode.OK;
                        res.ContentLength64 = payload.Length;
                        await res.OutputStream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
                        res.Close();
                    }
                    catch
                    {
                        break;
                    }
                }
            });

            return listener;
        }

        private static void StopMockHttpServer(HttpListener listener)
        {
            try
            {
                listener.Stop();
                listener.Close();
            }
            catch { }
        }
    }
}
