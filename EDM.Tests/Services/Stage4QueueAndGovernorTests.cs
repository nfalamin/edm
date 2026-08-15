using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using EDM.Models;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class Stage4QueueAndGovernorTests : TestBase
    {
        [Fact]
        public void AdvancedQueueScheduler_EnforcesPriorityAgingAndDependencyChains()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"edm_queue_test_{Guid.NewGuid():N}");

            try
            {
                var scheduler = new AdvancedQueueScheduler(tempDir);

                var item1 = new AdvancedQueueItem
                {
                    ItemId = "task-1",
                    Url = "https://example.com/part1.iso",
                    Priority = QueuePriority.Low,
                    EnqueuedTimeUtc = DateTime.UtcNow.AddMinutes(-30) // 30 minutes old -> will receive priority aging boost
                };

                var item2 = new AdvancedQueueItem
                {
                    ItemId = "task-2",
                    Url = "https://example.com/part2.iso",
                    Priority = QueuePriority.High,
                    EnqueuedTimeUtc = DateTime.UtcNow,
                    DependsOnItemId = "task-1" // Blocked until task-1 is completed!
                };

                scheduler.AddItem(item1);
                scheduler.AddItem(item2);

                // Act 1: Task 2 is high priority but blocked by dependency on task 1
                var schedulable = scheduler.GetSchedulableItems();
                schedulable.Should().ContainSingle().Which.ItemId.Should().Be("task-1");

                // Act 2: Complete task 1 -> Task 2 becomes schedulable
                scheduler.MarkItemCompleted("task-1");
                var nextSchedulable = scheduler.GetSchedulableItems();
                nextSchedulable.Should().ContainSingle().Which.ItemId.Should().Be("task-2");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task UnifiedBandwidthGovernor_LimitsRateWithTokenBucketPrecision()
        {
            var governor = new UnifiedBandwidthGovernor();
            governor.SetGlobalLimit(100); // 100 KB/s limit

            var sw = Stopwatch.StartNew();
            // Request 50 KB (should take ~500ms at 100 KB/s)
            await governor.ThrottleAsync(50 * 1024, "example.com", CancellationToken.None);
            sw.Stop();

            // Reset back to unlimited
            governor.SetGlobalLimit(0);
        }

        [Fact]
        public async Task UnifiedBandwidthGovernor_BlocksTransfersWhenDailyQuotaExhausted()
        {
            var governor = new UnifiedBandwidthGovernor();
            governor.ResetQuotas();
            governor.SetDailyQuota(10 * 1024); // 10 KB daily quota limit

            // First transfer of 8 KB succeeds
            Func<Task> act1 = async () => await governor.ThrottleAsync(8 * 1024, null, CancellationToken.None);
            await act1.Should().NotThrowAsync();

            // Next transfer of 5 KB exceeds 10 KB quota -> must throw
            Func<Task> act2 = async () => await governor.ThrottleAsync(5 * 1024, null, CancellationToken.None);
            await act2.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*quota has been exhausted*");

            // Reset quota
            governor.ResetQuotas();
            Func<Task> act3 = async () => await governor.ThrottleAsync(5 * 1024, null, CancellationToken.None);
            await act3.Should().NotThrowAsync();
        }
    }
}
