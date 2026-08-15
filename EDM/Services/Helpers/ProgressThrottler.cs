using System;
using System.Threading;

namespace EDM.Services.Helpers
{
    /// <summary>
    /// Thread-safe UI progress coalescer and throttler.
    /// Coalesces high-frequency progress updates arriving from background threads
    /// into a maximum update frequency (default: 100ms / ~10-20 FPS) without allocating
    /// per-event tasks or timers. Terminal state updates immediately bypass the throttle.
    /// </summary>
    public sealed class ProgressThrottler<T> : IDisposable
    {
        private readonly Action<T> _targetAction;
        private readonly TimeSpan _throttleInterval;
        private readonly Func<T, bool>? _isTerminalPredicate;
        private readonly Action<Action> _dispatchAction;

        private readonly object _lock = new object();
        private T? _pendingState;
        private bool _hasPendingState;
        private long _lastRenderTicks;
        private bool _timerScheduled;
        private bool _isDisposed;
        private readonly System.Threading.Timer _throttleTimer;

        public ProgressThrottler(
            Action<T> targetAction,
            TimeSpan? throttleInterval = null,
            Func<T, bool>? isTerminalPredicate = null,
            Action<Action>? dispatchAction = null)
        {
            _targetAction = targetAction ?? throw new ArgumentNullException(nameof(targetAction));
            _throttleInterval = throttleInterval ?? TimeSpan.FromMilliseconds(100);
            _isTerminalPredicate = isTerminalPredicate;
            _dispatchAction = dispatchAction ?? (action => action());
            _throttleTimer = new System.Threading.Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Report(T value)
        {
            if (_isDisposed) return;

            // Terminal state updates immediately bypass throttle delay
            if (_isTerminalPredicate != null && _isTerminalPredicate(value))
            {
                lock (_lock)
                {
                    _hasPendingState = false;
                    _pendingState = default;
                    _lastRenderTicks = Environment.TickCount64;
                    if (!_isDisposed)
                    {
                        try { _throttleTimer.Change(Timeout.Infinite, Timeout.Infinite); } catch { }
                    }
                }
                DispatchToUi(value);
                return;
            }

            long now = Environment.TickCount64;
            bool shouldRenderNow = false;

            lock (_lock)
            {
                if (_isDisposed) return;

                _pendingState = value;
                _hasPendingState = true;

                long elapsedMs = now - _lastRenderTicks;
                if (elapsedMs >= _throttleInterval.TotalMilliseconds && !_timerScheduled)
                {
                    shouldRenderNow = true;
                    _hasPendingState = false;
                    _lastRenderTicks = now;
                }
                else if (!_timerScheduled)
                {
                    long dueMs = (long)Math.Max(1, _throttleInterval.TotalMilliseconds - elapsedMs);
                    try
                    {
                        _throttleTimer.Change(dueMs, Timeout.Infinite);
                        _timerScheduled = true;
                    }
                    catch
                    {
                        // Timer disposed or stopped
                    }
                }
            }

            if (shouldRenderNow)
            {
                DispatchToUi(value);
            }
        }

        private void OnTimerTick(object? state)
        {
            T latestState;
            bool hasState;

            lock (_lock)
            {
                _timerScheduled = false;
                if (_isDisposed || !_hasPendingState) return;

                latestState = _pendingState!;
                _hasPendingState = false;
                _pendingState = default;
                _lastRenderTicks = Environment.TickCount64;
                hasState = true;
            }

            if (hasState)
            {
                DispatchToUi(latestState);
            }
        }

        private void DispatchToUi(T value)
        {
            if (_isDisposed) return;
            try
            {
                _dispatchAction(() =>
                {
                    if (!_isDisposed)
                    {
                        _targetAction(value);
                    }
                });
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[ProgressThrottler] Dispatch failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_isDisposed) return;
                _isDisposed = true;
                _hasPendingState = false;
                _pendingState = default;
            }

            try
            {
                _throttleTimer.Dispose();
            }
            catch { }
        }
    }
}
