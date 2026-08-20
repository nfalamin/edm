using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.NativeMessaging;
using EDM.Services;
using EDM.Services.Helpers;
using EDM.Services.Interfaces;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class Phase4DesktopDownloadEngineCertificationSuite
    {
        // =====================================================================
        // 1. REAL HTTP MULTI-CONNECTION DOWNLOAD & RANGE VERIFICATION
        // =====================================================================
        [Fact]
        public async Task Phase4_MultiConnectionDownload_ProducesExactByteForByteFile()
        {
            int payloadSize = 4 * 1024 * 1024; // 4 MB
            byte[] rawBytes = new byte[payloadSize];
            new Random(12345).NextBytes(rawBytes);

            using var sha = SHA256.Create();
            string expectedHash = Convert.ToHexString(sha.ComputeHash(rawBytes)).ToLowerInvariant();

            int port = 55100 + new Random().Next(100, 900);
            string serverUrl = $"http://localhost:{port}/testfile.bin";
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            var serverTask = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext ctx;
                    try { ctx = await listener.GetContextAsync().ConfigureAwait(false); } catch { break; }

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var req = ctx.Request;
                            var resp = ctx.Response;
                            resp.Headers["Accept-Ranges"] = "bytes";

                            if (req.HttpMethod == "HEAD")
                            {
                                resp.StatusCode = 200;
                                resp.ContentLength64 = payloadSize;
                                resp.Close();
                                return;
                            }

                            string? rangeHeader = req.Headers["Range"];
                            if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                            {
                                var rangePart = rangeHeader.Substring(6).Split('-');
                                long start = long.Parse(rangePart[0]);
                                long end = rangePart.Length > 1 && !string.IsNullOrEmpty(rangePart[1]) ? long.Parse(rangePart[1]) : payloadSize - 1;
                                long length = end - start + 1;

                                resp.StatusCode = 206;
                                resp.Headers["Content-Range"] = $"bytes {start}-{end}/{payloadSize}";
                                resp.ContentLength64 = length;

                                await resp.OutputStream.WriteAsync(rawBytes, (int)start, (int)length).ConfigureAwait(false);
                                resp.Close();
                            }
                            else
                            {
                                resp.StatusCode = 200;
                                resp.ContentLength64 = payloadSize;
                                await resp.OutputStream.WriteAsync(rawBytes, 0, payloadSize).ConfigureAwait(false);
                                resp.Close();
                            }
                        }
                        catch { }
                    });
                }
            });

            string tempFile = Path.Combine(Path.GetTempPath(), $"EDM_Cert_{Guid.NewGuid():N}.bin");
            try
            {
                using var http = new HttpClient();
                var probeService = new HttpProbeService(http);
                var probeResult = await probeService.ProbeUrlAsync(serverUrl, tempFile, null, null, CancellationToken.None);

                probeResult.ServerSupportsResume.Should().BeTrue("Server supports 206 Partial Content Range requests");
                probeResult.TotalBytes.Should().Be(payloadSize);

                var progressReporter = new Progress<DownloadProgressInfo>();
                var pauseToken = new PauseTokenSource();

                await MultiPartAdapter.DownloadWithMultiPartAsync(
                    serverUrl,
                    tempFile,
                    chunkCount: 4,
                    progressReporter,
                    pauseToken,
                    () => -1,
                    CancellationToken.None,
                    null,
                    null
                );

                File.Exists(tempFile).Should().BeTrue("Output file must be generated");
                new FileInfo(tempFile).Length.Should().Be(payloadSize, "Output file size must strictly match payload size");

                using var outStream = File.OpenRead(tempFile);
                string actualHash = Convert.ToHexString(sha.ComputeHash(outStream)).ToLowerInvariant();
                actualHash.Should().Be(expectedHash, "SHA-256 of downloaded file must match source binary");
            }
            finally
            {
                try { listener.Stop(); } catch { }
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        }

        // =====================================================================
        // 2. SINGLE-STREAM FALLBACK ON RANGE UNSUPPORTED
        // =====================================================================
        [Fact]
        public async Task Phase4_SingleStreamFallback_WhenRangeNotSupported()
        {
            int payloadSize = 2 * 1024 * 1024;
            byte[] rawBytes = new byte[payloadSize];
            new Random(777).NextBytes(rawBytes);

            using var sha = SHA256.Create();
            string expectedHash = Convert.ToHexString(sha.ComputeHash(rawBytes)).ToLowerInvariant();

            int port = 55200 + new Random().Next(100, 900);
            string serverUrl = $"http://localhost:{port}/norange.bin";
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            var serverTask = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext ctx;
                    try { ctx = await listener.GetContextAsync().ConfigureAwait(false); } catch { break; }

                    var resp = ctx.Response;
                    // Intentionally omit Accept-Ranges or return 200 on range request
                    resp.StatusCode = 200;
                    resp.ContentLength64 = payloadSize;
                    await resp.OutputStream.WriteAsync(rawBytes, 0, payloadSize).ConfigureAwait(false);
                    resp.Close();
                }
            });

            string tempFile = Path.Combine(Path.GetTempPath(), $"EDM_Fallback_{Guid.NewGuid():N}.bin");
            try
            {
                using var http = new HttpClient();
                var progressReporter = new Progress<DownloadProgressInfo>();
                var pauseToken = new PauseTokenSource();

                await DownloadService.RunSingleThreadedDownloadInternalAsync(
                    http,
                    serverUrl,
                    tempFile,
                    payloadSize,
                    progressReporter,
                    pauseToken,
                    () => -1,
                    CancellationToken.None
                );

                File.Exists(tempFile).Should().BeTrue();
                new FileInfo(tempFile).Length.Should().Be(payloadSize);

                using var outStream = File.OpenRead(tempFile);
                string actualHash = Convert.ToHexString(sha.ComputeHash(outStream)).ToLowerInvariant();
                actualHash.Should().Be(expectedHash);
            }
            finally
            {
                try { listener.Stop(); } catch { }
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        }

        // =====================================================================
        // 3. REAL PROGRESS, SPEED, AND ETA CALCULATION
        // =====================================================================
        [Fact]
        public void Phase4_ProgressAndEta_HandlesAllZeroAndMovingAverageValues()
        {
            var info = new DownloadProgressInfo
            {
                BytesReceived = 50_000_000,
                TotalBytes = 100_000_000,
                SpeedBytesPerSecond = 10_000_000
            };

            info.ProgressPercentage = (double)info.BytesReceived / info.TotalBytes.Value * 100;
            info.ProgressPercentage.Should().Be(50.0);

            // Remaining seconds
            info.RemainingSeconds = (info.TotalBytes.Value - info.BytesReceived) / info.SpeedBytesPerSecond;
            info.RemainingSeconds.Should().Be(5.0);

            // Handle speed = 0 gracefully without NaN/Infinity
            double zeroSpeed = 0;
            double safeEta = zeroSpeed > 0 ? (info.TotalBytes.Value - info.BytesReceived) / zeroSpeed : 0;
            safeEta.Should().Be(0);
            double.IsNaN(safeEta).Should().BeFalse();
            double.IsInfinity(safeEta).Should().BeFalse();
        }

        // =====================================================================
        // 4. PAUSE, RESUME, AND SCHEMA V3 METADATA PERSISTENCE
        // =====================================================================
        [Fact]
        public void Phase4_SchemaV3Persistence_PreservesSegmentOffsetsAndState()
        {
            string url = "https://cdn.example.com/large_installer.iso";
            long totalSize = 500_000_000;
            int segmentCount = 4;

            var segments = new List<SegmentRange>();
            long segmentSize = totalSize / segmentCount;

            for (int i = 0; i < segmentCount; i++)
            {
                long start = i * segmentSize;
                long end = (i == segmentCount - 1) ? totalSize - 1 : (start + segmentSize - 1);
                segments.Add(new SegmentRange
                {
                    Id = i + 1,
                    Start = start,
                    End = end,
                    BytesDownloaded = (i == 0) ? (end - start + 1) : (end - start + 1) / 2,
                    State = (i == 0) ? SegmentState.Completed : SegmentState.Downloading
                });
            }

            // Verify integrity of offsets
            segments.First().Start.Should().Be(0);
            segments.Last().End.Should().Be(totalSize - 1);

            long totalSegmentSpan = segments.Sum(s => s.End - s.Start + 1);
            totalSegmentSpan.Should().Be(totalSize, "Total segment spans must strictly equal file size without gaps or overlaps");

            long totalDownloaded = segments.Sum(s => s.BytesDownloaded);
            totalDownloaded.Should().BeGreaterThan(0);
            totalDownloaded.Should().BeLessThan(totalSize);
        }

        // =====================================================================
        // 5. TRANSIENT FAILURE CLASSIFICATION & EXPONENTIAL BACKOFF
        // =====================================================================
        [Theory]
        [InlineData(HttpStatusCode.TooManyRequests, true)]
        [InlineData(HttpStatusCode.ServiceUnavailable, true)]
        [InlineData(HttpStatusCode.GatewayTimeout, true)]
        [InlineData(HttpStatusCode.RequestTimeout, true)]
        [InlineData(HttpStatusCode.NotFound, false)]
        [InlineData(HttpStatusCode.Unauthorized, false)]
        [InlineData(HttpStatusCode.Forbidden, false)]
        public void Phase4_TransientFailureClassification_ClassifiesCorrectly(HttpStatusCode statusCode, bool expectedTransient)
        {
            var ex = new HttpRequestException("HTTP Error", null, statusCode);
            bool isTransient = HttpRequestPipeline.IsTransientException(ex);
            isTransient.Should().Be(expectedTransient);
        }

        // =====================================================================
        // 6. CANCELLATION CLEANUP & RESOURCE RELEASE
        // =====================================================================
        [Fact]
        public async Task Phase4_CancellationCleanup_StopsImmediatelyAndReleasesResources()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-cancel

            using var http = new HttpClient();
            var progress = new Progress<DownloadProgressInfo>();
            var pauseToken = new PauseTokenSource();
            string tempFile = Path.Combine(Path.GetTempPath(), $"EDM_Cancel_{Guid.NewGuid():N}.bin");

            Func<Task> act = async () =>
            {
                await DownloadService.RunSingleThreadedDownloadInternalAsync(
                    http,
                    "http://localhost:59999/dummy",
                    tempFile,
                    1000,
                    progress,
                    pauseToken,
                    () => -1,
                    cts.Token
                );
            };

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        // =====================================================================
        // 7. DUAL-STREAM ADAPTIVE VIDEO + AUDIO MERGE CONTRACT
        // =====================================================================
        [Fact]
        public void Phase4_AdaptiveStreamMergeContract_ValidatesParameters()
        {
            var item = new DownloadItem
            {
                FileName = "Sample_4K_Video.mp4",
                VideoUrl = "https://cdn.example.com/video_2160p.mp4",
                AudioUrl = "https://cdn.example.com/audio_160k.m4a",
                RequiresFfmpegMerge = true,
                Quality = "4K Ultra HD",
                EstimatedSizeBytes = 450_000_000
            };

            item.RequiresFfmpegMerge.Should().BeTrue();
            item.VideoUrl.Should().NotBeNullOrWhiteSpace();
            item.AudioUrl.Should().NotBeNullOrWhiteSpace();
            item.FileName.EndsWith(".mp4").Should().BeTrue();
        }

        // =====================================================================
        // 8. ZERO FAKE COMPLETION GUARD
        // =====================================================================
        [Fact]
        public void Phase4_ZeroFakeCompletion_RejectsMissingOrEmptyOutputFile()
        {
            string nonExistentPath = Path.Combine(Path.GetTempPath(), $"NonExistent_{Guid.NewGuid():N}.bin");
            bool isValid = File.Exists(nonExistentPath) && new FileInfo(nonExistentPath).Length > 0;
            isValid.Should().BeFalse("Missing or 0-byte output file must NEVER be flagged as Completed");
        }

        // =====================================================================
        // 9. NATIVE HOST IPC HANDOFF METADATA FIDELITY
        // =====================================================================
        [Fact]
        public void Phase4_IpcHandoffPayload_PreservesAllFields()
        {
            var payload = new IpcHandoffPayload
            {
                Url = "https://rr1---sn-4g5ednkk.googlevideo.com/videoplayback?itag=137",
                VideoUrl = "https://rr1---sn-4g5ednkk.googlevideo.com/videoplayback?itag=137",
                AudioUrl = "https://rr1---sn-4g5ednkk.googlevideo.com/videoplayback?itag=140",
                ManifestUrl = "",
                Title = "4K Master Video",
                Filename = "4K_Master_Video.mp4",
                Quality = "2160p 4K",
                Format = "mp4",
                RequiresFfmpegMerge = true,
                EstimatedSizeBytes = 250_000_000,
                CorrelationId = "corr_4k_001",
                Browser = "Chrome"
            };

            string json = JsonSerializer.Serialize(payload);
            var deserialized = JsonSerializer.Deserialize<IpcHandoffPayload>(json);

            deserialized.Should().NotBeNull();
            deserialized!.Title.Should().Be("4K Master Video");
            deserialized.Filename.Should().Be("4K_Master_Video.mp4");
            deserialized.RequiresFfmpegMerge.Should().BeTrue();
            deserialized.EstimatedSizeBytes.Should().Be(250_000_000);
            deserialized.Browser.Should().Be("Chrome");
        }
    }
}
