using System;
using System.Text.Json.Serialization;

namespace EDM.Models
{
    [Flags]
    public enum ScheduleDays
    {
        None = 0,
        Sunday = 1 << 0,
        Monday = 1 << 1,
        Tuesday = 1 << 2,
        Wednesday = 1 << 3,
        Thursday = 1 << 4,
        Friday = 1 << 5,
        Saturday = 1 << 6,
        Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
        Weekends = Saturday | Sunday,
        All = Weekdays | Weekends
    }

    public class ScheduleRule
    {
        public string RuleId { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Default Schedule";
        public string QueueId { get; set; } = "default"; // "default", "all", or specific queue ID
        public bool IsEnabled { get; set; } = true;
        public TimeSpan StartTime { get; set; } = new TimeSpan(0, 0, 0); // 00:00:00
        public TimeSpan? StopTime { get; set; } = new TimeSpan(6, 0, 0);  // 06:00:00 (null = run indefinitely)
        public ScheduleDays Days { get; set; } = ScheduleDays.All;
        public bool AutoStartDownloads { get; set; } = true;
        public bool StopActiveDownloadsOnWindowClose { get; set; } = false;
        public int SpeedLimitKbps { get; set; } = 0; // 0 = unlimited
        public PostQueueAction PostAction { get; set; } = PostQueueAction.None;
        public DateTime CreatedTimeUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Evaluates whether the schedule rule is active at a given reference time,
        /// taking into account active days of the week, standard time windows, and midnight wrap-around ranges.
        /// </summary>
        public bool IsActiveAt(DateTime time)
        {
            if (!IsEnabled) return false;

            var dayOfWeek = time.DayOfWeek;
            var currentTimeOfDay = time.TimeOfDay;

            // If no stop time is defined, active after StartTime on configured days
            if (!StopTime.HasValue)
            {
                if (!IsDayActive(dayOfWeek)) return false;
                return currentTimeOfDay >= StartTime;
            }

            var start = StartTime;
            var stop = StopTime.Value;

            if (start <= stop)
            {
                // Normal same-day window (e.g. 02:00 -> 06:00)
                if (!IsDayActive(dayOfWeek)) return false;
                return currentTimeOfDay >= start && currentTimeOfDay < stop;
            }
            else
            {
                // Midnight wrap-around window (e.g. 23:00 -> 05:00)
                // Case A: Before midnight (current time >= 23:00) -> check today's active day
                if (currentTimeOfDay >= start)
                {
                    return IsDayActive(dayOfWeek);
                }

                // Case B: After midnight (current time < 05:00) -> check yesterday's active day
                if (currentTimeOfDay < stop)
                {
                    var previousDay = (DayOfWeek)(((int)dayOfWeek + 6) % 7);
                    return IsDayActive(previousDay);
                }

                return false;
            }
        }

        private bool IsDayActive(DayOfWeek day)
        {
            ScheduleDays targetFlag = day switch
            {
                DayOfWeek.Sunday => ScheduleDays.Sunday,
                DayOfWeek.Monday => ScheduleDays.Monday,
                DayOfWeek.Tuesday => ScheduleDays.Tuesday,
                DayOfWeek.Wednesday => ScheduleDays.Wednesday,
                DayOfWeek.Thursday => ScheduleDays.Thursday,
                DayOfWeek.Friday => ScheduleDays.Friday,
                DayOfWeek.Saturday => ScheduleDays.Saturday,
                _ => ScheduleDays.None
            };

            return (Days & targetFlag) != 0;
        }
    }
}
