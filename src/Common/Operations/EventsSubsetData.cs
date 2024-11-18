// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Trainer.FlatBuffers;
using System;
using System.Collections.Generic;

namespace Microsoft.DecisionService.OnlineTrainer
{
    public class EventsSubsetData
    {
        private int count;
        private int batchSize, finalBatchSize;
        private DateTime? batchTime;
        private readonly List<ArraySegment<byte>> segments = new();
        private readonly bool needsHeader;

        // Binary logs need a header
        public EventsSubsetData(bool needsHeader, string eventType)
        {
            if (needsHeader)
            {
                segments.Add(ArraySegment<byte>.Empty);
            }
            this.needsHeader = needsHeader;
            EventType = eventType;
        }

        public bool HasNoEvents => count == 0;

        public int BatchSizeNoHeader => batchSize;

        public int BatchSizeWithHeader => finalBatchSize;

        public int EventCount => count;

        public string EventType { get; }

        public void Add(SerializedBatch batch)
        {
            count++;
            batchSize += batch.payload.Count;
            segments.Add(batch.payload);
            if (!batchTime.HasValue)
            {
                batchTime = batch.EnqueuedTimeUtc;
            }
        }

        public DateTime GetBatchTimeOrDefault(DateTime defaultValue)
        {
            return batchTime ?? defaultValue;
        }

        public ConcatenatedByteStreams CreateBlockStream(bool newBlob, CheckpointInfo checkpointInfo)
        {
            if (finalBatchSize > 0)
            {
                throw new InvalidOperationException("Cannot call CreateBlockStream more than once");
            }

            finalBatchSize = batchSize;

            if (this.needsHeader)
            {
                var bb = new BinaryLogHeaderBuilder();
                if (newBlob)
                {
                    bb.AddFileHeader(new Dictionary<string, string>()
                    {
                        { "joiner", ApplicationConstants.JoinerName },
                        { "file-type", EventType },
                    });
                }
                bb.AddCheckpointInfo(checkpointInfo);
                this.segments[0] = bb.Finish();

                finalBatchSize += this.segments[0].Count;
            }

            return new ConcatenatedByteStreams(this.segments);
        }
    }
}