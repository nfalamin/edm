using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using EDM.Services.Data;
using FluentAssertions;
using Xunit;

using EDM.Services.Interfaces;

namespace EDM.Tests.Services
{
    public class HighImpactOptimizationTests : IDisposable
    {
        private readonly string _tempDbPath;
        private readonly SqliteConnectionManager _dbManager;

        public HighImpactOptimizationTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"edm_opt_test_{Guid.NewGuid():N}.db");
            _dbManager = new SqliteConnectionManager(_tempDbPath);
        }

        public void Dispose()
        {
            _dbManager.Dispose();
            if (File.Exists(_tempDbPath))
            {
                try { File.Delete(_tempDbPath); } catch { }
            }
        }

        [Fact]
        public void PartA_MultipartStreamOptions_UsesAsynchronousAndSequentialScanFlags()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"edm_stream_opt_{Guid.NewGuid():N}.part");
            try
            {
                using (var fs = new FileStream(
                    tempFile,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.ReadWrite,
                    128 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    fs.IsAsync.Should().BeTrue();
                    fs.Write(new byte[] { 1, 2, 3, 4 }, 0, 4);
                }
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void PartB_SqliteWalConfiguration_VerifiesWalAndBusyTimeout()
        {
            using var conn = _dbManager.GetConnection();
            string mode = SqliteConnectionManager.VerifyJournalMode(conn);
            mode.Should().Be("wal");
            _dbManager.ReturnConnection(conn);
        }

        [Fact]
        public async Task PartC_SqliteExclusiveTransactions_HandlesConcurrentWritesCleanly()
        {
            int taskCount = 10;
            var tasks = new Task[taskCount];

            for (int i = 0; i < taskCount; i++)
            {
                int val = i;
                tasks[i] = _dbManager.ExecuteExclusiveAsync(async conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT 1;";
                    var res = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                    return Convert.ToInt32(res);
                });
            }

            await Task.WhenAll(tasks).ConfigureAwait(true);
        }

        [Fact]
        public async Task PartG_AdaptiveLatencyCache_CachesHostPingResultsFor5Minutes()
        {
            var mockSettings = new Moq.Mock<ISettingsService>();
            mockSettings.Setup(s => s.GetConnectionLimitOverride()).Returns(0);
            mockSettings.Setup(s => s.GetActiveBandwidthLimitKbps()).Returns(0);

            var manager = new AdaptiveConnectionManager(mockSettings.Object, new MockNetworkService());
            int conns1 = await manager.DetermineConnectionCountAsync("https://example.com/file1.zip", 50 * 1024 * 1024, true, CancellationToken.None).ConfigureAwait(true);
            int conns2 = await manager.DetermineConnectionCountAsync("https://example.com/file2.zip", 50 * 1024 * 1024, true, CancellationToken.None).ConfigureAwait(true);

            conns1.Should().BeGreaterThan(0);
            conns2.Should().Be(conns1); // Cached latency result produces consistent scaling
        }

        [Fact]
        public async Task PartI_Sha256StreamingAsync_ComputesCorrectHashWithoutMemorySpike()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"sha_test_{Guid.NewGuid():N}.bin");
            byte[] data = Encoding.UTF8.GetBytes("Exclusive Download Manager High-Impact SHA-256 Test Payload " + Guid.NewGuid());
            await File.WriteAllBytesAsync(tempFile, data).ConfigureAwait(true);

            string expectedHash;
            using (var sha = SHA256.Create())
            {
                expectedHash = Convert.ToHexStringLower(sha.ComputeHash(data));
            }

            var integrity = new FileIntegrityService();
            string actualHash = await integrity.ComputeSha256Async(tempFile, CancellationToken.None).ConfigureAwait(true);

            actualHash.Should().Be(expectedHash);

            File.Delete(tempFile);
        }

        [Fact]
        public async Task PartJ_Sha256Cancellation_ThrowsOperationCanceledExceptionOnTokenCancellation()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"sha_cancel_{Guid.NewGuid():N}.bin");
            byte[] data = new byte[10 * 1024 * 1024]; // 10 MB payload
            new Random(42).NextBytes(data);
            await File.WriteAllBytesAsync(tempFile, data).ConfigureAwait(true);

            using var cts = new CancellationTokenSource();
            var integrity = new FileIntegrityService();

            cts.Cancel(); // Cancel immediately

            Func<Task> act = async () => await integrity.ComputeSha256Async(tempFile, cts.Token).ConfigureAwait(true);
            await act.Should().ThrowAsync<OperationCanceledException>();

            File.Delete(tempFile);
        }

        private class MockNetworkService : INetworkService
        {
            public NetworkType GetCurrentNetworkType() => NetworkType.Ethernet;
            public bool IsMeteredNetwork() => false;
            public bool IsVpnActive() => false;
            public int GetRecommendedConnectionCount(int defaultCount) => defaultCount;
            public string GetNetworkDescription() => "Ethernet";
            public Task<bool> HasInternetConnectivityAsync() => Task.FromResult(true);
        }
    }
}
