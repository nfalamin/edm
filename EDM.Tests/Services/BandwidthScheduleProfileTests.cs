using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using EDM.Models;

namespace EDM.Tests.Services
{
    public class BandwidthScheduleProfileTests : TestBase
    {
        [Fact]
        public void GetActiveProfile_WhenNoSchedules_ReturnsNull()
        {
            // Act
            var active = BandwidthSchedule.GetActiveProfile(null, DateTime.Now);

            // Assert
            active.Should().BeNull();
        }

        [Fact]
        public void GetActiveProfile_SingleScheduleInRange_ReturnsSchedule()
        {
            // Arrange
            var workProfile = new BandwidthSchedule("Work Hours", 9, 17, 1024, priority: 1);
            var schedules = new List<BandwidthSchedule> { workProfile };
            var testTime = new DateTime(2026, 8, 10, 14, 0, 0); // 14:00 (2 PM)

            // Act
            var active = BandwidthSchedule.GetActiveProfile(schedules, testTime);

            // Assert
            active.Should().NotBeNull();
            active!.Name.Should().Be("Work Hours");
            active.SpeedLimitKbps.Should().Be(1024);
        }

        [Fact]
        public void GetActiveProfile_OverlappingProfiles_PrefersHigherPriority()
        {
            // Arrange
            var generalDayProfile = new BandwidthSchedule("Daytime", 8, 20, 2048, priority: 1);
            var highPriorityWorkProfile = new BandwidthSchedule("Work Rush", 9, 12, 512, priority: 5);

            var schedules = new List<BandwidthSchedule> { generalDayProfile, highPriorityWorkProfile };
            var testTime = new DateTime(2026, 8, 10, 10, 0, 0); // 10:00 AM

            // Act
            var active = BandwidthSchedule.GetActiveProfile(schedules, testTime);

            // Assert - highPriorityWorkProfile has priority 5 vs 1
            active.Should().NotBeNull();
            active!.Name.Should().Be("Work Rush");
            active.SpeedLimitKbps.Should().Be(512);
        }

        [Fact]
        public void GetActiveProfile_EqualPriorityOverlappingProfiles_PrefersMostRestrictiveLimit()
        {
            // Arrange
            var profileA = new BandwidthSchedule("Profile A", 9, 17, 2048, priority: 0);
            var profileB = new BandwidthSchedule("Profile B", 9, 17, 512, priority: 0);

            var schedules = new List<BandwidthSchedule> { profileA, profileB };
            var testTime = new DateTime(2026, 8, 10, 11, 0, 0); // 11:00 AM

            // Act
            var active = BandwidthSchedule.GetActiveProfile(schedules, testTime);

            // Assert - profileB has 512 KB/s vs 2048 KB/s
            active.Should().NotBeNull();
            active!.Name.Should().Be("Profile B");
            active.SpeedLimitKbps.Should().Be(512);
        }

        [Fact]
        public void TimeRange_WrapAroundMidnight_CalculatesCorrectly()
        {
            // Arrange - Night profile from 22:00 to 06:00
            var nightRange = new TimeRange(22, 6);

            // Act & Assert
            nightRange.IsInRange(23).Should().BeTrue();
            nightRange.IsInRange(2).Should().BeTrue();
            nightRange.IsInRange(5).Should().BeTrue();
            nightRange.IsInRange(12).Should().BeFalse();
        }
    }
}
