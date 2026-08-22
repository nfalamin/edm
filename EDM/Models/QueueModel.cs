using System;
using System.Collections.Generic;

namespace EDM.Models
{
    public enum QueuePriority
    {
        Lowest = 0,
        Low = 1,
        Normal = 2,
        High = 3,
        Highest = 4
    }


    public enum PostQueueAction
    {
        None = 0,
        Shutdown = 1,
        Sleep = 2,
        Hibernate = 3,
        Restart = 4,
        OpenFile = 5,
        OpenFolder = 6,
        PlaySound = 7,
        ShowNotification = 8,
        ExecuteApp = 9
    }


    public class DownloadQueueModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Main Queue";
        public QueuePriority Priority { get; set; } = QueuePriority.Normal;
        public int MaxConcurrentFiles { get; set; } = 2;
        public int MaxConnectionsPerFile { get; set; } = 8;
        public bool EnableSchedule { get; set; } = false;
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? StopTime { get; set; }
        public int SpeedLimitKbps { get; set; } = 0; // 0 = unlimited
        public PostQueueAction PostAction { get; set; } = PostQueueAction.None;
        public List<string> ItemIds { get; set; } = new List<string>();
        public bool IsActive { get; set; } = true;
        public bool IsPaused { get; set; } = false;
        public bool IsRunning { get; set; } = true;
        public string Description { get; set; } = string.Empty;
        public DateTime CreationTimeUtc { get; set; } = DateTime.UtcNow;
    }
}
