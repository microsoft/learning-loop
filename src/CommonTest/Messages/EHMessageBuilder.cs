// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Messaging.EventHubs;
using Google.FlatBuffers;
using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Trainer.Data;
using System;
using System.IO;

namespace CommonTest.Messages
{
    /// <summary>
    /// Event Hub Message Builder for flatbuffer based message on Event Hub
    /// </summary>
    public class EHMessageBuilder
    {
        public static EventData CreateEvent(MessageType msgType, IFlatbufferObject fbMessage)
        {
            var messageBytes = fbMessage.ByteBuffer.ToSizedArray();
            var messagePreamble = new MessagePreamble()
            {
                MsgType = (ushort) msgType,
                MsgSize = (uint) messageBytes.Length,
            };

            var stm = new MemoryStream();
            stm.Write(messagePreamble.ToBytes());
            stm.Write(messageBytes);

            var body = new ReadOnlyMemory<byte>(stm.GetBuffer(), 0, (int)stm.Length);
            return new EventData(body);
        }
    }
}
