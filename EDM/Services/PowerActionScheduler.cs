using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public enum PowerAction
    {
        None = 0,
        ExitApplication = 1,
        Sleep = 2,
        Hibernate = 3,
        Shutdown = 4,
        Restart = 5
    }

    public class PowerActionCountdownState
    {
        public bool IsCountdownActive { get; set; }
        public PowerAction PendingAction { get; set; }
        public int RemainingSeconds { get; set; }
    }

    /// <summary>
    /// Production-grade Windows Power Action Scheduler.
    /// Implements active-download verification, safety grace-period countdown,
    /// user cancellation, and system power transitions (Sleep, Hibernate, Shutdown).
    /// </summary>
    public class PowerActionScheduler
    {
        private static readonly Lazy<PowerActionScheduler> _instance = new(() => new PowerActionScheduler());
        public static PowerActionScheduler Instance => _instance.Value;

        private CancellationTokenSource? _countdownCts;
        public event Action<int, PowerAction>? CountdownTick;
        public event Action? ActionCancelled;
        public event Action<PowerAction>? ActionExecuting;

        public bool IsCountdownActive => _countdownCts != null && !_countdownCts.IsCancellationRequested;

        [DllImport("Powrprof.dll", SetLastError = true)]
        private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

        public async Task<bool> TriggerPowerActionAsync(PowerAction action, int gracePeriodSeconds = 30, Func<bool>? hasActiveDownloads = null)
        {
            if (action == PowerAction.None) return false;

            // 1. Sanity Check: Ensure no active downloads are ongoing
            if (hasActiveDownloads != null && hasActiveDownloads())
            {
                LoggingService.Log("[PowerActionScheduler] Power action suppressed: Active downloads still running.");
                return false;
            }

            CancelCountdown();
            _countdownCts = new CancellationTokenSource();
            var ct = _countdownCts.Token;

            LoggingService.Log($"[PowerActionScheduler] Initiating {action} grace period ({gracePeriodSeconds}s)...");

            try
            {
                for (int sec = gracePeriodSeconds; sec > 0; sec--)
                {
                    if (ct.IsCancellationRequested)
                    {
                        ActionCancelled?.Invoke();
                        return false;
                    }

                    CountdownTick?.Invoke(sec, action);
                    await Task.Delay(1000, ct).ConfigureAwait(false);
                }

                if (ct.IsCancellationRequested) return false;

                // Re-verify no downloads started during countdown
                if (hasActiveDownloads != null && hasActiveDownloads())
                {
                    LoggingService.Log("[PowerActionScheduler] Aborting: A download started during countdown.");
                    ActionCancelled?.Invoke();
                    return false;
                }

                ActionExecuting?.Invoke(action);
                ExecuteSystemPowerAction(action);
                return true;
            }
            catch (OperationCanceledException)
            {
                ActionCancelled?.Invoke();
                return false;
            }
        }

        public void CancelCountdown()
        {
            if (_countdownCts != null && !_countdownCts.IsCancellationRequested)
            {
                _countdownCts.Cancel();
                _countdownCts.Dispose();
                _countdownCts = null;
                ActionCancelled?.Invoke();
                LoggingService.Log("[PowerActionScheduler] Power action cancelled by user.");
            }
        }

        public void ExecuteSystemPowerAction(PowerAction action)
        {
            LoggingService.Log($"[PowerActionScheduler] Executing system action: {action}");

            switch (action)
            {
                case PowerAction.ExitApplication:
                    if (System.Windows.Application.Current != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
                    }
                    else
                    {
                        Environment.Exit(0);
                    }
                    break;

                case PowerAction.Sleep:
                    SetSuspendState(false, false, false);
                    break;

                case PowerAction.Hibernate:
                    SetSuspendState(true, false, false);
                    break;

                case PowerAction.Shutdown:
                    // shutdown /s /t 0
                    Process.Start(new ProcessStartInfo("shutdown", "/s /t 0") { CreateNoWindow = true, UseShellExecute = false });
                    break;

                case PowerAction.Restart:
                    // shutdown /r /t 0
                    Process.Start(new ProcessStartInfo("shutdown", "/r /t 0") { CreateNoWindow = true, UseShellExecute = false });
                    break;
            }
        }
    }
}
