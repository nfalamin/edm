using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    /// <summary>
    /// Advanced Download Scheduling, Automation & Smart Queue Control Engine.
    /// Manages time-window rules, midnight wrap-arounds, days of the week, network gating,
    /// manual overrides, deterministic conflict precedence, and bandwidth automation.
    /// </summary>
    public class SchedulerService : ISchedulerService, IDisposable
    {
        private static readonly Lazy<SchedulerService> _lazy = new(() => new SchedulerService());
        public static SchedulerService Instance => _lazy.Value;

        private readonly ISettingsService _settings;
        private readonly ITimeProvider _timeProvider;
        private readonly string _persistencePath;
        private readonly List<ScheduleRule> _rules = new();
        private readonly ConcurrentDictionary<string, bool> _manualOverrides = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        private System.Threading.Timer? _timer;
        private bool _disposed = false;
        private bool _isEvaluating = false;
        private int _lastAppliedLimitKbps = -1;

        public event Action? OnScheduleTriggered;
        public event Action<string>? OnScheduleWindowOpened;
        public event Action<string>? OnScheduleWindowClosed;

        public bool IsSchedulerEnabled
        {
            get => _settings.GetSchedulerEnabled();
            set => _settings.SetSetting("EnableScheduler", value.ToString().ToLowerInvariant());
        }

        public SchedulerService(ISettingsService? settings = null, ITimeProvider? timeProvider = null, string? storagePath = null)
        {
            _settings = settings ?? (App.ServiceProvider?.GetService(typeof(ISettingsService)) as ISettingsService) ?? new SettingsService();
            _timeProvider = timeProvider ?? SystemTimeProvider.Instance;

            string baseDir = !string.IsNullOrWhiteSpace(storagePath)
                ? storagePath
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EDM");

            try
            {
                if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
                _persistencePath = Path.Combine(baseDir, "schedules.json");
            }
            catch
            {
                _persistencePath = Path.Combine(AppContext.BaseDirectory, "schedules.json");
            }

            InitializeDefaultRules();
            LoadRules();

            // Lightweight 15-second evaluation timer
            _timer = new System.Threading.Timer(Tick, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));
        }

        private void InitializeDefaultRules()
        {
            lock (_lock)
            {
                if (_rules.Count == 0)
                {
                    _rules.Add(new ScheduleRule
                    {
                        RuleId = "default_nightly",
                        Name = "Nightly Off-Peak Window",
                        QueueId = "nightly",
                        StartTime = new TimeSpan(0, 0, 0), // 12:00 AM
                        StopTime = new TimeSpan(6, 0, 0),  // 06:00 AM
                        Days = ScheduleDays.All,
                        IsEnabled = true,
                        AutoStartDownloads = true
                    });
                }
            }
        }

        // ==================== RULE MANAGEMENT ====================

        public List<ScheduleRule> GetRules()
        {
            lock (_lock)
            {
                return _rules.Select(r => new ScheduleRule
                {
                    RuleId = r.RuleId,
                    Name = r.Name,
                    QueueId = r.QueueId,
                    IsEnabled = r.IsEnabled,
                    StartTime = r.StartTime,
                    StopTime = r.StopTime,
                    Days = r.Days,
                    AutoStartDownloads = r.AutoStartDownloads,
                    StopActiveDownloadsOnWindowClose = r.StopActiveDownloadsOnWindowClose,
                    SpeedLimitKbps = r.SpeedLimitKbps,
                    PostAction = r.PostAction,
                    CreatedTimeUtc = r.CreatedTimeUtc
                }).ToList();
            }
        }

        public ScheduleRule? GetRule(string ruleId)
        {
            lock (_lock)
            {
                return _rules.FirstOrDefault(r => string.Equals(r.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));
            }
        }

        public void AddOrUpdateRule(ScheduleRule rule)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.RuleId)) return;

            lock (_lock)
            {
                int idx = _rules.FindIndex(r => string.Equals(r.RuleId, rule.RuleId, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    _rules[idx] = rule;
                }
                else
                {
                    _rules.Add(rule);
                }
                SaveRules();
            }

            // Immediately re-evaluate schedule on rule changes
            _ = EvaluateAndTriggerAsync();
        }

        public bool DeleteRule(string ruleId)
        {
            lock (_lock)
            {
                int removed = _rules.RemoveAll(r => string.Equals(r.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));
                if (removed > 0)
                {
                    SaveRules();
                    return true;
                }
                return false;
            }
        }

        // ==================== MANUAL OVERRIDES ====================

        public void SetManualOverride(string targetId, bool allowImmediateStart = true)
        {
            if (string.IsNullOrWhiteSpace(targetId)) return;
            _manualOverrides[targetId] = allowImmediateStart;
            _ = EvaluateAndTriggerAsync();
        }

        public void ClearManualOverride(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId)) return;
            _manualOverrides.TryRemove(targetId, out _);
        }

        public bool HasManualOverride(string targetId)
        {
            return _manualOverrides.TryGetValue(targetId, out bool val) && val;
        }

        // ==================== ELIGIBILITY & PRECEDENCE EVALUATION ====================

        /// <summary>
        /// Evaluates whether a queue is currently eligible to start new downloads according to:
        /// 1. Manual override
        /// 2. Network connectivity
        /// 3. Global scheduler setting
        /// 4. Queue schedule rules & time windows
        /// </summary>
        public bool IsQueueEligibleToRun(string queueId, DateTime? referenceTime = null)
        {
            // 1. Explicit manual override has highest precedence
            if (HasManualOverride(queueId)) return true;

            // 2. Network connectivity check
            if (!IsNetworkAvailable()) return false;

            // 3. If global scheduler is disabled, all normal active queues are eligible
            if (!IsSchedulerEnabled) return true;

            var time = referenceTime ?? _timeProvider.Now;

            lock (_lock)
            {
                // Find rules applying to this specific queue or all queues
                var applicableRules = _rules
                    .Where(r => r.IsEnabled && (string.Equals(r.QueueId, queueId, StringComparison.OrdinalIgnoreCase) || string.Equals(r.QueueId, "all", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                // If no schedule rules are bound to this queue, it is unconstrained (eligible)
                if (applicableRules.Count == 0) return true;

                // If any applicable rule is active right now, the queue is eligible
                return applicableRules.Any(r => r.IsActiveAt(time));
            }
        }

        /// <summary>
        /// Evaluates whether a specific queued download item is eligible to run.
        /// </summary>
        public bool IsDownloadEligibleToRun(QueuedDownloadItem item, DateTime? referenceTime = null)
        {
            if (item == null) return false;

            // Manual override on item level
            if (HasManualOverride(item.DownloadId)) return true;

            return IsQueueEligibleToRun(item.QueueId, referenceTime);
        }

        public virtual bool IsNetworkAvailable()
        {
            try
            {
                return NetworkInterface.GetIsNetworkAvailable();
            }
            catch
            {
                return true; // Assume available on platform error
            }
        }

        // ==================== EVALUATION LOOP ====================

        private void Tick(object? state)
        {
            if (_disposed) return;
            _ = EvaluateAndTriggerAsync();
        }

        public async Task<int> EvaluateAndTriggerAsync()
        {
            if (_isEvaluating) return 0;
            _isEvaluating = true;

            try
            {
                ApplyBandwidthSchedules();

                var now = _timeProvider.Now;
                int startedCount = 0;

                // Check scheduler trigger events
                OnScheduleTriggered?.Invoke();

                var queues = DownloadQueueScheduler.Instance.GetQueues();
                foreach (var q in queues)
                {
                    bool isEligible = IsQueueEligibleToRun(q.Id, now);
                    if (isEligible && q.IsActive && q.IsRunning && !q.IsPaused)
                    {
                        var next = DownloadQueueScheduler.Instance.TryGetNextDownloadToStart(q.Id);
                        if (next != null)
                        {
                            startedCount++;
                            // Fire async start through UI Dispatcher
                            await DispatchDownloadStartAsync(next);
                        }
                    }
                }

                return startedCount;
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SchedulerService] EvaluateAndTriggerAsync failed", ex);
                return 0;
            }
            finally
            {
                _isEvaluating = false;
            }
        }

        private async Task DispatchDownloadStartAsync(QueuedDownloadItem queuedItem)
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;

            var dispatcher = app.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
            await dispatcher.InvokeAsync(() =>
            {
                if (app.MainWindow?.DataContext is ViewModels.DownloadManagerViewModel vm)
                {
                    var item = vm.AllDownloads.FirstOrDefault(d => d.Id.ToString("N") == queuedItem.DownloadId);
                    if (item != null && (item.Status == "Queued" || item.Status == "Waiting" || item.Status == "Retrying"))
                    {
                        _ = vm.StartDownloadProcessAsync(item);
                    }
                }
            });
        }

        private void ApplyBandwidthSchedules()
        {
            try
            {
                var now = _timeProvider.Now;
                int? activeLimitKbps = null;

                lock (_lock)
                {
                    var activeRules = _rules.Where(r => r.IsEnabled && r.SpeedLimitKbps > 0 && r.IsActiveAt(now)).ToList();
                    if (activeRules.Count > 0)
                    {
                        // Take the most restrictive non-zero limit
                        activeLimitKbps = activeRules.Min(r => r.SpeedLimitKbps);
                    }
                }

                if (activeLimitKbps.HasValue && activeLimitKbps.Value != _lastAppliedLimitKbps)
                {
                    _lastAppliedLimitKbps = activeLimitKbps.Value;
                    SharedHttpClient.SetBandwidthThrottle(activeLimitKbps.Value);
                    BandwidthThrottler.Instance.SetLimit(activeLimitKbps.Value);
                    LoggingService.Log($"[SchedulerService] Applied scheduled bandwidth throttle: {activeLimitKbps.Value} KB/s");
                }
                else if (!activeLimitKbps.HasValue && _lastAppliedLimitKbps > 0)
                {
                    _lastAppliedLimitKbps = 0;
                    SharedHttpClient.SetBandwidthThrottle(0);
                    BandwidthThrottler.Instance.SetLimit(0);
                    LoggingService.Log("[SchedulerService] Cleared scheduled bandwidth throttle");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SchedulerService] ApplyBandwidthSchedules failed", ex);
            }
        }

        // ==================== PERSISTENCE ====================

        public void SaveRules()
        {
            try
            {
                string json = JsonSerializer.Serialize(_rules, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_persistencePath, json);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SchedulerService] Failed to save schedules", ex);
            }
        }

        public void LoadRules()
        {
            try
            {
                if (!File.Exists(_persistencePath)) return;

                string json = File.ReadAllText(_persistencePath);
                var loaded = JsonSerializer.Deserialize<List<ScheduleRule>>(json);
                if (loaded != null && loaded.Count > 0)
                {
                    lock (_lock)
                    {
                        _rules.Clear();
                        _rules.AddRange(loaded);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SchedulerService] Failed to load schedules", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }
}
