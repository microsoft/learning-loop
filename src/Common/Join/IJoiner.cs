// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.OnlineTrainer.Join
{
    /// <summary>
    /// Abstract the type of sources used by the joiner: event hubs, local files, etc..
    /// </summary>
    public interface IJoiner
    {
        Task StartAsync(CancellationToken cancellationToken);

        Task Completion { get; }
    }
}
