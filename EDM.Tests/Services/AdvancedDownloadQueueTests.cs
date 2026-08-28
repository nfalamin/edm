using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using EDM.Models;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class AdvancedDownloadQueueTests : IDisposable
    {
        private readonly string _testStorageDir;

        public AdvancedDownloadQueueTests()
        {
            _testStorageDir = Path.Combine(Path.GetTempPath(), "EDM_QueueTests_" + Guid.NewGuid().ToString("N"));
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

        private DownloadQueueScheduler CreateScheduler(int maxActive = 4)
        {
            return new DownloadQueueScheduler(maxActive, _testStorageDir);
        }

        // 1. Add job to queue
        [Fact]
        public void Test1_AddJob_EnqueuesSuccessfully()
        {
            var scheduler = CreateScheduler();
            var item = new QueuedDownloadItem { DownloadId = "job_1", Url = "https://example.com/file1.zip" };

            scheduler.Enqueue(item);

            scheduler.QueuedCount.Should().Be(1);
            scheduler.GetItem("job_1").Should().NotBeNull();
            scheduler.GetItem("job_1")!.State.Should().Be(QueueItemState.Queued);
        }

        // 2. Remove job from queue
        [Fact]
        public void Test2_RemoveJob_RemovesSuccessfully()
        {
            var scheduler = CreateScheduler();
            var item = new QueuedDownloadItem { DownloadId = "job_remove", Url = "https://example.com/file.zip" };
            scheduler.Enqueue(item);

            scheduler.Remove("job_remove");

            scheduler.QueuedCount.Should().Be(0);
            scheduler.GetItem("job_remove").Should().BeNull();
        }

        // 3. Start queue
        [Fact]
        public void Test3_StartQueue_SetsQueueRunning()
        {
            var scheduler = CreateScheduler();
            scheduler.StopQueue("default");

            scheduler.StartQueue("default");

            var q = scheduler.GetQueue("default");
            q.Should().NotBeNull();
            q!.IsRunning.Should().BeTrue();
            q.IsPaused.Should().BeFalse();
        }

        // 4. Stop queue
        [Fact]
        public void Test4_StopQueue_PreventsJobSchedulingFromThisQueue()
        {
            var scheduler = CreateScheduler();
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "job_stopped", QueueId = "default" });

            scheduler.StopQueue("default");

            var next = scheduler.TryGetNextDownloadToStart();
            next.Should().BeNull("Scheduler must not start jobs from stopped queue");
        }

        // 5. Pause queue
        [Fact]
        public void Test5_PauseQueue_SetsIsPausedTrue()
        {
            var scheduler = CreateScheduler();
            scheduler.PauseQueue("default");

            var q = scheduler.GetQueue("default");
            q.Should().NotBeNull();
            q!.IsPaused.Should().BeTrue();

            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "job_paused", QueueId = "default" });
            var next = scheduler.TryGetNextDownloadToStart();
            next.Should().BeNull("Paused queue must not yield downloadable jobs");
        }

        // 6. Resume queue
        [Fact]
        public void Test6_ResumeQueue_AllowsJobsToScheduleAgain()
        {
            var scheduler = CreateScheduler();
            scheduler.PauseQueue("default");
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "job_resume", QueueId = "default" });

            scheduler.ResumeQueue("default");

            var next = scheduler.TryGetNextDownloadToStart();
            next.Should().NotBeNull();
            next!.DownloadId.Should().Be("job_resume");
        }

        // 7. Priority-based ordering
        [Fact]
        public void Test7_PriorityOrdering_SchedulesUrgentBeforeNormalAndLow()
        {
            var scheduler = CreateScheduler(maxActive: 1);
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "low", Priority = DownloadPriority.Low });
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "urgent", Priority = DownloadPriority.Urgent });
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "normal", Priority = DownloadPriority.Normal });

            var first = scheduler.TryGetNextDownloadToStart();
            first.Should().NotBeNull();
            first!.DownloadId.Should().Be("urgent");
        }

        // 8. Multiple queues isolation
        [Fact]
        public void Test8_MultipleQueues_IsolatesQueuesAndRetainsIdentity()
        {
            var scheduler = CreateScheduler();
            scheduler.AddOrUpdateQueue(new DownloadQueueModel { Id = "queue_a", Name = "Queue A" });
            scheduler.AddOrUpdateQueue(new DownloadQueueModel { Id = "queue_b", Name = "Queue B" });

            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "item_a", QueueId = "queue_a" });
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "item_b", QueueId = "queue_b" });

            var listA = scheduler.GetOrderedQueue("queue_a");
            var listB = scheduler.GetOrderedQueue("queue_b");

            listA.Should().ContainSingle(i => i.DownloadId == "item_a");
            listB.Should().ContainSingle(i => i.DownloadId == "item_b");
        }

        // 9. Global concurrency limit enforcement
        [Fact]
        public void Test9_GlobalConcurrencyLimit_RejectsStartsWhenMaxActiveReached()
        {
            var scheduler = CreateScheduler(maxActive: 2);
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "j1" });
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "j2" });
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "j3" });

            var s1 = scheduler.TryGetNextDownloadToStart();
            var s2 = scheduler.TryGetNextDownloadToStart();
            var s3 = scheduler.TryGetNextDownloadToStart();

            s1.Should().NotBeNull();
            s2.Should().NotBeNull();
            s3.Should().BeNull("Global limit of 2 is active");
        }

        // 10. Per-queue concurrency limit enforcement
        [Fact]
        public void Test10_PerQueueConcurrencyLimit_RespectsIndividualQueueCap()
        {
            var scheduler = CreateScheduler(maxActive: 10);
            scheduler.AddOrUpdateQueue(new DownloadQueueModel { Id = "restricted", Name = "Restricted", MaxConcurrentFiles = 1 });

            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "r1", QueueId = "restricted" });
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "r2", QueueId = "restricted" });

            var s1 = scheduler.TryGetNextDownloadToStart();
            var s2 = scheduler.TryGetNextDownloadToStart();

            s1.Should().NotBeNull();
            s1!.DownloadId.Should().Be("r1");
            s2.Should().BeNull("Per-queue cap of 1 reached for 'restricted' queue");
        }

        // 11. Queue fairness & starvation prevention (aging)
        [Fact]
        public void Test11_QueueFairnessAndAging_AgingIncreasesScoreForOlderItems()
        {
            var oldLowItem = new QueuedDownloadItem
            {
                DownloadId = "old_low",
                Priority = DownloadPriority.Low,
                EnqueuedTimeUtc = DateTime.UtcNow.AddMinutes(-30) // 30 min wait -> +60 aging score
            };

            var newNormalItem = new QueuedDownloadItem
            {
                DownloadId = "new_normal",
                Priority = DownloadPriority.Normal,
                EnqueuedTimeUtc = DateTime.UtcNow // 0 min wait
            };

            // Old low item (10 base + 60 aging = 70) beats new normal item (20 base)
            oldLowItem.CalculateQueueScore().Should().BeGreaterThan(newNormalItem.CalculateQueueScore());
        }

        // 12. Duplicate scheduling prevention (concurrency safety)
        [Fact]
        public void Test12_DuplicateSchedulingPrevention_ItemMarkedStartingCannotBeScheduledTwice()
        {
            var scheduler = CreateScheduler(maxActive: 4);
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "single_job" });

            var s1 = scheduler.TryGetNextDownloadToStart();
            var s2 = scheduler.TryGetNextDownloadToStart();

            s1.Should().NotBeNull();
            s1!.DownloadId.Should().Be("single_job");
            s2.Should().BeNull("Already starting job cannot be selected again");
        }

        // 13. Job completion slot release
        [Fact]
        public void Test13_JobCompletionSlotRelease_AllowsNextWaitingItemToRun()
        {
            var scheduler = CreateScheduler(maxActive: 1);
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "first" });
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "second" });

            var s1 = scheduler.TryGetNextDownloadToStart();
            s1.Should().NotBeNull();
            s1!.DownloadId.Should().Be("first");

            // Mark first completed -> frees slot
            scheduler.MarkCompleted("first");

            var s2 = scheduler.TryGetNextDownloadToStart();
            s2.Should().NotBeNull();
            s2!.DownloadId.Should().Be("second");
        }

        // 14. Job failure handling
        [Fact]
        public void Test14_JobFailureHandling_TransitionsStateCorrectly()
        {
            var scheduler = CreateScheduler();
            var item = new QueuedDownloadItem { DownloadId = "failing_job", MaxRetries = 1 };
            scheduler.Enqueue(item);

            // 1st failure -> Retrying
            scheduler.MarkFailed("failing_job", allowRetry: true);
            scheduler.GetItem("failing_job")!.State.Should().Be(QueueItemState.Retrying);

            // 2nd failure -> Failed (max retries reached)
            scheduler.MarkFailed("failing_job", allowRetry: true);
            scheduler.GetItem("failing_job")!.State.Should().Be(QueueItemState.Failed);
        }

        // 15. Retry integration
        [Fact]
        public void Test15_RetryIntegration_RetryingJobsCanBeRescheduled()
        {
            var scheduler = CreateScheduler(maxActive: 1);
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "retry_job", MaxRetries = 3 });

            var s1 = scheduler.TryGetNextDownloadToStart();
            scheduler.MarkFailed("retry_job", allowRetry: true);
            scheduler.GetItem("retry_job")!.NextRetryTimeUtc = DateTime.UtcNow.AddSeconds(-1);

            // Item is now in Retrying state -> eligible for scheduling again
            var s2 = scheduler.TryGetNextDownloadToStart();
            s2.Should().NotBeNull();
            s2!.DownloadId.Should().Be("retry_job");
        }

        // 16. Job cancellation
        [Fact]
        public void Test16_JobCancellation_MarksCancelledAndExcludesFromQueue()
        {
            var scheduler = CreateScheduler();
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "cancel_me" });

            scheduler.MarkCancelled("cancel_me");

            scheduler.GetItem("cancel_me")!.State.Should().Be(QueueItemState.Cancelled);
            var next = scheduler.TryGetNextDownloadToStart();
            next.Should().BeNull();
        }

        // 17. Queue state persistence
        [Fact]
        public void Test17_QueuePersistence_SavesAndLoadsAcrossSchedulerInstances()
        {
            var s1 = CreateScheduler();
            s1.AddOrUpdateQueue(new DownloadQueueModel { Id = "custom_q", Name = "Custom Queue", MaxConcurrentFiles = 5 });
            s1.Enqueue(new QueuedDownloadItem { DownloadId = "persisted_item", QueueId = "custom_q", Url = "https://example.com/p.zip" });
            s1.SaveState();

            // Create new scheduler instance from same directory
            var s2 = CreateScheduler();

            var loadedQ = s2.GetQueue("custom_q");
            loadedQ.Should().NotBeNull();
            loadedQ!.Name.Should().Be("Custom Queue");
            loadedQ.MaxConcurrentFiles.Should().Be(5);

            var loadedItem = s2.GetItem("persisted_item");
            loadedItem.Should().NotBeNull();
            loadedItem!.Url.Should().Be("https://example.com/p.zip");
        }

        // 18. Restart crash recovery
        [Fact]
        public void Test18_RestartCrashRecovery_HealsStaleActiveDownloads()
        {
            var scheduler = CreateScheduler();
            var staleItems = new List<DownloadItem>
            {
                new DownloadItem { Id = Guid.NewGuid(), FileName = "stale1.zip", Status = "Downloading" },
                new DownloadItem { Id = Guid.NewGuid(), FileName = "stale2.zip", Status = "Starting" },
                new DownloadItem { Id = Guid.NewGuid(), FileName = "ok.zip", Status = "Completed" }
            };

            int recovered = scheduler.RecoverStaleDownloads(staleItems);

            recovered.Should().Be(2);
            staleItems[0].Status.Should().Be("Paused");
            staleItems[1].Status.Should().Be("Paused");
            staleItems[2].Status.Should().Be("Completed");
        }

        // 19. Concurrent multi-thread queue access
        [Fact]
        public async Task Test19_ConcurrentAccess_IsThreadSafeUnderHighLoad()
        {
            var scheduler = CreateScheduler(maxActive: 8);

            var tasks = Enumerable.Range(0, 50).Select(i => Task.Run(() =>
            {
                string id = $"thread_item_{i}";
                scheduler.Enqueue(new QueuedDownloadItem { DownloadId = id });
                var next = scheduler.TryGetNextDownloadToStart();
                if (next != null)
                {
                    scheduler.MarkStarted(next.DownloadId);
                    scheduler.MarkCompleted(next.DownloadId);
                }
            })).ToArray();

            Func<Task> act = async () => await Task.WhenAll(tasks);
            await act.Should().NotThrowAsync();
        }

        // 20. UI state synchronization (GetQueuePosition)
        [Fact]
        public void Test20_UIStateSynchronization_Calculates1BasedPositionAccurately()
        {
            var scheduler = CreateScheduler();
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "first_pos", Priority = DownloadPriority.High });
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "second_pos", Priority = DownloadPriority.Normal });

            scheduler.GetQueuePosition("first_pos").Should().Be(1);
            scheduler.GetQueuePosition("second_pos").Should().Be(2);
            scheduler.GetQueuePosition("non_existent").Should().Be(0);
        }

        // 21. Reordering (MoveUp / MoveDown)
        [Fact]
        public void Test21_Reordering_MoveUpAndMoveDownAdjustsExecutionPriority()
        {
            var scheduler = CreateScheduler();
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "item_1", Priority = DownloadPriority.Normal });
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "item_2", Priority = DownloadPriority.Normal });

            // Move item_2 up
            bool moved = scheduler.MoveUp("item_2");
            moved.Should().BeTrue();

            var first = scheduler.TryGetNextDownloadToStart();
            first.Should().NotBeNull();
            first!.DownloadId.Should().Be("item_2");
        }

        // 22. Queue deletion (reassigns orphaned items)
        [Fact]
        public void Test22_QueueDeletion_ReassignsOrphanedItemsToDefaultQueue()
        {
            var scheduler = CreateScheduler();
            scheduler.AddOrUpdateQueue(new DownloadQueueModel { Id = "temporary_queue", Name = "Temp" });
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "orphan_item", QueueId = "temporary_queue" });

            bool deleted = scheduler.DeleteQueue("temporary_queue");
            deleted.Should().BeTrue();

            var item = scheduler.GetItem("orphan_item");
            item.Should().NotBeNull();
            item!.QueueId.Should().Be("default");
        }

        // 23. Queue renaming
        [Fact]
        public void Test23_QueueRenaming_RenamesQueueNameSuccessfully()
        {
            var scheduler = CreateScheduler();
            scheduler.AddOrUpdateQueue(new DownloadQueueModel { Id = "rename_q", Name = "Old Name" });

            bool renamed = scheduler.RenameQueue("rename_q", "New Shiny Name");
            renamed.Should().BeTrue();

            scheduler.GetQueue("rename_q")!.Name.Should().Be("New Shiny Name");
        }

        // 24. Dynamic max concurrency adjustment
        [Fact]
        public void Test24_DynamicMaxConcurrency_AdjustsAndCapsAtSafeLimits()
        {
            var scheduler = CreateScheduler();

            scheduler.MaxActiveDownloads = 12;
            scheduler.MaxActiveDownloads.Should().Be(12);

            scheduler.MaxActiveDownloads = 999; // Exceeds upper bound
            scheduler.MaxActiveDownloads.Should().Be(16, "Capped at 16 maximum safe parallel downloads");

            scheduler.MaxActiveDownloads = -5; // Below lower bound
            scheduler.MaxActiveDownloads.Should().Be(1, "Lower bound is at least 1");
        }

        // 25. No duplicate downloader invocation
        [Fact]
        public void Test25_NoDuplicateDownloaderInvocation_EnforcesStrictSingleStartGuarantee()
        {
            var scheduler = CreateScheduler(maxActive: 10);
            scheduler.Enqueue(new QueuedDownloadItem { DownloadId = "strict_single" });

            var parallelPicks = Enumerable.Range(0, 10).AsParallel().Select(_ => scheduler.TryGetNextDownloadToStart()).ToList();

            int nonNullCount = parallelPicks.Count(p => p != null && p.DownloadId == "strict_single");
            nonNullCount.Should().Be(1, "Exactly one thread can ever receive the item to start");
        }
    }
}
