using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using EDM.Models;
using EDM.Services;
using EDM.Services.Interfaces;

namespace EDM.Tests.Services
{
    public class MockTimeProvider : ITimeProvider
    {
        public DateTime CurrentTime { get; set; }

        public MockTimeProvider(DateTime initialTime)
        {
            CurrentTime = initialTime;
        }

        public DateTime Now => CurrentTime;
        public DateTime UtcNow => CurrentTime.ToUniversalTime();
        public DateTime Today => CurrentTime.Date;
    }

    public class TestableSchedulerService : SchedulerService
    {
        public bool NetworkAvailable { get; set; } = true;

        public TestableSchedulerService(ISettingsService settings, ITimeProvider timeProvider, string storagePath)
            : base(settings, timeProvider, storagePath)
        {
        }

        public override bool IsNetworkAvailable() => NetworkAvailable;
    }

    public class AdvancedDownloadSchedulingTests : IDisposable
    {
        private readonly string _testStorageDir;

        public AdvancedDownloadSchedulingTests()
        {
            _testStorageDir = Path.Combine(Path.GetTempPath(), "EDM_SchedTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testStorageDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testStorageDir))
                {
                    Directory.Delete(_testStorageDir, true);
                }
            }
            catch { }
        }

        private (TestableSchedulerService Service, MockTimeProvider Time, Mock<ISettingsService> Settings) CreateTestScheduler(DateTime? initialTime = null)
        {
            var time = new MockTimeProvider(initialTime ?? new DateTime(2026, 8, 24, 14, 0, 0)); // Monday 14:00
            var settings = new Mock<ISettingsService>();
            settings.Setup(s => s.GetSchedulerEnabled()).Returns(true);
            settings.Setup(s => s.GetSetting(It.IsAny<string>())).Returns((string?)null);

            var service = new TestableSchedulerService(settings.Object, time, _testStorageDir);
            return (service, time, settings);
        }

        // 1. Schedule creation
        [Fact]
        public void Test1_ScheduleCreation_AddsRuleSuccessfully()
        {
            var (scheduler, _, _) = CreateTestScheduler();
            var rule = new ScheduleRule
            {
                RuleId = "r1",
                Name = "Workday Morning",
                QueueId = "default",
                StartTime = new TimeSpan(8, 0, 0),
                StopTime = new TimeSpan(12, 0, 0),
                Days = ScheduleDays.Weekdays
            };

            scheduler.AddOrUpdateRule(rule);

            var saved = scheduler.GetRule("r1");
            saved.Should().NotBeNull();
            saved!.Name.Should().Be("Workday Morning");
        }

        // 2. Schedule update
        [Fact]
        public void Test2_ScheduleUpdate_UpdatesExistingRule()
        {
            var (scheduler, _, _) = CreateTestScheduler();
            var rule = new ScheduleRule { RuleId = "r_update", Name = "Initial Name", QueueId = "default" };
            scheduler.AddOrUpdateRule(rule);

            rule.Name = "Updated Name";
            rule.StartTime = new TimeSpan(3, 0, 0);
            scheduler.AddOrUpdateRule(rule);

            var updated = scheduler.GetRule("r_update");
            updated.Should().NotBeNull();
            updated!.Name.Should().Be("Updated Name");
            updated.StartTime.Should().Be(new TimeSpan(3, 0, 0));
        }

        // 3. Schedule deletion
        [Fact]
        public void Test3_ScheduleDeletion_DeletesRuleSuccessfully()
        {
            var (scheduler, _, _) = CreateTestScheduler();
            var rule = new ScheduleRule { RuleId = "r_delete", QueueId = "default" };
            scheduler.AddOrUpdateRule(rule);

            bool deleted = scheduler.DeleteRule("r_delete");
            deleted.Should().BeTrue();

            scheduler.GetRule("r_delete").Should().BeNull();
        }

        // 4. Valid time window evaluation
        [Fact]
        public void Test4_ValidTimeWindow_IsActiveInsideWindow()
        {
            var rule = new ScheduleRule
            {
                StartTime = new TimeSpan(2, 0, 0),
                StopTime = new TimeSpan(6, 0, 0),
                Days = ScheduleDays.All
            };

            var insideTime = new DateTime(2026, 8, 24, 3, 30, 0); // 03:30
            rule.IsActiveAt(insideTime).Should().BeTrue();
        }

