// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Trainer.Data;
using Microsoft.DecisionService.Common.Trainer.Join;
using Microsoft.DecisionService.OnlineTrainer.Data;
using Microsoft.DecisionService.OnlineTrainer.Join;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.DecisionService.OnlineTrainer;

namespace Tests.Join
{
    [TestClass]
    public class PartitionSourceTests
    {
        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public async System.Threading.Tasks.Task PartitionSource_Test1Async()
        {
            int receiveTimeout = (int)TimeSpan.FromSeconds(1).TotalMilliseconds;
            var ps1 = new PartitionSource("1", new JoinerConfig(), NullLogger.Instance);
            var ps2 = new PartitionSource("2", new JoinerConfig(), NullLogger.Instance);

            // make sure partition id is in there
            Assert.AreEqual("1", ps1.PartitionId);
            Assert.AreEqual("2", ps2.PartitionId);

            await ps1.Input.WriteAsync(new MessageBatch()
            {
                EnqueuedTimeUtc = new DateTime(2017, 1, 1),
                Messages =
                    new List<Message>()
                    {
                        new Message()
                        {
                            EventId = "a",
                            EnqueuedTimeUtc = new DateTime(2017, 1, 1),
                        }
                    }
            });

            await ps2.Input.WriteAsync(new MessageBatch()

            {
                EnqueuedTimeUtc = new DateTime(2017, 2, 1),
                Messages =
                    new List<Message>()
                    {
                        new Message()
                        {
                            EventId = "b",
                            EnqueuedTimeUtc = new DateTime(2017, 2, 1),
                        },
                        new Message()
                        {
                            EventId = "d",
                            EnqueuedTimeUtc = new DateTime(2017, 2, 1),
                        }
                    }
            });

            await ps2.Input.WriteAsync(new MessageBatch()
            {
                EnqueuedTimeUtc = new DateTime(2017, 4, 1),
                Messages =
                    new List<Message>()
                    {
                        new Message()
                        {
                            EventId = "d",
                            EnqueuedTimeUtc = new DateTime(2017, 4, 1),
                        }
                    }
            });

            // load both sources with a batch
            Assert.IsTrue(await ps1.WaitForDataAsync(CancellationToken.None));
            Assert.IsTrue(await ps2.WaitForDataAsync(CancellationToken.None));

            Assert.AreEqual(new DateTime(2017, 1, 1), ps1.Peek().EnqueuedTimeUtc);
            Assert.AreEqual(new DateTime(2017, 2, 1), ps2.Peek().EnqueuedTimeUtc);

            ps2.Pop();
            Assert.AreEqual(new DateTime(2017, 4, 1), ps2.Peek().EnqueuedTimeUtc);

            ps2.Pop();
            Assert.AreEqual(null, ps2.Peek());
        }
    }
}