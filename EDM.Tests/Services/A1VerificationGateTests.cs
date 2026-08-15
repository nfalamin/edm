using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public class A1VerificationGateTests
    {
        [Fact]
        public async Task Run_A1_Verification_100_Repetitions_FullProductionPipeline()
        {
            const int totalRuns = 100;


            int passCount = 0;
            int failCount = 0;
            int corruptionCount = 0;
            int overlapCount = 0;
            int gapCount = 0;
            int deadlockCount = 0;
            int unobservedExCount = 0;
            int sha256MatchCount = 0;
            int sha256MismatchCount = 0;

            var unobservedMsgs = new List<string>();

            for (int run = 1; run <= totalRuns; run++)
            {
                var rand = new Random(run * 1000 + 42);

                int payloadSize = 10 * 1024 * 1024; // 10 MB
                byte[] expectedPayload = new byte[payloadSize];
                rand.NextBytes(expectedPayload);

                string expectedSha256;
                using (var sha = SHA256.Create())
                {
                    expectedSha256 = Convert.ToHexString(sha.ComputeHash(expectedPayload));
                }

                int port = rand.Next(40000, 48000);
                string prefix = $"http://127.0.0.1:{port}/a1-gate-test-{run}/";

                using var listener = new HttpListener();
                listener.Prefixes.Add(prefix);
                listener.Start();

                int slowWorkerDelayMs = rand.Next(10, 50);

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
                                        ctx.Response.ContentLength64 = expectedPayload.Length;
                                    }
                                    else if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                                    {
                                        var parts = rangeHeader.Substring(6).Split('-');
                                        long start = long.Parse(parts[0]);
                                        long end = long.Parse(parts[1]);
                                        long len = end - start + 1;

                                        if (start == 0 && slowWorkerDelayMs > 0)
                                        {
                                            await Task.Delay(slowWorkerDelayMs).ConfigureAwait(false);
                                        }

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

                string tempSavePath = Path.Combine(Path.GetTempPath(), $"a1_gate_run_{run}_{Guid.NewGuid():N}.bin");

                try
                {
                    using var httpClient = new HttpClient();
                    var downloader = new MultiPartDownloader(httpClient);

                    var downloadTask = downloader.DownloadFileAsync(
                        fileUrl: new Uri(prefix),
                        destinationFilePath: tempSavePath,
                        chunkCount: 2,
                        maxConcurrency: 4,
                        progress: null,
                        cancellationToken: CancellationToken.None);


                    bool completedInTime = await Task.WhenAny(downloadTask, Task.Delay(15000)) == downloadTask;

                    if (!completedInTime)
                    {
                        deadlockCount++;
                        failCount++;
                        continue;
                    }

                    await downloadTask; // Rethrow any exception if faulted

                    if (!File.Exists(tempSavePath))
                    {
                        failCount++;
                        continue;
                    }

                    byte[] downloadedData = await File.ReadAllBytesAsync(tempSavePath);
                    if (downloadedData.Length != expectedPayload.Length)
                    {
                        if (downloadedData.Length > expectedPayload.Length) overlapCount++;
                        else gapCount++;
                        failCount++;
                        continue;
                    }

                    string actualSha256;
                    using (var sha = SHA256.Create())
                    {
                        actualSha256 = Convert.ToHexString(sha.ComputeHash(downloadedData));
                    }

                    if (actualSha256 == expectedSha256)
                    {
                        sha256MatchCount++;
                        passCount++;
                    }
                    else
                    {
                        sha256MismatchCount++;
                        corruptionCount++;
                        failCount++;
                    }
                }
                catch (Exception ex)
                {
                    unobservedExCount++;
                    unobservedMsgs.Add($"Run #{run}: {ex.GetType().Name} ({ex.Message})");
                    failCount++;
                }
                finally
                {
                    try { listener.Stop(); } catch { }
                    await Task.Delay(10).ConfigureAwait(false);
                    if (File.Exists(tempSavePath)) File.Delete(tempSavePath);
                }

            }

            passCount.Should().Be(totalRuns, $"All {totalRuns} end-to-end dynamic splitting runs must pass with 0 corruption and 0 byte overlaps. (Passed: {passCount}, Failed: {failCount}, Deadlocks: {deadlockCount}, Corruptions: {corruptionCount}, Overlaps: {overlapCount}, Gaps: {gapCount}, UnobservedEx: {unobservedExCount}, SHA256Matches: {sha256MatchCount}, Mismatches: {sha256MismatchCount}, Details: {string.Join(" | ", unobservedMsgs.Take(5))})");

            sha256MatchCount.Should().Be(totalRuns);
            failCount.Should().Be(0);
            deadlockCount.Should().Be(0);
        }
    }
}


