// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommonTest.Fakes.Messaging.EventHub;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading.Tasks;

namespace Tests.CommonTest.Messaging.EventHub;

[TestClass]
public class EHDataClientFactoryTests
{
    [TestMethod]
    [Description("Create eventhubs and verify they have the correct names and partition ids")]
    public async Task Create_EventHub_Receivers_Async()
    {
        var factory = new EHDataClientFactory(4);

        // EHDataClientFactory always returns the number partitions specified in the constructor
        // for any eventhub name (it assumes the eventhub exits)
        var sloweventPartitions = await factory.GetPartitionsIdAsync("slowevents");
        var fasteventsPartitions = await factory.GetPartitionsIdAsync("fastevents");
        var noeventsPartitions = await factory.GetPartitionsIdAsync("noevents");
        Assert.AreEqual(4, sloweventPartitions.Length);
        Assert.AreEqual(4, fasteventsPartitions.Length);
        Assert.AreEqual(4, noeventsPartitions.Length);

        // create some eventhub receivers
        var receiver1 = factory.CreateEventHubReceiver(EHDataClientFactory.MakePartitionId(0), "slowevents", new Mock<ILogger>().Object);
        var receiver2 = factory.CreateEventHubReceiver(EHDataClientFactory.MakePartitionId(1), "slowevents", new Mock<ILogger>().Object);
        var receiver3 = factory.CreateEventHubReceiver(EHDataClientFactory.MakePartitionId(0), "fastevents", new Mock<ILogger>().Object);

        // verify the partitions exist for the event types that have receivers
        sloweventPartitions = await factory.GetPartitionsIdAsync("slowevents");
        fasteventsPartitions = await factory.GetPartitionsIdAsync("fastevents");
        noeventsPartitions = await factory.GetPartitionsIdAsync("noevents");

        // verify the partitions are correct (even if not all receiver were created)
        Assert.AreEqual(4, sloweventPartitions.Length);
        Assert.AreEqual(EHDataClientFactory.MakePartitionId(0), sloweventPartitions[0]);
        Assert.AreEqual(EHDataClientFactory.MakePartitionId(1), sloweventPartitions[1]);
        Assert.AreEqual(EHDataClientFactory.MakePartitionId(2), sloweventPartitions[2]);
        Assert.AreEqual(EHDataClientFactory.MakePartitionId(3), sloweventPartitions[3]);
        Assert.AreEqual(4, fasteventsPartitions.Length);
        Assert.AreEqual(EHDataClientFactory.MakePartitionId(0), fasteventsPartitions[0]);
        Assert.AreEqual(EHDataClientFactory.MakePartitionId(1), fasteventsPartitions[1]);
        Assert.AreEqual(EHDataClientFactory.MakePartitionId(2), fasteventsPartitions[2]);
        Assert.AreEqual(EHDataClientFactory.MakePartitionId(3), fasteventsPartitions[3]);
        Assert.AreEqual(4, noeventsPartitions.Length);

        // verify the receivers have the correct partition ids
        Assert.AreEqual(sloweventPartitions[0], receiver1.PartitionId);
        Assert.AreEqual(sloweventPartitions[1], receiver2.PartitionId);
        Assert.AreEqual(fasteventsPartitions[0], receiver3.PartitionId);
    }
}
