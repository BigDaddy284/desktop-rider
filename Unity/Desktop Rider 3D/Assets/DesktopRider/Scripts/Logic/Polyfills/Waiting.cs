#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopRider
{
    internal static class Waiting
    {
        /// <summary>
        /// Waits for <paramref name="work"/>, but gives up with <see cref="OperationCanceledException"/> as soon as
        /// <paramref name="cancellationToken"/> is cancelled. .NET Standard 2.1 cannot cancel reading a line or a
        /// reply body directly, so the wait is abandoned instead; closing the reply then ends the reading itself.
        /// </summary>
        public static async Task<T> WithLimitAsync<T>(Task<T> work, CancellationToken cancellationToken)
        {
            if (!work.IsCompleted && cancellationToken.CanBeCanceled)
            {
                var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (cancellationToken.Register(state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancelled))
                {
                    if (await Task.WhenAny(work, cancelled.Task).ConfigureAwait(false) != work)
                    {
                        // The abandoned work may fail later, when its connection is closed. Nobody is waiting for it any more.
                        _ = work.ContinueWith(
                            abandoned => _ = abandoned.Exception,
                            CancellationToken.None,
                            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                            TaskScheduler.Default);
                        throw new OperationCanceledException(cancellationToken);
                    }
                }
            }

            return await work.ConfigureAwait(false);
        }
    }
}
