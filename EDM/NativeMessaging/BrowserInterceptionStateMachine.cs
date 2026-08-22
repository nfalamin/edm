using System;
using System.Collections.Concurrent;
using EDM.Services;

namespace EDM.NativeMessaging
{
    public enum InterceptionState
    {
        Detected,
        Validating,
        HandoffPending,
        HandedOff,
        BrowserCancelled,
        EdmQueued,
        EdmStarted,
        Failed,
        RecoverableFallback
    }

    public class InterceptionSession
    {
        public string CorrelationId { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public InterceptionState State { get; set; } = InterceptionState.Detected;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Production-grade deterministic state machine for browser download interception.
    /// Tracks correlation IDs across WebExtension background script, Native Messaging host,
    /// and EDM Queue Manager to prevent duplicate downloads and loss-on-cancel races.
    /// Includes structured diagnostic logging and automatic memory pruning.
    /// </summary>
    public class BrowserInterceptionStateMachine
    {
        private static readonly ConcurrentDictionary<string, InterceptionSession> Sessions = new();

        public static int ActiveSessionCount => Sessions.Count;

        public static InterceptionSession CreateSession(string correlationId, string url, string filename)
        {
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = "edm_corr_" + Guid.NewGuid().ToString("N");
            }

            var session = new InterceptionSession
            {
                CorrelationId = correlationId,
                Url = url,
                Filename = filename,
                State = InterceptionState.Detected,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow
            };

            Sessions[correlationId] = session;
            LogDiagnostic(correlationId, InterceptionState.Detected, "Session created");
            PruneStaleSessions(TimeSpan.FromMinutes(1));
            return session;
        }

        public static bool TransitionState(string correlationId, InterceptionState newState, string? error = null)
        {
            if (!Sessions.TryGetValue(correlationId, out var session)) return false;

            lock (session)
            {
                // Validate legal state transitions
                bool isValid = (session.State, newState) switch
                {
                    (InterceptionState.Detected, InterceptionState.Validating) => true,
                    (InterceptionState.Validating, InterceptionState.HandoffPending) => true,
                    (InterceptionState.HandoffPending, InterceptionState.HandedOff) => true,
                    (InterceptionState.HandedOff, InterceptionState.BrowserCancelled) => true,
                    (InterceptionState.BrowserCancelled, InterceptionState.EdmQueued) => true,
                    (InterceptionState.EdmQueued, InterceptionState.EdmStarted) => true,
                    (_, InterceptionState.Failed) => true,
                    (_, InterceptionState.RecoverableFallback) => true,
                    _ => false
                };

                if (isValid)
                {
                    session.State = newState;
                    session.LastUpdated = DateTime.UtcNow;
                    if (error != null) session.ErrorMessage = error;

                    LogDiagnostic(correlationId, newState, error);
                    return true;
                }
            }

            return false;
        }

        public static InterceptionSession? GetSession(string correlationId)
        {
            return Sessions.TryGetValue(correlationId, out var session) ? session : null;
        }

        public static int PruneStaleSessions(TimeSpan maxAge)
        {
            DateTime cutoff = DateTime.UtcNow - maxAge;
            int removed = 0;
            foreach (var kvp in Sessions)
            {
                if (kvp.Value.LastUpdated < cutoff)
                {
                    if (Sessions.TryRemove(kvp.Key, out _))
                    {
                        removed++;
                    }
                }
            }
            return removed;
        }

        public static void ResetForTesting()
        {
            Sessions.Clear();
        }

        private static void LogDiagnostic(string correlationId, InterceptionState state, string? message)
        {
            try
            {
                string msg = string.IsNullOrWhiteSpace(message) ? "" : $" | Details={message}";
                LoggingService.Log($"[DIAG:INTERCEPTION] CorrelationId={correlationId} | State={state}{msg}");
            }
            catch { }
        }
    }
}
