using System;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    // Minimal pause token implementation used by download tasks
    public class PauseTokenSource
    {
        private TaskCompletionSource<bool>? _pausedTcs;

        public bool IsPaused => _pausedTcs != null;

        // Event fired when pause state changes (true = paused, false = resumed)
        public event Action<bool>? OnPauseChanged;

        public void Pause()
        {
            // create a TCS if not already paused
            if (Interlocked.CompareExchange(ref _pausedTcs, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously), null) == null)
            {
                OnPauseChanged?.Invoke(true);
            }
        }

        public void Resume()
        {
            var tcs = Interlocked.Exchange(ref _pausedTcs, null);
            if (tcs != null)
            {
                try { tcs.TrySetResult(true); } catch (Exception ex) { LoggingService.LogException("[PauseTokenSource] Failed to resume (TrySetResult)", ex); }
                OnPauseChanged?.Invoke(false);
            }
        }

        public Task WaitIfPausedAsync()
        {
            var tcs = _pausedTcs;
            return tcs != null ? tcs.Task : Task.CompletedTask;
        }
    }
}
