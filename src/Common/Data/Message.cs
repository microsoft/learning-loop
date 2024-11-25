// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;
using reinforcement_learning.messages.flatbuff.v2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.DecisionService.Common.Trainer.FlatBuffers;

namespace Microsoft.DecisionService.Common.Data
{
    public class Message
    {
        public string AppId { get; set; }
        public string EventId { get; set; }

        public PayloadType PayloadType { get; set; }
        
        public DateTime EnqueuedTimeUtc { get; set; }

        /// <summary>
        /// DataSegment includes the above metadata. It is expanded for simplicity.
        /// This corresponds to the contents of SerializedEvent payload in the schema.
        /// </summary>
        public ArraySegment<byte> DataSegment { get; set; }
        
        [JsonIgnore]
        [IgnoreDataMember]
        public bool IsJoinableEvent => EventId != Microsoft.DecisionService.Common.ApplicationConstants.DictionaryEventId;
    }

    public class MessageBatch
    {
        public long Offset { get; set; }

        public long SequenceNumber { get; set; }

        public string PartitionId { get; set; }

        public ulong OriginalEventCount { get; set; }

        public DateTime EnqueuedTimeUtc { get; set; }

        public List<Message> Messages { get; set; } = new List<Message>();
    }

    // This corresponds to a single Interaction event message batch, hence the event hub position information corresponds to this.
    public class JoinedBatch
    {
        public long Offset { get; set; }

        public long SequenceNumber { get; set; }

        public string PartitionId { get; set; }

        public DateTime EnqueuedTimeUtc { get; set; }
        
        public List<List<Message>> Messages { get; set; }
        
        private static ArraySegment<byte> SerializeEvent(List<Message> joinedEvent)
        {
            int sizeEstimate = joinedEvent.Sum(evt => evt.DataSegment.Count);
            BinaryLogBuilder builder = new BinaryLogBuilder(sizeEstimate);
            foreach (var evt in joinedEvent)
            {
                builder.AddEventPayload(evt.DataSegment, evt.EnqueuedTimeUtc);
            }
            return builder.FinishEventMessage();
        }
        
        // TODO work out more efficient way to do this
        private ArraySegment<byte> SerializeMessages()
        {
            var res = Messages.Select(eventList => SerializeEvent(eventList)).ToArray();
            return new ArraySegment<byte>(res.SelectMany(x => x.ToArray()).ToArray());
        }

        public SerializedBatch Serialize()
        {
            return new SerializedBatch()
            {
                EnqueuedTimeUtc = this.EnqueuedTimeUtc,
                Offset = this.Offset,
                SequenceNumber = this.SequenceNumber,
                PartitionId = this.PartitionId,
                SourceMessageEventCount = this.Messages.Count,
                payload = this.SerializeMessages()
            };
        }
    }
    
    public class SerializedBatch
    {
        public long Offset { get; set; }

        public long SequenceNumber { get; set; }

        public string PartitionId { get; set; }

        public DateTime EnqueuedTimeUtc { get; set; }
        
        public long SourceMessageEventCount { get; set; }
        
        public ArraySegment<byte> payload { get; set; }
    }
}
