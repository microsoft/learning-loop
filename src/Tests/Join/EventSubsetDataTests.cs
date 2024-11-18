// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Trainer.FlatBuffers;
using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.DecisionService.OnlineTrainer.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace Tests.Join
{
    [TestClass]

    public class EventSubsetDataTests
    {
        [TestMethod]
        [TestCategory("Decision Service/Online Trainer/LOJ")]
        public void AddEvent()
        {
            EventsSubsetData esd = new EventsSubsetData(needsHeader: false, eventType: "test");

            var time = new DateTime(2020, 1, 1);

            var batch = new JoinedBatch()
            {
                EnqueuedTimeUtc = time,
                Messages = new List<List<Message>>()
                {
                    new List<Message>()
                    {
                        new Message()
                        {
                            EnqueuedTimeUtc = time,
                            DataSegment = new byte[4]
                        }
                    }
                }
            };
            esd.Add(batch.Serialize());

            Assert.IsFalse(esd.HasNoEvents);
            Assert.AreEqual(1, esd.EventCount);
            Assert.AreEqual(time, esd.GetBatchTimeOrDefault(default));
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer/LOJ")]
        public void CreateBlockNoHeader()
        {
            EventsSubsetData esd = new EventsSubsetData(needsHeader: false, eventType: "test");
            var time = new DateTime(2020, 1, 1);

            var batch = new JoinedBatch()
            {
                EnqueuedTimeUtc = time,
                Messages = new List<List<Message>>()
                {
                    new List<Message>()
                    {
                        new Message()
                        {
                            EnqueuedTimeUtc = time,
                            DataSegment = new byte[4]
                        }
                    }
                }
            };
            esd.Add(batch.Serialize());
            esd.Add(batch.Serialize());

            var ci = new CheckpointInfo();
            var stream = esd.CreateBlockStream(false, ci);

            Assert.AreEqual(esd.BatchSizeNoHeader, esd.BatchSizeWithHeader);
            Assert.AreEqual(2, stream.SegmentCount);
        }


        [TestMethod]
        [TestCategory("Decision Service/Online Trainer/LOJ")]
        public void CreateBlockWithHeader()
        {
            EventsSubsetData esd = new EventsSubsetData(needsHeader: true, eventType: "test");
            var time = new DateTime(2020, 1, 1);
            var batch = new JoinedBatch()
            {
                EnqueuedTimeUtc = time,
                Messages = new List<List<Message>>()
                {
                    new List<Message>()
                    {
                        new Message()
                        {
                            EnqueuedTimeUtc = time,
                            DataSegment = new byte[4]
                        }
                    }
                }
            };
            esd.Add(batch.Serialize());
            esd.Add(batch.Serialize());

            var ci = new CheckpointInfo();
            var stream = esd.CreateBlockStream(false, ci);

            Assert.IsTrue(esd.BatchSizeNoHeader < esd.BatchSizeWithHeader);
            Assert.AreEqual(3, stream.SegmentCount);
        }
    }
}
