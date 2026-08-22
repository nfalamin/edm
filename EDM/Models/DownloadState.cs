using System;

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
        Disposed = 10
    }

    public class DownloadStateController
    {
        private int _currentState = (int)DownloadState.Created;
        private readonly object _stateLock = new object();

        public DownloadState CurrentState => (DownloadState)_currentState;

        public bool TryTransition(DownloadState expected, DownloadState newState)
        {
            lock (_stateLock)
            {
                if (_currentState == (int)expected)
                {
                    _currentState = (int)newState;
                    return true;
                }
                return false;
            }
        }

        public bool ForceState(DownloadState newState)
        {
            lock (_stateLock)
            {
                if (_currentState == (int)DownloadState.Disposed && newState != DownloadState.Disposed)
                {
                    return false; // Cannot transition out of Disposed state
                }
                _currentState = (int)newState;
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
    }
}
