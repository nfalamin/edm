using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class ForensicHttpCorrectnessTests : TestBase
    {
        [Fact]
        public async Task HttpRequestPipeline_ExecutesWithRetry_CreatesFreshRequestPerAttempt()
        {
            // Arrange
            int attemptCount = 0;
            using var listener = new HttpListener();
            int port = Random.Shared.Next(15000, 19999);
            string prefix = $"http://127.0.0.1:{port}/retry-test/";
            listener.Prefixes.Add(prefix);
            listener.Start();

            var serverTask = Task.Run(async () =>
            {
                for (int i = 0; i < 3; i++)
                {
                    var ctx = await listener.GetContextAsync();
                    attemptCount++;
                    if (i < 2)
                    {
                        ctx.Response.StatusCode = 503; // Service Unavailable
                        ctx.Response.Headers.Add("Retry-After", "1");
                    }
                    else
                    {
                        ctx.Response.StatusCode = 200;
                        byte[] payload = System.Text.Encoding.UTF8.GetBytes("SUCCESS");
                        ctx.Response.ContentLength64 = payload.Length;
                        await ctx.Response.OutputStream.WriteAsync(payload);
                    }
                    ctx.Response.Close();
                }
            });

            try
            {
                using var client = new HttpClient();
                var pipeline = new HttpRequestPipeline(client);

                // Act: Request should retry twice on 503 and succeed on attempt 3
                var result = await pipeline.ExecuteWithRetryAsync(
                    requestFactory: () => pipeline.CreateFreshRequest(HttpMethod.Get, new Uri(prefix)),
                    completionOption: HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken: CancellationToken.None,
                    maxRetries: 3
                );

                // Assert
                result.Should().NotBeNull();
                result.Response.StatusCode.Should().Be(HttpStatusCode.OK);
                string responseText = await result.Response.Content.ReadAsStringAsync();
                responseText.Should().Be("SUCCESS");
                attemptCount.Should().Be(3, "Must execute exactly 3 attempts creating a fresh request each time");
            }
            finally
            {
                listener.Stop();
                await serverTask;
            }
        }

        [Fact]
        public async Task MultiPartDownloader_FallbackToSingleStream_WhenServerReturns200ToRangeRequest()
        {
            // Arrange: Local server that ignores Range headers and always returns 200 OK
            using var listener = new HttpListener();
            int port = Random.Shared.Next(20000, 24999);
            string prefix = $"http://127.0.0.1:{port}/200-fallback/";
            listener.Prefixes.Add(prefix);
            listener.Start();

            byte[] expectedData = new byte[1024 * 1024]; // 1 MB payload
            Random.Shared.NextBytes(expectedData);

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
                                ctx.Response.StatusCode = 200;
                                ctx.Response.ContentLength64 = expectedData.Length;
                                if (ctx.Request.HttpMethod != "HEAD")
                                {
                                    await ctx.Response.OutputStream.WriteAsync(expectedData).ConfigureAwait(false);
                                }
                                ctx.Response.Close();
                            }
                            catch { }
                        });
                    }
                    catch { break; }
                }
            });

            string tempFile = Path.Combine(Path.GetTempPath(), "fallback_test_" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                using var client = new HttpClient();
                var downloader = new MultiPartDownloader(client);

                // Act: Download file with 4 chunks requested against a server returning 200 OK
                await downloader.DownloadFileAsync(new Uri(prefix), tempFile, chunkCount: 4, maxConcurrency: 4, progress: null, cancellationToken: CancellationToken.None);

                // Assert: Output file must exist and match expected size and hash exactly
                File.Exists(tempFile).Should().BeTrue();
                byte[] downloadedData = await File.ReadAllBytesAsync(tempFile);
                downloadedData.Length.Should().Be(expectedData.Length);
                downloadedData.Should().Equal(expectedData, "200 OK fallback payload must match source payload exactly");
            }
            finally
            {
                listener.Stop();
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
