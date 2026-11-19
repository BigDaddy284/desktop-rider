#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

// In the namespace that encloses every test, so the tests can use these on any version of .NET.
namespace DesktopRider
{
    /// <summary>Waiting with a limit, which newer versions of .NET build in but .NET Standard 2.1 does not.</summary>
    internal static class TaskWaiting
    {
        /// <summary>Waits for the work, giving up with <see cref="OperationCanceledException"/> if cancelled first.</summary>
        public static async Task WaitAsync(this Task work, CancellationToken cancellationToken)
        {
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(work, cancelled.Task).ConfigureAwait(false) != work)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            await work.ConfigureAwait(false);
        }

        /// <summary>Asks for cancellation. Whatever was listening has been told by the time this returns.</summary>
        public static Task CancelAsync(this CancellationTokenSource source)
        {
            source.Cancel();
            return Task.CompletedTask;
        }

        /// <summary>Waits for the work, giving up with <see cref="TimeoutException"/> if it takes longer than allowed.</summary>
        public static async Task WaitAsync(this Task work, TimeSpan limit)
        {
            using var stop = new CancellationTokenSource();
            if (await Task.WhenAny(work, Task.Delay(limit, stop.Token)).ConfigureAwait(false) != work)
            {
                throw new TimeoutException();
            }

            stop.Cancel();
            await work.ConfigureAwait(false);
        }

        /// <summary>Waits for the work and returns its result, giving up with <see cref="TimeoutException"/> if it takes longer than allowed.</summary>
        public static async Task<T> WaitAsync<T>(this Task<T> work, TimeSpan limit)
        {
            await ((Task)work).WaitAsync(limit).ConfigureAwait(false);
            return await work.ConfigureAwait(false);
        }
    }
}
