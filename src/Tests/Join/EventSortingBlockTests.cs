// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommonTest;
using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Trainer.Join;
using Microsoft.DecisionService.Common.Trainer.Operations;
using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.DecisionService.OnlineTrainer.Data;
using Microsoft.DecisionService.OnlineTrainer.Join;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests.Join
{
    [TestClass]
    public class EventSortingBlockTests
    {
        [TestMethod]
        [Timeout(10000)]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task EventSortingBlockTests_Happy_1Async()
        {
            var tokenSource = new CancellationTokenSource();

            var joinerOption = new JoinerConfig();

            var block = new EventMergeSortBlock("test",
                NullLogger.Instance, 1, joinerOption, SystemTimeProvider.Instance);

            var partitionSource1 = block.Add("1");

            var p1 = partitionSource1.Input;

            var mainLoopTask = Task.Run(() => block.RunAsync(tokenSource.Token));

            var o1 = new Message() { EnqueuedTimeUtc = new DateTime(2017, 12, 1) };

            var b1 = new MessageBatch { Messages = new List<Message> { o1 } };

            await p1.WriteAsync(b1);

            p1.Complete();

            Assert.AreSame(b1, await block.Output.ReceiveAsync());

            tokenSource.Cancel();
            await block.Output.Completion;
            await mainLoopTask;
        }

        [TestMethod]
        [Timeout(100000)]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task EventSortingBlockTests_Happy_2Async()
        {
            var tokenSource = new CancellationTokenSource();
            var joinerOption = new JoinerConfig();
            var block = new EventMergeSortBlock("test", NullLogger.Instance, 2, joinerOption, SystemTimeProvider.Instance);

            var partitionSource1 = block.Add("1");
            var partitionSource2 = block.Add("2");

            var p1 = partitionSource1.Input;
            var p2 = partitionSource2.Input;

            var mainLoopTask = Task.Run(() => block.RunAsync(tokenSource.Token));

            var o1 = new Message() { EnqueuedTimeUtc = new DateTime(2017, 12, 1) };
            var o2 = new Message() { EnqueuedTimeUtc = new DateTime(2017, 12, 2) };
            var o3 = new Message() { EnqueuedTimeUtc = new DateTime(2017, 12, 3) };

            var b1 = new MessageBatch { Messages = new List<Message> { o1 } };
            var b2 = new MessageBatch { Messages = new List<Message> { o3 } };
            var b3 = new MessageBatch { Messages = new List<Message> { o2 } };

            await p1.WriteAsync(b1);
            await p2.WriteAsync(b2);
            await p1.WriteAsync(b3);

            Assert.AreSame(b1, await block.Output.ReceiveAsync());
            Assert.AreSame(b3, await block.Output.ReceiveAsync());
            Assert.AreSame(b2, await block.Output.ReceiveAsync());

            p1.Complete();
            p2.Complete();

            tokenSource.Cancel();
            await block.Output.Completion;
            await mainLoopTask;
        }

        [TestMethod]
        [Timeout(10000)]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task EventSortingBlockTests_CancelAsync()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var joinerOption = new JoinerConfig();

            var block = new EventMergeSortBlock("test", NullLogger.Instance, 1, joinerOption, SystemTimeProvider.Instance);

            var mainLoopTask = Task.Run(() => block.RunAsync(cancellationTokenSource.Token));

            cancellationTokenSource.Cancel();

            await block.Output.Completion;
            await mainLoopTask;
        }
    }
}
