// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Google.FlatBuffers;
using reinforcement_learning.messages.flatbuff.v2;
using HeaderCheckpointInfo = reinforcement_learning.messages.flatbuff.v2.CheckpointInfo;
using System;
using System.Collections.Generic;

namespace Microsoft.DecisionService.Common.Trainer.FlatBuffers
{
    public class BinaryLogBuilder
    {
        private readonly FlatBufferBuilder builder;
        private readonly List<Offset<JoinedEvent>> eventOffsets = new List<Offset<JoinedEvent>>();
        private Dictionary<string, string> headerProperties;
        private CheckpointInfo checkpoint;

        public const int EVENT_MESSAGE_ID = (int)-1;
        public const int FILE_HEADER_MESSAGE_ID = 0x55555555;
        public const int CHECKPOINT_MESSAGE_ID = 0x11111111;

        //This was found using the test suite
        public const int FILE_HEADER_SIZE_GUESTIMATE = 160;
        public const int CHECKPOINT_SIZE_GUESTIMATE = 40;

        //This is lower bound, use a larger value
        //Update Microsoft.DecisionService.Common.ApplicationConstants.BinaryLogMaxBatchHeaderSize when this change
        public const int HEADER_SIZE_GUESTIMATE = 8 + FILE_HEADER_SIZE_GUESTIMATE + CHECKPOINT_SIZE_GUESTIMATE;


        public BinaryLogBuilder(int sizeEstimate)
        {
            builder = new FlatBufferBuilder(sizeEstimate);

            /*
             * All payloads must be 8 bytes aligned so we do the following trick:
             * - flatbuffers naturally produces content aligned to 4 bytes.
             * - add 4 bytes at the end of the final payload
             * - remove that slack from the final output if needed to meet alignment needs
             */
            builder.PutInt(0);
        }

        private Offset<JoinedEvent> SerializeEvent(ArraySegment<byte> payload, DateTime timestamp)
        {
            var payloadOffset = SchemaV2Extensions.JoinedEventCreateEventVector(builder, payload.ToArray());
            JoinedEvent.StartJoinedEvent(builder);
            JoinedEvent.AddEvent(builder, payloadOffset);
            JoinedEvent.AddTimestamp(builder, timestamp.SerializeV2TimeStamp(builder));

            return JoinedEvent.EndJoinedEvent(builder);
        }

        public void AddEventPayload(ArraySegment<byte> payload, DateTime enqueuedTimeUtc)
        {
            eventOffsets.Add(SerializeEvent(payload, enqueuedTimeUtc));
        }

        private ArraySegment<byte> FinishMessage(int messageId)
        {
            //add message header
            builder.FinishSizePrefixed(1); //placeholder value, just using the Prep/AddInt machinery
            var dataBuffer = builder.DataBuffer;

            int messageSize = dataBuffer.Length - dataBuffer.Position  - 8;
            messageSize -= messageSize % 8;

            dataBuffer.PutInt(dataBuffer.Position, messageId);
            dataBuffer.PutInt(dataBuffer.Position + 4, messageSize);

            int size = dataBuffer.Length - dataBuffer.Position;
            size -= size % 8; // Drop extra padding if needed
            return dataBuffer.ToArraySegment(dataBuffer.Position, size);
        }

        public void AddCheckpointInfo(CheckpointInfo checkpointInfo)
        {
            this.checkpoint = checkpointInfo;
        }

        public void AddFileHeader(Dictionary<string, string> headerProperties)
        {
            this.headerProperties = headerProperties;
        }

        public ArraySegment<byte> FinishCheckpointInfo()
        {
            HeaderCheckpointInfo.StartCheckpointInfo(builder);
            HeaderCheckpointInfo.AddRewardFunctionType(builder, checkpoint.FbRewardType);
            HeaderCheckpointInfo.AddDefaultReward(builder, checkpoint.DefaultReward);
            builder.Finish(HeaderCheckpointInfo.EndCheckpointInfo(builder).Value);

            return FinishMessage(CHECKPOINT_MESSAGE_ID);
        }

        public ArraySegment<byte> FinishFileHeader()
        {
            List<Offset<KeyValue>> kv_offsets = new List<Offset<KeyValue>>();
            foreach (var kv in headerProperties)
            {
                var k = builder.CreateString(kv.Key);
                var v = builder.CreateString(kv.Value);

                KeyValue.StartKeyValue(builder);
                KeyValue.AddKey(builder, k);
                KeyValue.AddValue(builder, v);
                kv_offsets.Add(KeyValue.EndKeyValue(builder));
            }

            var props_off = FileHeader.CreatePropertiesVector(builder, kv_offsets.ToArray());
            FileHeader.StartFileHeader(builder);
            FileHeader.AddJoinTime(builder, DateTime.UtcNow.SerializeV2TimeStamp(builder));
            FileHeader.AddProperties(builder, props_off);
            builder.Finish(FileHeader.EndFileHeader(builder).Value);

            return FinishMessage(FILE_HEADER_MESSAGE_ID);
        }

        public ArraySegment<byte> FinishEventMessage()
        {
            var serializedEventOffsets = SchemaV2Extensions.JoinedPayloadCreateEventsVector(builder, eventOffsets);
            JoinedPayload.StartJoinedPayload(builder);
            JoinedPayload.AddEvents(builder, serializedEventOffsets);
            var payload = JoinedPayload.EndJoinedPayload(builder);
            builder.Finish(payload.Value);

            return FinishMessage(EVENT_MESSAGE_ID);
        }

        public static ArraySegment<byte> CreateActivationObservation(string eventId)
        {
            //FIXME tune size guestimate
            var fbb = new FlatBufferBuilder(10);
            var inner = new FlatBufferBuilder(10);

            inner.Finish(OutcomeEvent.CreateOutcomeEvent(inner, action_taken: true).Value);

            var id = fbb.CreateString(eventId);
            Metadata.StartMetadata(fbb);
            Metadata.AddEncoding(fbb, EventEncoding.Identity);
            Metadata.AddPayloadType(fbb, PayloadType.Outcome);
            Metadata.AddId(fbb, id);

            var md = Metadata.EndMetadata(fbb);
            var payload = Event.CreatePayloadVector(fbb, inner.DataBuffer.ToSizedArray());
            fbb.Finish(Event.CreateEvent(fbb, metaOffset: md, payloadOffset: payload).Value);

            return fbb.DataBuffer.ToSizedArray();
        }
    }
}
