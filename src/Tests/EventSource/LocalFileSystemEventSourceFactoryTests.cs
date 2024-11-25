// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// using Microsoft.DecisionService.Common.Data;
// using Microsoft.DecisionService.Common.Trainer.Data;
// using Microsoft.DecisionService.Common.Trainer.EventSource;
// using Microsoft.DecisionService.Common.Trainer.Join;
// using Microsoft.VisualStudio.TestTools.UnitTesting;
// using System;
// using System.Globalization;
// using System.IO;
// using System.Linq;
// using System.Threading;
// using System.Threading.Tasks.Dataflow;
//
// namespace Tests.EventSource
// {
//     [TestClass]
//     [DoNotParallelize]
//     public class LocalFileSystemEventSourceFactoryTests
//     {
//         public enum SourceType
//         {
//             Ranking = 0,
//             Reward = 1
//         }
//         private static Random rnd;
//
//         private static string rankingEventDirectory;
//         private static string rewardEventDirectory;
//
//         private static LocalFileSystemEventSourceFactory fsEventSourceFactory;
//         private static BufferBlock<IEvent> outputBufferBlock;
//
//         [ClassInitialize]
//         public static void TestClassInit(TestContext ctx)
//         {
//             rnd = new Random(1);
//             rankingEventDirectory = Path.Combine(Path.GetTempPath(), "RankingEventSource");
//             rewardEventDirectory = Path.Combine(Path.GetTempPath(), "RewardEventSource");
//             Directory.CreateDirectory(rankingEventDirectory);
//             Directory.CreateDirectory(rewardEventDirectory);
//
//             WriteEventData(SourceType.Ranking, new DateTime(2018, 1, 1, 6, 0, 0));
//             WriteEventData(SourceType.Ranking, new DateTime(2018, 1, 5, 7, 30, 30));
//             WriteEventData(SourceType.Ranking, new DateTime(2018, 1, 5, 9, 0, 0));
//             WriteEventData(SourceType.Ranking, new DateTime(2018, 1, 6, 8, 25, 0));
//
//             WriteEventData(SourceType.Reward, new DateTime(2018, 1, 1, 6, 0, 0));
//             WriteEventData(SourceType.Reward, new DateTime(2018, 1, 5, 7, 30, 30));
//             WriteEventData(SourceType.Reward, new DateTime(2018, 1, 5, 9, 0, 0));
//             WriteEventData(SourceType.Reward, new DateTime(2018, 1, 6, 8, 25, 0));
//
//             fsEventSourceFactory = new LocalFileSystemEventSourceFactory(rankingEventDirectory, rewardEventDirectory);
//             outputBufferBlock = new BufferBlock<IEvent>();
//         }
//
//         [ClassCleanup]
//         public static void TestClassCleanup()
//         {
//             Directory.Delete(rankingEventDirectory, true);
//             Directory.Delete(rewardEventDirectory, true);
//         }
//
//         private static void WriteEventData(SourceType srcType, DateTime time)
//         {
//             var directory = srcType == SourceType.Ranking ? rankingEventDirectory : rewardEventDirectory;
//             // writes three events to a file in the format of
//             // [event size (4 bytes)][timestamps (8 bytes)][event data (event size)]
//             // event size is a random length from 1 to 10 bytes
//             string fileName = time.ToString(FileEventListener.FileNameFormat);
//             DateTime lastEvntTime = time;
//             using (var fs = new FileStream(Path.Combine(directory, fileName), FileMode.Append, FileAccess.Write, FileShare.Read))
//             {
//                 using (var bwriter = new BinaryWriter(fs))
//                 {
//                     for (int i = 0; i < 3; i++)
//                     {
//                         int rndLen = rnd.Next(1, 1000);
//                         var preamble = new MessagePreamble { MsgType = 2, MsgSize = (uint)rndLen };
//                         byte[] data = new byte[rndLen];
//                         rnd.NextBytes(data);
//                         bwriter.Write(preamble.ToBytes());
//                         bwriter.Write(data);
//                     }
//                 }
//             }
//         }
//
//         private static DateTime GetLatestFileDate(SourceType srcType)
//         {
//             var directory = srcType == SourceType.Ranking ? rankingEventDirectory : rewardEventDirectory;
//             string lastFile = Path.GetFileName(Directory.EnumerateFiles(directory).Last());
//             return DateTime.ParseExact(lastFile, FileEventListener.FileNameFormat, CultureInfo.InvariantCulture);
//         }
//
//         [TestMethod]
//         [ExpectedException(typeof(ArgumentNullException))]
//         public void TestEventSourceLocal_SourceEnvironmentNull()
//         {
//             var factory = new LocalFileSystemEventSourceFactory();
//         }
//
//         [DataTestMethod]
//         [DataRow(SourceType.Ranking)]
//         [DataRow(SourceType.Reward)]
//         public void TestEventSourceLocal_CreateBlockWithOffset(SourceType srcType)
//         {
//             var refBlock = CreateBlockWithOffset(srcType, null);
//
//             refBlock.Receive();
//             var expectedEvent = refBlock.Receive();
//
//             var offsetBlock = CreateBlockWithOffset(srcType, expectedEvent.Position.Offset);
//             var evnt = offsetBlock.Receive();
//             AssertEventsAreEqual(expectedEvent, evnt);
//
//             FlushAndCompleteBlock(refBlock);
//             FlushAndCompleteBlock(offsetBlock);
//         }
//
//         [DataTestMethod]
//         [DataRow(SourceType.Ranking)]
//         [DataRow(SourceType.Reward)]
//         public void TestEventSourceLocal_CreateBlockWithNullOffset(SourceType srcType)
//         {
//             var refBlock = CreateBlockWithDateTime(srcType, DateTime.MinValue);
//             var expectedEvent1 = refBlock.Receive();
//             var expectedEvent2 = refBlock.Receive();
//             var expectedEvent3 = refBlock.Receive();
//
//             var nullBlock = CreateBlockWithOffset(srcType, null);
//             var event1 = nullBlock.Receive();
//             var event2 = nullBlock.Receive();
//             var event3 = nullBlock.Receive();
//             AssertEventsAreEqual(expectedEvent1, event1);
//             AssertEventsAreEqual(expectedEvent2, event2);
//             AssertEventsAreEqual(expectedEvent3, event3);
//
//             FlushAndCompleteBlock(refBlock);
//             FlushAndCompleteBlock(nullBlock);
//         }
//
//         [DataTestMethod]
//         [DataRow(SourceType.Ranking)]
//         [DataRow(SourceType.Reward)]
//         public void TestEventSourceLocal_CreateBlockWithDateTime(SourceType srcType)
//         {
//             var refBlock = CreateBlockWithOffset(srcType, null);
//             refBlock.Receive();
//             refBlock.Receive();
//             refBlock.Receive();
//             var expectedEvent = refBlock.Receive();
//             var dateTimeBlock = CreateBlockWithDateTime(srcType, expectedEvent.Position.EnqueuedTimeUtc);
//             var dateTimeEvent = dateTimeBlock.Receive();
//             AssertEventsAreEqual(expectedEvent, dateTimeEvent);
//             FlushAndCompleteBlock(refBlock);
//             FlushAndCompleteBlock(dateTimeBlock);
//         }
//
//         [DataTestMethod]
//         [DataRow(SourceType.Ranking)]
//         [DataRow(SourceType.Reward)]
//         public void TestEventSourceLocal_NewEventFileWritten(SourceType srcType)
//         {
//             var today = DateTime.UtcNow.Date;
//             var yesterday = DateTime.UtcNow.AddDays(-1).Date;
//
//             // block should not queue any historical events at this point
//             var refBlock = CreateBlockWithDateTime(srcType, today);
//             Assert.AreEqual(0, ((BufferBlock<IEvent>)refBlock).Count);
//
//             // new events file should trigger read
//             WriteEventData(srcType, today);
//             var expectedEvnt1 = refBlock.Receive();
//             var expectedEvnt2 = refBlock.Receive();
//             var expectedEvnt3 = refBlock.Receive();
//
//             // events just written should be read as historical events
//             var block = CreateBlockWithDateTime(srcType, yesterday);
//             var evnt1 = block.Receive();
//             var evnt2 = block.Receive();
//             var evnt3 = block.Receive();
//
//             AssertEventsAreEqual(expectedEvnt1, evnt1);
//             AssertEventsAreEqual(expectedEvnt2, evnt2);
//             AssertEventsAreEqual(expectedEvnt3, evnt3);
//
//             FlushAndCompleteBlock(refBlock);
//             FlushAndCompleteBlock(block);
//         }
//
//
//         [DataTestMethod]
//         [DataRow(SourceType.Ranking)]
//         [DataRow(SourceType.Reward)]
//         public void TestEventSourceLocal_NewEventsAppended(SourceType srcType)
//         {
//             var day = GetLatestFileDate(srcType);
//             var block = CreateBlockWithDateTime(srcType, day);
//
//             // new events appended to 2018/1/6
//             WriteEventData(srcType, day.AddHours(12));
//
//             SpinWait.SpinUntil(() => ((BufferBlock<IEvent>)block).Count == 6, 1000);
//
//             FlushAndCompleteBlock(block);
//         }
//
//         private void AssertEventsAreEqual(IEvent expectedEvent, IEvent actualEvent)
//         {
//             CollectionAssert.AreEqual(expectedEvent.Bytes.Array, actualEvent.Bytes.Array);
//             // TODO take out date when flatbuffer includes true enqueued time
//             Assert.AreEqual(expectedEvent.Position.EnqueuedTimeUtc.Date, actualEvent.Position.EnqueuedTimeUtc.Date);
//             Assert.AreEqual(expectedEvent.Position.Offset, actualEvent.Position.Offset);
//         }
//
//         private ISourceBlock<IEvent> CreateBlockWithDateTime(SourceType srcType, DateTime dateTime)
//         {
//             ISourceBlock<IEvent> block = null;
//             switch (srcType)
//             {
//                 case SourceType.Ranking:
//                     block = fsEventSourceFactory.CreateRankingSource(dateTime);
//                     break;
//                 case SourceType.Reward:
//                     block = fsEventSourceFactory.CreateOutcomeSource(dateTime);
//                     break;
//                 default:
//                     throw new ArgumentException("Please specify SourceType as ranking or reward");
//             }
//             return block;
//         }
//
//         private ISourceBlock<IEvent> CreateBlockWithOffset(SourceType srcType, string offset)
//         {
//             ISourceBlock<IEvent> block = null;
//             switch (srcType)
//             {
//                 case SourceType.Ranking:
//                     block = fsEventSourceFactory.CreateRankingSource(offset);
//                     break;
//                 case SourceType.Reward:
//                     block = fsEventSourceFactory.CreateOutcomeSource(offset);
//                     break;
//             }
//             return block;
//         }
//
//         private void FlushAndCompleteBlock(ISourceBlock<IEvent> block)
//         {
//             block.LinkTo(outputBufferBlock);
//             block.Complete();
//             block.Completion.GetAwaiter().GetResult();
//         }
//
//     }
// }
