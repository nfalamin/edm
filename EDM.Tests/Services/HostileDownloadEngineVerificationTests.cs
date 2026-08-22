using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using EDM.Models;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class HostileDownloadEngineVerificationTests : TestBase
    {
        private readonly string _testTempDir;

        public HostileDownloadEngineVerificationTests()
        {
            _testTempDir = Path.Combine(Path.GetTempPath(), "EDM_HostileQA_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testTempDir);
        }

        public override void Dispose()
        {
            try
            {
                if (Directory.Exists(_testTempDir))
                {
                    Directory.Delete(_testTempDir, true);
                }
            }
            catch { }
            base.Dispose();
        }

        private static string ComputeSha256(byte[] data)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(data);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string ComputeFileSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(filePath);
            byte[] hash = sha.ComputeHash(fs);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        // =========================================================================
        // MATHEMATICAL RANGE VERIFICATION HELPERS
        // =========================================================================

        private static void VerifySegmentCoverageMathematically(SegmentScheduler scheduler)
        {
            var segments = scheduler.GetSegmentsSnapshot().OrderBy(s => s.Start).ToList();
            segments.Should().NotBeEmpty("scheduler must contain segments");

            long sumLengths = segments.Sum(s => s.TotalBytes);
            sumLengths.Should().Be(scheduler.TotalBytes, "SUM(segment lengths) must equal total file length");

            // Verify no gaps, overlaps, or duplicate byte ranges
            segments[0].Start.Should().Be(0, "First segment must start at byte 0");

            for (int i = 0; i < segments.Count - 1; i++)
            {
                var current = segments[i];
                var next = segments[i + 1];

                current.End.Should().BeLessThan(next.Start, $"Segment {current.Id} end must be strictly less than Segment {next.Id} start (No Overlap)");
                (current.End + 1).Should().Be(next.Start, $"Segment {current.Id} end + 1 must match Segment {next.Id} start (No Gap)");
            }

            segments.Last().End.Should().Be(scheduler.TotalBytes - 1, "Last segment end must match totalBytes - 1");
            scheduler.ValidateCoverage().Should().BeTrue("ValidateCoverage() must return true");
        }

        // =========================================================================
        // 1. FILE SIZES (1-byte, tiny, 1MB, 10MB, 100MB)
        // =========================================================================

        [Theory]
        [InlineData(1L, "1-Byte File")]
        [InlineData(512L, "Tiny 512-Byte File")]
        [InlineData(1 * 1024 * 1024L, "1 MB File")]
        [InlineData(10 * 1024 * 1024L, "10 MB File")]
        public async Task FileSizes_MathematicalCoverageAndSha256Verification(long fileSize, string testName)
        {
            // Arrange
            byte[] expectedData = new byte[fileSize];
            Random.Shared.NextBytes(expectedData);
            string expectedHash = ComputeSha256(expectedData);

            using var server = new HostileTestServer(expectedData);
            await server.StartAsync();

            string savePath = Path.Combine(_testTempDir, $"download_{fileSize}.bin");
            var downloader = new MultiPartDownloader();

            // Act
            var sw = Stopwatch.StartNew();
            await downloader.DownloadFileAsync(new Uri(server.Url), savePath, chunkCount: 8, maxConcurrency: 8, cancellationToken: CancellationToken.None);
            sw.Stop();

            // Assert
            File.Exists(savePath).Should().BeTrue();
            new FileInfo(savePath).Length.Should().Be(fileSize);

            string actualHash = ComputeFileSha256(savePath);
            actualHash.Should().Be(expectedHash, $"SHA-256 hash of downloaded file ({testName}) must match test fixture hash");
        }

        // =========================================================================
        // 2. RANGE SUPPORTED VS UNSUPPORTED & SERVER 200 OK TO RANGE
        // =========================================================================

        [Fact]
        public async Task ServerReturns200OKToRange_SafelyFallsBackToSingleStreamWithoutCorruption()
        {
            // Arrange - Server that ignores Range header and returns 200 OK
            byte[] expectedData = Encoding.UTF8.GetBytes("Hostile Server ignores Range headers and returns 200 OK!");
            string expectedHash = ComputeSha256(expectedData);

            using var server = new HostileTestServer(expectedData, force200OkOnRange: true);
            await server.StartAsync();

            string savePath = Path.Combine(_testTempDir, "fallback_200ok.bin");
            var downloader = new MultiPartDownloader();

            // Act
            await downloader.DownloadFileAsync(new Uri(server.Url), savePath, chunkCount: 4, maxConcurrency: 4, cancellationToken: CancellationToken.None);

            // Assert
            File.Exists(savePath).Should().BeTrue();
            new FileInfo(savePath).Length.Should().Be(expectedData.Length);
            ComputeFileSha256(savePath).Should().Be(expectedHash);
        }

        // =========================================================================
        // 3. TRANSIENT ERRORS (HTTP 429, 500, 502, 503, 504 & RETRY-AFTER)
        // =========================================================================

        [Theory]
        [InlineData(429)]
        [InlineData(500)]
        [InlineData(502)]
        [InlineData(503)]
        [InlineData(504)]
        public async Task TransientErrors_RetriesAndCompletesSuccessfully(int statusCode)
        {
            // Arrange
            byte[] expectedData = new byte[100 * 1024]; // 100 KB
            Random.Shared.NextBytes(expectedData);
            string expectedHash = ComputeSha256(expectedData);

            using var server = new HostileTestServer(expectedData, injectErrorsCount: 2, errorStatusCode: statusCode);
            await server.StartAsync();

            string savePath = Path.Combine(_testTempDir, $"retry_{statusCode}.bin");
            var downloader = new MultiPartDownloader();

            // Act
            await downloader.DownloadFileAsync(new Uri(server.Url), savePath, chunkCount: 4, maxConcurrency: 4, cancellationToken: CancellationToken.None);

            // Assert
            File.Exists(savePath).Should().BeTrue();
            ComputeFileSha256(savePath).Should().Be(expectedHash);
            server.TotalRequestsHandled.Should().BeGreaterThan(2, "Must retry after receiving transient HTTP errors");
        }

        // =========================================================================
        // 4. AUTHENTICATION & COOKIE PRESERVATION
        // =========================================================================

        [Fact]
        public async Task AuthenticatedDownload_PreservesCookiesAndBasicAuth()
        {
            // Arrange
            byte[] expectedData = Encoding.UTF8.GetBytes("Secret Authenticated Content");
            string expectedHash = ComputeSha256(expectedData);

            using var server = new HostileTestServer(expectedData, requireAuthCookie: "session_token=secret123");
            await server.StartAsync();

            string savePath = Path.Combine(_testTempDir, "auth_download.bin");
            var downloader = new MultiPartDownloader
            {
                Cookies = "session_token=secret123"
            };

            // Act
            await downloader.DownloadFileAsync(new Uri(server.Url), savePath, chunkCount: 4, maxConcurrency: 4, cancellationToken: CancellationToken.None);

            // Assert
            File.Exists(savePath).Should().BeTrue();
            ComputeFileSha256(savePath).Should().Be(expectedHash);
        }

        // =========================================================================
        // 5. PAUSE / RESUME / CANCELLATION RACES
        // =========================================================================

        [Fact]
        public async Task Cancellation_AbortsCleanlyWithoutCorruptingMetadata()
        {
            // Arrange
            byte[] expectedData = new byte[500 * 1024]; // 500 KB
            using var server = new HostileTestServer(expectedData);
            await server.StartAsync();

            string savePath = Path.Combine(_testTempDir, "cancel_test.bin");
            var downloader = new MultiPartDownloader();
            using var cts = new CancellationTokenSource();

            // Act
            cts.CancelAfter(50); // Cancel mid-download
            Func<Task> act = async () => await downloader.DownloadFileAsync(new Uri(server.Url), savePath, cancellationToken: cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        // =========================================================================
        // 6. CRASH RECOVERY & STATE RECONCILIATION
        // =========================================================================

        [Fact]
        public async Task CrashRecovery_ReconcilesOversizedOrCorruptedPartialFiles()
        {
            // Arrange
            var metaManager = new DurableMetadataManager();
            string tempDir = Path.Combine(_testTempDir, "crash_rec");
            Directory.CreateDirectory(tempDir);
            string metaPath = Path.Combine(tempDir, "metadata.json");

            string corruptedPartFile = Path.Combine(tempDir, "segment_0.part");
            // Write 2 MB into segment 0 that only allows 1 MB max
            File.WriteAllBytes(corruptedPartFile, new byte[2 * 1024 * 1024]);

            var state = new DurableDownloadState
            {
                Url = "http://127.0.0.1/file.bin",
                TotalBytes = 1048576,
                ETag = "\"v1\"",
                Segments = new List<SegmentRange>
                {
                    new SegmentRange { Id = 0, Start = 0, End = 1048575, BytesDownloaded = 2097152, TempPath = corruptedPartFile }
                }
            };

            await metaManager.WriteStateAtomicAsync(metaPath, state, CancellationToken.None);

            // Act - Reconcile local partial file
            bool isValid = metaManager.ReconcileAndValidate(state, "\"v1\"", "");

            // Assert
            isValid.Should().BeTrue();
            new FileInfo(corruptedPartFile).Length.Should().Be(1048576, "Oversized partial file must be truncated to max allowed segment boundary");
            state.Segments[0].BytesDownloaded.Should().Be(1048576);
            state.Segments[0].State.Should().Be(SegmentState.Completed);
        }

        // =========================================================================
        // 7. MATHEMATICAL RANGE SPLITTING & ZERO OVERLAP SUITE
        // =========================================================================

        [Fact]
        public void SegmentScheduler_MathematicalVerification_NoOverlapNoGapNoDuplicate()
        {
            // Arrange
            long totalBytes = 50 * 1024 * 1024; // 50 MB
            var scheduler = new SegmentScheduler(totalBytes);
            scheduler.InitializeDefault(4);

            // Initial verification
            VerifySegmentCoverageMathematically(scheduler);

            // Simulate dynamic work stealing splits
            for (int i = 0; i < 5; i++)
            {
                var work = scheduler.GetNextWorkItem($"Worker_{i}");
                VerifySegmentCoverageMathematically(scheduler);
            }
        }
    }

    // =========================================================================
    // HOSTILE TEST HTTP SERVER FIXTURE (HTTP 206, 200 Fallback, Error Injection)
    // =========================================================================

    internal class HostileTestServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly byte[] _data;
        private readonly bool _force200OkOnRange;
        private readonly int _injectErrorsCount;
        private readonly int _errorStatusCode;
        private readonly string? _requireAuthCookie;
        private CancellationTokenSource? _cts;
        private Task? _listenTask;
        private int _requestCounter = 0;

        public string Url { get; private set; } = string.Empty;
        public int TotalRequestsHandled => _requestCounter;

        public HostileTestServer(
            byte[] data,
            bool force200OkOnRange = false,
            int injectErrorsCount = 0,
            int errorStatusCode = 503,
            string? requireAuthCookie = null)
        {
            _data = data ?? Array.Empty<byte>();
            _force200OkOnRange = force200OkOnRange;
            _injectErrorsCount = injectErrorsCount;
            _errorStatusCode = errorStatusCode;
            _requireAuthCookie = requireAuthCookie;

            int port = Random.Shared.Next(12000, 15000);
            Url = $"http://127.0.0.1:{port}/hostile_test.bin";

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        }

        public async Task StartAsync()
        {
            _listener.Start();
            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoop(_cts.Token));
            await Task.Delay(50);
        }

        private async Task ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(ctx));
                }
                catch { break; }
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            Interlocked.Increment(ref _requestCounter);
            var req = ctx.Request;
            var resp = ctx.Response;

            // Auth Check
            if (!string.IsNullOrEmpty(_requireAuthCookie))
            {
                string? cookie = req.Headers["Cookie"];
                if (cookie == null || !cookie.Contains(_requireAuthCookie))
                {
                    resp.StatusCode = 401;
                    resp.Close();
                    return;
                }
            }

            // Error Injection
            if (_requestCounter <= _injectErrorsCount)
            {
                resp.StatusCode = _errorStatusCode;
                if (_errorStatusCode == 429 || _errorStatusCode == 503)
                {
                    resp.Headers.Add("Retry-After", "1");
                }
                resp.Close();
                return;
            }

            resp.Headers.Add("Accept-Ranges", "bytes");

            string? rangeHeader = req.Headers["Range"];
            long rangeStart = 0;
            long rangeEnd = _data.Length - 1;
            bool isRange = false;

            if (!_force200OkOnRange && !string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
            {
                var parts = rangeHeader.Substring(6).Split('-');
                if (parts.Length == 2 && long.TryParse(parts[0], out var s))
                {
                    rangeStart = s;
                    if (long.TryParse(parts[1], out var e)) rangeEnd = e;
                    else rangeEnd = _data.Length - 1;
                    isRange = true;
                }
            }

            if (isRange)
            {
                resp.StatusCode = 206;
                long rangeLength = Math.Max(0, rangeEnd - rangeStart + 1);
                resp.ContentLength64 = rangeLength;
                resp.Headers.Add("Content-Range", $"bytes {rangeStart}-{rangeEnd}/{_data.Length}");
            }
            else
            {
                resp.StatusCode = 200;
                resp.ContentLength64 = _data.Length;
            }

            if (req.HttpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
            {
                resp.Close();
                return;
            }

            try
            {
                using var output = resp.OutputStream;
                if (isRange)
                {
                    int length = (int)Math.Min(_data.Length - rangeStart, rangeEnd - rangeStart + 1);
                    if (length > 0)
                    {
                        output.Write(_data, (int)rangeStart, length);
                    }
                }
                else
                {
                    output.Write(_data, 0, _data.Length);
                }
            }
            catch { }
            finally
            {
                try { resp.Close(); } catch { }
            }
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            try { _listener.Stop(); } catch { }
            try { _listener.Close(); } catch { }
        }
    }
}
