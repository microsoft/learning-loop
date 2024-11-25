// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common
{
    public interface ITimeProvider
    {
        DateTime UtcNow { get; }

        Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
    }
}