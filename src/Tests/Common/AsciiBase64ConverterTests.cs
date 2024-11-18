// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Tests.Common
{
    [TestClass]
    public class AsciiBase64ConverterTests
    {
        [DataTestMethod]
        [DataRow("0000")]
        [DataRow("0001")]
        [DataRow("")]
        [DataRow("Hello World")]
        [DataRow("model0000")]
        [DataRow("model0001")]
        [DataRow("metad0000")]
        [DataRow("metad0001")]
        public void EncodeDecode(string s)
        {
            string encoded = AsciiBase64Converter.Encode(s);
            string decoded = AsciiBase64Converter.Decode(encoded);
            Assert.AreEqual(s, decoded);
        }

        [TestMethod]
        public void Encode_Exceptions()
        {
            Assert.ThrowsException<ArgumentNullException>(() => AsciiBase64Converter.Encode(null));
        }

        [TestMethod]
        public void Decode_Exceptions()
        {
            Assert.ThrowsException<ArgumentNullException>(() => AsciiBase64Converter.Decode(null));
            Assert.ThrowsException<FormatException>(() => AsciiBase64Converter.Decode("?M4"));
            Assert.ThrowsException<FormatException>(() => AsciiBase64Converter.Decode("\u8888"));
        }
    }
}
