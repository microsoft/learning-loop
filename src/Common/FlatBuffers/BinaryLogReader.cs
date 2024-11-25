// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Google.FlatBuffers;
using reinforcement_learning.messages.flatbuff.v2;
using System;
using System.Collections.Generic;
using System.IO;
using FileHeader = reinforcement_learning.messages.flatbuff.v2.FileHeader;
using JoinedPayload = reinforcement_learning.messages.flatbuff.v2.JoinedPayload;
using rl = reinforcement_learning.messages.flatbuff.v2;

namespace Microsoft.DecisionService.Common.Trainer.FlatBuffers
{
    public abstract class BinaryLogMessage
    {
        public uint ByteOffset { get; set; }
    }

    public enum BinaryLogMessageType : uint
    {
        FileMagicMessage = 0x42465756, //'VWFB'
        RegularMessage = 0xFFFFFFFF,
        FileHeaderMessage = 0x55555555,
        CheckpointMessage = 0x11111111,
        EOFMessage = 0xAAAAAAAA,
    }

    public class HeaderMessage : BinaryLogMessage
    {
        public HeaderMessage(Dictionary<string, string> headerProperties)
        {
            this.HeaderProperties = headerProperties;
        }

        public Dictionary<string, string> HeaderProperties { get; }

    }

    public class LogCheckpointInfo : BinaryLogMessage
    {
        public LogCheckpointInfo(CheckpointInfo checkpoint)
        {
            this.Checkpoint = checkpoint;
        }

        public CheckpointInfo Checkpoint { get; }
    }

    public class SingleEvent
    {
        public SingleEvent(byte[] payload, DateTime timestamp)
        {
            this.Timestamp = timestamp;
            this.Payload = payload;
            this.Event = rl.Event.GetRootAsEvent(new ByteBuffer(payload));
            this.UnpackedEvent = UnpackEvent(this.Event.Meta.Value, this.Event.GetPayloadBytes().Value.ToArray());
        }

        public static IFlatbufferObject UnpackEvent(rl.Metadata meta, byte[] payload)
        {
            switch (meta.PayloadType)
            {
                case rl.PayloadType.CA:
                    return CaEvent.GetRootAsCaEvent(new ByteBuffer(payload));
                case rl.PayloadType.Outcome:
                    return rl.OutcomeEvent.GetRootAsOutcomeEvent(new ByteBuffer(payload));
                case rl.PayloadType.Episode:
                    return rl.EpisodeEvent.GetRootAsEpisodeEvent(new ByteBuffer(payload));
                case rl.PayloadType.DedupInfo:
                    return rl.DedupInfo.GetRootAsDedupInfo(new ByteBuffer(payload));
                case rl.PayloadType.CB:
                    return rl.CbEvent.GetRootAsCbEvent(new ByteBuffer(payload));
                case rl.PayloadType.Slates:
                case rl.PayloadType.CCB:
                    return rl.MultiSlotEvent.GetRootAsMultiSlotEvent(new ByteBuffer(payload));
                case rl.PayloadType.MultiStep:
                    return rl.MultiStepEvent.GetRootAsMultiStepEvent(new ByteBuffer(payload));
                default:
                    throw new ArgumentException("Unknown event type");
            }
        }

        public byte[] Payload { get; set; }
        public DateTime Timestamp { get; set; }
        public rl.Event Event { get; private set; }
        public IFlatbufferObject UnpackedEvent { get; private set; }
    }

    public class RegularMessage : BinaryLogMessage
    {
        public RegularMessage(SingleEvent[] events)
        {
            this.Events = events;
        }

        public SingleEvent[] Events { get; }
    }

    public class EofMessage : BinaryLogMessage
    {
        public EofMessage()
        {

        }
   }

    internal class RawBinaryLogMessage
    {
        public RawBinaryLogMessage(BinaryLogMessageType messageType, uint messageSize, byte[] payload, uint byteOffset)
        {
            MessageType = messageType;
            MessageSize = messageSize;
            Payload = payload;
            ByteOffset = byteOffset;
        }

