// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Google.FlatBuffers;
using reinforcement_learning.messages.flatbuff.v2;
using System;
using System.Collections.Generic;

namespace Microsoft.DecisionService.Common.Trainer.FlatBuffers
{
    public static class SchemaV2Extensions
    {
        public static DateTime ToDateTime(this TimeStamp timestamp)
        {
            return new DateTime(
                timestamp.Year,
                timestamp.Month,
                timestamp.Day,
                timestamp.Hour,
                timestamp.Minute,
                timestamp.Second,
                // Explicitly truncate subseconds (= 100 ns)
                DateTimeKind.Utc);
        }

        public static Offset<TimeStamp> SerializeV2TimeStamp(this DateTime time, FlatBufferBuilder builder)
        {
            return TimeStamp.CreateTimeStamp(builder,
                (ushort)time.Year,
                (byte)time.Month,
                (byte)time.Day,
                (byte)time.Hour,
                (byte)time.Minute,
                (byte)time.Second,
                0); //truncace us
        }

        // This helper method exists because the current generated code is bad
        public static VectorOffset JoinedEventCreateEventVector(FlatBufferBuilder builder, ArraySegment<byte> data)
        {
            JoinedEvent.StartEventVector(builder, data.Count);

            //TODO update the flatbuffers dependency so we can use a fast version of this loop
            for (int i = data.Count - 1; i >= 0; i--)
            {
                builder.AddByte(data[i]);
            }

            return builder.EndVector();
        }

        public static VectorOffset JoinedPayloadCreateEventsVector(FlatBufferBuilder builder, List<Offset<JoinedEvent>> events)
        {
            JoinedPayload.StartEventsVector(builder, events.Count);

            //XXX update the flatbuffers dependency so we can use a fast version of this loop
            for (int i = events.Count - 1; i >= 0; i--)
            {
                builder.AddOffset(events[i].Value);
            }

            return builder.EndVector();
        }


    }
}
