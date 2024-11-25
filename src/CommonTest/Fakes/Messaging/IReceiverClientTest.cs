// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Data;
using reinforcement_learning.messages.flatbuff.v2;
using System.Collections.Generic;

namespace CommonTest.Fakes.Messaging
{
    /// <summary>
    /// IReceiverClientTest is the testing interface for a ReceiverClient.
    /// </summary>
    public interface IReceiverClientTest
    {
        string Id { get; }
        IList<IMessageData> PendingEvents { get; }
        void InjectMessages(IList<EventBatch> messages);
    }
}
