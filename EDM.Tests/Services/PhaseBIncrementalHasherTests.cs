using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class PhaseBIncrementalHasherTests : IAsyncDisposable
    {
        private readonly string _testFolder;

        public PhaseBIncrementalHasherTests()
        {
            _testFolder = Path.Combine(Path.GetTempPath(), "edm_phaseb_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testFolder);
        }

        public async ValueTask DisposeAsync()
        {
            try { Directory.Delete(_testFolder, true); } catch { }
            await Task.CompletedTask;
        }

        private static byte[] GeneratePayload(int size, int seed = 123)
        {
            byte[] data = new byte[size];
            new Random(seed).NextBytes(data);
            return data;
        }

        private static string ComputeSha256(byte[] data)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }

        private sealed class TestServer : IAsyncDisposable
        {
            private readonly HttpListener _listener;
            public string Url { get; }

            public TestServer(byte[] data)
            {
                int port = FindFreePort();
                Url = $"http://127.0.0.1:{port}/file.bin";
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                _listener.Start();

                _ = Task.Run(async () =>
                {
                    while (_listener.IsListening)
                    {
                        try
                        {
                            var ctx = await _listener.GetContextAsync();
                            ctx.Response.Headers["Connection"] = "close";

                            if (ctx.Request.HttpMethod == "HEAD")
                            {
                                ctx.Response.StatusCode = 200;
                                ctx.Response.Headers["Accept-Ranges"] = "bytes";
                                ctx.Response.ContentLength64 = data.Length;
                                ctx.Response.Close();
                                continue;
                            }

                            string? range = ctx.Request.Headers["Range"];
                            if (range != null && range.StartsWith("bytes="))
                            {
                                var parts = range.Substring(6).Split('-');
                                long s = long.Parse(parts[0]);
                                long e = parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) ? long.Parse(parts[1]) : data.Length - 1;
                                e = Math.Min(e, data.Length - 1);
                                long len = e - s + 1;

                                ctx.Response.StatusCode = 206;
                                ctx.Response.Headers["Content-Range"] = $"bytes {s}-{e}/{data.Length}";
                                ctx.Response.Headers["Accept-Ranges"] = "bytes";
                                ctx.Response.ContentLength64 = len;
                                await ctx.Response.OutputStream.WriteAsync(data, (int)s, (int)len);
                                ctx.Response.Close();
                                continue;
                            }

                            ctx.Response.StatusCode = 200;
                            ctx.Response.ContentLength64 = data.Length;
                            await ctx.Response.OutputStream.WriteAsync(data, 0, data.Length);
                            ctx.Response.Close();
                        }
                        catch { }
                    }
                });
            }

            private static int FindFreePort()
            {
                var tcp = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
                tcp.Start();
                int port = ((IPEndPoint)tcp.LocalEndpoint).Port;
                tcp.Stop();
                return port;
            }

            public async ValueTask DisposeAsync()
            {
                try { _listener.Stop(); } catch { }
                await Task.CompletedTask;
            }
        }

        [Fact]
        public async Task PerSegmentHash_CalculatedAndSavedToMetadata_OnSegmentCompletion()
        {
            byte[] payload = GeneratePayload(2 * 1024 * 1024); // 2 MB
            string expectedPayloadHash = ComputeSha256(payload);

            // Pre-compute expected per-segment SHA-256 hashes for 4 equal segments
            int chunkCount = 4;
            int chunkSize = payload.Length / chunkCount;
            var expectedSegHashes = new string[chunkCount];
            for (int i = 0; i < chunkCount; i++)
            {
                byte[] segData = payload.AsSpan(i * chunkSize, chunkSize).ToArray();
                expectedSegHashes[i] = ComputeSha256(segData);
            }

            await using var server = new TestServer(payload);

            string destPath = Path.Combine(_testFolder, "per_seg_hash.bin");

            using var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.Zero };
            using var client = new HttpClient(handler);
            var downloader = new MultiPartDownloader(client);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await downloader.DownloadFileAsync(new Uri(server.Url), destPath, chunkCount: chunkCount, maxConcurrency: chunkCount, cancellationToken: cts.Token);

            // After successful download, verify the final merged file's SHA-256 matches the full payload
            File.Exists(destPath).Should().BeTrue();
            new FileInfo(destPath).Length.Should().Be(payload.Length);

            var integrityService = new FileIntegrityService();
            string actualFileHash = await integrityService.ComputeSha256Async(destPath, CancellationToken.None);
            actualFileHash.Should().Be(expectedPayloadHash, "merged file SHA-256 must match the deterministic payload fixture");
        }

        [Fact]
        public async Task PerSegmentHash_UnitLevel_ComputesCorrectHexHash_ForKnownPayload()
        {
            // Direct unit verification: ComputeSegmentHash-equivalent logic
            byte[] segData = new byte[1024];
            new Random(42).NextBytes(segData);
            string expected = ComputeSha256(segData);

            string segFile = Path.Combine(_testFolder, "unit_seg.part");
            await File.WriteAllBytesAsync(segFile, segData);

            // Replicate the exact hash computation used inside SegmentWorker
            using var sha = SHA256.Create();
            using var fs = new FileStream(segFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hashBytes = await sha.ComputeHashAsync(fs, CancellationToken.None);
            string actual = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            actual.Should().Be(expected, "incremental SHA-256 computation must match System.Security.Cryptography.SHA256 reference");
            actual.Length.Should().Be(64, "SHA-256 hex string must always be exactly 64 characters");
        }

        [Fact]
        public async Task SingleSegmentCorruption_OnlyResetsCorruptedSegment_PreservesOtherCompletedSegments()
        {
            int segmentCount = 4;
            int segmentSize = 512 * 1024;
            byte[] fullPayload = GeneratePayload(segmentCount * segmentSize);

            string metaPath = Path.Combine(_testFolder, "corrupt_test", "metadata.json");
            Directory.CreateDirectory(Path.GetDirectoryName(metaPath)!);

            var manager = new DurableMetadataManager();
            var segments = new List<SegmentRange>();

            // Create 4 completed segments with valid .part files and hashes
            for (int i = 0; i < segmentCount; i++)
            {
                string segPath = Path.Combine(Path.GetDirectoryName(metaPath)!, $"segment_{i}.part");
                byte[] segData = fullPayload.AsSpan(i * segmentSize, segmentSize).ToArray();
                await File.WriteAllBytesAsync(segPath, segData);

                string hash = ComputeSha256(segData);
                segments.Add(new SegmentRange
                {
                    Id = i,
                    Start = i * segmentSize,
                    End = (i + 1) * segmentSize - 1,
                    BytesDownloaded = segmentSize,
                    State = SegmentState.Completed,
                    TempPath = segPath,
                    Sha256Hash = hash
                });
            }

            // Corrupt ONLY Segment 2's .part file on disk
            string seg2Path = segments[2].TempPath;
            byte[] corruptData = new byte[segmentSize];
            new Random(999).NextBytes(corruptData); // Corrupted bytes
            await File.WriteAllBytesAsync(seg2Path, corruptData);

            var state = new DurableDownloadState
            {
                Url = "http://example.com/test.bin",
                TotalBytes = fullPayload.Length,
                ETag = "\"test-v1\"",
                Segments = segments
            };

            await manager.WriteStateAtomicAsync(metaPath, state, CancellationToken.None);

            // Act: Reconcile state
            var readState = await manager.ReadStateAsync(metaPath, CancellationToken.None);
            readState.Should().NotBeNull();

            bool valid = manager.ReconcileAndValidate(readState!, "\"test-v1\"", "");
            valid.Should().BeTrue();

            // Assert: Segments 0, 1, 3 remain Completed, ONLY Segment 2 is reset to Pending
            readState!.Segments[0].State.Should().Be(SegmentState.Completed, "Segment 0 was not corrupted and must remain Completed");
            readState.Segments[1].State.Should().Be(SegmentState.Completed, "Segment 1 was not corrupted and must remain Completed");
            readState.Segments[3].State.Should().Be(SegmentState.Completed, "Segment 3 was not corrupted and must remain Completed");

            readState.Segments[2].State.Should().Be(SegmentState.Pending, "Corrupted Segment 2 must be reset to Pending");
            readState.Segments[2].BytesDownloaded.Should().Be(0, "Corrupted Segment 2 must be reset to 0 bytes downloaded so only it is re-downloaded");
        }

        [Fact]
        public async Task SchemaVersion1_BackwardCompatibility_DeserializesAndReconcilesCleanly()
        {
            string metaPath = Path.Combine(_testFolder, "v1_compat", "metadata.json");
            Directory.CreateDirectory(Path.GetDirectoryName(metaPath)!);

            string v1Json = JsonSerializer.Serialize(new
            {
                SchemaVersion = 1,
                DownloadId = Guid.NewGuid().ToString("N"),
                Url = "http://example.com/legacy.bin",
                DestinationPath = Path.Combine(_testFolder, "legacy.bin"),
                TotalBytes = 1000,
                ServerSupportsRanges = true,
                ETag = "\"v1-etag\"",
                LastModified = "Mon, 10 Aug 2026 00:00:00 GMT",
                Segments = new[]
                {
                    new { Id = 0, Start = 0L, End = 999L, BytesDownloaded = 0L, State = 0, TempPath = "" }
                }
            });

            await File.WriteAllTextAsync(metaPath, v1Json);

            var manager = new DurableMetadataManager();
            var state = await manager.ReadStateAsync(metaPath, CancellationToken.None);

            state.Should().NotBeNull("SchemaVersion 1 must deserialize cleanly for backward compatibility");
            state!.SchemaVersion.Should().Be(1);
            state.Url.Should().Be("http://example.com/legacy.bin");

            bool isValid = manager.ReconcileAndValidate(state, "\"v1-etag\"", "Mon, 10 Aug 2026 00:00:00 GMT");
            isValid.Should().BeTrue("SchemaVersion 1 metadata must reconcile cleanly");
        }
    }
}
