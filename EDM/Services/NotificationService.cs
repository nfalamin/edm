using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace EDM.Services
{
    public enum NotificationSeverity
    {
        Info,
        Success,
        Warning,
        Error
    }

    public enum NotificationCategory
    {
        System,
        DownloadCompleted,
        DownloadFailed,
        UpdateAvailable,
        Licensing,
        Security,
        Support
    }

    public class NotificationEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;
        public NotificationCategory Category { get; set; } = NotificationCategory.System;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsRead { get; set; }

        public string CategoryIcon => Category switch
        {
            NotificationCategory.DownloadCompleted => "✅",
            NotificationCategory.DownloadFailed => "❌",
            NotificationCategory.UpdateAvailable => "🔄",
            NotificationCategory.Licensing => "🔑",
            NotificationCategory.Security => "🛡️",
            NotificationCategory.Support => "❓",
            _ => "🔔"
        };
    }

    /// <summary>
    /// Production-grade Notification System for EDM.
    /// Manages in-app notifications, event dispatching, unread counts, categories, and settings persistence.
    /// </summary>
    public class NotificationService
    {
        private static readonly Lazy<NotificationService> _instance = new(() => new NotificationService());
        public static NotificationService Instance => _instance.Value;

        private readonly ConcurrentQueue<NotificationEvent> _notifications = new();
        private const int MaxNotificationHistory = 50;

        public bool NotificationsEnabled { get; set; } = true;

        public event EventHandler<NotificationEvent>? NotificationReceived;
        public event EventHandler? UnreadCountChanged;

        public NotificationService()
        {
            try
            {
                var settings = new SettingsService();
                string? val = settings.GetSetting("NotificationsEnabled");
                NotificationsEnabled = !string.Equals(val, "false", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                NotificationsEnabled = true;
            }
        }

        public void Notify(string title, string message, NotificationSeverity severity = NotificationSeverity.Info, NotificationCategory category = NotificationCategory.System)
        {
            if (!NotificationsEnabled) return;

            var evt = new NotificationEvent
            {
                Title = title,
                Message = message,
                Severity = severity,
                Category = category,
                Timestamp = DateTime.Now,
                IsRead = false
            };

            _notifications.Enqueue(evt);

            // Maintain bounded queue
            while (_notifications.Count > MaxNotificationHistory && _notifications.TryDequeue(out _)) { }

            LoggingService.Log($"[NotificationService] [{severity}|{category}] {title}: {message}");

            // Trigger events
            try
            {
                NotificationReceived?.Invoke(this, evt);
                UnreadCountChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[NotificationService] Event dispatch error", ex);
            }
        }

        public IReadOnlyList<NotificationEvent> GetRecentNotifications()
        {
            return _notifications.Reverse().ToList();
        }

        public int GetUnreadCount()
        {
            return _notifications.Count(n => !n.IsRead);
        }

        public void MarkAllAsRead()
        {
            foreach (var n in _notifications)
            {
                n.IsRead = true;
            }
            UnreadCountChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            while (_notifications.TryDequeue(out _)) { }
            UnreadCountChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
