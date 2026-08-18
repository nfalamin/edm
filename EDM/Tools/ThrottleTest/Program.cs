using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services;

class ThrottleTestRunner
{
    public static async Task<int> RunThrottleTestAsync(string[]? args = null)
    {
        int consumers = 8;
        int limitKbps = 200; // default
        int durationSeconds = 20;

        if (args.Length >= 1 && int.TryParse(args[0], out var v)) consumers = v;
        if (args.Length >= 2 && int.TryParse(args[1], out var v2)) limitKbps = v2;
        if (args.Length >= 3 && int.TryParse(args[2], out var v3)) durationSeconds = v3;

        Console.WriteLine($"ThrottleTest starting: consumers={consumers}, limit={limitKbps} KB/s, duration={durationSeconds}s");

        // Apply limit
        BandwidthThrottler.Instance.SetLimit(limitKbps);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        var tasks = new List<Task>();
        long totalConsumed = 0;

        for (int i = 0; i < consumers; i++)
        {
            int id = i;
            tasks.Add(Task.Run(async () =>
            {
                var rnd = new Random(Environment.TickCount ^ id);
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        int chunk = rnd.Next(4 * 1024, 64 * 1024); // 4KB - 64KB
                        var sw = Stopwatch.StartNew();
                        await BandwidthThrottler.Instance.ThrottleAsync(chunk, cts.Token).ConfigureAwait(false);
                        sw.Stop();
                        System.Threading.Interlocked.Add(ref totalConsumed, chunk);
                        // small processing delay to simulate work
                        await Task.Delay(rnd.Next(1, 10), cts.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { }
            }));
        }

        // Monitor task
        var monitor = Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000, CancellationToken.None).ConfigureAwait(false);
                    long consumed = Interlocked.Exchange(ref totalConsumed, 0);
                    double kbpsObserved = consumed / 1024.0;
                    Console.WriteLine($"[Monitor] Observed: {consumed} B ({kbpsObserved:F2} KB/s)");
                    LoggingService.Log($"[ThrottleTest] Observed: {consumed} B ({kbpsObserved:F2} KB/s)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Monitor error: {ex}");
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        // Wait a moment for final monitor update
        await Task.Delay(500).ConfigureAwait(false);

        Console.WriteLine("ThrottleTest complete.");
        return 0;
    }
}
