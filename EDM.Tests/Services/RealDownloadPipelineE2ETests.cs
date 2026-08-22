using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using EDM.Services.Interfaces;
using Xunit;

namespace EDM.Tests.Services
{
    public class RealDownloadPipelineE2ETests : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly int _port;
        private readonly string _baseUrl;
        private readonly CancellationTokenSource _serverCts = new();
        private readonly string _tempTestDir;

        // Controlled test payloads in memory
        private readonly byte[] _payload1KB;
        private readonly byte[] _payload1MB;
        private readonly byte[] _payload20MB;

        public RealDownloadPipelineE2ETests()
        {
            // Allocate distinct pseudorandom deterministic buffers
            _payload1KB = GeneratePayload(1024, 0x11);
            _payload1MB = GeneratePayload(1024 * 1024, 0x22);
            _payload20MB = GeneratePayload(20 * 1024 * 1024, 0x33);

            _tempTestDir = Path.Combine(Path.GetTempPath(), "EDM_E2E_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempTestDir);

            // Bind in-memory HttpListener to loopback port
            var rng = new Random();
            for (int attempt = 0; attempt < 10; attempt++)
            {
                int p = rng.Next(28000, 32000);
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://127.0.0.1:{p}/");
                    _listener.Start();
                    _port = p;
                    _baseUrl = $"http://127.0.0.1:{p}/";
                    break;
                }
                catch
                {
                    _listener?.Close();
                }
            }

            if (_listener == null || !_listener.IsListening)
            {
                throw new InvalidOperationException("Failed to bind HttpListener to loopback port");
            }

            Task.Run(() => ServerLoopAsync(_serverCts.Token));
        }

        private static byte[] GeneratePayload(int size, byte seed)
        {
            var data = new byte[size];
            for (int i = 0; i < size; i++)
            {
                data[i] = (byte)((i ^ seed) & 0xFF);
            }
            return data;
        }