        public BinaryLogMessageType MessageType { get; set; }
        public uint MessageSize { get; set; }
        public byte[] Payload { get; set; }
        public uint ByteOffset { get; set; }
    }

    public class BinaryLogReader
    {
        private readonly BinaryReader reader;

        public BinaryLogReader(BinaryReader binaryReader, bool skipFileMagic = false)
        {
            reader = binaryReader;
            // Read the filemagic header.
            if (!skipFileMagic)
            {
                var message = ReadRawMessage();
                if (message.MessageType != BinaryLogMessageType.FileMagicMessage)
                {
                    throw new ArgumentException("Malformed file. Does not start with file magic.");
                }
            }
        }

        private RawBinaryLogMessage ReadRawMessage()
        {
            var byteOffset = reader.BaseStream.Position;

            BinaryLogMessageType messageType;
            // Check if end of file reached.
            try
            {
                messageType = (BinaryLogMessageType)reader.ReadUInt32();
            }
            catch (System.IO.EndOfStreamException)
            {
                return null;
            }

            if (messageType == BinaryLogMessageType.EOFMessage)
            {
                return new RawBinaryLogMessage(messageType, 0, null, (uint)byteOffset);
            }

            var messageSize = reader.ReadUInt32();
            var paddingBytes = messageSize % 8;

            if (messageType == BinaryLogMessageType.FileMagicMessage)
            {
                return new RawBinaryLogMessage(messageType, messageSize, null, (uint)byteOffset);
            }

            var payload = reader.ReadBytes(Convert.ToInt32(messageSize));
            reader.ReadBytes(Convert.ToInt32(paddingBytes));
            return new RawBinaryLogMessage(messageType, messageSize, payload, (uint)byteOffset);
        }

        public BinaryLogMessage ReadMessage()
        {
            var rawMessage = ReadRawMessage();
            if (rawMessage == null)
            {
                return null;
            }

            switch (rawMessage.MessageType)
            {
                case BinaryLogMessageType.FileMagicMessage:
                    throw new ArgumentException("Malformed file. File magic header discovered in incorrect location.");
                case BinaryLogMessageType.RegularMessage:
                    {
                        var buffer = new ByteBuffer(rawMessage.Payload);
                        var payload = JoinedPayload.GetRootAsJoinedPayload(buffer);
                        var eventsLength = payload.EventsLength;
                        var events = new SingleEvent[eventsLength];
                        for (var i = 0; i < eventsLength; i++)
                        {
                            var evt = payload.Events(i);
                            var timestamp = evt.Value.Timestamp.Value.ToDateTime();
                            events[i] = new SingleEvent(evt.Value.GetEventArray(), timestamp);
                        }
                        var msg = new RegularMessage(events)
                        {
                            ByteOffset = rawMessage.ByteOffset
                        };
                        return msg;
                    }
                case BinaryLogMessageType.CheckpointMessage:
                    {
                        var buffer = new ByteBuffer(rawMessage.Payload);
                        var payload = rl.CheckpointInfo.GetRootAsCheckpointInfo(buffer);
                        var rewardType = payload.RewardFunctionType;
                        var defaultReward = payload.DefaultReward;
                        var checkpoint = new CheckpointInfo() { FbRewardType = rewardType, DefaultReward = defaultReward };
                        return new LogCheckpointInfo(checkpoint);
                    }
                case BinaryLogMessageType.FileHeaderMessage:
                    {
                        var buffer = new ByteBuffer(rawMessage.Payload);
                        var payload = FileHeader.GetRootAsFileHeader(buffer);
                        var propertiesLength = payload.PropertiesLength;
                        var properties = new Dictionary<string, string>();
                        for (var i = 0; i < propertiesLength; i++)
                        {
                            var property = payload.Properties(i).Value;
                            properties.Add(property.Key, property.Value);
                        }

                        return new HeaderMessage(properties);
                    }
                case BinaryLogMessageType.EOFMessage:
                    {
                        return new EofMessage();
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
