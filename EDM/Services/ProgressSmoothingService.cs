using System;
using System.Threading;

namespace EDM.Services
{
    /// <summary>
    /// Smoothly interpolates progress updates to provide a fluid progress bar experience.
    /// Wraps an IProgress<DownloadProgressInfo> and accepts coarse updates, then emits
    /// interpolated intermediate updates on a timer until the target is reached.
    /// Also computes average and peak speeds (bytes/sec) using EWMA.
    /// </summary>
    public class SmoothProgressReporter : IProgress<DownloadProgressInfo>, IDisposable
    {
        private readonly IProgress<DownloadProgressInfo> _inner;
        private readonly object _sync = new object();
        private DownloadProgressInfo? _lastEmitted;
        private DownloadProgressInfo? _target;
        private CancellationTokenSource? _cts;
        private Task? _workerTask;
        private DateTime _animStart;
        private TimeSpan _animDuration;
        private readonly int _tickMs;

        // EWMA smoothing for average speed
        private double _ewmaSpeed = -1;
        private readonly double _ewmaAlpha; // smoothing factor
        private double _peakSpeed = 0;

        public SmoothProgressReporter(IProgress<DownloadProgressInfo> inner, int tickMs = 60, int animationMs = 800, double ewmaAlpha = 0.2)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _tickMs = Math.Max(20, tickMs);
            _animDuration = TimeSpan.FromMilliseconds(Math.Max(100, animationMs));
            _ewmaAlpha = Math.Clamp(ewmaAlpha, 0.01, 0.99);
        }

