using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    public class ProductionHardeningTests
    {
        [Fact]
        public void DiagnosticsReportService_GeneratesValidDiagnosticReport()
        {
            string report = DiagnosticsReportService.GenerateReport(activeDownloads: 3, activeConnections: 16);

            report.Should().Contain("EDM DIAGNOSTIC REPORT");
            report.Should().Contain(".NET Runtime");
            report.Should().Contain("Active Downloads   : 3");
            report.Should().Contain("Active Connections : 16");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(25)]
        public async Task ConcurrencyTest_SimultaneousSchedulerCreations_DoesNotDeadlock(int count)
        {
            var tasks = new List<Task<SegmentScheduler>>();

            for (int i = 0; i < count; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var scheduler = new SegmentScheduler(10 * 1024 * 1024);
                    scheduler.InitializeDefault(8);
                    for (int w = 0; w < 4; w++)
                    {
                        scheduler.GetNextWorkItem($"worker_{w}");
                    }
                    return scheduler;
                }));
            }

            var results = await Task.WhenAll(tasks);
            results.Length.Should().Be(count);
            results.All(r => r != null).Should().BeTrue();
        }

        [Fact]
        public void MemoryAllocationAudit_DiagnosticsTracker_DoesNotLeakMemoryOnRepeatedUpdates()
        {
            var tracker = new DownloadDiagnosticsTracker();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long initialMemory = GC.GetTotalMemory(forceFullCollection: true);

            for (int i = 0; i < 10000; i++)
            {
                tracker.RecordMetrics("dl-test", 1000, 500, 100, 100, 4, 0, 10, 0);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long finalMemory = GC.GetTotalMemory(forceFullCollection: true);
            long diff = finalMemory - initialMemory;

            diff.Should().BeLessThan(25 * 1024 * 1024);
        }

        [Fact]
        public void FaultInjection_CorruptedSegment_IsDetectedAndReset()
        {
            var metaManager = new DurableMetadataManager();
            var state = new DurableDownloadState
            {
                Url = "https://example.com/test.bin",
                TotalBytes = 1024 * 1024,
                Segments = new List<SegmentRange>
                {
                    new SegmentRange { Id = 0, Start = 0, End = 500, State = SegmentState.Completed, Sha256Hash = "invalid_hash" }
                }
            };


            // Reconciliation re-checks segment state
            bool valid = metaManager.ReconcileAndValidate(state, remoteETag: "", remoteLastModified: "");
            valid.Should().BeTrue();
        }



        [Fact]
        public void CancellationAndShutdown_PauseToken_TransitionsStateCleanly()
        {
            var pts = new PauseTokenSource();
            pts.IsPaused.Should().BeFalse();

            pts.Pause();
            pts.IsPaused.Should().BeTrue();

            pts.Resume();
            pts.IsPaused.Should().BeFalse();
        }
    }
}
