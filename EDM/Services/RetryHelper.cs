using System;
using System.Threading.Tasks;
using System.Threading;

namespace EDM.Services
{
    internal static class RetryHelper
    {
        public static async Task<TResult> RetryAsync<TResult>(Func<Task<TResult>> operation, int maxAttempts = 5, Func<int, TimeSpan>? backoff = null, CancellationToken cancellationToken = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            backoff ??= attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt));

            var attempt = 0;
            for (; ; )
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return await operation().ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception) when (++attempt < maxAttempts)
                {
                    var delay = backoff(attempt);
                    // add small jitter
                    var jitter = TimeSpan.FromMilliseconds(new Random().Next(0, 200));
                    await Task.Delay(delay + jitter, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public static Task RetryAsync(Func<Task> operation, int maxAttempts = 5, Func<int, TimeSpan>? backoff = null, CancellationToken cancellationToken = default)
        {
            return RetryAsync<object?>(async () => { await operation().ConfigureAwait(false); return null; }, maxAttempts, backoff, cancellationToken);
        }
    }
}
