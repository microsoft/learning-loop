// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.DecisionService.OnlineTrainer.Join;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.DecisionService.Common.Trainer
{
    public interface IDataClientFactory
    {
        Task<string[]> GetPartitionsIdAsync(string eventTypeName);
        IReceiverClient CreateEventHubReceiver(string partitionId, string eventTypeName, ILogger logger);
    }
}
