// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DecisionService.Common.Utils.FileSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCore.Tests.Common.Utils.FileSystem
{
    [TestClass]
    public class PathUtilsTests
    {
        [TestMethod]
        public void CheckPath_NullPathThrows()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                PathUtils.CheckPath(null));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("\t")]
        [DataRow("\n")]
        public void CheckPath_WhitespacePathThrows(string whitespacePath)
        {
            Assert.ThrowsException<ArgumentException>(() =>
                PathUtils.CheckPath(whitespacePath));
        }

        [DataTestMethod]
        [DynamicData(nameof(GenerateInvalidPaths), DynamicDataSourceType.Method)]
        public void CheckPath_InvalidPathThrows(string invalidPath)
        {
            Assert.ThrowsException<ArgumentException>(() =>
                PathUtils.CheckPath(invalidPath));
        }

        [TestMethod]
        public void CheckPath_ValidPathDoesNotThrow()
        {
            string validPath = Path.Join("/foo", "bar", "cat.txt");
            PathUtils.CheckPath(validPath);
        }

        #region Test Helpers

        private static IEnumerable<object[]> GenerateInvalidPaths()
        {
            return TestUtils.GenerateInvalidPaths();
        }

        #endregion
    }
}
