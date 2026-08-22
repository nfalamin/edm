using System;
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
    public class ForensicA3AdaptiveControllerTests : TestBase
    {
        [Fact]
        public void AdaptiveController_RecordsTelemetry_AndEvaluatesConnectionScaling()
        {
            var controller = new AdaptiveConnectionController(initialConnections: 4, minConnections: 2, maxConnections: 16);
            controller.CurrentConnections.Should().Be(4);

            // Record baseline telemetry samples
            controller.RecordTelemetry(1_000_000, 50.0, 0);
            controller.RecordTelemetry(1_500_000, 48.0, 0);
            controller.RecordTelemetry(2_500_000, 45.0, 0); // > 15% throughput gain

            int evaluated = controller.EvaluateConnectionCount(100 * 1024 * 1024, isMeteredNetwork: false);

            evaluated.Should().BeGreaterThan(4, "Controller must scale up connection count when throughput increases by > 15%");
        }

        [Fact]
        public void AdaptiveController_BacksOff_OnServerErrors()
        {
            var controller = new AdaptiveConnectionController(initialConnections: 8, minConnections: 2, maxConnections: 16);

            // Record baseline
            controller.RecordTelemetry(2_000_000, 50.0, 0);
            controller.RecordTelemetry(2_000_000, 50.0, 0);
            controller.RecordTelemetry(1_800_000, 55.0, 2); // 2 server errors (e.g. 429/503)

            int evaluated = controller.EvaluateConnectionCount(100 * 1024 * 1024, isMeteredNetwork: false);

            evaluated.Should().BeLessThan(8, "Controller must back off connections when server errors occur");
        }

        [Fact]
        public async Task EndToEnd_AdaptiveController_RuntimeScaling_SHA256AndLengthVerified()
        {
            byte[] expectedPayload = new byte[6 * 1024 * 1024]; // 6 MB
            new Random(999).NextBytes(expectedPayload);

            string expectedHash;
            using (var sha = SHA256.Create())
            {
                expectedHash = Convert.ToHexString(sha.ComputeHash(expectedPayload));
            }

            using var listener = new HttpListener();
            int port = Random.Shared.Next(50000, 54999);
            string prefix = $"http://127.0.0.1:{port}/adaptive-test/";
            listener.Prefixes.Add(prefix);
            listener.Start();

            var serverTask = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var ctx = await listener.GetContextAsync();
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                string rangeHeader = ctx.Request.Headers["Range"];
                                if (ctx.Request.HttpMethod == "HEAD")
                                {
                                    ctx.Response.StatusCode = 200;
                                    ctx.Response.Headers.Add("Accept-Ranges", "bytes");
                                    ctx.Response.ContentLength64 = expectedPayload.Length;
                                }
                                else if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                                {
                                    var parts = rangeHeader.Substring(6).Split('-');
                                    long start = long.Parse(parts[0]);
                                    long end = long.Parse(parts[1]);
                                    long len = end - start + 1;

                                    ctx.Response.StatusCode = 206;
                                    ctx.Response.Headers.Add("Content-Range", $"bytes {start}-{end}/{expectedPayload.Length}");
                                    ctx.Response.ContentLength64 = len;

                                    await ctx.Response.OutputStream.WriteAsync(expectedPayload.AsMemory((int)start, (int)len)).ConfigureAwait(false);
                                }
                                else
                                {
                                    ctx.Response.StatusCode = 200;
                                    ctx.Response.ContentLength64 = expectedPayload.Length;
                                    await ctx.Response.OutputStream.WriteAsync(expectedPayload).ConfigureAwait(false);
                                }
                                ctx.Response.Close();
                            }
                            catch { }
                        });
                    }
                    catch { break; }
                }
            });

            string tempPath = Path.Combine(Path.GetTempPath(), "adaptive_e2e_" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                using var client = new HttpClient();
                var downloader = new MultiPartDownloader(client);

                // Act: Start with 4 connections, allow adaptive feedback loop to scale dynamically up to 16
                await downloader.DownloadFileAsync(new Uri(prefix), tempPath, chunkCount: 4, maxConcurrency: 16, progress: null, cancellationToken: CancellationToken.None);

                // Assert
                File.Exists(tempPath).Should().BeTrue();
                byte[] actualData = await File.ReadAllBytesAsync(tempPath);
                actualData.Length.Should().Be(expectedPayload.Length);

                string actualHash;
                using (var sha = SHA256.Create())
                {
                    actualHash = Convert.ToHexString(sha.ComputeHash(actualData));
                }

                actualHash.Should().Be(expectedHash, "SHA-256 payload identity must be preserved under runtime adaptive connection scaling");
            }
            finally
            {
                listener.Stop();
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
    }
}
