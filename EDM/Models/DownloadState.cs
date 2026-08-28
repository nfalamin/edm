using System;
using System.Collections.Generic;

namespace EDM.Models
{
    public enum DownloadState
    {
        Created = 0,
        Starting = 1,
        Running = 2,
        Pausing = 3,
        Paused = 4,
        Resuming = 5,
        Completing = 6,
        Completed = 7,
        Failed = 8,
        Cancelled = 9,
        Disposed = 10,

        // Explicit Lifecycle States (Phase 1.2 Hardening)
        Queued = 11,
        Probing = 12,
        Preparing = 13,
        Downloading = 14,
        Retrying = 15,
        Recovering = 16,
        Verifying = 17,
        Cancelling = 18
    }

    public class DownloadStateController
    {
        private int _currentState;
        private readonly object _stateLock = new object();

        public DownloadStateController(DownloadState initialState = DownloadState.Created)
        {
            _currentState = (int)initialState;
        }

        public event Action<DownloadState, DownloadState>? StateChanged;

        public DownloadState CurrentState => (DownloadState)Volatile.Read(ref _currentState);

        private static readonly HashSet<(DownloadState From, DownloadState To)> ValidTransitions = new()
        {
            // Initial & Preparation flow
            (DownloadState.Created, DownloadState.Starting),
            (DownloadState.Created, DownloadState.Queued),
            (DownloadState.Created, DownloadState.Probing),
            (DownloadState.Created, DownloadState.Preparing),
            (DownloadState.Created, DownloadState.Running),
            (DownloadState.Created, DownloadState.Downloading),
            (DownloadState.Created, DownloadState.Cancelled),
            (DownloadState.Created, DownloadState.Cancelling),
            (DownloadState.Created, DownloadState.Failed),

            (DownloadState.Queued, DownloadState.Starting),
            (DownloadState.Queued, DownloadState.Probing),
            (DownloadState.Queued, DownloadState.Preparing),
            (DownloadState.Queued, DownloadState.Running),
            (DownloadState.Queued, DownloadState.Downloading),
            (DownloadState.Queued, DownloadState.Cancelling),
            (DownloadState.Queued, DownloadState.Cancelled),
            (DownloadState.Queued, DownloadState.Failed),

            (DownloadState.Probing, DownloadState.Preparing),
            (DownloadState.Probing, DownloadState.Running),
            (DownloadState.Probing, DownloadState.Downloading),
            (DownloadState.Probing, DownloadState.Retrying),
            (DownloadState.Probing, DownloadState.Pausing),
            (DownloadState.Probing, DownloadState.Paused),
            (DownloadState.Probing, DownloadState.Cancelling),
            (DownloadState.Probing, DownloadState.Cancelled),
            (DownloadState.Probing, DownloadState.Failed),

            (DownloadState.Starting, DownloadState.Running),
            (DownloadState.Starting, DownloadState.Probing),
            (DownloadState.Starting, DownloadState.Preparing),
            (DownloadState.Starting, DownloadState.Downloading),
            (DownloadState.Starting, DownloadState.Pausing),
            (DownloadState.Starting, DownloadState.Paused),
            (DownloadState.Starting, DownloadState.Cancelling),
            (DownloadState.Starting, DownloadState.Cancelled),
            (DownloadState.Starting, DownloadState.Failed),

            (DownloadState.Preparing, DownloadState.Running),
            (DownloadState.Preparing, DownloadState.Downloading),
            (DownloadState.Preparing, DownloadState.Pausing),
            (DownloadState.Preparing, DownloadState.Paused),
            (DownloadState.Preparing, DownloadState.Cancelling),
            (DownloadState.Preparing, DownloadState.Cancelled),
            (DownloadState.Preparing, DownloadState.Failed),

            // Active Downloading flow
            (DownloadState.Running, DownloadState.Downloading),
            (DownloadState.Running, DownloadState.Pausing),
            (DownloadState.Running, DownloadState.Paused),
            (DownloadState.Running, DownloadState.Retrying),
            (DownloadState.Running, DownloadState.Recovering),
            (DownloadState.Running, DownloadState.Completing),
            (DownloadState.Running, DownloadState.Verifying),
            (DownloadState.Running, DownloadState.Completed),
            (DownloadState.Running, DownloadState.Cancelling),
            (DownloadState.Running, DownloadState.Cancelled),
            (DownloadState.Running, DownloadState.Failed),

            (DownloadState.Downloading, DownloadState.Running),
            (DownloadState.Downloading, DownloadState.Pausing),
            (DownloadState.Downloading, DownloadState.Paused),
            (DownloadState.Downloading, DownloadState.Retrying),
            (DownloadState.Downloading, DownloadState.Recovering),
            (DownloadState.Downloading, DownloadState.Completing),
            (DownloadState.Downloading, DownloadState.Verifying),
            (DownloadState.Downloading, DownloadState.Completed),
            (DownloadState.Downloading, DownloadState.Cancelling),
            (DownloadState.Downloading, DownloadState.Cancelled),
            (DownloadState.Downloading, DownloadState.Failed),

            // Pause / Resume flow
            (DownloadState.Pausing, DownloadState.Paused),
            (DownloadState.Pausing, DownloadState.Cancelling),
            (DownloadState.Pausing, DownloadState.Cancelled),
            (DownloadState.Pausing, DownloadState.Failed),

            (DownloadState.Paused, DownloadState.Resuming),
            (DownloadState.Paused, DownloadState.Starting),
            (DownloadState.Paused, DownloadState.Running),
            (DownloadState.Paused, DownloadState.Downloading),
            (DownloadState.Paused, DownloadState.Cancelling),
            (DownloadState.Paused, DownloadState.Cancelled),
            (DownloadState.Paused, DownloadState.Disposed),

            (DownloadState.Resuming, DownloadState.Probing),
            (DownloadState.Resuming, DownloadState.Preparing),
            (DownloadState.Resuming, DownloadState.Running),
            (DownloadState.Resuming, DownloadState.Downloading),
            (DownloadState.Resuming, DownloadState.Recovering),
            (DownloadState.Resuming, DownloadState.Cancelling),
            (DownloadState.Resuming, DownloadState.Cancelled),
            (DownloadState.Resuming, DownloadState.Failed),

            // Retry & Recovery flow
            (DownloadState.Retrying, DownloadState.Probing),
            (DownloadState.Retrying, DownloadState.Preparing),
            (DownloadState.Retrying, DownloadState.Running),
            (DownloadState.Retrying, DownloadState.Downloading),
            (DownloadState.Retrying, DownloadState.Recovering),
            (DownloadState.Retrying, DownloadState.Pausing),
            (DownloadState.Retrying, DownloadState.Paused),
            (DownloadState.Retrying, DownloadState.Cancelling),
            (DownloadState.Retrying, DownloadState.Cancelled),
            (DownloadState.Retrying, DownloadState.Failed),

            (DownloadState.Recovering, DownloadState.Probing),
            (DownloadState.Recovering, DownloadState.Preparing),
            (DownloadState.Recovering, DownloadState.Running),
            (DownloadState.Recovering, DownloadState.Downloading),
            (DownloadState.Recovering, DownloadState.Pausing),
            (DownloadState.Recovering, DownloadState.Paused),
            (DownloadState.Recovering, DownloadState.Cancelling),
            (DownloadState.Recovering, DownloadState.Cancelled),
            (DownloadState.Recovering, DownloadState.Failed),

            // Completion & Verification flow
            (DownloadState.Completing, DownloadState.Verifying),
            (DownloadState.Completing, DownloadState.Completed),
            (DownloadState.Completing, DownloadState.Failed),
            (DownloadState.Completing, DownloadState.Cancelling),
            (DownloadState.Completing, DownloadState.Cancelled),

            (DownloadState.Verifying, DownloadState.Completed),
            (DownloadState.Verifying, DownloadState.Failed),

            // Cancellation flow
            (DownloadState.Cancelling, DownloadState.Cancelled),
            (DownloadState.Cancelling, DownloadState.Failed),

            // Terminal to Disposed flow
            (DownloadState.Completed, DownloadState.Disposed),
            (DownloadState.Failed, DownloadState.Disposed),
            (DownloadState.Cancelled, DownloadState.Disposed),
            (DownloadState.Failed, DownloadState.Queued),
            (DownloadState.Failed, DownloadState.Starting),
            (DownloadState.Cancelled, DownloadState.Queued),
            (DownloadState.Cancelled, DownloadState.Starting)
        };

