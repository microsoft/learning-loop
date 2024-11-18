// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Trainer.Data;

namespace Microsoft.DecisionService.Common.Trainer
{
    public interface IReceiverClient
    {
        public string PartitionId { get; }
        public IReceiver Connect(PartitionCheckpoint? position);
    }

}
