using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services;
using EDM.Services.Helpers;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class ProgressThrottlerTests
    {
        [Fact]
        public async Task Report_HighFrequencyEvents_AreCoalescedAndRateLimited()
        {
            var receivedUpdates = new List<DownloadProgressInfo>();
            var lockObj = new object();

            using var throttler = new ProgressThrottler<DownloadProgressInfo>(
                targetAction: info =>
                {
                    lock (lockObj)
                    {
                        receivedUpdates.Add(info);
                    }
                },
                throttleInterval: TimeSpan.FromMilliseconds(100)
            );

            // Report 1,000 progress events rapidly (within ~20ms)
            for (int i = 1; i <= 1000; i++)
            {
                throttler.Report(new DownloadProgressInfo
                {
                    BytesReceived = i * 1000,
                    ProgressPercentage = i / 10.0,
                    Status = "Downloading"
                });
            }

            // Wait 250ms for throttle window to elapse and timer to fire
            await Task.Delay(250);

            lock (lockObj)
            {
                // Out of 1,000 rapid reports, UI should only receive ~1-3 coalesced updates
                receivedUpdates.Count.Should().BeLessThan(10, "1,000 rapid updates must be coalesced into very few UI renders");
                receivedUpdates.Count.Should().BeGreaterThan(0, "At least one update must be delivered");

                // The final delivered update must hold the latest state (BytesReceived = 1,000,000)
                var last = receivedUpdates.Last();
                last.BytesReceived.Should().Be(1000 * 1000, "The latest state must be preserved upon coalescing");
            }
        }

        [Fact]
        public async Task Report_TerminalState_DeliveredImmediatelyWithoutThrottleDelay()
        {
            var receivedUpdates = new List<DownloadProgressInfo>();
            var lockObj = new object();

            using var throttler = new ProgressThrottler<DownloadProgressInfo>(
                targetAction: info =>
                {
                    lock (lockObj)
                    {
                        receivedUpdates.Add(info);
                    }
                },
                throttleInterval: TimeSpan.FromMilliseconds(500), // long 500ms throttle interval
                isTerminalPredicate: info => info.IsCompleted || info.Status == "Completed" || info.Status == "Failed"
            );

            // 1. Initial non-terminal report
            throttler.Report(new DownloadProgressInfo { BytesReceived = 100, Status = "Downloading" });

            // 2. Terminal report immediately afterwards
            var terminalInfo = new DownloadProgressInfo { BytesReceived = 500, Status = "Completed", IsCompleted = true };
            throttler.Report(terminalInfo);

            // Verify immediately (without waiting for the 500ms timer)
            await Task.Delay(20);

            lock (lockObj)
            {
                receivedUpdates.Should().Contain(terminalInfo, "Terminal state must be delivered immediately bypassing throttle delay");
            }
        }

        [Fact]
        public async Task Dispose_PreventsSubsequentUiCallbacks()
        {
            int callbackCount = 0;

            var throttler = new ProgressThrottler<DownloadProgressInfo>(
                targetAction: info => Interlocked.Increment(ref callbackCount),
                throttleInterval: TimeSpan.FromMilliseconds(100)
            );

            throttler.Report(new DownloadProgressInfo { BytesReceived = 100 });

            // Dispose immediately
            throttler.Dispose();

            // Additional reports after disposal
            throttler.Report(new DownloadProgressInfo { BytesReceived = 200 });
            throttler.Report(new DownloadProgressInfo { BytesReceived = 300 });

            await Task.Delay(200);

            // No callbacks should fire after disposal
            callbackCount.Should().BeLessThanOrEqualTo(1, "Disposal must prevent subsequent callbacks");
        }

        [Fact]
        public async Task Report_CrossThreadConcurrentUpdates_IsThreadSafe()
        {
            var receivedUpdates = new List<DownloadProgressInfo>();
            var lockObj = new object();

            using var throttler = new ProgressThrottler<DownloadProgressInfo>(
                targetAction: info =>
                {
                    lock (lockObj)
                    {
                        receivedUpdates.Add(info);
                    }
                },
                throttleInterval: TimeSpan.FromMilliseconds(50),
                isTerminalPredicate: info => info.IsCompleted
            );

            int workerCount = 10;
            int reportsPerWorker = 200;
            var tasks = new List<Task>();

            for (int w = 0; w < workerCount; w++)
            {
                int workerId = w;
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < reportsPerWorker; i++)
                    {
                        throttler.Report(new DownloadProgressInfo
                        {
                            BytesReceived = (workerId + 1) * 10000 + i,
                            Status = "Downloading"
                        });
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Report terminal state at end
            throttler.Report(new DownloadProgressInfo { BytesReceived = 999999, IsCompleted = true, Status = "Completed" });

            await Task.Delay(150);

            lock (lockObj)
            {
                receivedUpdates.Should().NotBeEmpty();
                receivedUpdates.Last().IsCompleted.Should().BeTrue("Final terminal state must be delivered");
            }
        }
    }
}