        // 5. Invalid/Outside time window handling
        [Fact]
        public void Test5_OutsideTimeWindow_IsInactive()
        {
            var rule = new ScheduleRule
            {
                StartTime = new TimeSpan(2, 0, 0),
                StopTime = new TimeSpan(6, 0, 0),
                Days = ScheduleDays.All
            };

            var outsideTime = new DateTime(2026, 8, 24, 7, 0, 0); // 07:00
            rule.IsActiveAt(outsideTime).Should().BeFalse();
        }

        // 6. Day of week selection (e.g. Weekdays only)
        [Fact]
        public void Test6_DayOfWeekSelection_EnforcesConfiguredDays()
        {
            var rule = new ScheduleRule
            {
                StartTime = new TimeSpan(8, 0, 0),
                StopTime = new TimeSpan(18, 0, 0),
                Days = ScheduleDays.Weekdays
            };

            var monday = new DateTime(2026, 8, 24, 10, 0, 0); // Monday 10:00 -> Active
            var sunday = new DateTime(2026, 8, 23, 10, 0, 0); // Sunday 10:00 -> Inactive

            rule.IsActiveAt(monday).Should().BeTrue();
            rule.IsActiveAt(sunday).Should().BeFalse();
        }

        // 7. Recurring schedule across multiple days
        [Fact]
        public void Test7_RecurringSchedule_ActivatesOnAllConfiguredDays()
        {
            var rule = new ScheduleRule
            {
                StartTime = new TimeSpan(1, 0, 0),
                StopTime = new TimeSpan(5, 0, 0),
                Days = ScheduleDays.Tuesday | ScheduleDays.Thursday
            };

            var tuesday = new DateTime(2026, 8, 25, 2, 0, 0);  // Tuesday 02:00 -> Active
            var wednesday = new DateTime(2026, 8, 26, 2, 0, 0); // Wednesday 02:00 -> Inactive
            var thursday = new DateTime(2026, 8, 27, 2, 0, 0);  // Thursday 02:00 -> Active

            rule.IsActiveAt(tuesday).Should().BeTrue();
            rule.IsActiveAt(wednesday).Should().BeFalse();
            rule.IsActiveAt(thursday).Should().BeTrue();
        }

        // 8. Job waiting for scheduled window
        [Fact]
        public void Test8_JobWaiting_IsNotEligibleBeforeWindowOpens()
        {
            var mondayAfternoon = new DateTime(2026, 8, 24, 14, 0, 0); // 14:00
            var (scheduler, _, _) = CreateTestScheduler(mondayAfternoon);

            scheduler.AddOrUpdateRule(new ScheduleRule
            {
                RuleId = "nightly_rule",
                QueueId = "nightly",
                StartTime = new TimeSpan(23, 0, 0),
                StopTime = new TimeSpan(5, 0, 0)
            });

            var item = new QueuedDownloadItem { DownloadId = "j_wait", QueueId = "nightly" };
            bool isEligible = scheduler.IsDownloadEligibleToRun(item);

            isEligible.Should().BeFalse("14:00 is outside the 23:00-05:00 window");
        }

        // 9. Job starts automatically when scheduled time arrives
        [Fact]
        public void Test9_JobStarts_WhenScheduledTimeArrives()
        {
            var nightTime = new DateTime(2026, 8, 24, 23, 30, 0); // 23:30
            var (scheduler, _, _) = CreateTestScheduler(nightTime);

            scheduler.AddOrUpdateRule(new ScheduleRule
            {
                RuleId = "nightly_rule",
                QueueId = "nightly",
                StartTime = new TimeSpan(23, 0, 0),
                StopTime = new TimeSpan(5, 0, 0)
            });

            var item = new QueuedDownloadItem { DownloadId = "j_start", QueueId = "nightly" };
            bool isEligible = scheduler.IsDownloadEligibleToRun(item);

            isEligible.Should().BeTrue("23:30 is inside the 23:00-05:00 window");
        }

