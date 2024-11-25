// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Google.FlatBuffers;
using Microsoft.DecisionService.Common.Trainer.Data;
using Microsoft.Extensions.Logging;
using reinforcement_learning.messages.flatbuff.v2;
using System;
using System.IO;

namespace Microsoft.DecisionService.Common.Data
{
    public class SchemaV2Parser
    {
        private readonly ILogger _logger;
        private readonly bool _useClientTime;

        public SchemaV2Parser(ILogger logger, bool useClientTime)
        {
            this._logger = logger;
            this._useClientTime = useClientTime;

            if (useClientTime && true)
            {
                //FIXME should be doable, but it's not needed by APS itself.
                throw new ArgumentException("SchemaV2Parser cannot handle client time and opaque mode at the same time");
            }
        }

        static public MessageBatch ProcessEventHubMessage(IMessageData data, ILogger? logger = null)
        {
            MessagePreamble preamble = new MessagePreamble();
            if (!preamble.ReadFromBytes(data.Bytes))
            {
                throw new InvalidDataException($"Failed to read preamble from message.");
            }

            if (preamble.Version != 0)
            {
                throw new ArgumentException($"Preamble version is not supported = {preamble.Version}");
            }

            if (preamble.MsgType != (ushort)MessageType.FlatBuffGenericEventBatch)
            {
                throw new ArgumentException($"Unhandled MsgType = {preamble.MsgType}");
            }

            MessageBatch messageBatch = new MessageBatch();

            ByteBuffer buffer = new ByteBuffer(data.Bytes.Array, data.Bytes.Offset + MessagePreamble.SerializedSize);

            EventBatch batch = EventBatch.GetRootAsEventBatch(buffer);
            var metadata = batch.Metadata;
            messageBatch.OriginalEventCount = metadata.Value.OriginalEventCount;
            for (int i = 0; i < batch.EventsLength; ++i)
            {
                if (!batch.Events(i).HasValue)
                {
                    logger?.LogWarning(
                        $"SchemaV2Parser batch with missing event at index:{i}");
                    continue;
                }
                var serializedEvent = batch.Events(i).Value;

                if (!serializedEvent.GetPayloadBytes().HasValue)
                {
                    logger?.LogWarning(
                        $"SchemaV2Parser batch event with missing payload at index:{i} ");
                    continue;
                }
                var payload = serializedEvent.GetPayloadBytes().Value;

                ByteBuffer eventBuffer = new ByteBuffer(payload.Array, payload.Offset);
                var evt = Event.GetRootAsEvent(eventBuffer);
                if (!evt.Meta.HasValue)
                {
                    logger?.LogWarning(
                        $"SchemaV2Parser batch event idx:{i} with missing metadata");
                    continue;
                }

                // Note that AppId is a nullable optional field
                var appId = evt.Meta.Value.AppId;

                var eventId = evt.Meta.Value.Id;
                if (string.IsNullOrEmpty(eventId))
                {
                    logger?.LogWarning(
                         $"SchemaV2Parser batch event idx:{i} with or empty event-id");
                    continue;
                }

                messageBatch.Messages.Add(new Message()
                {
                    AppId = appId,
                    EventId = eventId,
                    PayloadType = evt.Meta.Value.PayloadType,
                    EnqueuedTimeUtc = data.EnqueuedTimeUtc,
                    DataSegment = payload
                });
            }

            messageBatch.Offset = data.StreamOffset;
            messageBatch.SequenceNumber = data.StreamSequenceNumber;
            messageBatch.PartitionId = data.StreamId;
            messageBatch.EnqueuedTimeUtc = data.EnqueuedTimeUtc;

            return messageBatch;
        }
    }
}
