// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Tests
{
    [TestClass]
    public class ULongEncoderTest
    {
        private void Verify(string encoded, ulong decoded)
        {
            Assert.AreEqual(encoded, ULongEncoder.Encode(decoded), $"Encoding for '{decoded}' failed");
            Assert.IsTrue(ULongEncoder.TryDecode(encoded, out var value), "Only valid encoded values expected");
            Assert.AreEqual(decoded, value, "Decoding failed");
        }

        [TestMethod]
        [TestCategory("Decision Service/Front End")]
        public void ULongEncoder_Test()
        {
            Verify("0", 0);
            Verify("9", 9);
            Verify("a", 10);
            Verify("Z", 10+26+26-1);
            Verify("10", 10+26+26);
            Verify("102", (ulong)(Math.Pow(62, 2) + 2));
            Verify("8m0Kx", 123456789);
        }

        [TestMethod]
        [TestCategory("Decision Service/Front End")]
        public void ULongEncoder_TestInvalid()
        {
            Assert.IsFalse(ULongEncoder.TryDecode("123=", out var _), "Expected parsing error due to invalid char =");
        }
    }
}
