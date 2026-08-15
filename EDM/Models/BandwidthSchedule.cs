using System;
using System.Collections.Generic;
using System.Linq;

namespace EDM.Models
{
    /// <summary>
    /// Represents a time range (start and end hour) during which a bandwidth limit applies.
    /// Hours are in 24-hour format (0-23).
    /// </summary>
    public class TimeRange
    {
        public int StartHour { get; set; }  // 0-23
        public int EndHour { get; set; }    // 0-23

        public TimeRange() { }

        public TimeRange(int startHour, int endHour)
        {
            StartHour = Math.Max(0, Math.Min(23, startHour));
            EndHour = Math.Max(0, Math.Min(23, endHour));
        }

        /// <summary>
        /// Checks if the given hour falls within this time range.
        /// Handles wrap-around (e.g., 22:00 - 06:00 spans midnight).
        /// </summary>
        public bool IsInRange(int hour)
        {
            if (StartHour <= EndHour)
            {
                // Normal range (e.g., 9-17)
                return hour >= StartHour && hour < EndHour;
            }
            else
            {
                // Wrap-around range (e.g., 22-6 = 22,23,0,1,2,3,4,5)
                return hour >= StartHour || hour < EndHour;
            }
        }

        public override string ToString() => $"{StartHour:D2}:00 - {EndHour:D2}:00";
    }

    /// <summary>
    /// Represents a named bandwidth schedule profile with a time range, priority, and speed limit in KB/s.
    /// Supports multiple named profiles (e.g., "Work Hours", "Night", "Weekend").
    /// </summary>
    public class BandwidthSchedule
    {
        public string Name { get; set; } = "Default Profile";
        public int Priority { get; set; } = 0; // Higher values take precedence on overlap
        public TimeRange TimeRange { get; set; } = new TimeRange();
        public int SpeedLimitKbps { get; set; }  // 0 = unlimited

        public BandwidthSchedule() { }

        public BandwidthSchedule(string name, int startHour, int endHour, int speedLimitKbps, int priority = 0)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Profile" : name.Trim();
            TimeRange = new TimeRange(startHour, endHour);
            SpeedLimitKbps = Math.Max(0, speedLimitKbps);
            Priority = priority;
        }

        public BandwidthSchedule(int startHour, int endHour, int speedLimitKbps)
            : this("Profile", startHour, endHour, speedLimitKbps, 0)
        {
        }

        /// <summary>
        /// Evaluates active profiles for the current time. When time ranges overlap,
        /// returns the profile with the highest Priority (or lowest non-zero speed limit if priorities match).
        /// </summary>
        public static BandwidthSchedule? GetActiveProfile(IEnumerable<BandwidthSchedule>? schedules, DateTime time)
        {
            if (schedules == null) return null;
            int hour = time.Hour;

            var active = schedules.Where(s => s.TimeRange?.IsInRange(hour) ?? false).ToList();
            if (active.Count == 0) return null;

            // Sort by Priority descending, then by non-zero SpeedLimitKbps ascending (most restrictive first)
            return active
                .OrderByDescending(s => s.Priority)
                .ThenBy(s => s.SpeedLimitKbps > 0 ? s.SpeedLimitKbps : int.MaxValue)
                .FirstOrDefault();
        }

        public override string ToString() => $"[{Name}] {TimeRange} @ {(SpeedLimitKbps > 0 ? $"{SpeedLimitKbps} KB/s" : "Unlimited")} (Priority: {Priority})";
    }
}
