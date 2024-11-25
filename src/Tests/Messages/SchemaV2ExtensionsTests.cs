// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Google.FlatBuffers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DecisionService.Common.Trainer.FlatBuffers;
using reinforcement_learning.messages.flatbuff.v2;

namespace Tests.Messages.Flatbuffers
{
    [TestClass]
    public class SchemaV2ExtensionsTests
    {
        [TestMethod]
        [TestCategory("Decision Service/Online Trainer/Messages/SchemaV2")]
        public void TimeStampDateRoundTrip()
        {
            FlatBufferBuilder fbb = new FlatBufferBuilder(100);

            var date = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);

            var offset = date.SerializeV2TimeStamp(fbb);
            var bb = fbb.DataBuffer;

            /* This testing trick, explained.
             * Timestamp is a struct so it's encoded inline in tables with no headers.
             * To read one from a buffer we call __init with the offset to its first byte.
             * The `bb.Length - ffb.Offset` non-sense is because fbb::Offset tracks the reverse direction offset,
             *  from-end-to-start instead of start-to-end. If it's confusing, look your keyboard at the ASDF row.
             *  Normal, start-to-end offset, is the number of keys starting from Cap and moving right.
             *  FlatBufferBuilder, end-to-start offset, is the number of keys starting from Enter and moving legt.
             * PS: Sorry, but not sorry, DVORAK users.
             */
            TimeStamp stamp = default(TimeStamp);
            stamp.__init(bb.Length - fbb.Offset, bb);

            Assert.AreEqual(stamp.Year, date.Year);
            Assert.AreEqual(stamp.Month, date.Month);
            Assert.AreEqual(stamp.Day, date.Day);
            Assert.AreEqual(stamp.Hour, date.Hour);
            Assert.AreEqual(stamp.Minute, date.Minute);
            Assert.AreEqual(stamp.Second, date.Second);

        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer/Messages/SchemaV2")]
        public void JoinedEventCreateEventVector()
        {
            FlatBufferBuilder fbb = new FlatBufferBuilder(10);
            JoinedEvent.CreateEventVector(fbb, new byte[] { 1, 2, 3 });

            FlatBufferBuilder fbb2 = new FlatBufferBuilder(10);
            SchemaV2Extensions.JoinedEventCreateEventVector(fbb2, new byte[] { 1, 2, 3 });

            Assert.AreEqual(fbb.Offset, fbb2.Offset);

            CollectionAssert.AreEqual(fbb.DataBuffer.ToSizedArray(), fbb2.DataBuffer.ToSizedArray());
        }
    }
}
