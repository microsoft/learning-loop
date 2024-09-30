// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommonTest.Fakes.Messaging.EventHub;
using CommonTest.Messages;
using Microsoft.DecisionService.Common.Trainer;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using reinforcement_learning.messages.flatbuff.v2;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tests.CommonTest.Messaging.EventHub;

[TestClass]
public class EHControllerTests
{
    private static List<EventBatch> CreateTestEventBatch(int batchCount, int eventCount)
    {
        var batches = new List<EventBatch>();
        for (int i = 0; i < batchCount; i++)
        {
            var events = new List<Event>();
            for (int j = 0; j < eventCount; j++)
            {
                var e = FBMessageBuilder.CreateEvent("test-app-id", $"batch-{i}:event-id-{j}", DateTime.UtcNow, EventEncoding.Identity, 1.0f,
                    FBMessageBuilder.CreateCaEvent(false, 0.5f, new byte[] { 1, 2, 3, 4 }, 0.5f, "test-model", LearningModeType.Online)
                );
                events.Add(e);
            }
            batches.Add(FBMessageBuilder.CreateEventBatch(events));
        }
        return batches;
    }

    [TestMethod]
    [Description("Test EHController basics")]
    public async Task Test_EHControll_Async()
    {
        var controller = new EHController(4);

        // verify no eventhubs exist
        var eventTypes = controller.GetEventTypeNames();
        Assert.AreEqual(0, eventTypes.Count);

        // create some eventhubs
        var slowevent1 = controller.DataClientFactory.CreateEventHubReceiver(EHDataClientFactory.MakePartitionId(0), "slowevents", new Mock<ILogger>().Object);
        var slowevent2 = controller.DataClientFactory.CreateEventHubReceiver(EHDataClientFactory.MakePartitionId(1), "slowevents", new Mock<ILogger>().Object);
        var fastevent1 = controller.DataClientFactory.CreateEventHubReceiver(EHDataClientFactory.MakePartitionId(0), "fastevents", new Mock<ILogger>().Object);

        // verify the eventhubs exist
        eventTypes = controller.GetEventTypeNames();
        Assert.AreEqual(2, eventTypes.Count);
        Assert.IsTrue(eventTypes.Contains("slowevents"));
        Assert.IsTrue(eventTypes.Contains("fastevents"));

        // verify the partitions exist for the event types that have receivers
        var sloweventPartitions = await controller.DataClientFactory.GetPartitionsIdAsync("slowevents");
        var fasteventPartitions = await controller.DataClientFactory.GetPartitionsIdAsync("fastevents");
        var noPartitions = await controller.DataClientFactory.GetPartitionsIdAsync("non-event");
        Assert.AreEqual(4, sloweventPartitions.Length);
        Assert.AreEqual(4, fasteventPartitions.Length);
        Assert.AreEqual(4, noPartitions.Length);

        // verify all of the receivers exist
        var receivers = controller.ReceiverClients;
        Assert.AreEqual(3, receivers.Count);

        // verify the receivers can be retrieved by eventhub / partition id
        var sloweventReceiver1 = controller.GetReceiverClient("slowevents", EHDataClientFactory.MakePartitionId(0));
        Assert.AreSame(slowevent1, sloweventReceiver1 as IReceiverClient);
        var sloweventReceiver2 = controller.GetReceiverClient("slowevents", EHDataClientFactory.MakePartitionId(1));
        Assert.AreSame(slowevent2, sloweventReceiver2 as IReceiverClient);
        var sloweventReceiver3 = controller.GetReceiverClient("slowevents", EHDataClientFactory.MakePartitionId(3));
        Assert.IsNull(sloweventReceiver3);

        var fasteventReceiver1 = controller.GetReceiverClient("fastevents", EHDataClientFactory.MakePartitionId(0));
        Assert.AreSame(fastevent1, fasteventReceiver1 as IReceiverClient);
        var fasteventReceiver2 = controller.GetReceiverClient("fastevents", EHDataClientFactory.MakePartitionId(1));
        Assert.IsNull(fasteventReceiver2);

        controller.ProvideUnabalancedPartitionIds = true;
        // verify the first call after ProvideUnabalancedPartitionIds is set to true will return the same number of partition ids
        var unbalancedPartitionIds = await controller.DataClientFactory.GetPartitionsIdAsync("slowevents");
        Assert.AreEqual(sloweventPartitions.Length, unbalancedPartitionIds.Length);
        // verify the second call after ProvideUnabalancedPartitionIds is set to true will return a different number of partition ids
        unbalancedPartitionIds = await controller.DataClientFactory.GetPartitionsIdAsync("slowevents");
        Assert.AreNotEqual(sloweventPartitions.Length, unbalancedPartitionIds.Length);
    }

