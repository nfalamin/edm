using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace EDM.Services.Data
{
    /// <summary>
    /// Auditing service for tracking and logging all database operations.
    /// Provides diagnostics for performance monitoring and debugging.
    /// </summary>
    public class DatabaseAuditLog
    {
        private readonly ConcurrentBag<AuditEntry> _auditLog;
        private readonly int _maxEntriesInMemory;

        public DatabaseAuditLog(int maxEntriesInMemory = 1000)
        {
            _auditLog = new ConcurrentBag<AuditEntry>();
            _maxEntriesInMemory = maxEntriesInMemory;
        }

        /// <summary>
        /// Records a database query operation.
        /// </summary>
        public void LogQuery(string operationType, string query, long? executionTimeMs = null, bool success = true, string? errorMessage = null)
        {
            var entry = new AuditEntry
            {
                Timestamp = DateTime.UtcNow,
                OperationType = operationType,
                Query = query,
                ExecutionTimeMs = executionTimeMs ?? 0,
                Success = success,
                ErrorMessage = errorMessage
            };

            _auditLog.Add(entry);

            // Trim old entries if we exceed the limit
            if (_auditLog.Count > _maxEntriesInMemory)
            {
                var entriesToKeep = new ConcurrentBag<AuditEntry>();
                int count = 0;
                foreach (var e in _auditLog)
                {
                    if (count >= _maxEntriesInMemory / 2)
                        entriesToKeep.Add(e);
                    count++;
                }

                // This is not perfect but good enough for in-memory auditing
                while (_auditLog.TryTake(out _)) { }
                foreach (var e in entriesToKeep)
                    _auditLog.Add(e);
            }
        }

        /// <summary>
        /// Returns a summary of audit log statistics.
        /// </summary>
        public AuditStatistics GetStatistics()
        {
            var stats = new AuditStatistics();
            long totalExecutionTime = 0;
            int count = 0;
            int failures = 0;

            foreach (var entry in _auditLog)
            {
                count++;
                totalExecutionTime += entry.ExecutionTimeMs;
                if (!entry.Success)
                    failures++;

                if (!stats.OperationCounts.ContainsKey(entry.OperationType))
                    stats.OperationCounts[entry.OperationType] = 0;
                stats.OperationCounts[entry.OperationType]++;
            }

            stats.TotalOperations = count;
            stats.TotalExecutionTimeMs = totalExecutionTime;
            stats.FailedOperations = failures;
            stats.AverageExecutionTimeMs = count > 0 ? totalExecutionTime / (double)count : 0;

            return stats;
        }

        /// <summary>
        /// Gets recent audit entries for diagnostics.
        /// </summary>
        public AuditEntry[] GetRecentEntries(int count = 50)
        {
            var entries = new System.Collections.Generic.List<AuditEntry>(_auditLog);
            entries.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));

            if (entries.Count > count)
                entries = entries.GetRange(0, count);

            return entries.ToArray();
        }

        /// <summary>
        /// Clears the audit log.
        /// </summary>
        public void Clear()
        {
            while (_auditLog.TryTake(out _)) { }
        }

        /// <summary>
        /// Represents a single audit log entry.
        /// </summary>
        public class AuditEntry
        {
            public DateTime Timestamp { get; set; }
            public string OperationType { get; set; } = string.Empty; // INSERT, UPDATE, SELECT, DELETE, CREATE, etc.
            public string Query { get; set; } = string.Empty;
            public long ExecutionTimeMs { get; set; }
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }

            public override string ToString()
            {
                var status = Success ? "OK" : "FAIL";
                return $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {OperationType} ({status}) {ExecutionTimeMs}ms - {(string.IsNullOrEmpty(ErrorMessage) ? "" : ErrorMessage)}";
            }
        }

        /// <summary>
        /// Summary statistics for audit log.
        /// </summary>
        public class AuditStatistics
        {
            public int TotalOperations { get; set; }
            public long TotalExecutionTimeMs { get; set; }
            public int FailedOperations { get; set; }
            public double AverageExecutionTimeMs { get; set; }
            public System.Collections.Generic.Dictionary<string, int> OperationCounts { get; } = new();

            public override string ToString()
            {
                return $"Total: {TotalOperations}, Failed: {FailedOperations}, " +
                       $"Total Time: {TotalExecutionTimeMs}ms, Avg: {AverageExecutionTimeMs:F2}ms";
            }
        }
    }
}
