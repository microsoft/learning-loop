// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.DecisionService.Common.Trainer.Join
{
    /// <summary>
    /// The position of the event in the source.
    /// When the event's consumer starts reading the source, the starting position can be specified using 'EnqueuedTimeUtc' or 'Offset'
    /// </summary>
    public class EventPosition
    {
        /// <summary>
        /// Here, the offset can be virtually anything, depending on the nature of the source.
        /// For example, if the source is a file, the offset can be a file_offset. If the source is an event hub with distributed partitions, 
        /// the offset can be for example the pairs (partitionId, partition_offset), serialized as a string.
        /// </summary>
        public string Offset { get; set; }

        /// <summary>
        /// The event time
        /// </summary>
        public DateTime? EnqueuedTimeUtc { get; set; }
    }
}