    [TestMethod]
    [Description("Test EHController inject messages")]
    public async Task Test_EHControll_InjectMessages_Async()
    {
        var controller = new EHController(4);

        // create some eventhubs
        var slowreceiverClient1 = controller.DataClientFactory.CreateEventHubReceiver(EHDataClientFactory.MakePartitionId(0), "slowevents", new Mock<ILogger>().Object);
        var slowReceiver1 = slowreceiverClient1.Connect(null);
        var slowreceiverClient2 = controller.DataClientFactory.CreateEventHubReceiver(EHDataClientFactory.MakePartitionId(1), "slowevents", new Mock<ILogger>().Object);
        var slowReceiver2 = slowreceiverClient2.Connect(null);
        var fastreceiverClient1 = controller.DataClientFactory.CreateEventHubReceiver(EHDataClientFactory.MakePartitionId(0), "fastevents", new Mock<ILogger>().Object);
        var fastReceiver1 = fastreceiverClient1.Connect(null);

        // get the test receiver interface
        var sloweventReceiver1 = controller.GetReceiverClient("slowevents", EHDataClientFactory.MakePartitionId(0));
        Assert.IsNotNull(sloweventReceiver1);
        var sloweventReceiver2 = controller.GetReceiverClient("slowevents", EHDataClientFactory.MakePartitionId(1));
        Assert.IsNotNull(sloweventReceiver2);
        var fasteventReceiver1 = controller.GetReceiverClient("fastevents", EHDataClientFactory.MakePartitionId(0));
        Assert.IsNotNull(fasteventReceiver1);

        // inject some messages
        sloweventReceiver1.InjectMessages(CreateTestEventBatch(1, 2));
        sloweventReceiver2.InjectMessages(CreateTestEventBatch(2, 3));
        fasteventReceiver1.InjectMessages(CreateTestEventBatch(5, 8));

        // verify the messages were injected
        Assert.AreEqual(1, sloweventReceiver1.PendingEvents.Count);
        Assert.AreEqual(2, sloweventReceiver2.PendingEvents.Count);
        Assert.AreEqual(5, fasteventReceiver1.PendingEvents.Count);

        // verify the messages can be retrieved
        var slow1Messages = await slowReceiver1.ReceiveAsync(new TimeSpan(0));
        Assert.AreEqual(1, slow1Messages.Length);
        var slow2Messages = await slowReceiver2.ReceiveAsync(new TimeSpan(0));
        Assert.AreEqual(2, slow2Messages.Length);
        var fast1Messages = await fastReceiver1.ReceiveAsync(new TimeSpan(0));
        Assert.AreEqual(5, fast1Messages.Length);

        // verify the message have been consumed
        Assert.AreEqual(0, sloweventReceiver1.PendingEvents.Count);
        Assert.AreEqual(0, sloweventReceiver2.PendingEvents.Count);
        Assert.AreEqual(0, fasteventReceiver1.PendingEvents.Count);
        slow1Messages = await slowReceiver1.ReceiveAsync(new TimeSpan(0));
        Assert.AreEqual(0, slow1Messages.Length);
        slow2Messages = await slowReceiver2.ReceiveAsync(new TimeSpan(0));
        Assert.AreEqual(0, slow2Messages.Length);
        fast1Messages = await fastReceiver1.ReceiveAsync(new TimeSpan(0));
        Assert.AreEqual(0, fast1Messages.Length);
    }
}
