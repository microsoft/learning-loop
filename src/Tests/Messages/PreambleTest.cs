// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Trainer.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Messages
{
    [TestClass]
    public class PreambleTest
    {
        [TestMethod]
        [Timeout(10000)]
        [TestCategory("Decision Service/Online Trainer/Preamble")]
        public void Preamble_Read()
        {
            const ushort msgType = 2;
            const uint msgSize = 1024;
            var body = GenerateNetworkPreamble(msgType, msgSize);

            var pre = new MessagePreamble();
            Assert.IsTrue(pre.ReadFromBytes(body));
            Assert.AreEqual(pre.MsgType, msgType);
            Assert.AreEqual(pre.MsgSize, msgSize);
        }

        [TestMethod]
        [Timeout(10000)]
        [TestCategory("Decision Service/Online Trainer/Preamble")]
        public void Preamble_Write()
        {
            ushort msgType = 2;
            uint msgSize = 1024;
            var body = GenerateNetworkPreamble(msgType, msgSize);

            var pre = new MessagePreamble {MsgType = msgType, MsgSize = msgSize};
            byte [] preamble = pre.ToBytes();

            for (var i = 0; i < 8; i++)
            {
                Assert.AreEqual(preamble[i],body.Array[i]);
            }
        }

        private ArraySegment<byte> GenerateNetworkPreamble(ushort msgType, uint msgSize)
        {
            byte[] preamble =
            {
                0,0,        
                0,0,
                0,0,0,0,
                1,2,3,4,    // Misc Data
            };

            Array.Copy(BitConverter.GetBytes(msgType), 0, preamble, 2, 2);
            Array.Copy(BitConverter.GetBytes(msgSize), 0, preamble, 4, 4);

            // Network order is BigEndian.  Convert to BigEndian if necessary 
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(preamble, 2, 2);
                Array.Reverse(preamble, 4, 4);
            }

            return new ArraySegment<byte>(preamble);
        }
    }
}
