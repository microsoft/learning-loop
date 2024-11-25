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
    public sealed class FileReaderTests
    {
        private string TestFilePath { get; }

        public FileReaderTests()
        {
            TestFilePath = Path.Join(Directory.GetCurrentDirectory(), "ReadTestFile.txt");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            DeleteTestFile();
        }

        #region Read tests

        [TestMethod]
        public void Read_NullPathThrows()
        {
            var fileReader = new FileReader();
            Assert.ThrowsException<ArgumentNullException>(() =>
                fileReader.Read(null, new MemoryStream()));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("\t")]
        [DataRow("\n")]
        public void Read_WhitespacePathThrows(string path)
        {
            var fileReader = new FileReader();
            Assert.ThrowsException<ArgumentException>(() =>
                fileReader.Read(path, new MemoryStream()));
        }

        [DataTestMethod]
        [DynamicData(nameof(GenerateInvalidPaths), DynamicDataSourceType.Method)]
        public void Read_InvalidPathThrows(string invalidPath)
        {
            var fileReader = new FileReader();
            Assert.ThrowsException<ArgumentException>(() =>
                fileReader.Read(invalidPath, new MemoryStream())
            );
        }

        [TestMethod]
        public void Read_NullStreamThrows()
        {
            var fileReader = new FileReader();
            Assert.ThrowsException<ArgumentNullException>(() =>
                fileReader.Read(TestFilePath, null));
        }

        [DataTestMethod]
        public void Read_FileNotExistsThrows()
        {
            var notExistPath = Path.GetTempFileName();
            File.Delete(notExistPath);

            var fileReader = new FileReader();
            Assert.ThrowsException<FileNotFoundException>(
                () => fileReader.Read(notExistPath, new MemoryStream()));
        }

        [DataTestMethod]
        [DataRow(new byte[] { })]
        [DataRow(new byte[] {0})]
        [DataRow(new byte[] {1})]
        [DataRow(new byte[] {1, 2, 3, 4, 0, 1, byte.MaxValue, byte.MinValue})]
        public void Read_RawByteArray(byte[] expectedContent)
        {
            WriteTestFile(expectedContent);

            using var memoryStream = new MemoryStream();
            var fileReader = new FileReader();
            fileReader.Read(TestFilePath, memoryStream);

            CollectionAssert.AreEqual(expectedContent, memoryStream.ToArray());
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("\0")]
        [DataRow("\n")]
        [DataRow("hello world")]
        [DataRow("line 1\nline 2\nline3\n")]
        [DataRow("line 1\r\nline 2\r\nline 3\r\n")]
        public void Read_EncodedString(string expectedContent)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(expectedContent);
            WriteTestFile(bytes);

            var fileReader = new FileReader();
            var memoryStream = new MemoryStream();
            fileReader.Read(TestFilePath, memoryStream);

            Assert.AreEqual(expectedContent, Encoding.UTF8.GetString(memoryStream.ToArray()));
        }

        #endregion

        #region ReadAsync tests

        [TestMethod]
        public async Task ReadAsync_NullPathThrows_Async()
        {
            var fileReader = new FileReader();
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
                fileReader.ReadAsync(null, new MemoryStream()));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("\t")]
        [DataRow("\n")]
        public async Task ReadAsync_WhitespacePathThrows_Async(string path)
        {
            var fileReader = new FileReader();
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                fileReader.ReadAsync(path, new MemoryStream()));
        }

        [DataTestMethod]
        [DynamicData(nameof(GenerateInvalidPaths), DynamicDataSourceType.Method)]
        public async Task ReadAsync_InvalidPathThrows_Async(string invalidPath)
        {
            var fileReader = new FileReader();
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                fileReader.ReadAsync(invalidPath, new MemoryStream())
            );
        }

        [DataTestMethod]
        public async Task ReadAsync_FileNotExistsThrows_Async()
        {
            var notExistPath = Path.GetTempFileName();
            File.Delete(notExistPath);

            var fileReader = new FileReader();
            await Assert.ThrowsExceptionAsync<FileNotFoundException>(
                () => fileReader.ReadAsync(notExistPath, new MemoryStream()));
        }

        [TestMethod]
        public async Task ReadAsync_NullStreamThrows_Async()
        {
            var fileReader = new FileReader();
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
                fileReader.ReadAsync(TestFilePath, null));
        }

        [DataTestMethod]
        [DataRow(new byte[] { })]
        [DataRow(new byte[] {0})]
        [DataRow(new byte[] {1})]
        [DataRow(new byte[] {1, 2, 3, 4, 0, 1, byte.MaxValue, byte.MinValue})]
        public async Task ReadAsync_RawByteArray_Async(byte[] expectedContent)
        {
            WriteTestFile(expectedContent);

            await using var memoryStream = new MemoryStream();
            var fileReader = new FileReader();
            await fileReader.ReadAsync(TestFilePath, memoryStream);

            CollectionAssert.AreEqual(expectedContent, memoryStream.ToArray());
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("\0")]
        [DataRow("\n")]
        [DataRow("hello world")]
        [DataRow("line 1\nline 2\nline3\n")]
        [DataRow("line 1\r\nline 2\r\nline 3\r\n")]
        public async Task ReadAsync_EncodedString_Async(string expectedContent)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(expectedContent);
            WriteTestFile(bytes);

            var fileReader = new FileReader();
            var memoryStream = new MemoryStream();
            await fileReader.ReadAsync(TestFilePath, memoryStream);

            Assert.AreEqual(
                expectedContent,
                Encoding.UTF8.GetString(memoryStream.ToArray()));
        }

        #endregion

        #region Test Helpers

        private static IEnumerable<object[]> GenerateInvalidPaths()
        {
            return TestUtils.GenerateInvalidPaths();
        }

        private void WriteTestFile(byte[] content)
        {
            using var fs = new FileStream(TestFilePath, FileMode.Create, FileAccess.Write);
            fs.Write(content);
        }

        private void DeleteTestFile()
        {
            if (!File.Exists(TestFilePath)) return;
            File.Delete(TestFilePath);
        }

        #endregion
    }
}
