// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Google.FlatBuffers;
using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Trainer.FlatBuffers;
using Microsoft.DecisionService.OnlineTrainer.Join;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using reinforcement_learning.messages.flatbuff.v2;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.DecisionService.Common;
using CheckpointInfo = reinforcement_learning.messages.flatbuff.v2.CheckpointInfo;

namespace Tests.Messages.Flatbuffers
{
    [TestClass]
    public class BinaryFileTests
    {
         [TestMethod]
         [TestCategory("Decision Service/Online Trainer/FlatBuffers/BinaryFile")]
         public void BinaryBuilder_SerializeInteraction()
         {
             BinaryLogBuilder blb = new BinaryLogBuilder(10);

             blb.AddEventPayload(new byte[] { 1, 2, 3 }, new DateTime(2020, 2, 2));
             blb.AddEventPayload(new byte[] { 4, 5, 6, 7, 8 }, new DateTime(2020, 2, 3));

             var msg = blb.FinishEventMessage();

             //ensure it's 8 bytes aligned
             Assert.AreEqual(0, msg.Count % 8);

             //ensure it has the right message id
             ReadOnlySpan<int> span = MemoryMarshal.Cast<byte, int>(msg);
             Assert.AreEqual(BinaryLogBuilder.EVENT_MESSAGE_ID, span[0]);
             Assert.AreEqual(msg.Count - 8, span[1]);

             var bb = new ByteBuffer(msg.Array, msg.Offset + 8);
             JoinedPayload payload = JoinedPayload.GetRootAsJoinedPayload(bb);

             Assert.AreEqual(2, payload.EventsLength);

             Assert.IsTrue(payload.Events(0).HasValue);
             var evt0 = payload.Events(0).Value;
             Assert.IsTrue(evt0.Timestamp.HasValue);
             Assert.AreEqual(new DateTime(2020, 2, 2), evt0.Timestamp.Value.ToDateTime());

             Assert.AreEqual(3, evt0.EventLength);
             CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, evt0.GetEventBytes().Value.ToArray());

             //shorter check for second evt
             Assert.IsTrue(payload.Events(1).HasValue);
             var evt1 = payload.Events(1).Value;

             Assert.AreEqual(5, evt1.EventLength);
         }


         [TestMethod]
         [TestCategory("Decision Service/Online Trainer/FlatBuffers/BinaryFile")]
         public void BinaryBuilder_SerializeFileHeader()
         {
             var headerDict = new Dictionary<string, string>()
             {
                 {"joiner-version", "0.1" },
                 { "test-prop", "test-value" },
             };
             var res = new BinaryLogBuilder(BinaryLogBuilder.FILE_HEADER_SIZE_GUESTIMATE);
             res.AddFileHeader(headerDict);

             var msg = res.FinishFileHeader();

             //ensure it's 8 bytes aligned
             Assert.AreEqual(0, msg.Count % 8);

             //keep the guestimate honest.
             Assert.IsTrue(msg.Array.Length <= BinaryLogBuilder.FILE_HEADER_SIZE_GUESTIMATE);

             //ensure it has the right message id
             ReadOnlySpan<int> span = MemoryMarshal.Cast<byte, int>(msg);
             Assert.AreEqual(BinaryLogBuilder.FILE_HEADER_MESSAGE_ID, span[0]);
             Assert.AreEqual(msg.Count - 8, span[1]);

             var bb = new ByteBuffer(msg.Array, msg.Offset + 8);
             FileHeader header = FileHeader.GetRootAsFileHeader(bb);

             Assert.AreEqual(2, header.PropertiesLength);
         }

         [TestMethod]
         [TestCategory("Decision Service/Online Trainer/FlatBuffers/BinaryFile")]
         public void BinaryBuilder_SerializeCheckpoint()
         {
             Microsoft.DecisionService.Common.Trainer.FlatBuffers.CheckpointInfo info = new()
             {
                 FbRewardType = RewardFunctionType.Average,
                 DefaultReward = 0.5f,
             };

             var res = new BinaryLogBuilder(BinaryLogBuilder.CHECKPOINT_SIZE_GUESTIMATE);
             res.AddCheckpointInfo(info);

             var msg = res.FinishCheckpointInfo();

             //ensure it's 8 bytes aligned
             Assert.AreEqual(0, msg.Count % 8);

             //lets keep the guestimate honest
             Assert.IsTrue(msg.Array.Length <= BinaryLogBuilder.CHECKPOINT_SIZE_GUESTIMATE);

             //ensure it has the right message id
             ReadOnlySpan<int> span = MemoryMarshal.Cast<byte, int>(msg);
             Assert.AreEqual(BinaryLogBuilder.CHECKPOINT_MESSAGE_ID, span[0]);
             Assert.AreEqual(msg.Count - 8, span[1]);

             var bb = new ByteBuffer(msg.Array, msg.Offset + 8);
             var checkpoint = CheckpointInfo.GetRootAsCheckpointInfo(bb);

             Assert.AreEqual(RewardFunctionType.Average, checkpoint.RewardFunctionType);
             Assert.AreEqual(0.5f, checkpoint.DefaultReward);
         }

