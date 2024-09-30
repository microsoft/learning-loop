// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common
{
    public sealed class SystemTimeProvider : ITimeProvider
    {
        public static readonly SystemTimeProvider Instance = new SystemTimeProvider();

        private SystemTimeProvider()
        {
        }

        DateTime ITimeProvider.UtcNow => DateTime.UtcNow;

        public async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
        }
    }
}