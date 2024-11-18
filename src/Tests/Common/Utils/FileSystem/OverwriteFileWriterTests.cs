// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DecisionService.Common.Utils.FileSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Common.Utils.FileSystem
{
    [TestClass]
    public class OverwriteFileWriterTests
    {
        private string TestFilePath { get; }

        public OverwriteFileWriterTests()
        {
            TestFilePath = Path.Join(Directory.GetCurrentDirectory(), "WriteTestFile.txt");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            DeleteTestFile();
        }

        #region Write Tests

        [TestMethod]
        public void Write_NullPathThrows()
        {
            var fileWriter = new OverwriteFileWriter();
            Assert.ThrowsException<ArgumentNullException>(() =>
                fileWriter.Write(null, new byte[] {0}));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("\t")]
        [DataRow("\n")]
        public void Write_WhitespacePathThrows(string path)
        {
            var fileWriter = new OverwriteFileWriter();
            Assert.ThrowsException<ArgumentException>(() =>
                fileWriter.Write(path, new byte[] {0}));
        }

        [DataTestMethod]
        [DynamicData(nameof(GenerateInvalidPaths), DynamicDataSourceType.Method)]
        public void Write_InvalidPathThrows(string invalidPath)
        {
            var fileWriter = new OverwriteFileWriter();
            Assert.ThrowsException<ArgumentException>(
                () => fileWriter.Write(invalidPath, new byte[] {0}));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow(new byte[] { })]
        public void Write_NoBytesDoesNotCreateFile(byte[] emptyContent)
        {
            var fileWriter = new OverwriteFileWriter();
            fileWriter.Write(TestFilePath, emptyContent);
            Assert.IsFalse(TestFileExists());
        }

        [DataTestMethod]
        [DataRow(new byte[] {0})]
        [DataRow(new byte[] {1})]
        [DataRow(new byte[] {1, 2, 3, 4, 0, 1, byte.MaxValue, byte.MinValue})]
        public void Write_RawByteArray(byte[] expectedContent)
        {
            var fileWriter = new OverwriteFileWriter();
            fileWriter.Write(TestFilePath, expectedContent);

            Assert.IsTrue(TestFileExists());
            CollectionAssert.AreEqual(expectedContent, ReadTestFile());
        }

        [DataTestMethod]
        [DataRow("\0")]
        [DataRow("\n")]
        [DataRow("hello world")]
        [DataRow("line 1\nline 2\nline3\n")]
        [DataRow("line 1\r\nline 2\r\nline 3\r\n")]
        public void Write_EncodedString(string expectedContent)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(expectedContent);
            var fileWriter = new OverwriteFileWriter();
            fileWriter.Write(TestFilePath, bytes);

            Assert.IsTrue(TestFileExists());
            Assert.AreEqual(expectedContent, Encoding.UTF8.GetString(ReadTestFile()));
        }

        [TestMethod]
        public void Write_OverwritesExistingFile()
        {
            byte[] initialBytes = {1, 2, 3, 4};
            var fileWriter = new OverwriteFileWriter();
            fileWriter.Write(TestFilePath, initialBytes);
            byte[] expectedBytes = {7, 8, 9, 10};
            fileWriter.Write(TestFilePath, expectedBytes);

            Assert.IsTrue(TestFileExists());
            CollectionAssert.AreEqual(expectedBytes, ReadTestFile());
        }

        #endregion

        #region WriteAsync Tests

        [TestMethod]
        public async Task WriteAsync_NullPathThrows_Async()
        {
            var fileWriter = new OverwriteFileWriter();
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
                fileWriter.WriteAsync(null, new byte[] {0}));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("\t")]
        [DataRow("\n")]
        public async Task WriteAsync_WhitespacePathThrows_Async(string path)
        {
            var fileWriter = new OverwriteFileWriter();
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                fileWriter.WriteAsync(path, new byte[] {0}));
        }

        [DataTestMethod]
        [DynamicData(nameof(GenerateInvalidPaths), DynamicDataSourceType.Method)]
        public async Task WriteAsync_InvalidPathThrows_Async(string invalidPath)
        {
            var fileWriter = new OverwriteFileWriter();
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => fileWriter.WriteAsync(invalidPath, new byte[] {0}));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow(new byte[] { })]
        public async Task WriteAsync_NoBytesDoesNotCreateFile_Async(byte[] emptyContent)
        {
            var fileWriter = new OverwriteFileWriter();
            await fileWriter.WriteAsync(TestFilePath, emptyContent);
            Assert.IsFalse(TestFileExists());
        }

        [DataTestMethod]
        [DataRow(new byte[] {0})]
        [DataRow(new byte[] {1})]
        [DataRow(new byte[] {1, 2, 3, 4, 0, 1, byte.MaxValue, byte.MinValue})]
        public async Task WriteAsync_RawByteArray_Async(byte[] expectedContent)
        {
            var fileWriter = new OverwriteFileWriter();
            await fileWriter.WriteAsync(TestFilePath, expectedContent);

            Assert.IsTrue(TestFileExists());
            CollectionAssert.AreEqual(expectedContent, ReadTestFile());
        }

        [DataTestMethod]
        [DataRow("\0")]
        [DataRow("\n")]
        [DataRow("hello world")]
        [DataRow("line 1\nline 2\nline3\n")]
        [DataRow("line 1\r\nline 2\r\nline 3\r\n")]
        public async Task WriteAsync_EncodedString_Async(string expectedContent)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(expectedContent);
            var fileWriter = new OverwriteFileWriter();
            await fileWriter.WriteAsync(TestFilePath, bytes);

            Assert.IsTrue(TestFileExists());
            Assert.AreEqual(expectedContent, Encoding.UTF8.GetString(ReadTestFile()));
        }

        [TestMethod]
        public async Task WriteAsync_OverwritesExistingFile_Async()
        {
            byte[] initialBytes = {1, 2, 3, 4};
            var fileWriter = new OverwriteFileWriter();
            await fileWriter.WriteAsync(TestFilePath, initialBytes);
            byte[] expectedBytes = {7, 8, 9, 10};
            await fileWriter.WriteAsync(TestFilePath, expectedBytes);

            Assert.IsTrue(TestFileExists());
            CollectionAssert.AreEqual(expectedBytes, ReadTestFile());
        }

        #endregion

        #region Clear Tests

        [TestMethod]
        public void Clear_NullPathThrows()
        {
            var fileWriter = new OverwriteFileWriter();
            Assert.ThrowsException<ArgumentNullException>(() =>
                fileWriter.Clear(null));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("\t")]
        [DataRow("\n")]
        public void Clear_WhitespacePathThrows(string path)
        {
            var fileWriter = new OverwriteFileWriter();
            Assert.ThrowsException<ArgumentException>(() =>
                fileWriter.Clear(path));
        }

        [DataTestMethod]
        [DynamicData(nameof(GenerateInvalidPaths), DynamicDataSourceType.Method)]
        public void Clear_InvalidPathThrows(string invalidPath)
        {
            var fileWriter = new OverwriteFileWriter();
            Assert.ThrowsException<ArgumentException>(
                () => fileWriter.Clear(invalidPath));
        }

        [TestMethod]
        public void Clear_DoesNotCreateFile()
        {
            var fileWriter = new OverwriteFileWriter();
            fileWriter.Clear(TestFilePath);

            Assert.IsFalse(TestFileExists());
        }

        [TestMethod]
        public void Clear_FileHasNoContent()
        {
            var fileWriter = new OverwriteFileWriter();
            fileWriter.Write(TestFilePath, new byte[] {1, 2, 3});
            fileWriter.Clear(TestFilePath);

            Assert.IsTrue(TestFileExists());
            Assert.IsTrue(ReadTestFile().Length == 0);
        }

        #endregion

        #region ClearAsync Tests

        [TestMethod]
        public async Task ClearAsync_NullPathThrows_Async()
        {
            var fileWriter = new OverwriteFileWriter();
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
                fileWriter.ClearAsync(null));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("\t")]
        [DataRow("\n")]
        public async Task ClearAsync_WhitespacePathThrows_Async(string path)
        {
            var fileWriter = new OverwriteFileWriter();
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                fileWriter.ClearAsync(path));
        }

        [DataTestMethod]
        [DynamicData(nameof(GenerateInvalidPaths), DynamicDataSourceType.Method)]
        public async Task ClearAsync_InvalidPathThrows_Async(string invalidPath)
        {
            var fileWriter = new OverwriteFileWriter();
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => fileWriter.ClearAsync(invalidPath));
        }

        [TestMethod]
        public async Task ClearAsync_DoesNotCreateFile_Async()
        {
            var fileWriter = new OverwriteFileWriter();
            await fileWriter.ClearAsync(TestFilePath);

            Assert.IsFalse(TestFileExists());
        }

        [TestMethod]
        public async Task ClearAsync_FileHasNoContent_Async()
        {
            var fileWriter = new OverwriteFileWriter();
            await fileWriter.WriteAsync(TestFilePath, new byte[] {1, 2, 3});
            await fileWriter.ClearAsync(TestFilePath);

            Assert.IsTrue(TestFileExists());
            Assert.IsTrue(ReadTestFile().Length == 0);
        }

        #endregion

        #region Test Helpers

        private static IEnumerable<object[]> GenerateInvalidPaths()
        {
            return TestUtils.GenerateInvalidPaths();
        }

        private byte[] ReadTestFile()
        {
            using var fs = new FileStream(TestFilePath, FileMode.Open, FileAccess.Read);
            using var memStream = new MemoryStream();
            fs.CopyTo(memStream);
            return memStream.ToArray();
        }

        private bool TestFileExists()
        {
            return File.Exists(TestFilePath);
        }

        private void DeleteTestFile()
        {
            if (!File.Exists(TestFilePath)) return;
            File.Delete(TestFilePath);
        }

        #endregion
    }
}
