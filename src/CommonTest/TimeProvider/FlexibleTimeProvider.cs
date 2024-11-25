// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.TimeProvider
{
    public class FlexibleTimeProvider : ITimeProvider
    {
        public DateTime UtcNow { get; private set; } = new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public void IncrementBy(TimeSpan duration)
        {
            this.UtcNow += duration;
        }
    }
}
