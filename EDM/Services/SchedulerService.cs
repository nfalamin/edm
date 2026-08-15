using System;
using System.Threading;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    public class SchedulerService : IDisposable
    {
        private System.Threading.Timer? _timer;
        private readonly ISettingsService _settings;
        private bool _disposed = false;
        private DateTime _lastTriggered = DateTime.MinValue;
        private int _lastAppliedLimitKbps = -1;

        public event Action? OnScheduleTriggered;

        public SchedulerService(ISettingsService? settings = null)
        {
            _settings = settings ?? (App.ServiceProvider?.GetService(typeof(EDM.Services.Interfaces.ISettingsService)) as ISettingsService) ?? new SettingsService();
            // Timer ticks every 60 seconds to check both task scheduling and bandwidth schedules
            _timer = new System.Threading.Timer(Tick, null, TimeSpan.Zero, TimeSpan.FromSeconds(60));
        }

        private void Tick(object? state)
        {
            try
            {
                // Check and apply bandwidth schedule based on current time
                ApplyBandwidthSchedule();

                // Original scheduler task logic (if enabled)
                var enabled = _settings.GetSchedulerEnabled();
                var t = _settings.GetSchedulerTime();
                if (!enabled || t == null) return;

                var now = DateTime.Now;
                if (now.Date == _lastTriggered.Date && Math.Abs((now - _lastTriggered).TotalMinutes) < 1)
                {
                    // already triggered recently
                    return;
                }

                if (now.Hour == t.Value.Hours && now.Minute == t.Value.Minutes)
                {
                    _lastTriggered = now;
                    OnScheduleTriggered?.Invoke();
                }
            }
            catch (Exception ex) { LoggingService.Log($"[SchedulerService] Tick failed: {ex.Message}"); }
        }

        /// <summary>
        /// Checks current time against bandwidth schedules and applies active limit to SharedHttpClient.
        /// </summary>
        private void ApplyBandwidthSchedule()
        {
            try
            {
                var schedules = _settings.GetBandwidthSchedules();
                if (schedules == null || schedules.Count == 0)
                {
                    // No schedules defined
                    return;
                }

                int currentHour = DateTime.Now.Hour;
                int? activeLimitKbps = null;

                // Find the first schedule that applies to current hour
                foreach (var schedule in schedules)
                {
                    if (schedule.TimeRange?.IsInRange(currentHour) == true)
                    {
                        activeLimitKbps = schedule.SpeedLimitKbps;
                        break;
                    }
                }

                // Only update if the limit has changed
                if (activeLimitKbps.HasValue && activeLimitKbps.Value != _lastAppliedLimitKbps)
                {
                    _lastAppliedLimitKbps = activeLimitKbps.Value;
                    SharedHttpClient.SetBandwidthThrottle(activeLimitKbps.Value);
                    LoggingService.Log($"[SchedulerService] Applied bandwidth limit: {activeLimitKbps} KB/s");
                }
                else if (!activeLimitKbps.HasValue && _lastAppliedLimitKbps != 0)
                {
                    // No schedule applies, clear throttle
                    _lastAppliedLimitKbps = 0;
                    SharedHttpClient.SetBandwidthThrottle(0);
                    LoggingService.Log($"[SchedulerService] Cleared bandwidth limit (no active schedule)");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SchedulerService] ApplyBandwidthSchedule failed", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _timer?.Dispose(); } catch (Exception ex) { LoggingService.Log($"[SchedulerService] Timer dispose failed: {ex.Message}"); }
        }
    }
}
