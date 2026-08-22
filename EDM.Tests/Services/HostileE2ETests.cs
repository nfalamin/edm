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
using EDM.Models;
using EDM.Services;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace EDM.Tests.Services
{
    /// <summary>
    /// Phase A6 — Hostile End-to-End Download Validation & Benchmark Suite
    /// Includes deterministic server fault injection, matrix scenarios, performance diagnostics,
    /// and 100 randomized hostile stress tests.
    /// </summary>
    public class HostileE2ETests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _tempFolder;

        public HostileE2ETests(ITestOutputHelper output)
        {
            _output = output;
            _tempFolder = Path.Combine(Path.GetTempPath(), "edm_a6_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempFolder, recursive: true); } catch { }
        }

        #region Helper: Deterministic Payload & SHA256 Generator
        private static byte[] GenerateDeterministicPayload(int length, int seed = 42)
        {
            var data = new byte[length];
            var rng = new Random(seed);
            rng.NextBytes(data);
            return data;
        }

        private static string ComputeSha256(byte[] data)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }

        private static async Task<string> ComputeFileSha256Async(string filePath)
        {
            var service = new FileIntegrityService();
            return await service.ComputeSha256Async(filePath, CancellationToken.None);
        }
        #endregion

        #region Hostile Local Test Server
        public class HostileServerConfig
        {
            public byte[] Data { get; set; } = Array.Empty<byte>();
            public string ETag { get; set; } = "\"hostile-v1\"";
            public string LastModified { get; set; } = "Mon, 10 Aug 2026 00:00:00 GMT";
            public bool SupportRanges { get; set; } = true;
            public double ResetProbability { get; set; } = 0.0;
            public double Error429Probability { get; set; } = 0.0;
            public double Error503Probability { get; set; } = 0.0;
            public int DelayMs { get; set; } = 0;
            public bool CorruptContentRangeHeader { get; set; } = false;
            public bool TruncateResponseBody { get; set; } = false;
        }

        public class HostileServer : IAsyncDisposable
        {
            private readonly HttpListener _listener;
            private readonly HostileServerConfig _config;
            private readonly CancellationTokenSource _cts = new();

            public string Url { get; }
            public int TotalRequests => _totalRequests;
            public int ResetCount => _resetCount;
            public int Retry429Count => _retry429Count;
            public int Retry503Count => _retry503Count;

            private int _totalRequests;
            private int _resetCount;
            private int _retry429Count;
            private int _retry503Count;

            public HostileServer(HostileServerConfig config)
            {
                _config = config;
                int port = FindFreePort();
                Url = $"http://127.0.0.1:{port}/hostile-file";

                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                _listener.Start();

                _ = Task.Run(ListenAsync);
            }

            private static int FindFreePort()
            {
                var tcp = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
                tcp.Start();
                int port = ((IPEndPoint)tcp.LocalEndpoint).Port;
                tcp.Stop();
                return port;
            }

            private async Task ListenAsync()
            {
                while (_listener.IsListening && !_cts.IsCancellationRequested)
                {
                    try
                    {
                        var ctx = await _listener.GetContextAsync();
                        _ = Task.Run(() => HandleRequestAsync(ctx));
                    }
                    catch { break; }
                }
            }

            private async Task HandleRequestAsync(HttpListenerContext ctx)
            {
                Interlocked.Increment(ref _totalRequests);
                try
                {
                    ctx.Response.Headers["Connection"] = "close";
                    ctx.Response.Headers["ETag"] = _config.ETag;
                    ctx.Response.Headers["Last-Modified"] = _config.LastModified;

                    if (_config.DelayMs > 0)
                    {
                        await Task.Delay(_config.DelayMs);
                    }

                    // 429 Rate limiting fault injection
                    if (_config.Error429Probability > 0 && Random.Shared.NextDouble() < _config.Error429Probability)
                    {
                        Interlocked.Increment(ref _retry429Count);
                        ctx.Response.StatusCode = 429;
                        ctx.Response.Headers["Retry-After"] = "1";
                        ctx.Response.Close();
                        return;
                    }

                    // 503 Server error fault injection
                    if (_config.Error503Probability > 0 && Random.Shared.NextDouble() < _config.Error503Probability)
                    {
                        Interlocked.Increment(ref _retry503Count);
                        ctx.Response.StatusCode = 503;
                        ctx.Response.Headers["Retry-After"] = "1";
                        ctx.Response.Close();
                        return;
                    }

                    // HEAD request
                    if (ctx.Request.HttpMethod == "HEAD")
                    {
                        ctx.Response.StatusCode = 200;
                        if (_config.SupportRanges)
                        {
                            ctx.Response.Headers["Accept-Ranges"] = "bytes";
                        }
                        ctx.Response.ContentLength64 = _config.Data.Length;
                        ctx.Response.Close();
                        return;
                    }

                    string? rangeHeader = ctx.Request.Headers["Range"];
                    if (_config.SupportRanges && rangeHeader != null && rangeHeader.StartsWith("bytes="))
                    {
                        var parts = rangeHeader.Substring(6).Split('-');
                        long start = long.Parse(parts[0]);
                        long end = parts.Length > 1 && !string.IsNullOrEmpty(parts[1])
                            ? long.Parse(parts[1])
                            : _config.Data.Length - 1;

                        end = Math.Min(end, _config.Data.Length - 1);
                        long length = end - start + 1;

                        ctx.Response.StatusCode = 206;

                        if (_config.CorruptContentRangeHeader)
                        {
                            ctx.Response.Headers["Content-Range"] = $"bytes {start + 500}-{end + 500}/{_config.Data.Length}";
                        }
                        else
                        {
                            ctx.Response.Headers["Content-Range"] = $"bytes {start}-{end}/{_config.Data.Length}";
                        }

                        ctx.Response.ContentLength64 = length;

                        // Mid-stream connection reset injection
                        if (_config.ResetProbability > 0 && Random.Shared.NextDouble() < _config.ResetProbability)
                        {
                            Interlocked.Increment(ref _resetCount);
                            int partialWrite = (int)Math.Min(length / 2, 8192);
                            if (partialWrite > 0)
                            {
                                await ctx.Response.OutputStream.WriteAsync(_config.Data, (int)start, partialWrite);
                            }
                            ctx.Response.Abort();
                            return;
                        }

                        // Truncated response body injection
                        if (_config.TruncateResponseBody && length > 10)
                        {
                            int shortLen = (int)length - 5;
                            await ctx.Response.OutputStream.WriteAsync(_config.Data, (int)start, shortLen);
                            ctx.Response.Close();
                            return;
                        }

                        await ctx.Response.OutputStream.WriteAsync(_config.Data, (int)start, (int)length);
                        ctx.Response.Close();
                        return;
                    }

                    // Standard 200 response
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentLength64 = _config.Data.Length;
                    await ctx.Response.OutputStream.WriteAsync(_config.Data, 0, _config.Data.Length);
                    ctx.Response.Close();
                }
                catch
                {
                    try { ctx.Response.Abort(); } catch { }
                }
            }

            public async ValueTask DisposeAsync()
            {
                _cts.Cancel();
                try { _listener.Stop(); } catch { }
                await Task.CompletedTask;
            }
        }
        #endregion

        #region Helper: Downloader Factory
        private static MultiPartDownloader CreateEngine()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.Zero,
                PooledConnectionIdleTimeout = TimeSpan.Zero,
                MaxConnectionsPerServer = 64
            };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
            return new MultiPartDownloader(client);
        }
        #endregion

        #region 1. Connection Matrix Tests (1, 2, 4, 8, 16, 32 connections)
        [Theory]
        [InlineData(1, 1 * 1024 * 1024)]    // 1 MB, 1 connection
        [InlineData(2, 2 * 1024 * 1024)]    // 2 MB, 2 connections
        [InlineData(4, 5 * 1024 * 1024)]    // 5 MB, 4 connections
        [InlineData(8, 10 * 1024 * 1024)]   // 10 MB, 8 connections
        [InlineData(16, 10 * 1024 * 1024)]  // 10 MB, 16 connections
        [InlineData(32, 10 * 1024 * 1024)]  // 10 MB, 32 connections
        public async Task ConnectionMatrix_ValidatesSHA256AndLength(int connectionCount, int payloadSize)
        {
            byte[] payload = GenerateDeterministicPayload(payloadSize);
            string expectedHash = ComputeSha256(payload);

            var serverConfig = new HostileServerConfig { Data = payload };
            await using var server = new HostileServer(serverConfig);

            string destPath = Path.Combine(_tempFolder, $"conn_matrix_{connectionCount}.bin");

            var engine = CreateEngine();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            await engine.DownloadFileAsync(
                new Uri(server.Url),
                destPath,
                chunkCount: connectionCount,
                maxConcurrency: connectionCount,
                cancellationToken: cts.Token);

            File.Exists(destPath).Should().BeTrue();
            new FileInfo(destPath).Length.Should().Be(payloadSize);

            string actualHash = await ComputeFileSha256Async(destPath);
            actualHash.Should().Be(expectedHash, $"SHA-256 for {connectionCount} workers must match fixture");
        }
        #endregion

        #region 2. Hostile Failure Matrix Tests
        [Fact]
        public async Task Matrix_AdaptiveScaling_With429RateLimits_Succeeds()
        {
            byte[] payload = GenerateDeterministicPayload(3 * 1024 * 1024);
            string expectedHash = ComputeSha256(payload);

            var serverConfig = new HostileServerConfig
            {
                Data = payload,
                Error429Probability = 0.5 // 50% rate-limit injection to ensure retries trigger
            };
            await using var server = new HostileServer(serverConfig);

            string destPath = Path.Combine(_tempFolder, "matrix_429.bin");
            var engine = CreateEngine();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            await engine.DownloadFileAsync(
                new Uri(server.Url),
                destPath,
                chunkCount: 4,
                maxConcurrency: 8,
                cancellationToken: cts.Token);

            new FileInfo(destPath).Length.Should().Be(payload.Length);
            string actualHash = await ComputeFileSha256Async(destPath);
            actualHash.Should().Be(expectedHash);
        }

        [Fact]
        public async Task Matrix_DynamicSplit_WithConnectionResets_RecoversAndCompletes()
        {
            byte[] payload = GenerateDeterministicPayload(4 * 1024 * 1024);
            string expectedHash = ComputeSha256(payload);

            var serverConfig = new HostileServerConfig
            {
                Data = payload,
                ResetProbability = 0.5 // 50% mid-stream reset injection to test retry/recovery
            };
            await using var server = new HostileServer(serverConfig);

            string destPath = Path.Combine(_tempFolder, "matrix_resets.bin");
            var engine = CreateEngine();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            await engine.DownloadFileAsync(
                new Uri(server.Url),
                destPath,
                chunkCount: 4,
                maxConcurrency: 4,
                cancellationToken: cts.Token);

            new FileInfo(destPath).Length.Should().Be(payload.Length);
            string actualHash = await ComputeFileSha256Async(destPath);
            actualHash.Should().Be(expectedHash);
        }

        [Fact]
        public async Task Matrix_PauseAndResume_PreservesIntegrity()
        {
            byte[] payload = GenerateDeterministicPayload(5 * 1024 * 1024);
            string expectedHash = ComputeSha256(payload);

            var serverConfig = new HostileServerConfig { Data = payload };
            await using var server = new HostileServer(serverConfig);

            string destPath = Path.Combine(_tempFolder, "matrix_pause_resume.bin");
            var engine = CreateEngine();

            // Cancel mid-way after 100ms
            using (var cts1 = new CancellationTokenSource(100))
            {
                try
                {
                    await engine.DownloadFileAsync(new Uri(server.Url), destPath, chunkCount: 4, maxConcurrency: 4, cancellationToken: cts1.Token);
                }
                catch (OperationCanceledException) { }
            }

            // Resume to completion
            using (var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                await engine.DownloadFileAsync(new Uri(server.Url), destPath, chunkCount: 4, maxConcurrency: 4, cancellationToken: cts2.Token);
            }

            new FileInfo(destPath).Length.Should().Be(payload.Length);
            string actualHash = await ComputeFileSha256Async(destPath);
            actualHash.Should().Be(expectedHash);
        }
        #endregion

        #region 3. Adaptive Controller Evidence & Benchmark
        [Fact]
        public async Task AdaptiveController_DemonstratesActiveScalingDecisions()
        {
            var controller = new AdaptiveConnectionController(initialConnections: 2, minConnections: 1, maxConnections: 8);

            // Feed samples indicating high throughput gain
            controller.RecordTelemetry(aggregateThroughputBps: 1_000_000, averageRttMs: 15, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 2_000_000, averageRttMs: 15, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 4_000_000, averageRttMs: 15, errorCount: 0);

            controller.ResetCooldown();
            int updatedConns = controller.EvaluateConnectionCount(totalFileSize: 50 * 1024 * 1024, isMeteredNetwork: false);
            updatedConns.Should().BeGreaterThanOrEqualTo(2, "controller should maintain or scale up connections on high throughput");

            // Feed errors to trigger scale down
            controller.RecordTelemetry(aggregateThroughputBps: 10_000, averageRttMs: 200, errorCount: 3);
            controller.RecordTelemetry(aggregateThroughputBps: 10_000, averageRttMs: 200, errorCount: 3);
            controller.RecordTelemetry(aggregateThroughputBps: 10_000, averageRttMs: 200, errorCount: 3);

            controller.ResetCooldown();
            int scaledDownConns = controller.EvaluateConnectionCount(totalFileSize: 50 * 1024 * 1024, isMeteredNetwork: false);
            scaledDownConns.Should().BeLessThan(updatedConns, "controller should scale down under errors");
        }

        [Fact]
        public async Task PerformanceBenchmark_MeasuresAllocationsAndThroughput()
        {
            byte[] payload = GenerateDeterministicPayload(10 * 1024 * 1024); // 10 MB
            string expectedHash = ComputeSha256(payload);

            var serverConfig = new HostileServerConfig { Data = payload };
            await using var server = new HostileServer(serverConfig);

            string destPath = Path.Combine(_tempFolder, "benchmark_10mb.bin");
            var engine = CreateEngine();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long initialMemory = GC.GetTotalAllocatedBytes(precise: true);
            var sw = Stopwatch.StartNew();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await engine.DownloadFileAsync(new Uri(server.Url), destPath, chunkCount: 8, maxConcurrency: 8, cancellationToken: cts.Token);

            sw.Stop();
            long finalMemory = GC.GetTotalAllocatedBytes(precise: true);
            long bytesAllocated = finalMemory - initialMemory;

            double mbps = (payload.Length / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;

            _output.WriteLine($"[Benchmark] Duration: {sw.ElapsedMilliseconds} ms | Speed: {mbps:F2} MB/s | Memory Allocated: {bytesAllocated / (1024 * 1024)} MB");

            new FileInfo(destPath).Length.Should().Be(payload.Length);
            string actualHash = await ComputeFileSha256Async(destPath);
            actualHash.Should().Be(expectedHash);
        }
        #endregion

        #region 4. 100 Randomized Hostile Stress Tests
        [Fact]
        public async Task StressTest_100RandomizedHostileScenarios_PassesWithoutCorruptionOrDeadlock()
        {
            int passCount = 0;
            int totalScenarios = 100;
            var rng = new Random(2026);

            for (int i = 0; i < totalScenarios; i++)
            {
                int payloadSize = rng.Next(64 * 1024, 2 * 1024 * 1024); // 64 KB to 2 MB
                byte[] payload = GenerateDeterministicPayload(payloadSize, seed: i);
                string expectedHash = ComputeSha256(payload);

                var config = new HostileServerConfig
                {
                    Data = payload,
                    ResetProbability = rng.NextDouble() < 0.3 ? 0.1 : 0.0,
                    Error429Probability = rng.NextDouble() < 0.3 ? 0.1 : 0.0,
                    Error503Probability = rng.NextDouble() < 0.2 ? 0.1 : 0.0,
                    DelayMs = rng.Next(0, 15)
                };

                await using var server = new HostileServer(config);
                string destPath = Path.Combine(_tempFolder, $"stress_scenario_{i}.bin");
                var engine = CreateEngine();

                int connections = rng.Next(1, 9);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                try
                {
                    await engine.DownloadFileAsync(
                        new Uri(server.Url),
                        destPath,
                        chunkCount: connections,
                        maxConcurrency: connections,
                        cancellationToken: cts.Token);

                    if (File.Exists(destPath) && new FileInfo(destPath).Length == payloadSize)
                    {
                        string actualHash = await ComputeFileSha256Async(destPath);
                        if (actualHash == expectedHash)
                        {
                            passCount++;
                        }
                        else
                        {
                            _output.WriteLine($"[Stress Fail] Hash mismatch in scenario {i}");
                        }
                    }
                    else
                    {
                        _output.WriteLine($"[Stress Fail] File size mismatch in scenario {i}");
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"[Stress Exception] Scenario {i} failed: {ex.Message}");
                }
            }

            _output.WriteLine($"[Stress Summary] Passed {passCount} / {totalScenarios} hostile scenarios.");
            passCount.Should().BeGreaterOrEqualTo(95, "at least 95 of 100 randomized hostile stress scenarios must pass without data corruption or deadlocks");
        }
        #endregion
    }
}
