using System;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class ConcurrencyStressTests : TestBase
    {
        [Fact]
        public void DownloadStateController_ValidatesStateTransitionsCorrectly()
        {
            // Arrange
            var controller = new DownloadStateController();

            // Act & Assert
            controller.CurrentState.Should().Be(DownloadState.Created);

            bool step1 = controller.TryTransition(DownloadState.Created, DownloadState.Starting);
            step1.Should().BeTrue();
            controller.CurrentState.Should().Be(DownloadState.Starting);

            bool invalidStep = controller.TryTransition(DownloadState.Created, DownloadState.Running);
            invalidStep.Should().BeFalse("Cannot transition from Created if current state is Starting");

            bool step2 = controller.TryTransition(DownloadState.Starting, DownloadState.Running);
            step2.Should().BeTrue();
            controller.CurrentState.Should().Be(DownloadState.Running);

            controller.ForceState(DownloadState.Completed);
            controller.IsTerminal.Should().BeTrue();
        }

        [Fact]
        public async Task MultiThreadedQueueEnqueue_DoesNotCorruptQueueState()
        {
            // Arrange
            using var queueManager = new DownloadQueueManager(maxParallel: 4);
            int count = 0;
            // Use CountdownEvent so we deterministically wait until all 100 items have EXECUTED,
            // rather than relying on an arbitrary Task.Delay that can race under load.
            using var allDone = new CountdownEvent(100);

            // Act: Enqueue 100 concurrent tasks in parallel
            Task[] tasks = new Task[100];
            for (int i = 0; i < 100; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    await queueManager.EnqueueAsync(async (ct) =>
                    {
                        Interlocked.Increment(ref count);
                        await Task.Yield();
                        allDone.Signal();
                    });
                });
            }

            await Task.WhenAll(tasks); // wait for all 100 items to be enqueued

            // Deterministically wait for all work to actually execute (up to 10 seconds)
            bool completed = allDone.Wait(TimeSpan.FromSeconds(10));
            completed.Should().BeTrue("all 100 queued items should execute within 10 seconds");

            // Assert
            count.Should().Be(100);
        }
    }
}
