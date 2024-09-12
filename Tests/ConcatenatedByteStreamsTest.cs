// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.DecisionService.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tests
{
    [TestClass]
    public class ConcatenatedByteStreamsTest
    {
        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public void ConcatenatedByteStreams_SingleByteArray()
        {
            var input = new ConcatenatedByteStreams(new[]
            {
                new ArraySegment<byte>(
                    array: new byte[] { 1, 2, 3, 4, 5 },
                    offset: 0,
                    count: 3)
            });

            var buffer = new byte[1024];
            Assert.AreEqual(2, input.Read(buffer, 0, 2));
            Assert.AreEqual(1, buffer[0]);
            Assert.AreEqual(2, buffer[1]);

            Assert.AreEqual(1, input.Read(buffer, 0, 10));
            Assert.AreEqual(3, buffer[0]);
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public void ConcatenatedByteStreams_TwoByteArray()
        {
            var input = new ConcatenatedByteStreams(new[]
            {
                new ArraySegment<byte>(new byte[] { 0, 1, 2, 3, 4, 5 }),
                new ArraySegment<byte>(new byte[] { 6, 7, 8 })
            });

            using (var memStream = new MemoryStream())
            {
                input.CopyTo(memStream);

                CollectionAssert.AreEqual(
                    Enumerable.Range(0, 9).Select(x => (byte)x).ToArray(),
                    memStream.ToArray());
            }
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public void ConcatenatedByteStreams_Empty_Segment()
        {
            var arrays = new List<ArraySegment<byte>>() {
                new ArraySegment<byte>(new byte[] { 1, 1, 1 }),
                ArraySegment<byte>.Empty,
                new ArraySegment<byte>(new byte[] { 2, 2, 2 }),
                ArraySegment<byte>.Empty,
            };

            var input = new ConcatenatedByteStreams(arrays);

            Assert.AreEqual(6, input.Length);
            Assert.AreEqual(0, input.Position);

            byte[] data = new byte[10];
            int res = input.Read(data, 0, 10);

            Assert.AreEqual(6, res);
            Assert.AreEqual(1, data[0]);
            Assert.AreEqual(2, data[3]);
        }


        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public void ConcatenatedByteStreams_Recursion_in_Read()
        {
            const int size = 100_000;
            var arrays = new List<ArraySegment<byte>>();
            for (int i = 0; i < size; ++i)
            {
                arrays.Add(new ArraySegment<byte>(new byte[] { (byte)(i & 0xff) }));
            }

            var input = new ConcatenatedByteStreams(arrays);

            Assert.AreEqual(size, input.Length);

            byte[] data = new byte[size];
            int res = input.Read(data, 0, size);

            Assert.AreEqual(size, res);
            for (int i = 0; i < size; ++i)
            {
                Assert.AreEqual((byte)(i & 0xff), data[i]);
            }

        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public void ConcatenatedByteStreams_Seek()
        {
            var arrays = new List<ArraySegment<byte>>();

            // total virtual array is 0,1,2,3,4,... 
            var total = 0;
            for (int i = 1; i < 6; i++)
            {
                arrays.Add(new ArraySegment<byte>(
                    array: Enumerable.Range(total, i).Select(x => (byte)x).ToArray(),
                    offset: 0,
                    count: i
                ));

                total += i;
            }

            var input = new ConcatenatedByteStreams(arrays);

            var buffer = new byte[5];
            for (int i = 0; i < total; i++)
            {
                input.Seek(i, SeekOrigin.Begin);

                var bytesRead = input.Read(buffer, 0, 5);

                // we read at n
                // expect n, n+1, n+2, n+3, n+4
                for (int j = 0; j < bytesRead; j++)
                    Assert.AreEqual(i + j, buffer[j]);
            }
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public void ConcatenatedByteStreams_LengthIsRespected()
        {
            var arrays = new List<ArraySegment<byte>>()
            {
                new ArraySegment<byte>(
                    array: new byte[] { 1, 2, 3 },
                    offset: 1,
                    count: 1
                ),
                new ArraySegment<byte>(
                    array: new byte[] { 4, 5, 6},
                    offset: 1,
                    count: 1
                )
            };

            var stream = new ConcatenatedByteStreams(arrays);

            Assert.AreEqual(2, stream.Length);
            byte[] data = new byte[6];
            int read = stream.Read(data);
            Assert.AreEqual(2, read);
            CollectionAssert.AreEqual(new byte[] { 2, 5, 0, 0, 0, 0 }, data);
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public void ConcatenatedByteStreams_PositionDoesSeek()
        {

            var arrays = new List<ArraySegment<byte>>()
            {
                new ArraySegment<byte>(new byte[] { 1, 2, 3 }),
                new ArraySegment<byte>(new byte[] { 4, 5, 6 }),
            };

            var stream = new ConcatenatedByteStreams(arrays);
            Assert.AreEqual(6, stream.Length);
            Assert.AreEqual(0, stream.Position);

            stream.Position = 3;

            byte[] data = new byte[6];
            int read = stream.Read(data);
            Assert.AreEqual(3, read);
            Assert.AreEqual(6, stream.Position);
            CollectionAssert.AreEqual(new byte[] { 4, 5, 6, 0, 0, 0 }, data);
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public void ConcatenatedByteStreams_PositionDoesSeek2()
        {

            var arrays = new List<ArraySegment<byte>>()
            {
                new ArraySegment<byte>(new byte[] { 1, 2, 3 }),
                new ArraySegment<byte>(new byte[] { 4, 5, 6 }),
            };

            var stream = new ConcatenatedByteStreams(arrays);
            Assert.AreEqual(6, stream.Length);
            Assert.AreEqual(0, stream.Position);

            byte[] data = new byte[4];
            int read = stream.Read(data);
            Assert.AreEqual(4, read);
            Assert.AreEqual(4, stream.Position);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4}, data);

            stream.Position = 0;
            read = stream.Read(data);
            Assert.AreEqual(4, read);
            Assert.AreEqual(4, stream.Position);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, data);

        }
    }
}
