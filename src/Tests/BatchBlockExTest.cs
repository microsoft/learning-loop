// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Tests.TimeProvider;

namespace Tests
{
    [TestClass]
    public class BatchBlockExTest
    {
        private BatchBlockEx<int> block;
        private BufferBlock<IList<int>> output;
        private FlexibleTimeProvider timeProvider;

        private void Setup(TimeSpan maximumFlushLatency, DataflowBlockOptions opts = null)
        {
            this.timeProvider = new FlexibleTimeProvider();
            this.output = new BufferBlock<IList<int>>(opts ?? new DataflowBlockOptions());

            this.block = new BatchBlockEx<int>(
                new BatchBlockExOptions<int>
                {
                    BoundedCapacity = 100,
                    TimeProvider = this.timeProvider,
                    MaximumFlushLatency = maximumFlushLatency,
                    MeasureItem = item => item,
                    CancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token
                },
                this.output);
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task BatchBlockEx_RaceAsync()
        {
            this.Setup(maximumFlushLatency: TimeSpan.FromSeconds(2), opts: new DataflowBlockOptions { BoundedCapacity = 1 });

            // make sure lastSendTime is set to Utc now
            await this.block.Input.SendAsync(120);
            CollectionAssert.AreEqual(new[] { 120 }, (await this.output.ReceiveAsync()).ToArray());

            // this will block SendIfNotEmtpy()
            await this.output.SendAsync(new List<int>());
            // this will trigger a batch and call SendIfNotEmpty()
            await this.block.Input.SendAsync(150);
            // SendIfNotEmpty() is blocked now

            // make sure the flush triggers
            this.timeProvider.IncrementBy(TimeSpan.FromSeconds(3));

            // wait long enough (200ms is the wall time wait)
            await Task.Delay(TimeSpan.FromSeconds(1));

            // the empty
            Assert.AreEqual(0, (await this.output.ReceiveAsync()).Count);
            CollectionAssert.AreEqual(new[] { 150 }, (await this.output.ReceiveAsync()).ToArray());

            Assert.IsFalse(this.output.TryReceive(out var _));

            this.block.Input.Complete();
            await this.block.Completion;

            await this.output.Completion;

            Assert.IsFalse(this.output.TryReceive(out var _), "No elements produced, no elements expected");
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task BatchBlockEx_NoItemsAsync()
        {
            this.Setup(maximumFlushLatency: TimeSpan.FromSeconds(200));

            this.block.Input.Complete();
            await this.block.Completion;

            await this.output.Completion;

            Assert.IsFalse(this.output.TryReceive(out var _), "No elements produced, no elements expected");
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task BatchBlockEx_1BatchAsync()
        {
            this.Setup(maximumFlushLatency: TimeSpan.FromSeconds(200));

            await this.block.Input.SendAsync(50);
            await this.block.Input.SendAsync(49);
            await this.block.Input.SendAsync(10);

            this.block.Input.Complete();
            await this.block.Completion;

            Assert.IsTrue(this.output.TryReceive(out var batch1), "Expected a batch");
            CollectionAssert.AreEqual(new[] { 50, 49 }, batch1.ToArray(), "Expected first batch of items less than 100");

            Assert.IsTrue(this.output.TryReceive(out var batch2), "Expected a batch");
            CollectionAssert.AreEqual(new[] { 10 }, batch2.ToArray(), "Expected second batch of items going beyond 100");

            Assert.IsFalse(this.output.TryReceive(out var _), "Buffer should be empty");
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task BatchBlockEx_3BatchesAsync()
        {
            this.Setup(maximumFlushLatency: TimeSpan.FromSeconds(200));

            await this.block.Input.SendAsync(50);
            await this.block.Input.SendAsync(49);
            await this.block.Input.SendAsync(90);
            await this.block.Input.SendAsync(15);
            await this.block.Input.SendAsync(10);

            this.block.Input.Complete();
            await this.block.Completion;

            Assert.IsTrue(this.output.TryReceive(out var batch1), "Expected a batch");
            CollectionAssert.AreEqual(new[] { 50, 49 }, batch1.ToArray(), "Expected first batch of items less than 100");

            Assert.IsTrue(this.output.TryReceive(out var batch2), "Expected a batch");
            CollectionAssert.AreEqual(new[] { 90 }, batch2.ToArray(), "Expected second batch of items going beyond 100");

            Assert.IsTrue(this.output.TryReceive(out var batch3), "Expected a batch");
            CollectionAssert.AreEqual(new[] { 15, 10 }, batch3.ToArray(), "Expected second batch of items going beyond 100");

            Assert.IsFalse(this.output.TryReceive(out var _), "Buffer should be empty");
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task BatchBlockEx_TriggerFlushtimeAsync()
        {
            this.Setup(maximumFlushLatency: TimeSpan.FromMilliseconds(200));

            await this.block.Input.SendAsync(50);

            // let the code pick up the first item
            await Task.Delay(400);

            Assert.IsFalse(this.output.TryReceive(out var _), "TimeProvider time hasn't moved forward yet, no examples");

            // move time provider time forward and now time should be moving...
            this.timeProvider.IncrementBy(TimeSpan.FromMilliseconds(300));

            var batch1 = await this.output.ReceiveAsync();
            CollectionAssert.AreEqual(new[] { 50 }, batch1.ToArray(), "200ms flush time hit");

            this.block.Input.Complete();
            await this.block.Completion;

            Assert.IsFalse(this.output.TryReceive(out var _), "Buffer should be empty");
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task BatchBlockEx_TriggerFlushTwiceAsync()
        {
            this.Setup(maximumFlushLatency: TimeSpan.FromMilliseconds(200));

            await this.block.Input.SendAsync(30);

            // make sure it can pick up time before moving forward
            await Task.Delay(400);
            this.timeProvider.IncrementBy(TimeSpan.FromMilliseconds(300));

            var batch1 = await this.output.ReceiveAsync();
            CollectionAssert.AreEqual(new[] { 30 }, batch1.ToArray(), "flush time hit");

            await this.block.Input.SendAsync(40);

            // make sure it can pick up time before moving forward
            await Task.Delay(400);
            this.timeProvider.IncrementBy(TimeSpan.FromMilliseconds(300));

            var batch2 = await this.output.ReceiveAsync();
            CollectionAssert.AreEqual(new[] { 40 }, batch2.ToArray(), "flush time hit");

            this.block.Input.Complete();
            await this.block.Completion;

            Assert.IsFalse(this.output.TryReceive(out var _), "Buffer should be empty");
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task BatchBlockEx_WaitForFlushOnEmptyAsync()
        {
            this.Setup(maximumFlushLatency: TimeSpan.FromMilliseconds(200));

            // wait 2x maximum flush time
            await Task.Delay(200);
            this.timeProvider.IncrementBy(TimeSpan.FromMilliseconds(300));

            this.block.Input.Complete();
            await this.block.Completion;

            Assert.IsFalse(this.output.TryReceive(out var _), "Buffer should be empty");
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task BatchBlockEx_BigItemAsync()
        {
            this.Setup(maximumFlushLatency: TimeSpan.FromSeconds(200));

            await this.block.Input.SendAsync(120);

            var batch1 = await this.output.ReceiveAsync();
            CollectionAssert.AreEqual(new[] { 120 }, batch1.ToArray(), "Expected one big item");

            this.block.Input.Complete();
            await this.block.Completion;
        }

        public class MyException : Exception
        { }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        [ExpectedException(typeof(MyException))]
        public async Task BatchBlockEx_MeasureFailedAsync()
        {
            this.timeProvider = new FlexibleTimeProvider();
            this.output = new BufferBlock<IList<int>>();

            this.block = new BatchBlockEx<int>(
                new BatchBlockExOptions<int>
                {
                    BoundedCapacity = 100,
                    TimeProvider = FixedTimeProvider.Instance,
                    MaximumFlushLatency = TimeSpan.FromSeconds(200),
                    MeasureItem = _ => throw new MyException(),
                    CancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token
                },
                this.output);

            await this.block.Input.SendAsync(120);

            await this.block.Completion;
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task BatchBlockEx_PerformanceAsync()
        {
            this.timeProvider = new FlexibleTimeProvider();

            // increase increase number of events to enable proper profiling
            int eventsExpected = 1 * 1024 * 1024;
            int eventsActual = 0;

            var output = new ActionBlock<IList<int>>(
                batch => eventsActual += batch.Count,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 1
                });

            this.block = new BatchBlockEx<int>(
                new BatchBlockExOptions<int>
                {
                    BoundedCapacity = 100,
                    TimeProvider = FixedTimeProvider.Instance,
                    MaximumFlushLatency = TimeSpan.FromSeconds(200),
                    MeasureItem = item => item,
                    CancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token
                },
                output);

            var random = new Random(42);

            for (int i = 0; i < eventsExpected; i++)
                await this.block.Input.SendAsync(random.Next(30));

            this.block.Input.Complete();
            await this.block.Completion;

            await output.Completion;

            Assert.AreEqual(eventsExpected, eventsActual);
        }
    }
}
