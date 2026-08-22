using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EDM.Services;
using Xunit;

namespace EDM.Tests
{
    public class ThrottledProgressTests
    {
        [Fact]
        public async Task ThrottledProgress_CoalescesRapidUpdates_ToTwoReports()
        {
            var reports = new List<DownloadProgressInfo>();
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int expected = 2; // immediate + one delayed

            var inner = new Progress<DownloadProgressInfo>(info =>
            {
                lock (reports)
                {
                    reports.Add(info);
                    if (reports.Count >= expected) tcs.TrySetResult(true);
                }
            });

            var throttled = new DownloadService.ThrottledProgress(inner, TimeSpan.FromMilliseconds(100));
            var progress = throttled.AsProgress();

            // Rapidly fire multiple updates
            progress.Report(new DownloadProgressInfo { BytesReceived = 1 });
            progress.Report(new DownloadProgressInfo { BytesReceived = 2 });
            progress.Report(new DownloadProgressInfo { BytesReceived = 3 });
            progress.Report(new DownloadProgressInfo { BytesReceived = 4 });

            // Wait for up to 2s for the expected reports
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
            Assert.True(completed == tcs.Task, "Timed out waiting for throttled reports");

            lock (reports)
            {
                throttled.Dispose();
                Assert.Equal(expected, reports.Count);
                // First report should be the first immediate one (BytesReceived == 1)
                Assert.Equal(1, reports[0].BytesReceived);
                // Last report should carry the last value provided (4)
                Assert.Equal(4, reports[^1].BytesReceived);
            }
        }

        [Fact]
        public async Task ThrottledProgress_ImmediateSingleReport_SendsOnce()
        {
            var reports = new List<DownloadProgressInfo>();
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var inner = new Progress<DownloadProgressInfo>(info =>
            {
                lock (reports)
                {
                    reports.Add(info);
                    tcs.TrySetResult(true);
                }
            });

            var throttled = new DownloadService.ThrottledProgress(inner, TimeSpan.FromMilliseconds(200));
            var progress = throttled.AsProgress();

            progress.Report(new DownloadProgressInfo { BytesReceived = 42 });

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(1000));
            Assert.True(completed == tcs.Task, "Timed out waiting for single report");

            lock (reports) { Assert.Single(reports); Assert.Equal(42, reports[0].BytesReceived); }
        }
    }
}
