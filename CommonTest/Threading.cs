// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common
{
    public class Threading
    {
        /// <summary>
        /// Spins until the specified action condition is satisfied.
        /// </summary>
        /// <param name="action">Action to invoke in the spin block.</param>
        /// <param name="timeOut">Total time for which the action will be retried. Value of TimeSpan.Zero will retry indefinitely.</param>
        /// <param name="retryDelayInMilliseconds">Delay between each retry attempt.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the spin block.</param>
        /// <returns></returns>
        public static async Task<bool> SpinUntilAsync(Func<Task<bool>> action, TimeSpan timeOut, int retryDelayInMilliseconds, CancellationToken cancellationToken = default(CancellationToken))
        {
            // This method exists because SpinWait.SpinUntil never times out when interacting with 
            // Thread.Sleep or Task.Delay.
            Stopwatch sw = Stopwatch.StartNew();
            bool done;
            bool shouldRetry;
            do
            {
                done = await action.Invoke();
                if (!done) await Task.Delay(retryDelayInMilliseconds, cancellationToken);
                shouldRetry = timeOut == TimeSpan.Zero ? true : sw.Elapsed < timeOut;
            } while (!cancellationToken.IsCancellationRequested && !done && shouldRetry);
            return done;
        }

        /// <summary>
        /// Spins until the specified action condition is satisfied.
        /// </summary>
        /// <param name="action">Action to invoke in the spin block.</param>
        /// <param name="timeOut">Total time for which the action will be retried. Value of 0 or less will retry indefinitely.</param>
        /// <param name="retryDelayInMilliseconds">Delay between each retry attempt.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the spin block.</param>
        /// <returns></returns>
        public static async Task<bool> SpinUntilAsync(Func<bool> action, TimeSpan timeOut, int retryDelayInMilliseconds, CancellationToken cancellationToken = default(CancellationToken))
        {
            Func<Task<bool>> func = () =>
            {
                return Task.FromResult(action.Invoke());
            };
            return await SpinUntilAsync(func, timeOut, retryDelayInMilliseconds, cancellationToken);
        }
    }
}
