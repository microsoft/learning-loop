// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Azure.Messaging.EventHubs;
using Microsoft.DecisionService.Common.Data;

namespace Microsoft.DecisionService.OnlineTrainer.Data
{
    public class EventHubData : IMessageData
    {
        private readonly EventData eventData;

        public EventHubData(EventData eventData, string streamId)
        {
            this.eventData = eventData;
            this.StreamId = streamId;
        }
        public string StreamId { get; }
        public long StreamOffset => this.eventData.Offset;
        public long StreamSequenceNumber => this.eventData.SequenceNumber;
        public DateTime EnqueuedTimeUtc => this.eventData.EnqueuedTime.UtcDateTime;
        public ArraySegment<byte> Bytes => this.eventData.Body.ToArray();
    }
}