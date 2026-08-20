using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class A2ScenarioTestServerSuite
    {
        // Scenario A: Throughput improves with additional workers -> Controller increases workers
        [Fact]
        public void ScenarioA_ThroughputImprovesWithAdditionalWorkers_ControllerScalesUp()
        {
            var controller = new AdaptiveConnectionController(initialConnections: 4, minConnections: 2, maxConnections: 16);
            controller.ResetCooldown();

            // Simulate 3 samples showing throughput scaling up with connection count
            controller.RecordTelemetry(aggregateThroughputBps: 2_000_000, averageRttMs: 30.0, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 3_000_000, averageRttMs: 30.0, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 4_500_000, averageRttMs: 30.0, errorCount: 0);

            int scaled = controller.EvaluateConnectionCount(totalFileSize: 50 * 1024 * 1024, isMeteredNetwork: false);
            scaled.Should().Be(6, "Controller must gradually scale from 4 -> 6 (+2) when throughput consistently improves");
        }

        // Scenario B: Throughput saturates -> Controller stops increasing
        [Fact]
        public void ScenarioB_ThroughputSaturates_ControllerStopsIncreasing()
        {
            var controller = new AdaptiveConnectionController(initialConnections: 8, minConnections: 2, maxConnections: 16);
            controller.ResetCooldown();

            // Simulate 3 samples showing saturated flat throughput (0% gain)
            controller.RecordTelemetry(aggregateThroughputBps: 5_000_000, averageRttMs: 40.0, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 5_010_000, averageRttMs: 40.0, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 5_005_000, averageRttMs: 40.0, errorCount: 0);

            int scaled = controller.EvaluateConnectionCount(totalFileSize: 50 * 1024 * 1024, isMeteredNetwork: false);
            scaled.Should().Be(8, "Controller must stop scaling up when throughput gain saturates");
        }

        // Scenario C: More workers reduce throughput -> Controller eventually backs down
        [Fact]
        public void ScenarioC_MoreWorkersReduceThroughput_ControllerBacksDown()
        {
            var controller = new AdaptiveConnectionController(initialConnections: 12, minConnections: 2, maxConnections: 16);
            controller.ResetCooldown();

            // Simulate 3 samples showing throughput dropping by > 20% due to connection congestion
            controller.RecordTelemetry(aggregateThroughputBps: 10_000_000, averageRttMs: 50.0, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 8_000_000, averageRttMs: 80.0, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 6_500_000, averageRttMs: 120.0, errorCount: 0);

            int scaled = controller.EvaluateConnectionCount(totalFileSize: 50 * 1024 * 1024, isMeteredNetwork: false);
            scaled.Should().BeLessThan(12, "Controller must decrease connections when throughput drops significantly");
        }

        // Scenario D: HTTP 429 after high concurrency -> Controller reduces pressure
        [Fact]
        public void ScenarioD_Http429AfterHighConcurrency_ControllerReducesPressure()
        {
            var controller = new AdaptiveConnectionController(initialConnections: 16, minConnections: 2, maxConnections: 32);
            controller.ResetCooldown();

            // Record 3 samples ending with HTTP 429
            controller.RecordTelemetry(aggregateThroughputBps: 12_000_000, averageRttMs: 60.0, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 12_000_000, averageRttMs: 60.0, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 12_000_000, averageRttMs: 60.0, errorCount: 0, http429Count: 1);

            int scaled = controller.EvaluateConnectionCount(totalFileSize: 100 * 1024 * 1024, isMeteredNetwork: false);
            scaled.Should().Be(14, "Controller must reduce concurrency (-2) upon encountering HTTP 429 rate limiting");
        }

        // Scenario E: Repeated 503 -> Concurrency decreases and retry policy engages
        [Fact]
        public void ScenarioE_Repeated503_ConcurrencyDecreasesAndRetryPolicyEngages()
        {
            var controller = new AdaptiveConnectionController(initialConnections: 10, minConnections: 2, maxConnections: 32);
            controller.ResetCooldown();

            // Record 3 samples ending with HTTP 503 errors
            controller.RecordTelemetry(aggregateThroughputBps: 4_000_000, averageRttMs: 150.0, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 4_000_000, averageRttMs: 150.0, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 4_000_000, averageRttMs: 150.0, errorCount: 0, http5xxCount: 2);

            int scaled = controller.EvaluateConnectionCount(totalFileSize: 100 * 1024 * 1024, isMeteredNetwork: false);
            scaled.Should().Be(8, "Controller must decrease concurrency by -2 on 503 Service Unavailable errors");
        }


        // Scenario F: High RTT -> Controller adjusts based on measurements
        [Fact]
        public void ScenarioF_HighRTT_ControllerBacksDownOnLatencySpike()
        {
            var controller = new AdaptiveConnectionController(initialConnections: 10, minConnections: 2, maxConnections: 32);
            controller.ResetCooldown();

            // Baseline RTT 40ms -> High RTT 120ms (> 200% baseline)
            controller.RecordTelemetry(aggregateThroughputBps: 5_000_000, averageRttMs: 40.0, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 5_000_000, averageRttMs: 80.0, errorCount: 0);
            controller.RecordTelemetry(aggregateThroughputBps: 5_000_000, averageRttMs: 120.0, errorCount: 0);

            int scaled = controller.EvaluateConnectionCount(totalFileSize: 100 * 1024 * 1024, isMeteredNetwork: false);
            scaled.Should().Be(9, "Controller must scale down (-1) when RTT latency spikes significantly over baseline");
        }

        // Scenario G: One worker is extremely slow -> Other workers continue safely and scheduler uses remaining work efficiently
        [Fact]
        public async Task ScenarioG_OneWorkerExtremelySlow_OtherWorkersContinueAndCompleteSafely()
        {
            int payloadSize = 5 * 1024 * 1024; // 5 MB
            byte[] payload = new byte[payloadSize];
            new Random(77).NextBytes(payload);

            string expectedSha256;
            using (var sha = SHA256.Create())
            {
                expectedSha256 = Convert.ToHexString(sha.ComputeHash(payload));
            }

            HttpListener? listener = null;
            int port = 0;
            string prefix = "";
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    port = Random.Shared.Next(48500, 59000);
                    prefix = $"http://127.0.0.1:{port}/scenario-g/";
                    var l = new HttpListener();
                    l.Prefixes.Add(prefix);
                    l.Start();
                    listener = l;
                    break;
                }
                catch (HttpListenerException) when (attempt < 9) { }
            }
            if (listener == null) throw new InvalidOperationException("Failed to bind HttpListener after retries");
            using var cleanupListener = listener;

            // Server delays range start == 0 (Worker 0) by 200ms
            var serverTask = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var ctx = await listener.GetContextAsync().ConfigureAwait(false);
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                string rangeHeader = ctx.Request.Headers["Range"];
                                if (ctx.Request.HttpMethod == "HEAD")
                                {
                                    ctx.Response.StatusCode = 200;
                                    ctx.Response.Headers.Add("Accept-Ranges", "bytes");
                                    ctx.Response.ContentLength64 = payload.Length;
                                }
                                else if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                                {
                                    var parts = rangeHeader.Substring(6).Split('-');
                                    long start = long.Parse(parts[0]);
                                    long end = long.Parse(parts[1]);
                                    long len = end - start + 1;

                                    if (start == 0)
                                    {
                                        await Task.Delay(200).ConfigureAwait(false); // Simulate slow worker 0
                                    }

                                    ctx.Response.StatusCode = 206;
                                    ctx.Response.Headers.Add("Content-Range", $"bytes {start}-{end}/{payload.Length}");
                                    ctx.Response.ContentLength64 = len;

                                    await ctx.Response.OutputStream.WriteAsync(payload.AsMemory((int)start, (int)len)).ConfigureAwait(false);
                                }
                                else
                                {
                                    ctx.Response.StatusCode = 200;
                                    ctx.Response.ContentLength64 = payload.Length;
                                    await ctx.Response.OutputStream.WriteAsync(payload).ConfigureAwait(false);
                                }
                                ctx.Response.Close();
                            }
                            catch { }
                        });
                    }
                    catch { break; }
                }
            });

            string tempFile = Path.Combine(Path.GetTempPath(), $"scenario_g_{Guid.NewGuid():N}.bin");

            try
            {
                using var httpClient = new HttpClient();
                var downloader = new MultiPartDownloader(httpClient);

                await downloader.DownloadFileAsync(
                    fileUrl: new Uri(prefix),
                    destinationFilePath: tempFile,
                    chunkCount: 2,
                    maxConcurrency: 4,
                    progress: null,
                    cancellationToken: CancellationToken.None).ConfigureAwait(false);

                File.Exists(tempFile).Should().BeTrue();
                byte[] downloadedData = await File.ReadAllBytesAsync(tempFile);
                downloadedData.Length.Should().Be(payload.Length);

                string actualSha256;
                using (var sha = SHA256.Create())
                {
                    actualSha256 = Convert.ToHexString(sha.ComputeHash(downloadedData));
                }

                actualSha256.Should().Be(expectedSha256, "Download must complete cleanly with 100% SHA256 match when one worker is slow");
            }
            finally
            {
                listener.Stop();
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
