// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    public class FixedTimeProvider : ITimeProvider
    {
        public static readonly FixedTimeProvider Instance = new FixedTimeProvider();

        public DateTime UtcNow => new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            // Wait a small amount so that we don't cause a busy loop
            // during test
            await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
        }
    }
}
