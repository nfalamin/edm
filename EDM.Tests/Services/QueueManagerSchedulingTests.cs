using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class QueueManagerSchedulingTests
    {
        [Fact]
        public async Task EnqueueAndReprioritize_ReordersQueueItemsCorrectly()
        {
            using var queueManager = new DownloadQueueManager(1);

            var executedOrder = new List<string>();

            var item1Started = new TaskCompletionSource();
            var allowItem1ToFinish = new TaskCompletionSource();
            var tcs1 = new TaskCompletionSource();
            var tcs2 = new TaskCompletionSource();
            var tcs3 = new TaskCompletionSource();

            await queueManager.EnqueueAsync("item-1", "q1", QueuePriority.Normal, async ct =>
            {
                executedOrder.Add("item-1");
                item1Started.SetResult();
                await allowItem1ToFinish.Task;
                tcs1.SetResult();
            });

            await queueManager.EnqueueAsync("item-2", "q1", QueuePriority.Normal, async ct =>
            {
                executedOrder.Add("item-2");
                tcs2.SetResult();
                await Task.CompletedTask;
            });

            await queueManager.EnqueueAsync("item-3", "q1", QueuePriority.Normal, async ct =>
            {
                executedOrder.Add("item-3");
                tcs3.SetResult();
                await Task.CompletedTask;
            });

            // Wait until item-1 has popped and is running
            await item1Started.Task;

            // Reprioritize item-3 to position 0 (top of remaining queued items ahead of item-2)
            queueManager.Reprioritize("item-3", 0).Should().BeTrue();

            // Allow item-1 to complete
            allowItem1ToFinish.SetResult();

            await Task.WhenAll(tcs1.Task, tcs2.Task, tcs3.Task);

            executedOrder.Should().HaveCount(3);
            executedOrder[0].Should().Be("item-1");
            executedOrder[1].Should().Be("item-3"); // item-3 was reprioritized ahead of item-2
            executedOrder[2].Should().Be("item-2");
        }


        [Fact]
        public void DownloadQueueModel_DefaultConfiguration_HasSensibleDefaults()
        {
            var model = new DownloadQueueModel();
            model.Id.Should().NotBeNullOrEmpty();
            model.Name.Should().Be("Main Queue");
            model.Priority.Should().Be(QueuePriority.Normal);
            model.MaxConcurrentFiles.Should().Be(2);
            model.SpeedLimitKbps.Should().Be(0);
        }
    }
}
