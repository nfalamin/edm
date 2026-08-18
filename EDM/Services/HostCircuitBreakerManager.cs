using System;
using System.Collections.Concurrent;
using System.Net;

namespace EDM.Services
{
    public enum CircuitState
    {
        Closed,   // Normal healthy operation
        Open,     // Tripped: host is failing / rate-limiting, blocking rapid retries
        HalfOpen  // Recovery testing: single probe allowed
    }

    public class HostCircuitState
    {
        public string Host { get; set; } = string.Empty;
        public CircuitState State { get; set; } = CircuitState.Closed;
        public int ConsecutiveFailures { get; set; }
        public int ConsecutiveSuccesses { get; set; }
        public DateTime LastFailureTime { get; set; }
        public DateTime LastStateChangeTime { get; set; } = DateTime.UtcNow;
        public TimeSpan CurrentOpenDuration { get; set; } = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// HostCircuitBreakerManager — Protects remote hosts and local network from retry storms
    /// by isolating repeatedly failing hosts with an adaptive, jittered circuit breaker.
    /// </summary>
    public class HostCircuitBreakerManager
    {
        private static readonly Lazy<HostCircuitBreakerManager> _lazy = new(() => new HostCircuitBreakerManager());
        public static HostCircuitBreakerManager Instance => _lazy.Value;

        private readonly ConcurrentDictionary<string, HostCircuitState> _hostCircuits = new(StringComparer.OrdinalIgnoreCase);
        private readonly int _failureThreshold;
        private readonly TimeSpan _baseOpenDuration;
        private readonly object _lock = new();

        public HostCircuitBreakerManager(int failureThreshold = 5, TimeSpan? baseOpenDuration = null)
        {
            _failureThreshold = Math.Max(2, failureThreshold);
            _baseOpenDuration = baseOpenDuration ?? TimeSpan.FromSeconds(10);
        }

        public CircuitState GetHostState(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return CircuitState.Closed;
            string key = host.Trim().ToLowerInvariant();

            if (_hostCircuits.TryGetValue(key, out var circuit))
            {
                lock (circuit)
                {
                    if (circuit.State == CircuitState.Open)
                    {
                        if (DateTime.UtcNow - circuit.LastStateChangeTime > circuit.CurrentOpenDuration)
                        {
                            circuit.State = CircuitState.HalfOpen;
                            circuit.LastStateChangeTime = DateTime.UtcNow;
                            LoggingService.Log($"[HostCircuitBreaker] Host '{host}' transitioned to HalfOpen (probing recovery).");
                        }
                    }
                    return circuit.State;
                }
            }
            return CircuitState.Closed;
        }

        public bool CanExecute(string host, out TimeSpan requiredDelay)
        {
            requiredDelay = TimeSpan.Zero;
            var state = GetHostState(host);

            if (state == CircuitState.Open)
            {
                string key = host.Trim().ToLowerInvariant();
                if (_hostCircuits.TryGetValue(key, out var circuit))
                {
                    lock (circuit)
                    {
                        var remaining = circuit.CurrentOpenDuration - (DateTime.UtcNow - circuit.LastStateChangeTime);
                        requiredDelay = remaining > TimeSpan.Zero ? remaining : TimeSpan.FromSeconds(1);
                        return false;
                    }
                }
            }

            return true;
        }

        public void RecordSuccess(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return;
            string key = host.Trim().ToLowerInvariant();
            var circuit = _hostCircuits.GetOrAdd(key, k => new HostCircuitState { Host = k, CurrentOpenDuration = _baseOpenDuration });

            lock (circuit)
            {
                circuit.ConsecutiveFailures = 0;
                circuit.ConsecutiveSuccesses++;

                if (circuit.State == CircuitState.HalfOpen || circuit.State == CircuitState.Open)
                {
                    circuit.State = CircuitState.Closed;
                    circuit.CurrentOpenDuration = _baseOpenDuration;
                    circuit.LastStateChangeTime = DateTime.UtcNow;
                    LoggingService.Log($"[HostCircuitBreaker] Host '{host}' circuit closed (recovered).");
                }
            }
        }

        public void RecordFailure(string host, HttpStatusCode? statusCode = null, Exception? ex = null)
        {
            if (string.IsNullOrWhiteSpace(host)) return;
            string key = host.Trim().ToLowerInvariant();
            var circuit = _hostCircuits.GetOrAdd(key, k => new HostCircuitState { Host = k, CurrentOpenDuration = _baseOpenDuration });

            lock (circuit)
            {
                circuit.ConsecutiveFailures++;
                circuit.ConsecutiveSuccesses = 0;
                circuit.LastFailureTime = DateTime.UtcNow;

                bool isSevere = statusCode == HttpStatusCode.TooManyRequests ||
                                statusCode == HttpStatusCode.ServiceUnavailable ||
                                statusCode == HttpStatusCode.BadGateway;

                int threshold = isSevere ? Math.Max(2, _failureThreshold / 2) : _failureThreshold;

                if (circuit.ConsecutiveFailures >= threshold)
                {
                    if (circuit.State != CircuitState.Open)
                    {
                        circuit.State = CircuitState.Open;
                        circuit.LastStateChangeTime = DateTime.UtcNow;
                        circuit.CurrentOpenDuration = TimeSpan.FromSeconds(Math.Min(120, circuit.CurrentOpenDuration.TotalSeconds * 1.5));
                        LoggingService.LogWarning($"[HostCircuitBreaker] Circuit TRIPPED for host '{host}' ({circuit.ConsecutiveFailures} failures). Backing off for {circuit.CurrentOpenDuration.TotalSeconds:F0}s.");
                    }
                }
            }
        }

        public void Reset(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return;
            _hostCircuits.TryRemove(host.Trim().ToLowerInvariant(), out _);
        }
    }
}
