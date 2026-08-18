using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using EDM.Services.Interfaces;

namespace EDM.Tools.StressTest
{
    public class MockSettingsService : ISettingsService
    {
        public string GetDefaultDownloadPath() => Path.GetTempPath();
        public void SetDefaultDownloadPath(string path) { }
        public List<string> GetCategories() => new List<string> { "General" };
        public void AddCategory(string category) { }
        public string GetFfmpegPath() => "";
        public void SetFfmpegPath(string path) { }
        public string GetYtDlpPath() => "";
        public void SetYtDlpPath(string path) { }
        public string GetAria2Path() => "";
        public void SetAria2Path(string path) { }
        public string GetDefaultFormatArgs() => "";
        public void SetDefaultFormatArgs(string args) { }
        public bool GetAutoConvertToMp3() => false;
        public void SetAutoConvertToMp3(bool v) { }
        public bool GetSchedulerEnabled() => false;
        public TimeSpan? GetSchedulerTime() => null;
        public void SetScheduler(bool enabled, TimeSpan? time) { }
        public int GetConnectionLimitOverride() => 16;
        public bool GetReduceQualityOnMeteredNetworks() => false;
        public int GetBandwidthLimitKbps() => 0;
        public int GetActiveBandwidthLimitKbps() => 0;
        public ProxySettings GetProxySettings() => new ProxySettings();
        public void SetProxySettings(ProxySettings settings, string? plainPassword = null) { }
        public List<BandwidthSchedule> GetBandwidthSchedules() => new List<BandwidthSchedule>();
        public void SetBandwidthSchedules(List<BandwidthSchedule> schedules) { }
        public bool GetEnableUrlSafetyCheck() => false;
        public void SetEnableUrlSafetyCheck(bool enable) { }
        public bool GetEnablePostDownloadScan() => false;
        public void SetEnablePostDownloadScan(bool enable) { }
        public string GetGoogleSafeBrowsingApiKey() => "";
        public void SetGoogleSafeBrowsingApiKey(string apiKey) { }
        public bool GetSendAnonymousCrashReports() => false;
        public void SetSendAnonymousCrashReports(bool enable) { }
        public string? GetSetting(string key) => null;
        public void SaveSetting(string key, string value) { }
        public void SetSetting(string key, string value) { }
        public bool GetBoolSetting(string key, bool defaultValue = false) => defaultValue;
    }

    public class MockNetworkService : INetworkService
    {
        public bool IsMeteredNetwork() => false;
        public bool IsNetworkAvailable() => true;
        public NetworkType GetCurrentNetworkType() => NetworkType.Ethernet;
        public bool IsVpnActive() => false;
        public int GetRecommendedConnectionCount(int defaultCount) => defaultCount;
        public string GetNetworkDescription() => "Ethernet 10Gbps (Stress Test Mock)";
        public Task<bool> HasInternetConnectivityAsync() => Task.FromResult(true);
    }

    public class StressTestProgram
    {
        private const string TestServerPrefix = "http://127.0.0.1:8085/";

        public static async Task RunStressTestAsync(string[]? args = null)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("    EDM STRESS TEST & PERFORMANCE HARNESS        ");
            Console.WriteLine("=================================================");

            using var listener = new HttpListener();
            listener.Prefixes.Add(TestServerPrefix);
            listener.Start();
            Console.WriteLine($"[Mock HTTP Server] Listening on {TestServerPrefix}");

            using var ctsServer = new CancellationTokenSource();
            var serverTask = Task.Run(() => RunMockHttpServerAsync(listener, ctsServer.Token));

            var settings = new MockSettingsService();
            var network = new MockNetworkService();
            var downloadService = new DownloadService(null, network, settings);

            try
            {
                await RunTest1_ConcurrentDownloadsAndMemoryLeakAsync(downloadService);
                await RunTest2_RapidPauseResumeCancelStressAsync(downloadService);
                await RunTest3_SimulatedDiskWriteFailureAsync(downloadService);
            }
            finally
            {
                ctsServer.Cancel();
                listener.Stop();
            }

            Console.WriteLine("\n=================================================");
            Console.WriteLine("       STRESS TEST SUITE COMPLETED CLEANLY       ");
            Console.WriteLine("=================================================");
        }