         [TestMethod]
         [TestCategory("Decision Service/Online Trainer/FlatBuffers/BinaryFile")]
         public void BinaryBuilder_SerializeLogHeader_Only()
         {
             BinaryLogHeaderBuilder builder = new BinaryLogHeaderBuilder();
             var msg = builder.Finish();
             //file magic only shows up if you add a file header
             Assert.AreEqual(0, msg.Count);
         }


         [TestMethod]
         [TestCategory("Decision Service/Online Trainer/FlatBuffers/BinaryFile")]
         public void BinaryBuilder_SerializeLogHeader_AddCheckpointOnly()
         {
             BinaryLogHeaderBuilder builder = new BinaryLogHeaderBuilder();
             Microsoft.DecisionService.Common.Trainer.FlatBuffers.CheckpointInfo info = new()
             {
                 FbRewardType = RewardFunctionType.Median,
                 DefaultReward = 0.8f,
             };

             builder.AddCheckpointInfo(info);
             var msg = builder.Finish();

             ReadOnlySpan<int> span = MemoryMarshal.Cast<byte, int>(msg);
             Assert.AreEqual(BinaryLogBuilder.CHECKPOINT_MESSAGE_ID, span[0]);
             int cpSize = span[1];
             Assert.AreEqual(0, cpSize % 8);
             Assert.AreEqual(cpSize + 8, msg.Count);


             var bb = new ByteBuffer(msg.Array, msg.Offset + 8);
             var checkpoint = CheckpointInfo.GetRootAsCheckpointInfo(bb);
             Assert.AreEqual(RewardFunctionType.Median, checkpoint.RewardFunctionType);
         }

         [TestMethod]
         [TestCategory("Decision Service/Online Trainer/FlatBuffers/BinaryFile")]
         public void BinaryBuilder_SerializeLogHeader_AddEverything()
         {
             BinaryLogHeaderBuilder builder = new BinaryLogHeaderBuilder();
             var headerDict = new Dictionary<string, string>()
             {
                 {"joiner-version", "0.1" },
                 { "test-prop", "test-value" },
                 { "other", "val" },
             };
             builder.AddFileHeader(headerDict);

             Microsoft.DecisionService.Common.Trainer.FlatBuffers.CheckpointInfo info = new()
             {
                 FbRewardType = RewardFunctionType.Median,
                 DefaultReward = 0.8f,
             };

             builder.AddCheckpointInfo(info);
             var msg = builder.Finish();

             ReadOnlySpan<int> span = MemoryMarshal.Cast<byte, int>(msg);
             Assert.AreEqual(BinaryLogHeaderBuilder.FILE_MAGIC, span[0]);
             Assert.AreEqual(BinaryLogHeaderBuilder.FILE_VERSION, span[1]);

             //first is file header
             Assert.AreEqual(BinaryLogBuilder.FILE_HEADER_MESSAGE_ID, span[2]);
             int headerSize = span[3];
             Assert.IsTrue(headerSize + 16 < msg.Count);
             Assert.AreEqual(0, headerSize % 8);

             //second is checkpoint
             int cpOff = headerSize + 16; //8 for file header + 8 for message header

             // we do cpOff/4 in the next two statements since cpOff is in bytes and span indexes ints
             Assert.AreEqual(BinaryLogBuilder.CHECKPOINT_MESSAGE_ID, span[cpOff/4]);
             int cpSize = span[cpOff/4 + 1];
             Assert.AreEqual(0, cpSize % 8);
             Assert.AreEqual(cpOff + 8 + cpSize, msg.Count);


             var bb = new ByteBuffer(msg.Array, msg.Offset + 16);
             FileHeader header = FileHeader.GetRootAsFileHeader(bb);
             Assert.AreEqual(3, header.PropertiesLength);

             bb = new ByteBuffer(msg.Array, cpOff + 8);
             var checkpoint = CheckpointInfo.GetRootAsCheckpointInfo(bb);
             Assert.AreEqual(RewardFunctionType.Median, checkpoint.RewardFunctionType);
         }
     }
}

