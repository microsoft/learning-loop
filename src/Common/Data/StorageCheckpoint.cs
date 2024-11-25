// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace Microsoft.DecisionService.Common.Trainer.Data
{

    public class PartitionCheckpoint
    {
        public long? Offset { get; set; } = null;
        public DateTime? EnqueuedTimeUtc { get; set; } = null;
    }
    public class EventHubCheckpoint
    {
        public Dictionary<string, PartitionCheckpoint> PartitionCheckpoints { get; set; }

        public EventHubCheckpoint()
        {
            PartitionCheckpoints = new Dictionary<string, PartitionCheckpoint>();
        }

        public void Update(IList<SerializedBatch> batches, ILogger logger)
        {
            // Find the last offset and datetime for each partition
            // We only need to consider the batches as a unit because they are downloaded from event hub as a single message.
            foreach (var batch in batches)
            {
                if (!PartitionCheckpoints.TryGetValue(batch.PartitionId, out var partitionCheckpoint))
                {
                    partitionCheckpoint = new PartitionCheckpoint();
                    PartitionCheckpoints.Add(batch.PartitionId, partitionCheckpoint);
                }

                // Assert that we are not going backwards in time
                if (partitionCheckpoint.Offset > batch.Offset)
                {

                    logger.LogError($"Partition {batch.PartitionId} has gone backwards in time from {partitionCheckpoint.Offset} to {batch.Offset}");
                    continue;
                }

                // Assert that the offset does not go backwards
                if (partitionCheckpoint.EnqueuedTimeUtc > batch.EnqueuedTimeUtc)
                {
                    logger.LogError($"Partition {batch.PartitionId} has gone backwards in time from {partitionCheckpoint.EnqueuedTimeUtc} to {batch.EnqueuedTimeUtc}");
                    continue;
                }

                partitionCheckpoint.Offset = batch.Offset;
                partitionCheckpoint.EnqueuedTimeUtc = batch.EnqueuedTimeUtc;
            }
        }
    }

    public class StorageCheckpoint
    {
        [DataMember, Description("Storage position to which data was last written.")]
        public BlockPosition BlockPosition;

        [DataMember, Description("Event position of the last uploaded event.")]
        public EventHubCheckpoint EventPosition;

        [DataMember, Description("Storage blob property of the last written data.")]
        public BlobProperty BlobProperty;
    }
}
