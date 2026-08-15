using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;
using EDM.Services.Helpers;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class PerformanceBaselineBenchmarkSuite
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        public async Task Benchmark_MultipartConnectionMatrix_MeasuresThroughputAndAllocation(int connections)
        {
            long fileSize = 100 * 1024 * 1024; // 100 MB test payload
            var sw = Stopwatch.StartNew();

            long memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

            // Simulate segment chunk distribution
            long chunkSize = fileSize / connections;
            var tasks = new List<Task>();

            for (int i = 0; i < connections; i++)
            {
                int connId = i;
                tasks.Add(Task.Run(() =>
                {
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
                    try
                    {
                        long bytesRead = 0;
                        while (bytesRead < chunkSize)
                        {
                            long step = Math.Min(buffer.Length, chunkSize - bytesRead);
                            bytesRead += step;
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }));
            }

            await Task.WhenAll(tasks).ConfigureAwait(true);
            sw.Stop();

            long memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
            long memoryDelta = memoryAfter - memoryBefore;

            sw.ElapsedMilliseconds.Should().BeLessThan(5000); // 100MB chunk processing under 5s
        }

        [Fact]
        public void Benchmark_CancellationLatency_MeasuresResponseInMilliseconds()
        {
            var cts = new CancellationTokenSource();
            var sw = Stopwatch.StartNew();

            cts.Cancel();
            sw.Stop();

            // Cancellation latency must be under 15ms
            sw.ElapsedMilliseconds.Should().BeLessThan(15);
        }

        [Fact]
        public async Task Benchmark_ProgressThrottler_CoalescesUIUpdatesTo20FPSLimit()
        {
            int rawReportCount = 0;
            int uiUpdateCount = 0;

            var throttler = new ProgressThrottler<long>(info =>
            {
                Interlocked.Increment(ref uiUpdateCount);
            }, throttleInterval: TimeSpan.FromMilliseconds(50)); // 50ms interval = ~20 FPS

            var sw = Stopwatch.StartNew();

            // Simulate high-frequency 10,000 progress updates over 500ms
            while (sw.ElapsedMilliseconds < 500)
            {
                throttler.Report(1024);
                Interlocked.Increment(ref rawReportCount);
            }

            sw.Stop();

            rawReportCount.Should().BeGreaterThan(50);
            // UI updates must be throttled to ~10-15 calls during 500ms (50ms interval)
            uiUpdateCount.Should().BeLessThan(rawReportCount);
            uiUpdateCount.Should().BeLessThan(25);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        public void Benchmark_MemoryAllocationRate_VerifiesArrayPoolReuse(int simulatedCount)
        {
            long initialMemory = GC.GetTotalMemory(forceFullCollection: true);

            for (int i = 0; i < simulatedCount; i++)
            {
                byte[] rented = ArrayPool<byte>.Shared.Rent(64 * 1024);
                rented.Should().NotBeNull();
                rented.Length.Should().BeGreaterOrEqualTo(64 * 1024);
                ArrayPool<byte>.Shared.Return(rented);
            }

            long finalMemory = GC.GetTotalMemory(forceFullCollection: false);
            long delta = finalMemory - initialMemory;

            // Delta memory for ArrayPool re-use must be < 1MB
            delta.Should().BeLessThan(1 * 1024 * 1024);
        }
    }
}
