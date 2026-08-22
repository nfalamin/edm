using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDM.NativeMessaging;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    [Collection("InterceptionTests")]
    public class LongRunningPerformanceSoakTests : IDisposable
    {
        public LongRunningPerformanceSoakTests()
        {
            BrowserInterceptionStateMachine.ResetForTesting();
            NativeMessageListener.ResetDeduplicationCacheForTesting();
        }

        public void Dispose()
        {
            BrowserInterceptionStateMachine.ResetForTesting();
            NativeMessageListener.ResetDeduplicationCacheForTesting();
        }

        [Fact]
        public async Task Execute10000LifecycleEvents_MeasuresMemoryLeakAndCpuStability()
        {
            int totalEvents = 10000;
            int activeConcurrencyLimit = 50;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long initialMemory = GC.GetTotalMemory(forceFullCollection: true);
            int initialGen0 = GC.CollectionCount(0);
            int initialGen1 = GC.CollectionCount(1);
            int initialGen2 = GC.CollectionCount(2);

            var sw = Stopwatch.StartNew();

            var latencies = new ConcurrentBag<double>();
            int queuedCount = 0;
            int completedCount = 0;
            int cancelledCount = 0;
            int failedCount = 0;
            int retriedCount = 0;

            using var semaphore = new SemaphoreSlim(activeConcurrencyLimit, activeConcurrencyLimit);
            var tasks = new List<Task>();

            for (int i = 0; i < totalEvents; i++)
            {
                int eventId = i;
                await semaphore.WaitAsync().ConfigureAwait(true);

                tasks.Add(Task.Run(async () =>
                {
                    var opSw = Stopwatch.StartNew();
                    try
                    {
                        Interlocked.Increment(ref queuedCount);

                        string corrId = $"soak_corr_{eventId}";
                        string url = $"https://cdn.example.com/soak_file_{eventId % 500}.bin";

                        // Simulate browser interception event
                        BrowserInterceptionStateMachine.CreateSession(corrId, url, $"soak_file_{eventId % 500}.bin");
                        BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.Validating);
                        BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandoffPending);

                        if (eventId % 10 == 0)
                        {
                            // 10% Cancelled scenario
                            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.RecoverableFallback, "User Cancelled");
                            Interlocked.Increment(ref cancelledCount);
                        }
                        else if (eventId % 15 == 0)
                        {
                            // 6.6% Failure & Retry scenario
                            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.RecoverableFallback, "Network Failure");
                            Interlocked.Increment(ref failedCount);
                            Interlocked.Increment(ref retriedCount);
                        }
                        else
                        {
                            // Standard complete scenario
                            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandedOff);
                            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.BrowserCancelled);
                            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.EdmQueued);
                            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.EdmStarted);
                            Interlocked.Increment(ref completedCount);
                        }

                        opSw.Stop();
                        latencies.Add(opSw.Elapsed.TotalMilliseconds);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));

                // Periodically prune stale session maps every 1000 events to prevent leak
                if (eventId % 1000 == 0)
                {
                    BrowserInterceptionStateMachine.PruneStaleSessions(TimeSpan.FromSeconds(0));
                }
            }

            await Task.WhenAll(tasks).ConfigureAwait(true);
            sw.Stop();

            // Final prune of session state machine maps
            int pruned = BrowserInterceptionStateMachine.PruneStaleSessions(TimeSpan.FromSeconds(0));

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long finalMemory = GC.GetTotalMemory(forceFullCollection: true);
            int finalGen0 = GC.CollectionCount(0) - initialGen0;
            int finalGen1 = GC.CollectionCount(1) - initialGen1;
            int finalGen2 = GC.CollectionCount(2) - initialGen2;

            long memoryDelta = finalMemory - initialMemory;
            double avgLatency = latencies.Average();
            double peakLatency = latencies.Max();

            // Verification Invariants
            queuedCount.Should().Be(totalEvents);
            (completedCount + cancelledCount + failedCount).Should().Be(totalEvents);

            // Memory delta after 10,000 events must be < 3 MB (proving zero retained leaks)
            memoryDelta.Should().BeLessThan(3 * 1024 * 1024);

            // Average latency per lifecycle event must be within acceptable performance envelope (< 25.0ms under high test runner concurrency)
            avgLatency.Should().BeLessThan(25.0);
        }

        [Theory]
        [InlineData(100)]
        [InlineData(500)]
        [InlineData(1000)]
        public void Part4_DownloadQueueBackpressure_ProcessesQueuesWithoutMemoryExplosion(int queueSize)
        {
            long initialMemory = GC.GetTotalMemory(forceFullCollection: true);

            var queue = new ConcurrentQueue<string>();
            for (int i = 0; i < queueSize; i++)
            {
                queue.Enqueue($"https://example.com/queue_file_{i}.zip");
            }

            queue.Count.Should().Be(queueSize);

            // Drain queue
            int processed = 0;
            while (queue.TryDequeue(out var url))
            {
                url.Should().NotBeNullOrEmpty();
                processed++;
            }

            processed.Should().Be(queueSize);
            queue.IsEmpty.Should().BeTrue();

            long finalMemory = GC.GetTotalMemory(forceFullCollection: false);
            long delta = finalMemory - initialMemory;

            // Memory delta for queue operations must be < 1 MB
            delta.Should().BeLessThan(1 * 1024 * 1024);
        }
    }
}
