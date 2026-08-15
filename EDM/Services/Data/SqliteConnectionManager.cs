using Microsoft.Data.Sqlite;
using System;
using System.Collections.Concurrent;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services.Data
{
    /// <summary>
    /// Thread-safe SQLite connection pool & concurrency manager with verified WAL mode,
    /// busy_timeout=5000, and SemaphoreSlim(1, 1) exclusive coordination.
    /// Supports backward compatibility with pre-WAL database files.
    /// </summary>
    public class SqliteConnectionManager : IDisposable
    {
        private readonly string _dbPath;
        private readonly ConcurrentDictionary<int, SqliteConnection> _connectionPool;
        private readonly SemaphoreSlim _poolLock;
        private readonly SemaphoreSlim _exclusiveLock;
        private readonly int _maxPoolSize;
        private int _connectionCounter;
        private bool _isWalVerified;
        private string _activeJournalMode = "unknown";
        private bool _disposed;

        public string DbPath => _dbPath;
        public string ActiveJournalMode => _activeJournalMode;

        /// <summary>
        /// Initializes a new SqliteConnectionManager instance with specified DB path and pool size.
        /// </summary>
        public SqliteConnectionManager(string dbPath, int maxPoolSize = 5)
        {
            _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));

            string? dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _maxPoolSize = Math.Max(1, maxPoolSize);
            _connectionPool = new ConcurrentDictionary<int, SqliteConnection>();
            _poolLock = new SemaphoreSlim(_maxPoolSize, _maxPoolSize);
            _exclusiveLock = new SemaphoreSlim(1, 1);
            _connectionCounter = 0;
            _isWalVerified = false;
        }

        /// <summary>
        /// Gets a connection from the pool or creates and configures a new one.
        /// </summary>
        public SqliteConnection GetConnection()
        {
            ThrowIfDisposed();
            _poolLock.Wait();

            try
            {
                var connId = Interlocked.Increment(ref _connectionCounter);

                // Try to reuse an existing open connection from pool
                if (_connectionPool.TryGetValue(connId, out var conn) && conn != null)
                {
                    if (conn.State == ConnectionState.Open)
                    {
                        return conn;
                    }
                    else
                    {
                        _connectionPool.TryRemove(connId, out _);
                        try { conn.Dispose(); } catch { }
                    }
                }

                // Create a new configured connection
                var newConn = CreateAndConfigureConnection();
                _connectionPool.TryAdd(connId, newConn);
                return newConn;
            }
            catch
            {
                _poolLock.Release();
                throw;
            }
        }

        /// <summary>
        /// Asynchronously gets a connection with cancellation support.
        /// </summary>
        public async Task<SqliteConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await _poolLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var connId = Interlocked.Increment(ref _connectionCounter);

                if (_connectionPool.TryGetValue(connId, out var conn) && conn != null)
                {
                    if (conn.State == ConnectionState.Open)
                    {
                        return conn;
                    }
                    else
                    {
                        _connectionPool.TryRemove(connId, out _);
                        try { conn.Dispose(); } catch { }
                    }
                }

                var newConn = await CreateAndConfigureConnectionAsync(cancellationToken).ConfigureAwait(false);
                _connectionPool.TryAdd(connId, newConn);
                return newConn;
            }
            catch
            {
                _poolLock.Release();
                throw;
            }
        }

        /// <summary>
        /// Returns a connection to the pool.
        /// </summary>
        public void ReturnConnection(SqliteConnection? connection)
        {
            if (_disposed) return;
            try
            {
                _poolLock.Release();
            }
            catch { }
        }

        /// <summary>
        /// Executes an operation requiring exclusive serialized database access using SemaphoreSlim(1, 1).
        /// </summary>
        public async Task<T> ExecuteExclusiveAsync<T>(Func<SqliteConnection, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            await _exclusiveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using (var conn = CreateAndConfigureConnection())
                {
                    return await operation(conn).ConfigureAwait(false);
                }
            }
            finally
            {
                _exclusiveLock.Release();
            }
        }

        /// <summary>
        /// Executes a synchronous exclusive database operation.
        /// </summary>
        public T ExecuteExclusive<T>(Func<SqliteConnection, T> operation)
        {
            ThrowIfDisposed();
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            _exclusiveLock.Wait();
            try
            {
                using (var conn = CreateAndConfigureConnection())
                {
                    return operation(conn);
                }
            }
            finally
            {
                _exclusiveLock.Release();
            }
        }

        /// <summary>
        /// Creates a new SQLite connection, applies PRAGMA journal_mode=WAL, PRAGMA busy_timeout=5000,
        /// and verifies the actual journal mode returned.
        /// </summary>
        private SqliteConnection CreateAndConfigureConnection()
        {
            var csBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                DefaultTimeout = 5,
                Pooling = false
            };

            var connection = new SqliteConnection(csBuilder.ToString());
            connection.Open();

            ConfigurePragmas(connection);
            return connection;
        }

        private async Task<SqliteConnection> CreateAndConfigureConnectionAsync(CancellationToken cancellationToken)
        {
            var csBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                DefaultTimeout = 5,
                Pooling = false
            };

            var connection = new SqliteConnection(csBuilder.ToString());
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            ConfigurePragmas(connection);
            return connection;
        }

        /// <summary>
        /// Applies and verifies SQLite WAL mode, busy timeout, and performance pragmas.
        /// </summary>
        private void ConfigurePragmas(SqliteConnection connection)
        {
            // 1. Enable WAL mode
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode=WAL;";
                cmd.CommandTimeout = 5;
                cmd.ExecuteNonQuery();
            }

            // 2. Set 5000ms busy timeout
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA busy_timeout=5000;";
                cmd.CommandTimeout = 5;
                cmd.ExecuteNonQuery();
            }

            // 3. Synchronous mode NORMAL for WAL efficiency
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA synchronous=NORMAL;";
                cmd.CommandTimeout = 5;
                cmd.ExecuteNonQuery();
            }

            // 4. Foreign key constraints
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys=ON;";
                cmd.CommandTimeout = 5;
                cmd.ExecuteNonQuery();
            }

            // Verify journal mode once during initialization
            if (!_isWalVerified)
            {
                _activeJournalMode = VerifyJournalMode(connection);
                _isWalVerified = true;
                LoggingService.Log($"[SqliteConnectionManager] Verified Database Journal Mode: {_activeJournalMode}");
            }
        }

        /// <summary>
        /// Queries the database for its actual active journal mode (e.g. "wal").
        /// </summary>
        public static string VerifyJournalMode(SqliteConnection connection)
        {
            if (connection == null || connection.State != ConnectionState.Open) return "closed";
            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA journal_mode;";
                    cmd.CommandTimeout = 5;
                    var result = cmd.ExecuteScalar();
                    return result?.ToString()?.ToLowerInvariant() ?? "unknown";
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[SqliteConnectionManager] Failed to verify journal_mode: {ex.Message}");
                return "error";
            }
        }

        /// <summary>
        /// Closes all pooled connections and releases resources.
        /// </summary>
        public void CloseAllConnections()
        {
            foreach (var kvp in _connectionPool)
            {
                try
                {
                    if (kvp.Value != null)
                    {
                        kvp.Value.Close();
                        kvp.Value.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[SqliteConnectionManager] Closing connection failed", ex);
                }
            }
            _connectionPool.Clear();
        }

        /// <summary>
        /// Commits WAL checkpoint to flush log pages to the main database file.
        /// </summary>
        public void Checkpoint()
        {
            ThrowIfDisposed();
            ExecuteExclusive(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA wal_checkpoint(RESTART);";
                    cmd.CommandTimeout = 10;
                    cmd.ExecuteNonQuery();
                }
                return true;
            });
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            CloseAllConnections();
            _poolLock?.Dispose();
            _exclusiveLock?.Dispose();
        }
    }
}
