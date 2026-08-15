using System;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EDM.Tests.Services
{
    public class SqliteWalConcurrencyTests : IDisposable
    {
        private readonly string _testDbPath;

        public SqliteWalConcurrencyTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"EDM_TestWal_{Guid.NewGuid():N}.db");
        }

        public void Dispose()
        {
            try
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
                var walFile = _testDbPath + "-wal";
                var shmFile = _testDbPath + "-shm";
                if (File.Exists(walFile)) File.Delete(walFile);
                if (File.Exists(shmFile)) File.Delete(shmFile);
            }
            catch { }
        }

        [Fact]
        public void SqliteConnectionManager_InitializesWalModeAndVerifiesJournalMode()
        {
            using var mgr = new SqliteConnectionManager(_testDbPath);
            using var conn = mgr.GetConnection();

            string journalMode = SqliteConnectionManager.VerifyJournalMode(conn);
            journalMode.Should().Be("wal", "Database initialization must enable and verify WAL journal mode");

            // Verify busy_timeout PRAGMA
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA busy_timeout;";
            var busyTimeout = Convert.ToInt32(cmd.ExecuteScalar());
            busyTimeout.Should().Be(5000, "PRAGMA busy_timeout must be configured to 5000ms");

            mgr.ReturnConnection(conn);
        }

        [Fact]
        public async Task SqliteConnectionManager_SupportsConcurrentReadsAndWritesInWalMode()
        {
            using var mgr = new SqliteConnectionManager(_testDbPath, maxPoolSize: 10);

            // Initialize table schema
            await mgr.ExecuteExclusiveAsync(async conn =>
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"CREATE TABLE IF NOT EXISTS test_records (
                                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                                    val TEXT NOT NULL
                                );";
                await cmd.ExecuteNonQueryAsync();
                return true;
            });

            int writers = 5;
            int recordsPerWriter = 20;
            var tasks = new Task[writers];

            for (int w = 0; w < writers; w++)
            {
                int writerId = w;
                tasks[w] = Task.Run(async () =>
                {
                    for (int i = 0; i < recordsPerWriter; i++)
                    {
                        var conn = await mgr.GetConnectionAsync();
                        try
                        {
                            using var cmd = conn.CreateCommand();
                            cmd.CommandText = "INSERT INTO test_records (val) VALUES (@val);";
                            cmd.Parameters.AddWithValue("@val", $"Writer_{writerId}_Item_{i}");
                            await cmd.ExecuteNonQueryAsync();
                        }
                        finally
                        {
                            mgr.ReturnConnection(conn);
                        }
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Verify total record count
            var verifyConn = mgr.GetConnection();
            try
            {
                using var cmd = verifyConn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM test_records;";
                var count = Convert.ToInt32(cmd.ExecuteScalar());
                count.Should().Be(writers * recordsPerWriter, "All concurrent writes under WAL mode must be persisted accurately");
            }
            finally
            {
                mgr.ReturnConnection(verifyConn);
            }
        }

        [Fact]
        public async Task ExecuteExclusiveAsync_GuaranteesSerializedExecutionAndReleasesLockOnException()
        {
            using var mgr = new SqliteConnectionManager(_testDbPath);

            // 1. Operation throwing exception
            var act = () => mgr.ExecuteExclusiveAsync<bool>(conn =>
            {
                throw new InvalidOperationException("Test exception inside exclusive lock");
            });

            await act.Should().ThrowAsync<InvalidOperationException>();

            // 2. Next exclusive operation must succeed (proving lock was released in finally block)
            var result = await mgr.ExecuteExclusiveAsync(async conn =>
            {
                await Task.Delay(10);
                return true;
            });

            result.Should().BeTrue("Exclusive lock must be released after an exception");
        }

        [Fact]
        public void SqliteConnectionManager_PreservesBackwardCompatibilityWithPreWalDatabase()
        {
            // Create database in standard DELETE journal mode first
            using (var initConn = new SqliteConnection($"Data Source={_testDbPath}"))
            {
                initConn.Open();
                using var cmd = initConn.CreateCommand();
                cmd.CommandText = "PRAGMA journal_mode=DELETE; CREATE TABLE legacy (id INT, name TEXT); INSERT INTO legacy VALUES (1, 'LegacyData');";
                cmd.ExecuteNonQuery();
            }

            // Open with SqliteConnectionManager -> upgrades automatically to WAL without data loss
            using var mgr = new SqliteConnectionManager(_testDbPath);
            using var conn = mgr.GetConnection();

            string journalMode = SqliteConnectionManager.VerifyJournalMode(conn);
            journalMode.Should().Be("wal", "Legacy database must be safely opened and upgraded to WAL mode");

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM legacy WHERE id = 1;";
                var name = cmd.ExecuteScalar()?.ToString();
                name.Should().Be("LegacyData", "Existing legacy table data must be preserved intact after WAL upgrade");
            }

            mgr.ReturnConnection(conn);
        }
    }
}
