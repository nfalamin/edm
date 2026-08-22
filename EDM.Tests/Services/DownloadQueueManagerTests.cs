using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class DownloadQueueManagerTests : TestBase
    {
        [Fact]
        public async Task MaxConcurrentLimit_IsRespected()
        {
            // Arrange - Queue manager with maxParallel = 2
            await using var queueManager = new DownloadQueueManager(maxParallel: 2);

            int currentConcurrent = 0;
            int maxObservedConcurrent = 0;
            var lockObj = new object();

            var completionSignal = new TaskCompletionSource<bool>();
            var tasksExecuted = 0;

            Func<CancellationToken, Task> work = async (ct) =>
            {
                lock (lockObj)
                {
                    currentConcurrent++;
                    if (currentConcurrent > maxObservedConcurrent)
                    {
                        maxObservedConcurrent = currentConcurrent;
                    }
                }

                await Task.Delay(100, ct);

                lock (lockObj)
                {
                    currentConcurrent--;
                    tasksExecuted++;
                    if (tasksExecuted == 4)
                    {
                        completionSignal.TrySetResult(true);
                    }
                }
            };

            // Act - Enqueue 4 tasks
            for (int i = 0; i < 4; i++)
            {
                await queueManager.EnqueueAsync(work);
            }

            await Task.WhenAny(completionSignal.Task, Task.Delay(3000));

            // Assert
            maxObservedConcurrent.Should().BeLessOrEqualTo(2);
            tasksExecuted.Should().Be(4);
        }

        [Fact]
        public async Task FailedTask_DoesNotBlockRestOfQueue()
        {
            // Arrange
            await using var queueManager = new DownloadQueueManager(maxParallel: 1);

            bool task1Failed = false;
            bool task2Succeeded = false;
            var tcs = new TaskCompletionSource<bool>();

            // Act - Task 1 throws, Task 2 succeeds
            await queueManager.EnqueueAsync("Task1", ct =>
            {
                task1Failed = true;
                throw new InvalidOperationException("Simulated queue task crash");
            });

            await queueManager.EnqueueAsync("Task2", ct =>
            {
                task2Succeeded = true;
                tcs.TrySetResult(true);
                return Task.CompletedTask;
            });

            await Task.WhenAny(tcs.Task, Task.Delay(2000));

            // Assert
            task1Failed.Should().BeTrue();
            task2Succeeded.Should().BeTrue();
        }

        [Fact]
        public async Task ReprioritizeItem_ChangesExecutionOrder()
        {
            // Arrange - Pause queue execution using a gate so we can reprioritize before processing starts
            var gate = new SemaphoreSlim(0, 1);
            await using var queueManager = new DownloadQueueManager(maxParallel: 1);

            var executedOrder = new List<string>();
            var tcs = new TaskCompletionSource<bool>();

            // Enqueue blocker task first
            await queueManager.EnqueueAsync("Blocker", async ct =>
            {
                await gate.WaitAsync(ct);
            });

            // Enqueue ItemA then ItemB
            await queueManager.EnqueueAsync("ItemA", ct =>
            {
                lock (executedOrder)
                {
                    executedOrder.Add("ItemA");
                    if (executedOrder.Count == 2) tcs.TrySetResult(true);
                }
                return Task.CompletedTask;
            });

            await queueManager.EnqueueAsync("ItemB", ct =>
            {
                lock (executedOrder)
                {
                    executedOrder.Add("ItemB");
                    if (executedOrder.Count == 2) tcs.TrySetResult(true);
                }
                return Task.CompletedTask;
            });

            // Act - Move ItemB to top of queue (position 0)
            bool moved = queueManager.Reprioritize("ItemB", 0);

            // Release blocker to start queue processing
            gate.Release();

            await Task.WhenAny(tcs.Task, Task.Delay(2000));

            // Assert
            moved.Should().BeTrue();
            executedOrder.Should().ContainInOrder("ItemB", "ItemA");
        }
    }
}