        public void Report(DownloadProgressInfo value)
        {
            if (value == null) return;
            lock (_sync)
            {
                // update EWMA and peak using raw incoming speed sample
                try
                {
                    if (value.SpeedBytesPerSecond > 0)
                    {
                        if (_ewmaSpeed < 0) _ewmaSpeed = value.SpeedBytesPerSecond;
                        else _ewmaSpeed = _ewmaAlpha * value.SpeedBytesPerSecond + (1 - _ewmaAlpha) * _ewmaSpeed;
                        if (value.SpeedBytesPerSecond > _peakSpeed) _peakSpeed = value.SpeedBytesPerSecond;
                    }
                }
                catch (Exception ex) { LoggingService.Log($"[SmoothProgressReporter] EWMA update failed: {ex.Message}"); }

                // Ensure we have a baseline lastEmitted
                if (_lastEmitted == null)
                {
                    _lastEmitted = CloneInfo(value);
                    _lastEmitted.SmoothedProgressPercentage = _lastEmitted.ProgressPercentage;
                    _lastEmitted.AverageSpeedBytesPerSecond = _ewmaSpeed > 0 ? _ewmaSpeed : _lastEmitted.SpeedBytesPerSecond;
                    _lastEmitted.PeakSpeedBytesPerSecond = _peakSpeed;
                    // emit immediately
                    SafeEmit(_lastEmitted);
                    return;
                }

                // Update target and restart animation
                _target = CloneInfo(value);
                _target.AverageSpeedBytesPerSecond = _ewmaSpeed > 0 ? _ewmaSpeed : _target.SpeedBytesPerSecond;
                _target.PeakSpeedBytesPerSecond = _peakSpeed;

                _animStart = DateTime.UtcNow;
                // duration scales with delta but bounded
                var delta = Math.Abs(_target.ProgressPercentage - _lastEmitted.ProgressPercentage);
                var dur = (int)Math.Clamp(_animDuration.TotalMilliseconds * Math.Min(1.0, delta / 10.0), 100, _animDuration.TotalMilliseconds);
                _animDuration = TimeSpan.FromMilliseconds(dur);

                if (_workerTask == null)
                {
                    _cts = new CancellationTokenSource();
                    _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token));
                }
            }
        }

        private void TimerTick()
        {
            DownloadProgressInfo? emit = null;
            lock (_sync)
            {
                if (_target == null || _lastEmitted == null)
                {
                    // nothing to do
                    return;
                }

                var now = DateTime.UtcNow;
                var elapsed = now - _animStart;
                double t = _animDuration.TotalMilliseconds <= 0 ? 1.0 : Math.Clamp(elapsed.TotalMilliseconds / _animDuration.TotalMilliseconds, 0.0, 1.0);
                // ease-out cubic
                double ease = 1 - Math.Pow(1 - t, 3);

                var next = CloneInfo(_target);
                // interpolate progress percentage
                next.SmoothedProgressPercentage = Lerp(_lastEmitted.SmoothedProgressPercentage <= 0 ? _lastEmitted.ProgressPercentage : _lastEmitted.SmoothedProgressPercentage, _target.ProgressPercentage, ease);
                // interpolate bytes received
                next.BytesReceived = (long)Lerp(_lastEmitted.BytesReceived, _target.BytesReceived, ease);
                // speed: use EWMA as average; report both instantaneous and average
                next.AverageSpeedBytesPerSecond = _target.AverageSpeedBytesPerSecond;
                next.PeakSpeedBytesPerSecond = _target.PeakSpeedBytesPerSecond;
                // remaining seconds interpolate
                next.RemainingSeconds = Lerp(_lastEmitted.RemainingSeconds, _target.RemainingSeconds, ease);

                emit = next;

                if (t >= 1.0)
                {
                    // reached target; update lastEmitted and clear target
                    _lastEmitted = CloneInfo(next);
                    _lastEmitted.ProgressPercentage = _target.ProgressPercentage;
                    _lastEmitted.SmoothedProgressPercentage = _target.ProgressPercentage;
                    _target = null;
                    // stop worker when target reached (worker will exit naturally if no target)
                }
            }

            if (emit != null)
            {
                SafeEmit(emit);
            }
        }

        private async Task WorkerLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        TimerTick();
                    }
                    catch (Exception ex) { LoggingService.LogException("[SmoothProgressReporter] TimerTick failed", ex); }
                    await Task.Delay(_tickMs, ct).ConfigureAwait(false);
                    // if no target and no pending animation, pause loop briefly to reduce CPU
                    lock (_sync)
                    {
                        if (_target == null) break;
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                // cleanup
                lock (_sync)
                {
                    _workerTask = null;
                    try { _cts?.Dispose(); } catch (Exception ex) { LoggingService.Log($"[SmoothProgressReporter] Failed to dispose _cts: {ex.Message}"); }
                    _cts = null;
                }
            }
        }

        private void SafeEmit(DownloadProgressInfo info)
        {
            try
            {
                _inner.Report(info);
            }
            catch { }
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
        private static long Lerp(long a, long b, double t) => (long)(a + (b - a) * t);

        private static DownloadProgressInfo CloneInfo(DownloadProgressInfo src)
        {
            return new DownloadProgressInfo
            {
                BytesReceived = src.BytesReceived,
                SegmentBytes = src.SegmentBytes ?? System.Array.Empty<long>(),
                SegmentCount = src.SegmentCount,
                TotalBytes = src.TotalBytes,
                ProgressPercentage = src.ProgressPercentage,
                SmoothedProgressPercentage = src.SmoothedProgressPercentage,
                SpeedBytesPerSecond = src.SpeedBytesPerSecond,
                AverageSpeedBytesPerSecond = src.AverageSpeedBytesPerSecond,
                PeakSpeedBytesPerSecond = src.PeakSpeedBytesPerSecond,
                ErrorMessage = src.ErrorMessage,
                IsCompleted = src.IsCompleted,
                Status = src.Status,
                ServerSupportsResume = src.ServerSupportsResume,
                RemainingSeconds = src.RemainingSeconds
            };
        }

        public void Dispose()
        {
            lock (_sync)
            {
                try { _cts?.Cancel(); } catch (Exception ex) { LoggingService.LogException("[SmoothProgressReporter] Cancel failed", ex); }
                try { _cts?.Dispose(); } catch (Exception ex) { LoggingService.LogException("[SmoothProgressReporter] Dispose failed", ex); }
                _cts = null;
                _workerTask = null;
            }
        }
    }
}
