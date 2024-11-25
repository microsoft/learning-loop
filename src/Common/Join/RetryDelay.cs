// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Trainer.Join
{
    public class RetryDelay
    {
        private readonly TimeSpan initialDelayOnException = TimeSpan.FromMilliseconds(200);
        private TimeSpan nextDelayOnException;

        public RetryDelay()
        {
            this.nextDelayOnException = this.initialDelayOnException;
        }

        public void Reset()
        {
            this.nextDelayOnException = this.initialDelayOnException;
        }

        public async Task DelayAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(this.nextDelayOnException, cancellationToken);

            // wait 200ms, 400ms, 800ms, ..., 10s
            this.nextDelayOnException = TimeSpan.FromMilliseconds(
                    Math.Min(this.nextDelayOnException.TotalMilliseconds * 2, 10 * 1000));
        }
    }
}