        public bool CanTransition(DownloadState from, DownloadState to)
        {
            if (from == to) return true;
            if (from == DownloadState.Disposed) return false;
            return ValidTransitions.Contains((from, to));
        }

        public bool TryTransition(DownloadState expected, DownloadState newState)
        {
            lock (_stateLock)
            {
                var current = (DownloadState)_currentState;
                if (current == expected)
                {
                    if (!CanTransition(current, newState))
                    {
                        return false;
                    }
                    _currentState = (int)newState;
                    StateChanged?.Invoke(current, newState);
                    return true;
                }
                return false;
            }
        }

        public bool TransitionTo(DownloadState newState)
        {
            lock (_stateLock)
            {
                var current = (DownloadState)_currentState;
                if (current == newState) return true;
                if (!CanTransition(current, newState))
                {
                    return false;
                }
                _currentState = (int)newState;
                StateChanged?.Invoke(current, newState);
                return true;
            }
        }

        public bool ForceState(DownloadState newState)
        {
            lock (_stateLock)
            {
                var current = (DownloadState)_currentState;
                if (current == DownloadState.Disposed && newState != DownloadState.Disposed)
                {
                    return false;
                }
                _currentState = (int)newState;
                StateChanged?.Invoke(current, newState);
                return true;
            }
        }

        public bool IsTerminal => CurrentState switch
        {
            DownloadState.Completed => true,
            DownloadState.Failed => true,
            DownloadState.Cancelled => true,
            DownloadState.Disposed => true,
            _ => false
        };

        public bool IsActive => CurrentState switch
        {
            DownloadState.Starting => true,
            DownloadState.Queued => true,
            DownloadState.Probing => true,
            DownloadState.Preparing => true,
            DownloadState.Running => true,
            DownloadState.Downloading => true,
            DownloadState.Resuming => true,
            DownloadState.Retrying => true,
            DownloadState.Recovering => true,
            DownloadState.Completing => true,
            DownloadState.Verifying => true,
            _ => false
        };
    }
}
