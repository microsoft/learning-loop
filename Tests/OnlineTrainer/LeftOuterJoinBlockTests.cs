// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Trainer.Data;
using Microsoft.DecisionService.Common.Trainer.FlatBuffers;
using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.DecisionService.OnlineTrainer.Data;
using Microsoft.DecisionService.OnlineTrainer.Join;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.DecisionService.Common.Join;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Messages;
using Tests.Messages.Flatbuffers;
using Tests.TimeProvider;

namespace Tests.OnlineJoiner
{
    public static class AsyncTestExtension
    {
        public static async Task AssertTrueAsync(this Task<bool> task)
        {
            if (!await task)
                Assert.Inconclusive("check that the event was correctly pushed in the buffer");
        }
    }

    [TestClass]
    public class LeftOuterJoinBlockTests
    {
        protected BufferBlock<MessageBatch> left;
        protected BufferBlock<Message> right;
        protected BufferBlock<JoinedBatch> output;
        protected IncrementingTimeProvider timeProvider;
        protected CancellationTokenSource cancellationTokenSource;
        protected TimeSpan experimentalUnitDuration;
        protected TimeSpan timeProviderIncrement;
        protected TimeSpan backwardEventJoinWindowTimeSpan;
        protected TimeSpan punctuationSlack;


        [TestInitialize]
        public void TestInit()
        {
            this.left = new BufferBlock<MessageBatch>();
            this.right = new BufferBlock<Message>();
            this.output = new BufferBlock<JoinedBatch>();
            // Note:
            // These tests have know defects with current time provider implementation:
            // 1. if (number of ITimeProvider.utcNow being called) * (TimeIncrementing in IncrementingTestTimeProvider)
            // > ExperimentalUnitDuration + LateArrivalTimeSpan
            // these tests will fail.
            // 2. Make sure PunctuationTimeout and TimeProviderIncrement using the same value to keep the behavior same as real world.

            this.experimentalUnitDuration = TimeSpan.FromMilliseconds(100);
            this.punctuationSlack = TimeSpan.FromMilliseconds(25);
            this.timeProviderIncrement = TimeSpan.FromMilliseconds(15);
            this.timeProvider = new IncrementingTimeProvider(new DateTime(2017, 7, 24, 0, 0, 0), timeProviderIncrement);
            this.backwardEventJoinWindowTimeSpan = TimeSpan.FromSeconds(2);

            this.cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        }

        private static MessageBatch SingleMessageInteractionBatch(DateTime time, string eventId)
        {
            return new MessageBatch()
            {
                EnqueuedTimeUtc = time,
                Messages = new List<Message>()
                {
                    new()
                    {
                        EventId = eventId,
                        EnqueuedTimeUtc = time,
                        DataSegment = null
                    }
                }
            };
        }