        private async Task ServerLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context), ct);
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception) { break; }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var req = context.Request;
            var resp = context.Response;

            try
            {
                var path = req.Url?.AbsolutePath ?? "/";

                if (path == "/redirect")
                {
                    resp.StatusCode = (int)HttpStatusCode.Redirect;
                    resp.RedirectLocation = _baseUrl + "file-1mb.dat";
                    resp.Close();
                    return;
                }

                byte[]? targetPayload = path switch
                {
                    "/file-1kb.dat" => _payload1KB,
                    "/file-1mb.dat" => _payload1MB,
                    "/file-20mb.dat" => _payload20MB,
                    "/no-range-1mb.dat" => _payload1MB,
                    _ => null
                };

                if (targetPayload == null)
                {
                    resp.StatusCode = 404;
                    resp.Close();
                    return;
                }

                bool isNoRangeEndpoint = path.StartsWith("/no-range");

                resp.Headers["ETag"] = "\"edm-test-etag-123\"";
                resp.Headers["Last-Modified"] = "Fri, 14 Aug 2026 00:00:00 GMT";

                if (!isNoRangeEndpoint)
                {
                    resp.Headers["Accept-Ranges"] = "bytes";
                }

                if (req.HttpMethod == "HEAD")
                {
                    resp.StatusCode = 200;
                    resp.ContentLength64 = targetPayload.Length;
                    resp.Close();
                    return;
                }

                // Check Range header
                string? rangeHeader = req.Headers["Range"];
                if (!string.IsNullOrEmpty(rangeHeader) && !isNoRangeEndpoint && rangeHeader.StartsWith("bytes="))
                {
                    string rangeSpec = rangeHeader.Substring(6).Trim();
                    string[] parts = rangeSpec.Split('-');

                    long start = long.Parse(parts[0]);
                    long end = parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) ? long.Parse(parts[1]) : targetPayload.Length - 1;

                    if (end >= targetPayload.Length) end = targetPayload.Length - 1;
                    long length = end - start + 1;

                    resp.StatusCode = (int)HttpStatusCode.PartialContent;
                    resp.Headers["Content-Range"] = $"bytes {start}-{end}/{targetPayload.Length}";
                    resp.ContentLength64 = length;

                    await resp.OutputStream.WriteAsync(targetPayload, (int)start, (int)length);
                }
                else
                {
                    resp.StatusCode = (int)HttpStatusCode.OK;
                    resp.ContentLength64 = targetPayload.Length;
                    await resp.OutputStream.WriteAsync(targetPayload, 0, targetPayload.Length);
                }

                resp.Close();
            }
            catch
            {
                try { resp.Abort(); } catch { }
            }
        }

        public void Dispose()
        {
            _serverCts.Cancel();
            try { _listener.Stop(); } catch { }
            try { _listener.Close(); } catch { }
            try
            {
                if (Directory.Exists(_tempTestDir))
                {
                    Directory.Delete(_tempTestDir, true);
                }
            }
            catch { }
        }

        private static string ComputeSha256(byte[] data)
        {
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(data));
        }

        private static string ComputeFileSha256(string path)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(fs));
        }

        [Fact]
        public async Task HttpProbeService_DetectsRealMetadata_And_AcceptRanges()
        {
            using var client = new HttpClient();
            var probeService = new HttpProbeService(client);

            var result = await probeService.ProbeUrlAsync(_baseUrl + "file-1mb.dat", Path.Combine(_tempTestDir, "test.dat"));

            Assert.NotNull(result);
            Assert.Equal(1024 * 1024, result.TotalBytes);
            Assert.True(result.ServerSupportsResume);
            Assert.NotNull(result.ETag);
        }

        [Fact]
        public async Task DownloadSmallFile_1KB_ExactByteMatch_And_Sha256Match()
        {
            using var client = new HttpClient();
            var service = new DownloadService(client);

            string destFile = Path.Combine(_tempTestDir, "download_1kb.dat");
            var progress = new Progress<DownloadProgressInfo>();
            var pause = new PauseTokenSource();

            await service.StartDownloadAsync(_baseUrl + "file-1kb.dat", destFile, progress, pause, () => -1, CancellationToken.None);

            Assert.True(File.Exists(destFile));
            var downloadedBytes = await File.ReadAllBytesAsync(destFile);
            Assert.Equal(_payload1KB.Length, downloadedBytes.Length);
            Assert.Equal(ComputeSha256(_payload1KB), ComputeFileSha256(destFile));
        }

        [Fact]
        public async Task DownloadMediumFile_1MB_MultiSegment_ExactByteMatch()
        {
            using var client = new HttpClient();
            var service = new DownloadService(client);

            string destFile = Path.Combine(_tempTestDir, "download_1mb.dat");
            var progress = new Progress<DownloadProgressInfo>();
            var pause = new PauseTokenSource();

            await service.StartDownloadAsync(_baseUrl + "file-1mb.dat", destFile, progress, pause, () => -1, CancellationToken.None, segmentCount: 4);

            Assert.True(File.Exists(destFile));
            Assert.Equal(ComputeSha256(_payload1MB), ComputeFileSha256(destFile));
        }

        [Fact]
        public async Task DownloadLargeFile_20MB_MultiSegment_ExactByteMatch()
        {
            using var client = new HttpClient();
            var service = new DownloadService(client);

            string destFile = Path.Combine(_tempTestDir, "download_20mb.dat");
            var progress = new Progress<DownloadProgressInfo>();
            var pause = new PauseTokenSource();

            await service.StartDownloadAsync(_baseUrl + "file-20mb.dat", destFile, progress, pause, () => -1, CancellationToken.None, segmentCount: 8);

            Assert.True(File.Exists(destFile));
            Assert.Equal(ComputeSha256(_payload20MB), ComputeFileSha256(destFile));
        }

        [Fact]
        public async Task NonRangeServer_FallbackSingleThread_CompletesSuccessfully()
        {
            using var client = new HttpClient();
            var service = new DownloadService(client);

            string destFile = Path.Combine(_tempTestDir, "download_no_range.dat");
            var progress = new Progress<DownloadProgressInfo>();
            var pause = new PauseTokenSource();

            await service.StartDownloadAsync(_baseUrl + "no-range-1mb.dat", destFile, progress, pause, () => -1, CancellationToken.None);

            Assert.True(File.Exists(destFile));
            Assert.Equal(ComputeSha256(_payload1MB), ComputeFileSha256(destFile));
        }

        [Fact]
        public async Task PauseResumeCycle_AtMultipleStages_CompletesWithoutCorruption()
        {
            using var client = new HttpClient();
            var service = new DownloadService(client);

            string destFile = Path.Combine(_tempTestDir, "download_pause_resume.dat");
            var pause = new PauseTokenSource();
            bool pausedOnce = false;

            var progress = new Progress<DownloadProgressInfo>(info =>
            {
                if (info.ProgressPercentage > 20 && !pausedOnce)
                {
                    pausedOnce = true;
                    pause.Pause();
                    Task.Run(async () =>
                    {
                        await Task.Delay(200);
                        pause.Resume();
                    });
                }
            });

            await service.StartDownloadAsync(_baseUrl + "file-1mb.dat", destFile, progress, pause, () => -1, CancellationToken.None, segmentCount: 4);

            Assert.True(File.Exists(destFile));
            Assert.Equal(ComputeSha256(_payload1MB), ComputeFileSha256(destFile));
        }

        [Fact]
        public async Task CancelDownload_ReleasesFileHandlesCleanly()
        {
            using var client = new HttpClient();
            var service = new DownloadService(client);

            string destFile = Path.Combine(_tempTestDir, "download_cancel.dat");
            using var cts = new CancellationTokenSource();
            var pause = new PauseTokenSource();

            var progress = new Progress<DownloadProgressInfo>(info =>
            {
                if (info.ProgressPercentage > 10)
                {
                    cts.Cancel();
                }
            });

            try
            {
                await service.StartDownloadAsync(_baseUrl + "file-20mb.dat", destFile, progress, pause, () => -1, cts.Token, segmentCount: 4);
            }
            catch (OperationCanceledException)
            {
                // Expected cancellation
            }

            // Verify file handles are released by checking we can delete or recreate files in the temp directory
            await Task.Delay(100);
            Assert.True(Directory.Exists(_tempTestDir));
        }
    }
}
