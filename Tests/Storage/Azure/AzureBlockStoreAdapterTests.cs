// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using global::Microsoft.DecisionService.Common.Storage;
using Microsoft.DecisionService.Common.Storage.Azure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Storage.Azure
{
    [TestClass]
    public class AzureBlockStoreAdapterTests
    {
        private Mock<BlockBlobClient> mockBlockBlobClient;
        private AzureBlockStoreAdapter adapter;

        [TestInitialize]
        public void Setup()
        {
            mockBlockBlobClient = new Mock<BlockBlobClient>(MockBehavior.Loose);
            mockBlockBlobClient.Setup(x => x.BlockBlobMaxStageBlockBytes).Returns(50 * 1024);
            adapter = AzureBlockStoreAdapter.CreateWithBlob(mockBlockBlobClient.Object);
        }

        [TestMethod]
        public void TestCreateWithName_ValidInput_ReturnsAdapter()
        {
            // while the blob client is created, we are not connecting to the actual storage account
            var container = new BlobContainerClient(new Uri("http://localhost"), new BlobClientOptions());
            var blob = AzureBlockStoreAdapter.CreateWithName(container, "testblob");
            Assert.IsNotNull(blob);
        }

        [TestMethod]
        public void TestCreateWithBlob_ValidInput_ReturnsAdapter()
        {
            var result = AzureBlockStoreAdapter.CreateWithBlob(mockBlockBlobClient.Object);
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task TestExistsAsync_ValidInput_ReturnsTrueAsync()
        {
            mockBlockBlobClient.Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue(true, null));
            var result = await adapter.ExistsAsync(CancellationToken.None);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task TestExistsAsync_RequestFailedException_ThrowsStorageExceptionAsync()
        {
            mockBlockBlobClient.Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new RequestFailedException("Error"));
            var exception = await Assert.ThrowsExceptionAsync<StorageException>(() => adapter.ExistsAsync(CancellationToken.None));
            Assert.AreEqual("Error", exception.Message);
        }

        [TestMethod]
        public async Task TestWriteBlockAsync_ValidInput_WritesBlockAsync()
        {
            string blockName = "testBlock";
            using var stream = new MemoryStream(new byte[adapter.MinBlockSizeInBytes]);
            await adapter.WriteBlockAsync(blockName, stream, CancellationToken.None);
            mockBlockBlobClient.Verify(x => x.StageBlockAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<BlockBlobStageBlockOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task TestWriteBlockAsync_RequestFailedException_ThrowsStorageExceptionAsync()
        {
            string blockName = "testBlock";
            using var stream = new MemoryStream(new byte[adapter.MinBlockSizeInBytes]);
            mockBlockBlobClient.Setup(x => x.StageBlockAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<BlockBlobStageBlockOptions>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RequestFailedException("Error"));
            var exception = await Assert.ThrowsExceptionAsync<StorageException>(() => adapter.WriteBlockAsync(blockName, stream, CancellationToken.None));
            Assert.AreEqual("Error", exception.Message);
        }

        [TestMethod]
        public async Task TestWriteAsync_ValidInput_UploadsStreamAsync()
        {
            using var stream = new MemoryStream(new byte[adapter.MinBlockSizeInBytes]);
            await adapter.WriteAsync(stream, CancellationToken.None);
            mockBlockBlobClient.Verify(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobHttpHeaders>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobRequestConditions>(), It.IsAny<AccessTier?>(), It.IsAny<IProgress<long>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task TestWriteAsync_RequestFailedException_ThrowsStorageExceptionAsync()
        {
            using var stream = new MemoryStream(new byte[adapter.MinBlockSizeInBytes]);
            mockBlockBlobClient.Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobHttpHeaders>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobRequestConditions>(), It.IsAny<AccessTier?>(), It.IsAny<IProgress<long>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RequestFailedException("Error"));
            var exception = await Assert.ThrowsExceptionAsync<StorageException>(() => adapter.WriteAsync(stream, CancellationToken.None));
            Assert.AreEqual("Error", exception.Message);
        }

        [TestMethod]
        public async Task TestCommitBlocksAsync_ValidInput_CommitsBlocksAsync()
        {
            var blockIds = new List<string> { "block1", "block2" };
            await adapter.CommitBlocksAsync(blockIds, CancellationToken.None);
            mockBlockBlobClient.Verify(x => x.CommitBlockListAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<BlobHttpHeaders>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobRequestConditions>(), It.IsAny<AccessTier?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task TestCommitBlocksAsync_RequestFailedException_ThrowsStorageExceptionAsync()
        {
            var blockIds = new List<string> { "block1", "block2" };
            mockBlockBlobClient.Setup(x => x.CommitBlockListAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<BlobHttpHeaders>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobRequestConditions>(), It.IsAny<AccessTier?>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RequestFailedException("Error"));
            var exception = await Assert.ThrowsExceptionAsync<StorageException>(() => adapter.CommitBlocksAsync(blockIds, CancellationToken.None));
            Assert.AreEqual("Error", exception.Message);
        }

        [TestMethod]
        public async Task TestGetBlockInfoListAsync_ValidInput_ReturnsBlockInfoListAsync()
        {
            var blockListMock = new Mock<BlockList>();
            mockBlockBlobClient.Setup(x => x.GetBlockListAsync(It.IsAny<BlockListTypes>(), It.IsAny<string>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue(blockListMock.Object, null));
            var result = await adapter.GetBlockInfoListAsync("Committed", CancellationToken.None);
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task TestGetBlockInfoListAsync_RequestFailedException_ThrowsStorageExceptionAsync()
        {
            mockBlockBlobClient.Setup(x => x.GetBlockListAsync(It.IsAny<BlockListTypes>(), It.IsAny<string>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RequestFailedException("Error"));
            var exception = await Assert.ThrowsExceptionAsync<StorageException>(() => adapter.GetBlockInfoListAsync("Committed", CancellationToken.None));
            Assert.AreEqual("Error", exception.Message);
        }

        [TestMethod]
        public async Task TestReadBlockAsync_InvalidBlockType_ThrowsArgumentExceptionAsync()
        {
            using var writeStream = new MemoryStream();
            var exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => adapter.ReadBlockAsync(null, writeStream, CancellationToken.None));
            Assert.AreEqual($"block is not runtime type AzureListBlockItemAdapter (Parameter 'block')", exception.Message);
        }

        [TestMethod]
        public async Task TestReadBlockToAsync_ValidInput_ReadsBlockToStreamAsync()
        {
            var writeStream = new MemoryStream();

            await adapter.ReadBlockToAsync(writeStream, CancellationToken.None);

            mockBlockBlobClient.Verify(x => x.DownloadToAsync(writeStream, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task TestReadBlockToAsync_RequestFailedException_ThrowsStorageExceptionAsync()
        {
            var writeStream = new MemoryStream();
            mockBlockBlobClient.Setup(x => x.DownloadToAsync(writeStream, It.IsAny<CancellationToken>())).ThrowsAsync(new RequestFailedException("Error"));

            var exception = await Assert.ThrowsExceptionAsync<StorageException>(() => adapter.ReadBlockToAsync(writeStream, CancellationToken.None));
            Assert.AreEqual("Error", exception.Message);
        }

        [TestMethod]
        public async Task TestGetPropertiesAsync_ValidInput_ReturnsPropertiesAsync()
        {
            var properties = BlobsModelFactory.BlobProperties();
            mockBlockBlobClient.Setup(x => x.GetPropertiesAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue(properties, null));

            var result = await adapter.GetPropertiesAsync(CancellationToken.None);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task TestGetPropertiesAsync_RequestFailedException_ThrowsStorageExceptionAsync()
        {
            mockBlockBlobClient.Setup(x => x.GetPropertiesAsync(null, It.IsAny<CancellationToken>())).ThrowsAsync(new RequestFailedException("Error"));

            var exception = await Assert.ThrowsExceptionAsync<StorageException>(() => adapter.GetPropertiesAsync(CancellationToken.None));
            Assert.AreEqual("Error", exception.Message);
        }

        [TestMethod]
        public async Task TestDeleteIfExistsAsync_ValidInput_DeletesBlobAsync()
        {
            mockBlockBlobClient.Setup(x => x.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue(true, null));
            await adapter.DeleteIfExistsAsync(CancellationToken.None);
            mockBlockBlobClient.Verify(x => x.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task TestDeleteIfExistsAsync_RequestFailedException_ThrowsStorageExceptionAsync()
        {
            mockBlockBlobClient.Setup(x => x.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RequestFailedException("Error"));
            var exception = await Assert.ThrowsExceptionAsync<StorageException>(() => adapter.DeleteIfExistsAsync(CancellationToken.None));
            Assert.AreEqual("Error", exception.Message);
        }
    }
}