        [DataTestMethod,
         Description(
             "This test checks that an observation does match with an interaction when the observation happens after the interaction but in the matching window")]
        [TestCategory("Decision Service/Online Trainer")]
        [DataRow(true, 2)]
        [DataRow(false, 0)]
        [DataRow(true, 1)]
        public async Task LeftOuterJoinBlock_MatchWhenInWindowAsync(bool addPuncSlack, int maxTimeoutRetryCount)
        {
            JoinerConfig settings = SetJoinerOptions(addPunctuationSlack: addPuncSlack,
                maxTimeoutRetryCount: maxTimeoutRetryCount);
            LeftOuterJoinBlock block = new LeftOuterJoinBlock(this.left, this.right, this.output, settings,
                timeProvider, null, NullLogger.Instance, CancellationToken.None);

            //create 2 DateTimes, shorter than the matching window
            DateTime interactionDateTime = timeProvider.UtcNow;
            DateTime observationDateTime =
                interactionDateTime.Add(experimentalUnitDuration - TimeSpan.FromMilliseconds(5));

            //add an interaction
            await this.left.SendAsync(SingleMessageInteractionBatch(interactionDateTime, "abc"),
                this.cancellationTokenSource.Token).AssertTrueAsync();

            //add an observation, out of the matching window
            await right.SendAsync(new Message()
            {
                EventId = "abc",
                EnqueuedTimeUtc = observationDateTime
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            // move time forwards
            await Task.Delay(this.experimentalUnitDuration);

            var joinedBatch = await output.ReceiveAsync(this.cancellationTokenSource.Token);
            Assert.AreEqual(1, joinedBatch.Messages.Count);
            Assert.AreEqual(2, joinedBatch.Messages[0].Count);
            Assert.AreEqual("abc", joinedBatch.Messages[0][0].EventId, "event ids should match");
            Assert.AreEqual("abc", joinedBatch.Messages[0][1].EventId, "event ids should match");
        }

        [DataTestMethod,
         Description("Test that a joined event is not available before 'experimental unit duration' time elapsed")]
        [TestCategory("Decision Service/Online Trainer")]
        [DataRow(true, 2)]
        [DataRow(false, 0)]
        public async Task LeftOuterJoinBlock_HoldJoinedEventsUntilExperimentalUnitDurationTimeElapsedAsync(
            bool addPuncSlack, int maxTimeoutRetryCount)
        {
            JoinerConfig settings = SetJoinerOptions(addPunctuationSlack: addPuncSlack,
                maxTimeoutRetryCount: maxTimeoutRetryCount);
            LeftOuterJoinBlock block = new LeftOuterJoinBlock(this.left, this.right, this.output, settings,
                timeProvider, null, NullLogger.Instance, CancellationToken.None);

            //add an interaction
            var interactionTimestamp = timeProvider.UtcNow;
            await this.left.SendAsync(SingleMessageInteractionBatch(interactionTimestamp, "abc"),
                this.cancellationTokenSource.Token).AssertTrueAsync();

            await right.SendAsync(new Message()
            {
                EventId = "abc",
                EnqueuedTimeUtc = timeProvider.UtcNow
            }, this.cancellationTokenSource.Token).AssertTrueAsync();


            // move time forward
            await Task.Delay(this.experimentalUnitDuration);

            var joinedBatch = await output.ReceiveAsync(this.cancellationTokenSource.Token);
            Assert.AreEqual(1, joinedBatch.Messages.Count);
            Assert.AreEqual(2, joinedBatch.Messages[0].Count);
            Assert.AreEqual("abc", joinedBatch.Messages[0][0].EventId, "event ids should match");
            Assert.AreEqual("abc", joinedBatch.Messages[0][1].EventId, "event ids should match");
        }

        [DataTestMethod, Description("Test that a joined event matched multiple observation events")]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task LeftOuterJoinBlock_MultipleRewardsAsync()
        {
            JoinerConfig settings = SetJoinerOptions();
            LeftOuterJoinBlock block = new LeftOuterJoinBlock(this.left, this.right, this.output, settings,
                timeProvider, null, NullLogger.Instance, CancellationToken.None);

            //add an interaction
            var interactionTimestamp = timeProvider.UtcNow;
            await this.left.SendAsync(SingleMessageInteractionBatch(interactionTimestamp, "abc"),
                this.cancellationTokenSource.Token).AssertTrueAsync();

            //add an observation
            await right.SendAsync(new Message()
            {
                EventId = "abc",
                EnqueuedTimeUtc = interactionTimestamp
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            //add an observation
            await right.SendAsync(new Message()
            {
                EventId = "abc",
                EnqueuedTimeUtc = interactionTimestamp.AddMilliseconds(1)
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            //add an observation
            await right.SendAsync(new Message()
            {
                EventId = "abc",
                EnqueuedTimeUtc = interactionTimestamp.AddMilliseconds(10)
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            //add an observation out of EUD windwo, so should not be joined
            await right.SendAsync(new Message()
            {
                EventId = "abc",
                EnqueuedTimeUtc = interactionTimestamp.Add(experimentalUnitDuration)
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            // move time forward
            await Task.Delay(this.experimentalUnitDuration);

            var joinedBatch = await output.ReceiveAsync(this.cancellationTokenSource.Token);
            Assert.AreEqual(1, joinedBatch.Messages.Count);
            Assert.AreEqual(4, joinedBatch.Messages[0].Count);
            Assert.AreEqual("abc", joinedBatch.Messages[0][0].EventId, "event ids should match");
            Assert.AreEqual("abc", joinedBatch.Messages[0][1].EventId, "event ids should match");
            Assert.AreEqual("abc", joinedBatch.Messages[0][2].EventId, "event ids should match");
            Assert.AreEqual("abc", joinedBatch.Messages[0][3].EventId, "event ids should match");
            Assert.AreEqual(1, block.DanglingObservationsCount);
        }

        [DataTestMethod,
         Description(
             "This test checks that an observation does not match with an interaction when the observation happens after the interaction but is out of the matching window")]
        [TestCategory("Decision Service/Online Trainer")]
        [DataRow(true, 2)]
        [DataRow(false, 0)]
        public async Task LeftOuterJoinBlock_DoesNotMatchWhenOutOfWindowAsync(bool addPuncSlack,
            int maxTimeoutRetryCount)
        {
            JoinerConfig settings = SetJoinerOptions(addPunctuationSlack: addPuncSlack,
                maxTimeoutRetryCount: maxTimeoutRetryCount);
            LeftOuterJoinBlock block = new LeftOuterJoinBlock(this.left, this.right, this.output, settings,
                timeProvider, null, NullLogger.Instance, CancellationToken.None);

            //create 2 DateTimes, longer than the matching window
            DateTime interactionDateTime = timeProvider.UtcNow;
            DateTime observationDateTime =
                interactionDateTime.Add(this.experimentalUnitDuration + TimeSpan.FromMilliseconds(5));

            //add an interaction
            await this.left.SendAsync(SingleMessageInteractionBatch(interactionDateTime, "abc"),
                this.cancellationTokenSource.Token).AssertTrueAsync();

            //add an observation, out of the matching window
            await this.right.SendAsync(new Message()
            {
                EventId = "abc",
                EnqueuedTimeUtc = observationDateTime
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            // Wait for the join to happen
            var joinedBatch = await output.ReceiveAsync(this.cancellationTokenSource.Token);
            Assert.AreEqual(1, joinedBatch.Messages.Count);
            Assert.AreEqual(1, joinedBatch.Messages[0].Count);
            Assert.AreEqual("abc", joinedBatch.Messages[0][0].EventId, "event ids should match");
            Assert.AreEqual(1, block.DanglingObservationsCount);
        }


        [DataTestMethod,
         Description(
             "This test checks that an observation matches with an interaction when the interaction happened before the observation in the backward matching window")]
        [TestCategory("Decision Service/Online Trainer")]
        [DataRow(true, 2)]
        [DataRow(false, 0)]
        public async Task LeftOuterJoinBlock_MatchWhenObservationHappensBeforeInteractionInBackwardWindowAsync(
            bool addPuncSlack, int maxTimeoutRetryCount)
        {
            JoinerConfig settings = SetJoinerOptions(addPunctuationSlack: addPuncSlack,
                maxTimeoutRetryCount: maxTimeoutRetryCount);
            LeftOuterJoinBlock block = new LeftOuterJoinBlock(this.left, this.right, this.output, settings,
                timeProvider, null, NullLogger.Instance, CancellationToken.None);

            //create 2 DateTimes, shorter than the backward matching window
            DateTime interactionDateTime = timeProvider.UtcNow;
            DateTime observationDateTime =
                interactionDateTime.Add(-settings.BackwardEventJoinWindowTimeSpan + TimeSpan.FromMilliseconds(5));

            //add an interaction
            await this.left.SendAsync(SingleMessageInteractionBatch(interactionDateTime, "abc"),
                this.cancellationTokenSource.Token).AssertTrueAsync();

            //add an observation, out of the matching window
            await this.right.SendAsync(new Message()
            {
                EventId = "abc",
                EnqueuedTimeUtc = observationDateTime
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            // Wait for the join to happen
            var joinedBatch = await output.ReceiveAsync(this.cancellationTokenSource.Token);
            Assert.AreEqual(1, joinedBatch.Messages.Count);
            Assert.AreEqual(2, joinedBatch.Messages[0].Count);
            Assert.AreEqual("abc", joinedBatch.Messages[0][0].EventId, "event ids should match");
            Assert.AreEqual("abc", joinedBatch.Messages[0][1].EventId, "event ids should match");
            Assert.AreEqual(0, block.DanglingObservationsCount);
        }

        [DataTestMethod,
         Description(
             "This test checks that an 2 observations match with an interactions when both observations are before interactions but within the backward matching window")]
        [TestCategory("Decision Service/Online Trainer")]
        [DataRow(true, 2)]
        [DataRow(false, 0)]
        public async Task
            LeftOuterJoinBlock_MatchEventsWhenTwoObservationsHappenBefore2InteractionsInBackwardWindowAsync(
                bool addPuncSlack, int maxTimeoutRetryCount)
        {
            JoinerConfig settings = SetJoinerOptions(addPunctuationSlack: addPuncSlack,
                maxTimeoutRetryCount: maxTimeoutRetryCount);
            LeftOuterJoinBlock block = new LeftOuterJoinBlock(this.left, this.right, this.output, settings,
                timeProvider, null, NullLogger.Instance, CancellationToken.None);

            //create 2 DateTimes, shorter than the backward matching window
            DateTime interactionDateTime1 = timeProvider.UtcNow;
            DateTime observationDateTime1 =
                interactionDateTime1.Add(-settings.BackwardEventJoinWindowTimeSpan + TimeSpan.FromMilliseconds(5));

            DateTime interactionDateTime2 = interactionDateTime1.Add(TimeSpan.FromMilliseconds(100));
            DateTime observationDateTime2 = observationDateTime1.Add(TimeSpan.FromMilliseconds(100));

            //add an interaction
            await this.left.SendAsync(SingleMessageInteractionBatch(interactionDateTime1, "abc"),
                this.cancellationTokenSource.Token).AssertTrueAsync();
            await this.left.SendAsync(SingleMessageInteractionBatch(interactionDateTime2, "def"),
                this.cancellationTokenSource.Token).AssertTrueAsync();

            //add an observation, out of the matching window
            await this.right.SendAsync(new Message()
            {
                EventId = "abc",
                EnqueuedTimeUtc = observationDateTime1
            }, this.cancellationTokenSource.Token).AssertTrueAsync();
            await this.right.SendAsync(new Message()
            {
                EventId = "def",
                EnqueuedTimeUtc = observationDateTime2
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            // move time forward
            await Task.Delay(this.experimentalUnitDuration + TimeSpan.FromMilliseconds(10));

            var joinedBatch = await output.ReceiveAsync(this.cancellationTokenSource.Token);
            Assert.AreEqual(1, joinedBatch.Messages.Count);
            Assert.AreEqual(2, joinedBatch.Messages[0].Count);
            Assert.AreEqual("abc", joinedBatch.Messages[0][0].EventId, "event ids should match");
            Assert.AreEqual("abc", joinedBatch.Messages[0][1].EventId, "event ids should match");

            joinedBatch = await output.ReceiveAsync(this.cancellationTokenSource.Token);
            Assert.AreEqual(1, joinedBatch.Messages.Count);
            Assert.AreEqual(2, joinedBatch.Messages[0].Count);
            Assert.AreEqual("def", joinedBatch.Messages[0][0].EventId, "event ids should match");
            Assert.AreEqual("def", joinedBatch.Messages[0][1].EventId, "event ids should match");
        }


        [DataTestMethod,
         Description(
             "This test checks that an observation does not match with an interaction that happened before the observation out of the backward matching window")]
        [TestCategory("Decision Service/Online Trainer")]
        [DataRow(true, 2)]
        [DataRow(false, 0)]
        public async Task
            LeftOuterJoinBlock_DoesNotMatchWhenObservationHappensBeforeInteractionOutOfBackwardWindowAsync(
                bool addPuncSlack, int maxTimeoutRetryCount)
        {
            JoinerConfig settings = SetJoinerOptions(addPunctuationSlack: addPuncSlack,
                maxTimeoutRetryCount: maxTimeoutRetryCount);
            LeftOuterJoinBlock block = new LeftOuterJoinBlock(this.left, this.right, this.output, settings,
                timeProvider, null, NullLogger.Instance, CancellationToken.None);

            //create 2 DateTimes, longer than the backward matching window
            DateTime interactionDateTime = timeProvider.UtcNow;
            DateTime observationDateTime =
                interactionDateTime.Add(-settings.BackwardEventJoinWindowTimeSpan - TimeSpan.FromMilliseconds(5));

            //add an interaction
            await this.left.SendAsync(SingleMessageInteractionBatch(interactionDateTime, "abc"),
                this.cancellationTokenSource.Token).AssertTrueAsync();

            //add an observation, out of the matching window
            await this.right.SendAsync(new Message()
            {
                EventId = "abc",
                EnqueuedTimeUtc = observationDateTime
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            // move time forward
            await Task.Delay(this.experimentalUnitDuration + settings.BackwardEventJoinWindowTimeSpan);

            var joinedBatch = await output.ReceiveAsync();
            Assert.AreEqual(1, joinedBatch.Messages.Count);
            Assert.AreEqual(1, joinedBatch.Messages[0].Count);
            Assert.AreEqual("abc", joinedBatch.Messages[0][0].EventId, "event ids should match");
        }

        //
        [DataTestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        [DataRow(true, 2)]
        [DataRow(false, 0)]
        public async Task LeftOuterJoinBlock_TryReceiveWithOneValidMatchAsync(bool addPuncSlack,
            int maxTimeoutRetryCount)
        {
            JoinerConfig settings = SetJoinerOptions(addPunctuationSlack: addPuncSlack,
                maxTimeoutRetryCount: maxTimeoutRetryCount);
            LeftOuterJoinBlock block = new LeftOuterJoinBlock(this.left, this.right, this.output, settings,
                timeProvider, null, NullLogger.Instance, CancellationToken.None);

            // add a left item
            var interactionTimestamp = timeProvider.UtcNow;
            await this.left.SendAsync(SingleMessageInteractionBatch(interactionTimestamp, "abc"),
                this.cancellationTokenSource.Token).AssertTrueAsync();


            // add a right item
            await this.right.SendAsync(new Message()
            {
                EventId = "abc",
                EnqueuedTimeUtc = timeProvider.UtcNow
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            // move time forward
            await Task.Delay(this.experimentalUnitDuration);

            // make sure it's valid
            var joinedBatch = await output.ReceiveAsync(this.cancellationTokenSource.Token);

            Assert.AreEqual(1, joinedBatch.Messages.Count);
            Assert.AreEqual(2, joinedBatch.Messages[0].Count);
            Assert.AreEqual("abc", joinedBatch.Messages[0][0].EventId, "event ids should match");
            Assert.AreEqual("abc", joinedBatch.Messages[0][1].EventId, "event ids should match");
        }

        //
        [DataTestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        [DataRow(true, 2)]
        [DataRow(false, 0)]
        public async Task LeftOuterJoinBlock_InteractionAndNoObservationsAsync(bool addPuncSlack,
            int maxTimeoutRetryCount)
        {
            JoinerConfig settings = SetJoinerOptions(addPunctuationSlack: addPuncSlack,
                maxTimeoutRetryCount: maxTimeoutRetryCount);
            LeftOuterJoinBlock block = new LeftOuterJoinBlock(this.left, this.right, this.output, settings,
                timeProvider, null, NullLogger.Instance, CancellationToken.None);

            // add a left item
            var interactionTimestamp = timeProvider.UtcNow;
            await this.left.SendAsync(SingleMessageInteractionBatch(interactionTimestamp, "abc"),
                this.cancellationTokenSource.Token).AssertTrueAsync();

            // move time forwards
            await Task.Delay(this.experimentalUnitDuration);

            // make sure it's valid
            var joinedBatch = await output.ReceiveAsync();
            Assert.AreEqual(1, joinedBatch.Messages.Count);
            Assert.AreEqual(1, joinedBatch.Messages[0].Count);
            Assert.AreEqual("abc", joinedBatch.Messages[0][0].EventId, "event ids should match");
        }

        //
        [DataTestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        [DataRow(true, 2)]
        [DataRow(false, 0)]
        public async Task LeftOuterJoinBlock_DanglingObservationsAsync(bool addPuncSlack, int maxTimeoutRetryCount)
        {
            JoinerConfig settings = SetJoinerOptions(addPunctuationSlack: addPuncSlack,
                maxTimeoutRetryCount: maxTimeoutRetryCount);
            LeftOuterJoinBlock block = new LeftOuterJoinBlock(this.left, this.right, this.output, settings,
                timeProvider, null, NullLogger.Instance, CancellationToken.None);

            // add a left item
            var interactionTimestamp = timeProvider.UtcNow;
            await this.left.SendAsync(SingleMessageInteractionBatch(interactionTimestamp, "abc"),
                this.cancellationTokenSource.Token).AssertTrueAsync();

            // now add some right items some of which match the interaction that's enqueued, and other's that don't
            await right.SendAsync(new Message()
            {
                EventId = "def",
                EnqueuedTimeUtc = timeProvider.UtcNow
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            await right.SendAsync(new Message()
            {
                EventId = "ghi",
                EnqueuedTimeUtc = timeProvider.UtcNow
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            await right.SendAsync(new Message()
            {
                EventId = "abc",
                EnqueuedTimeUtc = timeProvider.UtcNow
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            await Task.Delay(200);

            // make sure there are some dangling observations
            Assert.IsTrue(block.DanglingObservationsCount > 0, "Some dangling observations exist");

            // move time forwards
            await Task.Delay(this.experimentalUnitDuration);

            // make sure it's valid
            var joinedBatch = await output.ReceiveAsync(this.cancellationTokenSource.Token);
            Assert.AreEqual(1, joinedBatch.Messages.Count);
            Assert.AreEqual(2, joinedBatch.Messages[0].Count);
            Assert.AreEqual("abc", joinedBatch.Messages[0][0].EventId, "event ids should match");
            Assert.AreEqual("abc", joinedBatch.Messages[0][1].EventId, "event ids should match");

            await Task.Delay(TimeSpan.FromMilliseconds(200));

            Assert.IsFalse(output.TryReceive(out var _), "make sure there are no duplicate interactions");
        }

        [DataTestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        [DataRow(true)]
        [DataRow(false)]
        public async Task LeftOuterJoinBlock_JoinWithDanglingObservationsAsync(bool rewardWithinEUD)
        {
            JoinerConfig settings = SetJoinerOptions();
            LeftOuterJoinBlock block = new LeftOuterJoinBlock(this.left, this.right, this.output, settings,
                timeProvider, null, NullLogger.Instance, CancellationToken.None);

            var interactionTimestamp = timeProvider.UtcNow;
            await this.left.SendAsync(SingleMessageInteractionBatch(interactionTimestamp, "abc"),
                this.cancellationTokenSource.Token).AssertTrueAsync();
            await this.left.SendAsync(SingleMessageInteractionBatch(interactionTimestamp, "def"),
                this.cancellationTokenSource.Token).AssertTrueAsync();

            var rewardTimeStamp = rewardWithinEUD
                ? interactionTimestamp
                : interactionTimestamp.Add(this.experimentalUnitDuration);
            await right.SendAsync(new Message()
            {
                EventId = "def",
                EnqueuedTimeUtc = rewardTimeStamp
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            // move time forwards
            await Task.Delay(this.experimentalUnitDuration);

            // make sure it's valid
            var joinedBatch = await output.ReceiveAsync(this.cancellationTokenSource.Token);
            Assert.AreEqual(1, joinedBatch.Messages.Count);
            Assert.AreEqual(1, joinedBatch.Messages[0].Count);
            Assert.AreEqual("abc", joinedBatch.Messages[0][0].EventId, "event ids should match");

            if (rewardWithinEUD)
            {
                joinedBatch = await output.ReceiveAsync(this.cancellationTokenSource.Token);
                Assert.AreEqual(1, joinedBatch.Messages.Count);
                Assert.AreEqual(2, joinedBatch.Messages[0].Count);
                Assert.AreEqual("def", joinedBatch.Messages[0][0].EventId, "event ids should match");
                Assert.AreEqual("def", joinedBatch.Messages[0][1].EventId, "event ids should match");
                await Task.Delay(TimeSpan.FromMilliseconds(200));
                Assert.IsFalse(output.TryReceive(out var _), "make sure there are no duplicate interactions");
            }
            else
            {
                joinedBatch = await output.ReceiveAsync(this.cancellationTokenSource.Token);
                Assert.AreEqual(1, joinedBatch.Messages.Count);
                Assert.AreEqual(1, joinedBatch.Messages[0].Count);
                Assert.AreEqual("def", joinedBatch.Messages[0][0].EventId, "event ids should match");
                await Task.Delay(TimeSpan.FromMilliseconds(200));
                Assert.IsFalse(output.TryReceive(out var _), "make sure there are no duplicate interactions");
            }
        }

        [DataTestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        [DataRow(true, 2)]
        [DataRow(false, 0)]
        public async Task LeftOuterJoinBlock_DanglingObservationJsonTestAsync(bool addPuncSlack,
            int maxTimeoutRetryCount)
        {
            JoinerConfig settings = SetJoinerOptions(addPunctuationSlack: addPuncSlack,
                maxTimeoutRetryCount: maxTimeoutRetryCount);
            var now = timeProvider.UtcNow;
            LeftOuterJoinBlock block = new LeftOuterJoinBlock(this.left, this.right, this.output, settings,
                timeProvider, null, NullLogger.Instance, CancellationToken.None);

            // add a left item
            var interactionTimestamp = timeProvider.UtcNow;

            await this.left.SendAsync(SingleMessageInteractionBatch(interactionTimestamp, "abc"),
                this.cancellationTokenSource.Token).AssertTrueAsync();

            await this.right.SendAsync(new Message()
            {
                EventId = "dangling",
                EnqueuedTimeUtc = timeProvider.UtcNow - TimeSpan.FromMinutes(30)
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            await this.right.SendAsync(new Message()
            {
                EventId = "close",
                EnqueuedTimeUtc = timeProvider.UtcNow + this.experimentalUnitDuration + TimeSpan.FromMinutes(1)
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            await Task.Delay(this.experimentalUnitDuration);

            var joinedBatch = await this.output.ReceiveAsync(this.cancellationTokenSource.Token);
            Assert.AreEqual(1, joinedBatch.Messages.Count);
            Assert.AreEqual(1, joinedBatch.Messages[0].Count);
            Assert.AreEqual("abc", joinedBatch.Messages[0][0].EventId, "event ids should match");
            Assert.AreEqual(block.DanglingObservationsCount, 2);
            Assert.IsFalse(output.TryReceive(out var _), "make sure there are no duplicate interactions");
        }


        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        [DataRow(true, 2)]
        [DataRow(false, 0)]
        public async Task LeftOuterJoinBlock_ObservationReadyAsync(bool addPuncSlack, int maxTimeoutRetryCount)
        {
            JoinerConfig settings = SetJoinerOptions(addPunctuationSlack: addPuncSlack,
                maxTimeoutRetryCount: maxTimeoutRetryCount);
            LeftOuterJoinBlock block = new LeftOuterJoinBlock(this.left, this.right, this.output, settings,
                timeProvider, null, NullLogger.Instance, CancellationToken.None);

            var now = timeProvider.UtcNow;

            // get observation ready
            await right.SendAsync(new Message()
            {
                EventId = "abc",
                EnqueuedTimeUtc = now + TimeSpan.FromMilliseconds(100)
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            await Task.Delay(200);

            // push other interaction so observation get's consumed

            await this.left.SendAsync(SingleMessageInteractionBatch(now, "def"), this.cancellationTokenSource.Token)
                .AssertTrueAsync();
            await this.left.SendAsync(SingleMessageInteractionBatch(now + TimeSpan.FromMilliseconds(100), "abc"),
                this.cancellationTokenSource.Token).AssertTrueAsync();
            // move time forward
            await Task.Delay(this.experimentalUnitDuration + TimeSpan.FromSeconds(1));

            var joinedBatch = await output.ReceiveAsync(this.cancellationTokenSource.Token);
            Assert.AreEqual(1, joinedBatch.Messages.Count);
            Assert.AreEqual(1, joinedBatch.Messages[0].Count);
            Assert.AreEqual("def", joinedBatch.Messages[0][0].EventId, "event ids should match");

            joinedBatch = await output.ReceiveAsync(this.cancellationTokenSource.Token);
            Assert.AreEqual(1, joinedBatch.Messages.Count);
            Assert.AreEqual(2, joinedBatch.Messages[0].Count);
            Assert.AreEqual("abc", joinedBatch.Messages[0][0].EventId, "event ids should match");
            Assert.AreEqual("abc", joinedBatch.Messages[0][1].EventId, "event ids should match");
        }

        [DataTestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        [DataRow(true, 2)]
        [DataRow(false, 0)]
        public async Task LeftOuterJoinBlock_MultipleObservationsAsync(bool addPuncSlack, int maxTimeoutRetryCount)
        {
            JoinerConfig settings = SetJoinerOptions(addPunctuationSlack: addPuncSlack,
                maxTimeoutRetryCount: maxTimeoutRetryCount);
            LeftOuterJoinBlock block = new LeftOuterJoinBlock(this.left, this.right, this.output, settings,
                timeProvider, null, NullLogger.Instance, CancellationToken.None);

            await this.left.SendAsync(SingleMessageInteractionBatch(timeProvider.UtcNow, "abc"),
                this.cancellationTokenSource.Token).AssertTrueAsync();

            // send 2 observations
            await this.right.SendAsync(new Message()
            {
                EventId = "abc",
                EnqueuedTimeUtc = timeProvider.UtcNow,
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            await this.right.SendAsync(new Message()
            {
                EventId = "abc",
                EnqueuedTimeUtc = timeProvider.UtcNow,
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            await Task.Delay(this.experimentalUnitDuration + TimeSpan.FromSeconds(1));

            var joinedBatch = await output.ReceiveAsync(this.cancellationTokenSource.Token);
            Assert.AreEqual(1, joinedBatch.Messages.Count);
            Assert.AreEqual(3, joinedBatch.Messages[0].Count);
            Assert.AreEqual("abc", joinedBatch.Messages[0][0].EventId, "event ids should match");
            Assert.AreEqual("abc", joinedBatch.Messages[0][1].EventId, "event ids should match");
            Assert.AreEqual("abc", joinedBatch.Messages[0][2].EventId, "event ids should match");
        }

        [DataTestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        [DataRow(true, 2)]
        [DataRow(false, 0)]
        public async Task LeftOuterJoinBlock_InteractionBatchMultipleEventsAsync(bool addPuncSlack, int maxTimeoutRetryCount)
        {
            JoinerConfig settings = SetJoinerOptions(addPunctuationSlack: addPuncSlack,
                maxTimeoutRetryCount: maxTimeoutRetryCount);
            LeftOuterJoinBlock block = new LeftOuterJoinBlock(this.left, this.right, this.output, settings,
                timeProvider, null, NullLogger.Instance, CancellationToken.None);

            var batchTime = timeProvider.UtcNow;
            await this.left.SendAsync(new MessageBatch()
                {
                    EnqueuedTimeUtc = batchTime,
                    Messages = new List<Message>()
                    {
                        new()
                        {
                            EventId = "abc",
                            EnqueuedTimeUtc = batchTime,
                            DataSegment = null
                        },
                        new()
                        {
                            EventId = "def",
                            EnqueuedTimeUtc = batchTime,
                            DataSegment = null
                        },
                        new()
                        {
                            EventId = "ghi",
                            EnqueuedTimeUtc = batchTime,
                            DataSegment = null
                        }
                    }
                },
                this.cancellationTokenSource.Token).AssertTrueAsync();
            var batch2Time = timeProvider.UtcNow;
            await this.left.SendAsync(new MessageBatch()
                {
                    EnqueuedTimeUtc = batch2Time,
                    Messages = new List<Message>()
                    {
                        new()
                        {
                            EventId = "jkl",
                            EnqueuedTimeUtc = batch2Time,
                            DataSegment = null
                        }
                    }
                },
                this.cancellationTokenSource.Token).AssertTrueAsync();

            // send 2 observations
            await this.right.SendAsync(new Message()
            {
                EventId = "abc",
                EnqueuedTimeUtc = timeProvider.UtcNow,
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            await this.right.SendAsync(new Message()
            {
                EventId = "abc",
                EnqueuedTimeUtc = timeProvider.UtcNow,
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            await this.right.SendAsync(new Message()
            {
                EventId = "def",
                EnqueuedTimeUtc = timeProvider.UtcNow,
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            await this.right.SendAsync(new Message()
            {
                EventId = "unmatched",
                EnqueuedTimeUtc = timeProvider.UtcNow,
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            await this.right.SendAsync(new Message()
            {
                EventId = "jkl",
                EnqueuedTimeUtc = timeProvider.UtcNow,
            }, this.cancellationTokenSource.Token).AssertTrueAsync();

            await Task.Delay(this.experimentalUnitDuration + TimeSpan.FromSeconds(1));

            var joinedBatch = await output.ReceiveAsync(this.cancellationTokenSource.Token);
            Assert.AreEqual(3, joinedBatch.Messages.Count);
            Assert.AreEqual(3, joinedBatch.Messages[0].Count);
            Assert.AreEqual("abc", joinedBatch.Messages[0][0].EventId, "event ids should match");
            Assert.AreEqual("abc", joinedBatch.Messages[0][1].EventId, "event ids should match");
            Assert.AreEqual("abc", joinedBatch.Messages[0][2].EventId, "event ids should match");

            Assert.AreEqual(2, joinedBatch.Messages[1].Count);
            Assert.AreEqual("def", joinedBatch.Messages[1][0].EventId, "event ids should match");
            Assert.AreEqual("def", joinedBatch.Messages[1][1].EventId, "event ids should match");

            Assert.AreEqual(1, joinedBatch.Messages[2].Count);
            Assert.AreEqual("ghi", joinedBatch.Messages[2][0].EventId, "event ids should match");

            joinedBatch = await output.ReceiveAsync(this.cancellationTokenSource.Token);
            Assert.AreEqual(1, joinedBatch.Messages.Count);
            Assert.AreEqual(2, joinedBatch.Messages[0].Count);
            Assert.AreEqual("jkl", joinedBatch.Messages[0][0].EventId, "event ids should match");
            Assert.AreEqual("jkl", joinedBatch.Messages[0][1].EventId, "event ids should match");


            Assert.AreEqual(block.DanglingObservationsCount, 1);

            Assert.IsFalse(output.TryReceive(out var _), "make sure there are no duplicate interactions");
        }

        private JoinerConfig SetJoinerOptions(
            bool addPunctuationSlack = false,
            int maxTimeoutRetryCount = 0,
            int punctuationSlackMs = -1)
        {
            return new JoinerConfig
            {
                ExperimentalUnitDuration = this.experimentalUnitDuration,
                BackwardEventJoinWindowTimeSpan = this.backwardEventJoinWindowTimeSpan,
                AddPunctuationSlack = addPunctuationSlack,
                EventReceiveTimeoutMaxRetryCount = maxTimeoutRetryCount,
                PunctuationSlack = punctuationSlackMs == -1
                    ? this.punctuationSlack
                    : TimeSpan.FromMilliseconds(punctuationSlackMs),
                PunctuationTimeout = this.timeProviderIncrement,
            };
        }
    }
}