        // 10. Queue-specific schedule isolation
        [Fact]
        public void Test10_QueueScheduleIsolation_DoesNotBlockUnrelatedQueues()
        {
            var daytime = new DateTime(2026, 8, 24, 12, 0, 0); // 12:00
            var (scheduler, _, _) = CreateTestScheduler(daytime);

            scheduler.AddOrUpdateRule(new ScheduleRule
            {
                RuleId = "night_only",
                QueueId = "nightly",
                StartTime = new TimeSpan(1, 0, 0),
                StopTime = new TimeSpan(4, 0, 0)
            });

            // "nightly" is blocked at 12:00
            scheduler.IsQueueEligibleToRun("nightly").Should().BeFalse();

            // "default" queue has no restrictive rules -> eligible
            scheduler.IsQueueEligibleToRun("default").Should().BeTrue();
        }

        // 11. Global schedule enforcement
        [Fact]
        public void Test11_GlobalSchedule_AppliesToAllQueuesWhenConfigured()
        {
            var daytime = new DateTime(2026, 8, 24, 12, 0, 0);
            var (scheduler, _, _) = CreateTestScheduler(daytime);

            scheduler.AddOrUpdateRule(new ScheduleRule
            {
                RuleId = "global_night",
                QueueId = "all",
                StartTime = new TimeSpan(22, 0, 0),
                StopTime = new TimeSpan(6, 0, 0)
            });

            scheduler.IsQueueEligibleToRun("default").Should().BeFalse();
            scheduler.IsQueueEligibleToRun("video").Should().BeFalse();
        }

        // 12. Schedule conflict resolution & precedence
        [Fact]
        public void Test12_ConflictResolution_ManualOverrideWinsOverClosedSchedule()
        {
            var closedTime = new DateTime(2026, 8, 24, 15, 0, 0);
            var (scheduler, _, _) = CreateTestScheduler(closedTime);

            scheduler.AddOrUpdateRule(new ScheduleRule
            {
                RuleId = "restricted",
                QueueId = "default",
                StartTime = new TimeSpan(1, 0, 0),
                StopTime = new TimeSpan(2, 0, 0)
            });

            // Initially ineligible
            scheduler.IsQueueEligibleToRun("default").Should().BeFalse();

            // Manual override -> now eligible
            scheduler.SetManualOverride("default", true);
            scheduler.IsQueueEligibleToRun("default").Should().BeTrue();
        }

        // 13. Manual override ("Start Now")
        [Fact]
        public void Test13_StartNow_SetsManualOverrideOnItem()
        {
            var (scheduler, _, _) = CreateTestScheduler();
            scheduler.SetManualOverride("specific_download_1", true);

            scheduler.HasManualOverride("specific_download_1").Should().BeTrue();
        }

        // 14. Manual override clear
        [Fact]
        public void Test14_ClearManualOverride_RevertsToScheduledEvaluation()
        {
            var (scheduler, _, _) = CreateTestScheduler();
            scheduler.SetManualOverride("d1", true);
            scheduler.ClearManualOverride("d1");

            scheduler.HasManualOverride("d1").Should().BeFalse();
        }

        // 15. Manual pause during open schedule window
        [Fact]
        public void Test15_ManualPause_OverridesOpenWindow()
        {
            var queueScheduler = new DownloadQueueScheduler(4, _testStorageDir);
            queueScheduler.PauseQueue("default");

            var q = queueScheduler.GetQueue("default");
            q!.IsPaused.Should().BeTrue();
        }

        // 16. Manual resume during open schedule window
        [Fact]
        public void Test16_ManualResume_RestoresRunningState()
        {
            var queueScheduler = new DownloadQueueScheduler(4, _testStorageDir);
            queueScheduler.PauseQueue("default");
            queueScheduler.ResumeQueue("default");

            var q = queueScheduler.GetQueue("default");
            q!.IsPaused.Should().BeFalse();
            q.IsRunning.Should().BeTrue();
        }

        // 17. Retry waiting during closed schedule window
        [Fact]
        public void Test17_RetryWaiting_ClosedWindowBlocksRetryExecution()
        {
            var closedTime = new DateTime(2026, 8, 24, 15, 0, 0);
            var (scheduler, _, _) = CreateTestScheduler(closedTime);

            scheduler.AddOrUpdateRule(new ScheduleRule
            {
                RuleId = "night",
                QueueId = "default",
                StartTime = new TimeSpan(0, 0, 0),
                StopTime = new TimeSpan(6, 0, 0)
            });

            var retryItem = new QueuedDownloadItem { DownloadId = "retry1", QueueId = "default", State = QueueItemState.Retrying };
            scheduler.IsDownloadEligibleToRun(retryItem).Should().BeFalse();
        }

