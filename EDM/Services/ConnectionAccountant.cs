using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace EDM.Services
{
    public enum WorkerState
    {
        Idle,
        Connecting,
        Downloading,
        Stalled,
        Completed,
        Failed
    }

    /// <summary>
    /// Per-worker telemetry record tracking detailed performance and network characteristics.
    /// </summary>
    public sealed record WorkerTelemetrySnapshot
    {
        public string WorkerId { get; init; } = string.Empty;
        public int SegmentId { get; init; }
        public long BytesDownloaded { get; init; }
        public DateTime StartTime { get; init; }
        public DateTime? EndTime { get; init; }
        public double DurationSeconds { get; init; }
        public double AverageThroughputBps { get; init; }
        public double CurrentThroughputBps { get; init; }
        public double RttMs { get; init; }
        public double TtfbMs { get; init; }
        public int Retries { get; init; }
        public int Errors { get; init; }
        public int HttpStatus { get; init; }
        public WorkerState State { get; init; }
    }

    /// <summary>
    /// Immutable telemetry snapshot of the connection accounting state.
    /// </summary>
    public sealed record ConnectionTelemetrySnapshot
    {
        public int RequestedConnections { get; init; }
        public int ConfiguredMaximumConnections { get; init; }
        public int ActiveConnections { get; init; }
        public int StartingConnections { get; init; }
        public int IdleWorkers { get; init; }
        public int QueuedSegments { get; init; }
        public int RunningSegments { get; init; }
        public int CompletedSegments { get; init; }
        public double MeasuredRttMs { get; init; }
        public double TimeToFirstByteMs { get; init; }
        public int TotalErrors { get; init; }
        public int Http429Count { get; init; }
        public int Http5xxCount { get; init; }
        public int TimeoutCount { get; init; }
        public int ConnectionResetCount { get; init; }
        public IReadOnlyList<WorkerTelemetrySnapshot> WorkerSnapshots { get; init; } = Array.Empty<WorkerTelemetrySnapshot>();
    }

    /// <summary>
    /// ConnectionAccountant — Authoritative, thread-safe manager for connection and worker state.
    /// Guarantees that active connection counters never drift, become negative, or misrepresent
    /// idle workers or queued segments as active network connections. Also maintains granular
    /// per-worker telemetry and slow connection detection.
    /// </summary>
    public sealed class ConnectionAccountant
    {
        private int _requestedConnections;
        private int _configuredMaximumConnections;
        private int _startingConnections;
        private int _activeConnections;
        private int _idleWorkers;
        private int _totalErrors;
        private int _http429Count;
        private int _http5xxCount;
        private int _timeoutCount;
        private int _connectionResetCount;

        // RTT telemetry tracking (EWMA smoothed)
        private double _measuredRttMs = -1;
        private double _ttfbMs = -1;
        private readonly object _rttLock = new();

        private readonly ConcurrentDictionary<string, InternalWorkerState> _workers = new();

        private class InternalWorkerState
        {
            public string WorkerId { get; set; } = string.Empty;
            public int SegmentId { get; set; }
            public long BytesDownloaded { get; set; }
            public DateTime StartTime { get; set; } = DateTime.UtcNow;
            public DateTime? EndTime { get; set; }
            public double CurrentThroughputBps { get; set; }
            public double RttMs { get; set; }
            public double TtfbMs { get; set; }
            public int Retries { get; set; }
            public int Errors { get; set; }
            public int HttpStatus { get; set; } = 200;
            public WorkerState State { get; set; } = WorkerState.Idle;
            public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        }

        public ConnectionAccountant(int configuredMaxConnections = 16)
        {
            _configuredMaximumConnections = Math.Max(1, configuredMaxConnections);
            _requestedConnections = _configuredMaximumConnections;
        }

        public int RequestedConnections => Volatile.Read(ref _requestedConnections);
        public int ConfiguredMaximumConnections => Volatile.Read(ref _configuredMaximumConnections);
        public int ActiveConnections => Volatile.Read(ref _activeConnections);
        public int StartingConnections => Volatile.Read(ref _startingConnections);
        public int IdleWorkers => Volatile.Read(ref _idleWorkers);
        public double MeasuredRttMs { get { lock (_rttLock) { return _measuredRttMs > 0 ? _measuredRttMs : 0; } } }
        public double TimeToFirstByteMs { get { lock (_rttLock) { return _ttfbMs > 0 ? _ttfbMs : 0; } } }

        public void SetRequestedConnections(int count)
        {
            Volatile.Write(ref _requestedConnections, Math.Clamp(count, 1, ConfiguredMaximumConnections));
        }

        public void SetConfiguredMaximum(int max)
        {
            Volatile.Write(ref _configuredMaximumConnections, Math.Max(1, max));
        }

        public void RegisterWorker(string workerId, int segmentId)
        {
            var ws = _workers.GetOrAdd(workerId, id => new InternalWorkerState { WorkerId = id });
            lock (ws)
            {
                ws.SegmentId = segmentId;
                ws.StartTime = DateTime.UtcNow;
                ws.EndTime = null;
                ws.State = WorkerState.Connecting;
                ws.LastActivity = DateTime.UtcNow;
            }
        }

        public void RecordWorkerProgress(string workerId, int segmentId, long bytesDownloaded, double currentSpeedBps, double rttMs = 0, double ttfbMs = 0)
        {
            if (_workers.TryGetValue(workerId, out var ws))
            {
                lock (ws)
                {
                    ws.SegmentId = segmentId;
                    ws.BytesDownloaded = bytesDownloaded;
                    ws.CurrentThroughputBps = currentSpeedBps;
                    ws.State = WorkerState.Downloading;
                    ws.LastActivity = DateTime.UtcNow;
                    if (rttMs > 0) ws.RttMs = rttMs;
                    if (ttfbMs > 0) ws.TtfbMs = ttfbMs;
                }
            }
        }

        public void RecordWorkerError(string workerId, int segmentId, Exception ex)
        {
            if (_workers.TryGetValue(workerId, out var ws))
            {
                lock (ws)
                {
                    ws.Errors++;
                    ws.State = WorkerState.Failed;
                    ws.LastActivity = DateTime.UtcNow;
                }
            }
        }

        public void CompleteWorker(string workerId)
        {
            if (_workers.TryGetValue(workerId, out var ws))
            {
                lock (ws)
                {
                    ws.EndTime = DateTime.UtcNow;
                    ws.State = WorkerState.Completed;
                }
            }
        }

        /// <summary>
        /// Identifies underperforming workers whose throughput is below the threshold ratio of cluster median throughput.
        /// </summary>
        public List<string> DetectSlowWorkers(double thresholdRatio = 0.25)
        {
            var activeWorkers = _workers.Values
                .Where(w => w.State == WorkerState.Downloading && w.CurrentThroughputBps > 0)
                .ToList();

            if (activeWorkers.Count < 3) return new List<string>();

            var speeds = activeWorkers.Select(w => w.CurrentThroughputBps).OrderBy(s => s).ToList();
            double medianSpeed = speeds[speeds.Count / 2];

            if (medianSpeed < 50 * 1024) return new List<string>(); // Ignore when cluster is very slow overall

            double slowThreshold = medianSpeed * thresholdRatio;
            return activeWorkers
                .Where(w => w.CurrentThroughputBps < slowThreshold && (DateTime.UtcNow - w.StartTime).TotalSeconds > 3.0)
                .Select(w => w.WorkerId)
                .ToList();
        }

        public void OnConnectionRequested()
        {
            Interlocked.Increment(ref _startingConnections);
        }

        public void OnConnectionStarted()
        {
            Interlocked.Decrement(ref _startingConnections);
            Interlocked.Increment(ref _activeConnections);
        }

        public void OnConnectionActive()
        {
        }

        public void OnWorkerIdle()
        {
            Interlocked.Increment(ref _idleWorkers);
        }

        public void OnWorkerBusy()
        {
            int current = Volatile.Read(ref _idleWorkers);
            if (current > 0)
            {
                Interlocked.Decrement(ref _idleWorkers);
            }
        }

        public void OnConnectionCompleted()
        {
            DecrementActiveSafe();
        }

        public void OnConnectionFailed(Exception? ex = null)
        {
            DecrementActiveSafe();
            Interlocked.Increment(ref _totalErrors);

            if (ex != null)
            {
                if (ex is System.Net.Http.HttpRequestException httpEx && httpEx.StatusCode.HasValue)
                {
                    int code = (int)httpEx.StatusCode.Value;
                    if (code == 429) Interlocked.Increment(ref _http429Count);
                    else if (code >= 500) Interlocked.Increment(ref _http5xxCount);
                }
                else if (ex is TimeoutException || ex is OperationCanceledException)
                {
                    Interlocked.Increment(ref _timeoutCount);
                }
                else if (ex is System.IO.IOException || ex is System.Net.Sockets.SocketException)
                {
                    Interlocked.Increment(ref _connectionResetCount);
                }
            }
        }

        public void OnConnectionCancelled()
        {
            DecrementActiveSafe();
        }

        public void OnConnectionDisposed()
        {
            int starting = Volatile.Read(ref _startingConnections);
            if (starting > 0)
            {
                Interlocked.CompareExchange(ref _startingConnections, 0, starting);
            }
        }

        public void RecordNetworkMetrics(double rttMs, double ttfbMs = -1)
        {
            if (rttMs <= 0) return;
            lock (_rttLock)
            {
                if (_measuredRttMs <= 0)
                {
                    _measuredRttMs = rttMs;
                }
                else
                {
                    // EWMA filter (alpha = 0.3)
                    _measuredRttMs = (0.3 * rttMs) + (0.7 * _measuredRttMs);
                }

                if (ttfbMs > 0)
                {
                    if (_ttfbMs <= 0) _ttfbMs = ttfbMs;
                    else _ttfbMs = (0.3 * ttfbMs) + (0.7 * _ttfbMs);
                }
            }
        }

        public ConnectionTelemetrySnapshot GetSnapshot(int queuedSegments = 0, int runningSegments = 0, int completedSegments = 0)
        {
            double rtt, ttfb;
            lock (_rttLock)
            {
                rtt = _measuredRttMs > 0 ? _measuredRttMs : 0;
                ttfb = _ttfbMs > 0 ? _ttfbMs : 0;
            }

            var workerSnapshots = _workers.Values.Select(w =>
            {
                double duration = (w.EndTime.HasValue ? w.EndTime.Value - w.StartTime : DateTime.UtcNow - w.StartTime).TotalSeconds;
                double avgBps = duration > 0.1 ? w.BytesDownloaded / duration : 0;

                return new WorkerTelemetrySnapshot
                {
                    WorkerId = w.WorkerId,
                    SegmentId = w.SegmentId,
                    BytesDownloaded = w.BytesDownloaded,
                    StartTime = w.StartTime,
                    EndTime = w.EndTime,
                    DurationSeconds = duration,
                    AverageThroughputBps = avgBps,
                    CurrentThroughputBps = w.CurrentThroughputBps,
                    RttMs = w.RttMs,
                    TtfbMs = w.TtfbMs,
                    Retries = w.Retries,
                    Errors = w.Errors,
                    HttpStatus = w.HttpStatus,
                    State = w.State
                };
            }).ToList();

            return new ConnectionTelemetrySnapshot
            {
                RequestedConnections = Volatile.Read(ref _requestedConnections),
                ConfiguredMaximumConnections = Volatile.Read(ref _configuredMaximumConnections),
                ActiveConnections = Math.Max(0, Volatile.Read(ref _activeConnections)),
                StartingConnections = Math.Max(0, Volatile.Read(ref _startingConnections)),
                IdleWorkers = Math.Max(0, Volatile.Read(ref _idleWorkers)),
                QueuedSegments = queuedSegments,
                RunningSegments = runningSegments,
                CompletedSegments = completedSegments,
                MeasuredRttMs = rtt,
                TimeToFirstByteMs = ttfb,
                TotalErrors = Volatile.Read(ref _totalErrors),
                Http429Count = Volatile.Read(ref _http429Count),
                Http5xxCount = Volatile.Read(ref _http5xxCount),
                TimeoutCount = Volatile.Read(ref _timeoutCount),
                ConnectionResetCount = Volatile.Read(ref _connectionResetCount),
                WorkerSnapshots = workerSnapshots
            };
        }

        public void Reset()
        {
            Volatile.Write(ref _startingConnections, 0);
            Volatile.Write(ref _activeConnections, 0);
            Volatile.Write(ref _idleWorkers, 0);
            Volatile.Write(ref _totalErrors, 0);
            Volatile.Write(ref _http429Count, 0);
            Volatile.Write(ref _http5xxCount, 0);
            Volatile.Write(ref _timeoutCount, 0);
            Volatile.Write(ref _connectionResetCount, 0);
            _workers.Clear();
            lock (_rttLock)
            {
                _measuredRttMs = -1;
                _ttfbMs = -1;
            }
        }

        private void DecrementActiveSafe()
        {
            while (true)
            {
                int current = Volatile.Read(ref _activeConnections);
                if (current <= 0) break;
                if (Interlocked.CompareExchange(ref _activeConnections, current - 1, current) == current)
                    break;
            }
        }
    }
}