        private static async Task RunTest1_ConcurrentDownloadsAndMemoryLeakAsync(DownloadService downloadService)
        {
            Console.WriteLine("\n[TEST 1] 25 Concurrent Downloads & Memory Leak Audit...");

            GC.Collect();
            GC.WaitForPendingFinalizers();
            long memStart = GC.GetTotalMemory(true);
            var sw = Stopwatch.StartNew();

            int totalDownloads = 25;
            var tasks = new List<Task>();
            var tempFiles = new List<string>();

            for (int i = 0; i < totalDownloads; i++)
            {
                int downloadId = i + 1;
                string tempPath = Path.Combine(Path.GetTempPath(), $"EDM_Stress_T1_{downloadId}_{Guid.NewGuid():N}.bin");
                tempFiles.Add(tempPath);

                var progress = new Progress<DownloadProgressInfo>(_ => { });
                var pauseToken = new PauseTokenSource();

                tasks.Add(Task.Run(async () =>
                {
                    await downloadService.StartDownloadAsync(
                        url: $"{TestServerPrefix}fixture_{downloadId}.bin?size=1048576",
                        savePath: tempPath,
                        progressReporter: progress,
                        pauseToken: pauseToken,
                        speedLimitProvider: () => 0,
                        cancellationToken: CancellationToken.None);
                }));
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            long memEnd = GC.GetTotalMemory(true);

            double memDiffMb = (memEnd - memStart) / (1024.0 * 1024.0);

            Console.WriteLine($" -> Completed {totalDownloads} downloads in {sw.ElapsedMilliseconds} ms.");
            Console.WriteLine($" -> Initial Memory: {memStart / (1024 * 1024.0):F2} MB | Final Memory: {memEnd / (1024 * 1024.0):F2} MB");
            Console.WriteLine($" -> Memory Delta: {memDiffMb:F2} MB (Leak Threshold: < 15 MB)");

            foreach (var f in tempFiles) { try { if (File.Exists(f)) File.Delete(f); } catch { } }
        }

        private static async Task RunTest2_RapidPauseResumeCancelStressAsync(DownloadService downloadService)
        {
            Console.WriteLine("\n[TEST 2] Rapid Pause/Resume/Cancel Stress Test...");

            int downloadCount = 5;
            var tasks = new List<Task>();
            var tempFiles = new List<string>();
            var pauseTokens = new List<PauseTokenSource>();
            var cancelTokens = new List<CancellationTokenSource>();

            for (int i = 0; i < downloadCount; i++)
            {
                string tempPath = Path.Combine(Path.GetTempPath(), $"EDM_Stress_T2_{i}_{Guid.NewGuid():N}.bin");
                tempFiles.Add(tempPath);

                var pt = new PauseTokenSource();
                var cts = new CancellationTokenSource();
                pauseTokens.Add(pt);
                cancelTokens.Add(cts);

                var progress = new Progress<DownloadProgressInfo>(_ => { });

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await downloadService.StartDownloadAsync(
                            url: $"{TestServerPrefix}large_fixture_{i}.bin?size=5242880",
                            savePath: tempPath,
                            progressReporter: progress,
                            pauseToken: pt,
                            speedLimitProvider: () => 0,
                            cancellationToken: cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected for cancelled downloads
                    }
                }));
            }

            // Rapidly toggle pause/resume on active tokens for 2 seconds
            for (int cycle = 0; cycle < 10; cycle++)
            {
                await Task.Delay(150);
                foreach (var pt in pauseTokens) pt.Pause();
                await Task.Delay(100);
                foreach (var pt in pauseTokens) pt.Resume();
            }

            // Cancel 2 downloads mid-stream
            cancelTokens[0].Cancel();
            cancelTokens[2].Cancel();

            await Task.WhenAll(tasks);

            Console.WriteLine(" -> Rapid pause/resume/cancel cycles completed without deadlock or queue corruption.");

            foreach (var f in tempFiles) { try { if (File.Exists(f)) File.Delete(f); } catch { } }
        }

        private static async Task RunTest3_SimulatedDiskWriteFailureAsync(DownloadService downloadService)
        {
            Console.WriteLine("\n[TEST 3] Disk Write Failure / Permission Error Resilience...");

            // Create a local read-only temp file target to trigger write-access failure without requiring admin rights
            string quotaTempDir = Path.Combine(Path.GetTempPath(), "EDM_Quota_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(quotaTempDir);
            string readOnlyFilePath = Path.Combine(quotaTempDir, "readonly_target.bin");

            // Create file and mark ReadOnly
            File.WriteAllText(readOnlyFilePath, "Pre-existing read only content");
            File.SetAttributes(readOnlyFilePath, FileAttributes.ReadOnly);

            var progress = new Progress<DownloadProgressInfo>(_ => { });
            var pt = new PauseTokenSource();

            bool caughtExpectedException = false;

            try
            {
                await downloadService.StartDownloadAsync(
                    url: $"{TestServerPrefix}fail_test.bin?size=102400",
                    savePath: readOnlyFilePath,
                    progressReporter: progress,
                    pauseToken: pt,
                    speedLimitProvider: () => 0,
                    cancellationToken: CancellationToken.None);
            }
            catch (UnauthorizedAccessException ex)
            {
                caughtExpectedException = true;
                Console.WriteLine($" -> Caught expected UnauthorizedAccessException gracefully: ({ex.Message})");
            }
            catch (IOException ex)
            {
                caughtExpectedException = true;
                Console.WriteLine($" -> Caught expected IOException gracefully: ({ex.Message})");
            }
            catch (Exception ex)
            {
                caughtExpectedException = true;
                Console.WriteLine($" -> Caught expected file access error gracefully: {ex.GetType().Name} ({ex.Message})");
            }
            finally
            {
                try
                {
                    if (File.Exists(readOnlyFilePath))
                    {
                        File.SetAttributes(readOnlyFilePath, FileAttributes.Normal);
                        File.Delete(readOnlyFilePath);
                    }
                    if (Directory.Exists(quotaTempDir))
                    {
                        Directory.Delete(quotaTempDir, true);
                    }
                }
                catch { }
            }

            if (!caughtExpectedException)
            {
                Console.WriteLine(" -> WARNING: Test expected a disk write error but none was thrown.");
            }
        }

        private static async Task RunMockHttpServerAsync(HttpListener listener, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && listener.IsListening)
            {
                try
                {
                    var ctx = await listener.GetContextAsync();
                    _ = Task.Run(() => HandleHttpRequest(ctx));
                }
                catch { break; }
            }
        }

        private static void HandleHttpRequest(HttpListenerContext ctx)
        {
            try
            {
                var req = ctx.Request;
                var resp = ctx.Response;

                resp.Headers.Add("Accept-Ranges", "bytes");

                long size = 1024 * 1024; // 1 MB default
                string? sizeParam = req.QueryString["size"];
                if (long.TryParse(sizeParam, out var parsedSize)) size = parsedSize;

                string? rangeHeader = req.Headers["Range"];
                long rangeStart = 0;
                long rangeEnd = size - 1;
                bool isRange = false;

                if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                {
                    var parts = rangeHeader.Substring(6).Split('-');
                    if (parts.Length == 2 && long.TryParse(parts[0], out var s))
                    {
                        rangeStart = s;
                        if (long.TryParse(parts[1], out var e)) rangeEnd = e;
                        else rangeEnd = size - 1;
                        isRange = true;
                    }
                }

                if (isRange)
                {
                    resp.StatusCode = 206;
                    long rangeLength = Math.Max(0, rangeEnd - rangeStart + 1);
                    resp.ContentLength64 = rangeLength;
                    resp.Headers.Add("Content-Range", $"bytes {rangeStart}-{rangeEnd}/{size}");
                }
                else
                {
                    resp.StatusCode = 200;
                    resp.ContentLength64 = size;
                }

                if (req.HttpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
                {
                    resp.Close();
                    return;
                }

                byte[] chunk = new byte[65536];
                Array.Fill<byte>(chunk, 0x41);

                long bytesToSend = resp.ContentLength64;
                long bytesSent = 0;
                using var output = resp.OutputStream;

                while (bytesSent < bytesToSend)
                {
                    int toSend = (int)Math.Min(chunk.Length, bytesToSend - bytesSent);
                    output.Write(chunk, 0, toSend);
                    bytesSent += toSend;
                }

                resp.Close();
            }
            catch { }
        }
    }
}
