using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using EDM.Models;

namespace EDM.Services.Cloud
{
    public class RemotePushItem
    {
        public string PushId { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Url { get; set; } = string.Empty;
        public string? SuggestedFileName { get; set; }
        public string DeviceSource { get; set; } = "Mobile Companion";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool AutoStart { get; set; } = true;
    }

    /// <summary>
    /// Remote Push Receiver Subsystem.
    /// Intercepts downloads dispatched from mobile companions, remote webhooks, or secondary PCs.
    /// </summary>
    public class RemotePushService
    {
        private static readonly Lazy<RemotePushService> _instance = new(() => new RemotePushService());
        public static RemotePushService Instance => _instance.Value;

        private readonly ConcurrentQueue<RemotePushItem> _pendingPushes = new();

        public event Action<RemotePushItem>? RemoteUrlReceived;

        public void EnqueueRemotePush(string url, string? fileName = null, string deviceSource = "Mobile Companion", bool autoStart = true)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            var item = new RemotePushItem
            {
                Url = url.Trim(),
                SuggestedFileName = fileName,
                DeviceSource = deviceSource,
                Timestamp = DateTime.Now,
                AutoStart = autoStart
            };

            _pendingPushes.Enqueue(item);
            RemoteUrlReceived?.Invoke(item);
        }

        public IReadOnlyList<RemotePushItem> GetPendingPushes()
        {
            return _pendingPushes.ToList();
        }
    }
}
