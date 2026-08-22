using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using EDM.Services.Interfaces;
using EDM.Services.Storage;

namespace EDM.Tests.Services
{
    public class TestSettingsService : ISettingsService
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _dict = new(StringComparer.OrdinalIgnoreCase);
        public string? GetSetting(string key) => _dict.TryGetValue(key, out var val) ? val : null;
        public void SaveSetting(string key, string value) => _dict[key] = value;
        public void SetSetting(string key, string value) => _dict[key] = value;
        public bool GetBoolSetting(string key, bool defaultValue = false) => _dict.TryGetValue(key, out var val) && bool.TryParse(val, out var b) ? b : defaultValue;
        public int GetIntSetting(string key, int defaultValue = 0) => _dict.TryGetValue(key, out var val) && int.TryParse(val, out var i) ? i : defaultValue;
        public double GetDoubleSetting(string key, double defaultValue = 0) => _dict.TryGetValue(key, out var val) && double.TryParse(val, out var d) ? d : defaultValue;
        public string GetDefaultDownloadPath() => "C:\\Downloads";
        public void SetDefaultDownloadPath(string path) { }
        public List<string> GetCategories() => new() { "General" };
        public void AddCategory(string category) { }
        public string GetFfmpegPath() => string.Empty;
        public void SetFfmpegPath(string path) { }
        public string GetYtDlpPath() => string.Empty;
        public void SetYtDlpPath(string path) { }
        public string GetAria2Path() => string.Empty;
        public void SetAria2Path(string path) { }
        public string GetDefaultFormatArgs() => string.Empty;
        public void SetDefaultFormatArgs(string args) { }
        public bool GetAutoConvertToMp3() => false;
        public void SetAutoConvertToMp3(bool v) { }
        public bool GetSchedulerEnabled() => false;
        public TimeSpan? GetSchedulerTime() => null;
        public void SetScheduler(bool enabled, TimeSpan? time) { }
        public int GetConnectionLimitOverride() => 0;
        public bool GetReduceQualityOnMeteredNetworks() => true;
        public int GetBandwidthLimitKbps() => 0;
        public int GetActiveBandwidthLimitKbps() => 0;
        public EDM.Models.ProxySettings GetProxySettings() => new();
        public void SetProxySettings(EDM.Models.ProxySettings settings, string? plainPassword = null) { }
        public List<EDM.Models.BandwidthSchedule> GetBandwidthSchedules() => new();
        public void SetBandwidthSchedules(List<EDM.Models.BandwidthSchedule> schedules) { }
        public bool GetEnableUrlSafetyCheck() => false;
        public void SetEnableUrlSafetyCheck(bool enable) { }
        public bool GetEnablePostDownloadScan() => false;
        public void SetEnablePostDownloadScan(bool enable) { }
        public string GetGoogleSafeBrowsingApiKey() => string.Empty;
        public void SetGoogleSafeBrowsingApiKey(string apiKey) { }
        public bool GetSendAnonymousCrashReports() => false;
        public void SetSendAnonymousCrashReports(bool enable) { }
        public bool GetEnableClipboardMonitoring() => false;
        public void SetEnableClipboardMonitoring(bool enable) { }
        public bool GetClipboardMonitorHttp() => true;
        public void SetClipboardMonitorHttp(bool enable) { }
        public bool GetClipboardMonitorHttps() => true;
        public void SetClipboardMonitorHttps(bool enable) { }
        public bool GetClipboardMonitorFtp() => true;
        public void SetClipboardMonitorFtp(bool enable) { }
        public EDM.Services.Interfaces.ClipboardAction GetClipboardAction() => EDM.Services.Interfaces.ClipboardAction.AskBeforeDownload;
        public void SetClipboardAction(EDM.Services.Interfaces.ClipboardAction action) { }
        public bool GetClipboardIgnoreDuplicates() => true;
        public void SetClipboardIgnoreDuplicates(bool enable) { }
        public bool GetClipboardShowNotification() => true;
        public void SetClipboardShowNotification(bool enable) { }
        public bool GetEnableBrowserIntegration() => true;
        public void SetEnableBrowserIntegration(bool enable) { }
        public bool GetBrowserCaptureDownloads() => true;
        public void SetBrowserCaptureDownloads(bool enable) { }
        public bool GetBrowserShowConfirmation() => true;
        public void SetBrowserShowConfirmation(bool enable) { }
        public bool GetBrowserShowNotification() => true;
        public void SetBrowserShowNotification(bool enable) { }
    }

    public class BidirectionalSyncAndWatcherTestSuite : IDisposable
    {
        private readonly string _tempWorkspace;
        private readonly TestSettingsService _settings;
        private readonly LocalHddStorageEngine _storageEngine;
        private readonly LocalHddFileSystemWatcher _watcher;

        public BidirectionalSyncAndWatcherTestSuite()
        {
            _tempWorkspace = Path.Combine(Path.GetTempPath(), "EDM_Sync_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempWorkspace);

            _settings = new TestSettingsService();
            _settings.SetSetting("CustomStorageRootPath", _tempWorkspace);

            _storageEngine = new LocalHddStorageEngine(_settings);
            _watcher = new LocalHddFileSystemWatcher(_storageEngine);
        }

        public void Dispose()
        {
            _watcher.Dispose();
            if (Directory.Exists(_tempWorkspace))
            {
                try { Directory.Delete(_tempWorkspace, true); } catch { /* Ignore */ }
            }
        }

        [Fact]
        public async Task Watcher_Debounces_Multiple_Rapid_Modifications_Into_Single_Event()
        {
            _watcher.Start();
            var eventsReceived = new List<LocalFileChangeEvent>();
            _watcher.FileChanged += e =>
            {
                lock (eventsReceived) eventsReceived.Add(e);
            };

            string testFile = Path.Combine(_tempWorkspace, "debounced_doc.txt");

            // Write 5 rapid updates in quick succession (under debounce threshold)
            for (int i = 1; i <= 5; i++)
            {
                await File.WriteAllTextAsync(testFile, $"Content version {i}");
                await Task.Delay(50);
            }

            // Wait for debounce buffer (600ms) to elapse
            await Task.Delay(1000);

            _watcher.Stop();

            // All rapid writes should coalesce into 1 or at most 2 events (created + final modified)
            eventsReceived.Count.Should().BeInRange(1, 2);
            eventsReceived.Last().RelativePath.Should().Be("debounced_doc.txt");
        }

        [Fact]
        public void Watcher_Suppresses_Self_Generated_Cloud_Writes_Loop_Prevention()
        {
            string relPath = "Documents/cloud_synced_report.pdf";

            _watcher.SuppressPath(relPath, TimeSpan.FromSeconds(5));
            _watcher.IsPathSuppressed(relPath).Should().BeTrue();

            _watcher.RecordAppliedCloudHash(relPath, "abc123hash");
            _watcher.GetAppliedCloudHash(relPath).Should().Be("abc123hash");

            _watcher.ClearAppliedCloudHash(relPath);
            _watcher.GetAppliedCloudHash(relPath).Should().BeNull();
        }

        [Fact]
        public async Task WaitForFileReady_Retries_On_Locked_File()
        {
            string testFile = Path.Combine(_tempWorkspace, "locked_file.dat");
            await File.WriteAllTextAsync(testFile, "Locked content");

            var lockAcquired = new ManualResetEventSlim(false);
            var lockRelease = new ManualResetEventSlim(false);

            var lockTask = Task.Run(() =>
            {
                using var fs = new FileStream(testFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                lockAcquired.Set();
                lockRelease.Wait(2000);
            });

            lockAcquired.Wait(1000);

            // Start waiting in background
            var waitTask = LocalHddFileSystemWatcher.WaitForFileReadyAsync(testFile, maxAttempts: 15, initialDelayMs: 50);

            // Release lock after 200ms
            await Task.Delay(200);
            lockRelease.Set();

            bool ready = await waitTask;
            ready.Should().BeTrue();

            await lockTask;
        }

        [Fact]
        public async Task StreamWriteAtomic_Performs_Safe_Atomic_Write_And_Calculates_Sha256()
        {
            string content = "Testing atomic stream write and SHA-256 computation!";
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
            using var ms = new MemoryStream(bytes);

            var meta = await _storageEngine.StreamWriteAtomicAsync(ms, "Projects/atomic_test.txt", bytes.Length);

            meta.Should().NotBeNull();
            meta.FileName.Should().Be("atomic_test.txt");
            meta.RelativePath.Should().Be("Projects/atomic_test.txt");
            meta.FileSizeBytes.Should().Be(bytes.Length);
            meta.Sha256Hash.Should().NotBeNullOrEmpty();

            string fullDest = Path.Combine(_tempWorkspace, "Projects", "atomic_test.txt");
            File.Exists(fullDest).Should().BeTrue();
            (await File.ReadAllTextAsync(fullDest)).Should().Be(content);
        }

        [Fact]
        public async Task Overwriting_Existing_File_Creates_Versioned_Backup()
        {
            string file = Path.Combine(_tempWorkspace, "version_doc.txt");
            await File.WriteAllTextAsync(file, "Original v1 content");

            byte[] v2Bytes = System.Text.Encoding.UTF8.GetBytes("Updated v2 content");
            using var ms = new MemoryStream(v2Bytes);

            await _storageEngine.StreamWriteAtomicAsync(ms, "version_doc.txt", v2Bytes.Length);

            string versionsDir = Path.Combine(_tempWorkspace, ".edm_versions");
            Directory.Exists(versionsDir).Should().BeTrue();
            Directory.GetFiles(versionsDir, "version_doc_v*.txt").Length.Should().BeGreaterThanOrEqualTo(1);
        }

        [Fact]
        public void Offline_Queue_Enqueues_And_Replays_Safely()
        {
            var opId = Guid.NewGuid();
            var op = new OfflineSyncOperation(
                OperationId: opId,
                OperationType: "REGISTER",
                FileId: Guid.NewGuid(),
                RelativePath: "Offline/doc.pdf",
                Sha256Hash: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                FileSizeBytes: 1024,
                Version: 1,
                QueuedAtUtc: DateTime.UtcNow);

            _storageEngine.EnqueueOfflineSyncOperation(op);

            var pending = _storageEngine.GetPendingOfflineOperations();
            pending.Should().ContainSingle(x => x.OperationId == opId);

            _storageEngine.ClearProcessedOfflineOperation(opId);
            _storageEngine.GetPendingOfflineOperations().Any(x => x.OperationId == opId).Should().BeFalse();
        }

        [Fact]
        public void Preflight_DiskSpace_Validation_Throws_On_Insufficient_Space()
        {
            // Request an unrealistically large size (e.g. 50,000 Terabytes)
            long hugeBytes = 50_000L * 1024 * 1024 * 1024 * 1024;

            Action act = () => _storageEngine.ValidateAvailableDiskSpace(hugeBytes);
            act.Should().Throw<InsufficientDiskSpaceException>();
        }
    }
}