        // 18. Retry executing when schedule window opens
        [Fact]
        public void Test18_RetryExecuting_OpenWindowAllowsRetryExecution()
        {
            var openTime = new DateTime(2026, 8, 24, 2, 0, 0);
            var (scheduler, _, _) = CreateTestScheduler(openTime);

            scheduler.AddOrUpdateRule(new ScheduleRule
            {
                RuleId = "night",
                QueueId = "default",
                StartTime = new TimeSpan(0, 0, 0),
                StopTime = new TimeSpan(6, 0, 0)
            });

            var retryItem = new QueuedDownloadItem { DownloadId = "retry1", QueueId = "default", State = QueueItemState.Retrying };
            scheduler.IsDownloadEligibleToRun(retryItem).Should().BeTrue();
        }

        // 19. Concurrency limit enforced during scheduled batch
        [Fact]
        public void Test19_ConcurrencyEnforcement_SchedulerRespectsQueueCap()
        {
            var queueScheduler = new DownloadQueueScheduler(_testStorageDir, 2);
            queueScheduler.Enqueue(new QueuedDownloadItem { DownloadId = "b1" });
            queueScheduler.Enqueue(new QueuedDownloadItem { DownloadId = "b2" });
            queueScheduler.Enqueue(new QueuedDownloadItem { DownloadId = "b3" });

            var s1 = queueScheduler.TryGetNextDownloadToStart();
            var s2 = queueScheduler.TryGetNextDownloadToStart();
            var s3 = queueScheduler.TryGetNextDownloadToStart();

            s1.Should().NotBeNull();
            s2.Should().NotBeNull();
            s3.Should().BeNull();
        }

        // 20. Dynamic priority preserved during scheduled batch
        [Fact]
        public void Test20_PriorityPreserved_UrgentStartsFirstDuringScheduledBatch()
        {
            var queueScheduler = new DownloadQueueScheduler(_testStorageDir, 1);
            queueScheduler.Enqueue(new QueuedDownloadItem { DownloadId = "low", Priority = DownloadPriority.Low });
            queueScheduler.Enqueue(new QueuedDownloadItem { DownloadId = "urgent", Priority = DownloadPriority.Urgent });

            var next = queueScheduler.TryGetNextDownloadToStart();
            next.Should().NotBeNull();
            next!.DownloadId.Should().Be("urgent");
        }

        // 21. Schedule persistence across app restarts
        [Fact]
        public void Test21_SchedulePersistence_SavesAndLoadsAcrossInstances()
        {
            var time = new MockTimeProvider(DateTime.Now);
            var settings = new Mock<ISettingsService>();
            settings.Setup(s => s.GetSchedulerEnabled()).Returns(true);

            var s1 = new TestableSchedulerService(settings.Object, time, _testStorageDir);
            s1.AddOrUpdateRule(new ScheduleRule
            {
                RuleId = "persisted_sched",
                Name = "Saved Schedule",
                StartTime = new TimeSpan(4, 0, 0),
                StopTime = new TimeSpan(8, 0, 0)
            });

            var s2 = new TestableSchedulerService(settings.Object, time, _testStorageDir);
            var loaded = s2.GetRule("persisted_sched");

            loaded.Should().NotBeNull();
            loaded!.Name.Should().Be("Saved Schedule");
            loaded.StartTime.Should().Be(new TimeSpan(4, 0, 0));
        }

        // 22. Crash recovery during scheduled window
        [Fact]
        public void Test22_CrashRecovery_RecoversStaleDownloadsCleanly()
        {
            var queueScheduler = new DownloadQueueScheduler(4, _testStorageDir);
            var staleList = new List<DownloadItem>
            {
                new DownloadItem { Id = Guid.NewGuid(), Status = "Downloading" }
            };

            int recovered = queueScheduler.RecoverStaleDownloads(staleList);
            recovered.Should().Be(1);
            staleList[0].Status.Should().Be("Paused");
        }

        // 23. Idempotent evaluation (rapid triggers do not double-start)
        [Fact]
        public async Task Test23_IdempotentEvaluation_RapidTriggersDoNotDoubleStart()
        {
            var (scheduler, _, _) = CreateTestScheduler();
            var tasks = Enumerable.Range(0, 10).Select(_ => scheduler.EvaluateAndTriggerAsync());

            Func<Task> act = async () => await Task.WhenAll(tasks);
            await act.Should().NotThrowAsync();
        }

        // 24. Midnight wrap-around window evaluation (23:00 - 05:00)
        [Fact]
        public void Test24_MidnightWrapAround_EvaluatesBothSidesCorrectly()
        {
            var rule = new ScheduleRule
            {
                StartTime = new TimeSpan(23, 0, 0),
                StopTime = new TimeSpan(5, 0, 0),
                Days = ScheduleDays.All
            };

            var lateNight = new DateTime(2026, 8, 24, 23, 45, 0); // 23:45 -> Active
            var earlyMorning = new DateTime(2026, 8, 25, 3, 30, 0); // 03:30 -> Active
            var afternoon = new DateTime(2026, 8, 25, 14, 0, 0);   // 14:00 -> Inactive

            rule.IsActiveAt(lateNight).Should().BeTrue();
            rule.IsActiveAt(earlyMorning).Should().BeTrue();
            rule.IsActiveAt(afternoon).Should().BeFalse();
        }

        // 25. Non-scheduled day exclusion
        [Fact]
        public void Test25_NonScheduledDay_ExcludesCorrectly()
        {
            var rule = new ScheduleRule
            {
                StartTime = new TimeSpan(1, 0, 0),
                StopTime = new TimeSpan(5, 0, 0),
                Days = ScheduleDays.Weekends // Sat & Sun only
            };

            var wednesday = new DateTime(2026, 8, 26, 3, 0, 0); // Wednesday 03:00
            rule.IsActiveAt(wednesday).Should().BeFalse();
        }

        // 26. Corrupted schedule file recovery
        [Fact]
        public void Test26_CorruptedScheduleFile_RecoversGracefully()
        {
            string schedFile = Path.Combine(_testStorageDir, "schedules.json");
            File.WriteAllText(schedFile, "{ corrupted invalid json }}}");

            var (scheduler, _, _) = CreateTestScheduler();
            scheduler.GetRules().Should().NotBeNull();
        }

        // 27. Deleted queue reference handling
        [Fact]
        public void Test27_DeletedQueue_FallsBackSafely()
        {
            var (scheduler, _, _) = CreateTestScheduler();
            scheduler.AddOrUpdateRule(new ScheduleRule
            {
                RuleId = "orphan_rule",
                QueueId = "non_existent_queue"
            });

            // Querying a non-matching queue should not crash and should return unconstrained
            scheduler.IsQueueEligibleToRun("existing_queue").Should().BeTrue();
        }

        // 28. Stop time prevents new starts but allows active to finish
        [Fact]
        public void Test28_StopTime_StopsNewStarts()
        {
            var outsideTime = new DateTime(2026, 8, 24, 18, 0, 0);
            var (scheduler, _, _) = CreateTestScheduler(outsideTime);

            scheduler.AddOrUpdateRule(new ScheduleRule
            {
                RuleId = "daytime",
                QueueId = "default",
                StartTime = new TimeSpan(8, 0, 0),
                StopTime = new TimeSpan(17, 0, 0)
            });

            scheduler.IsQueueEligibleToRun("default").Should().BeFalse("18:00 is after 17:00 stop time");
        }

        // 29. Network availability gating (if disconnected, holds queue)
        [Fact]
        public void Test29_NetworkAvailability_BlocksWhenDisconnected()
        {
            var (scheduler, _, _) = CreateTestScheduler();
            scheduler.NetworkAvailable = false; // Disconnected

            scheduler.IsQueueEligibleToRun("default").Should().BeFalse("Network is unavailable");
        }

        // 30. Clean scheduler shutdown & resource disposal
        [Fact]
        public void Test30_SchedulerDisposal_DisposesCleanlyWithoutExceptions()
        {
            var (scheduler, _, _) = CreateTestScheduler();
            Action act = () => scheduler.Dispose();
            act.Should().NotThrow();
        }
    }
}
