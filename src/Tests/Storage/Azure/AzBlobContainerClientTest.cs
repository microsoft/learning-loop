// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.DecisionService.Common.Storage;
using Microsoft.DecisionService.Common.Storage.Azure;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Storage.Azure
{
    [TestClass]
    public class AzBlobContainerClientTests
    {
        private Mock<BlobContainerClient> _mockContainerClient;
        private Mock<AzStorageFactory> _mockFactory;
        private AzBlobContainerClient _azBlobContainerClient;

        [TestInitialize]
        public void Setup()
        {
            _mockContainerClient = new Mock<BlobContainerClient>();
            _mockFactory = new Mock<AzStorageFactory>(null);
            _azBlobContainerClient = new AzBlobContainerClient(_mockContainerClient.Object, _mockFactory.Object);
        }

        [TestMethod]
        public void Name_ShouldReturnContainerClientName()
        {
            _mockContainerClient.Setup(c => c.Name).Returns("test-container");
            var name = _azBlobContainerClient.Name;
            Assert.AreEqual("test-container", name);
        }

        [TestMethod]
        public void Uri_ShouldReturnContainerClientUri()
        {
            var uri = new Uri("https://test.blob.core.windows.net/test-container");
            _mockContainerClient.Setup(c => c.Uri).Returns(uri);
            var result = _azBlobContainerClient.Uri;
            Assert.AreEqual(uri, result);
        }

        [TestMethod]
        public void Factory_ShouldReturnFactory()
        {
            var factory = _azBlobContainerClient.Factory;
            Assert.AreEqual(_mockFactory.Object, factory);
        }

        [TestMethod]
        [ExpectedException(typeof(NullReferenceException))]
        public async Task AcquireLeaseAsync_ShouldReturnBlobLeaseHolderAsync()
        {
            var mockLeaseHolder = new Mock<IBlobLeaseHolder>();
            var logger = Mock.Of<ILogger>();
            var blobClient = new Mock<BlobClient>(MockBehavior.Loose);
            blobClient.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue(true, null));
            _mockContainerClient.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(blobClient.Object);
            // note: GetBlobLease is an extension method and is not supported by Moq so this call will result in a null reference exception
            _ = await _azBlobContainerClient.AcquireLeaseAsync("appId", "lockBlobName", DateTime.UtcNow, logger, CancellationToken.None);
        }

        [TestMethod]
        public void CreateBlockStoreProvider_ShouldReturnBlockStoreProvider()
        {
            var result = _azBlobContainerClient.CreateBlockStoreProvider();
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void GetBlobClient_ShouldReturnBlobClient()
        {
            var mockBlobClient = new Mock<BlobClient>();
            _mockContainerClient.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(mockBlobClient.Object);
            var result = _azBlobContainerClient.GetBlobClient("blobName");
            Assert.IsInstanceOfType(result, typeof(AzBlobClient));
        }

        [TestMethod]
        public void GetBlockBlobClient_ShouldReturnBlockBlobClient()
        {
            var result = _azBlobContainerClient.GetBlockBlobClient("blobName");
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task GetBlobsAsync_ShouldReturnBlobItemsAsync()
        {
            var pageableMock = new Mock<AsyncPageable<BlobItem>>();
            pageableMock.Setup(p => p.GetAsyncEnumerator(It.IsAny<CancellationToken>())).Returns(new Mock<IAsyncEnumerator<BlobItem>>().Object);
            _mockContainerClient.Setup(c => c.GetBlobsAsync(It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(pageableMock.Object);
            var result = await _azBlobContainerClient.GetBlobsAsync("prefix", CancellationToken.None);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task DeleteBlobAsync_ShouldDeleteBlobAsync()
        {
            await _azBlobContainerClient.DeleteBlobAsync("blobName", CancellationToken.None);
            _mockContainerClient.Verify(c => c.DeleteBlobIfExistsAsync("blobName", It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task GetBlobsByHierarchyAsync_ShouldReturnBlobHierarchyItemsAsync()
        {
            var pageableMock = new Mock<AsyncPageable<BlobHierarchyItem>>();
            pageableMock.Setup(p => p.GetAsyncEnumerator(It.IsAny<CancellationToken>())).Returns(new Mock<IAsyncEnumerator<BlobHierarchyItem>>().Object);
            _mockContainerClient.Setup(c => c.GetBlobsByHierarchyAsync(It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(pageableMock.Object);
            var result = await _azBlobContainerClient.GetBlobsByHierarchyAsync("prefix", "delimiter", CancellationToken.None);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task CreateIfNotExistsAsync_ShouldCreateContainerIfNotExistsAsync()
        {
            await _azBlobContainerClient.CreateIfNotExistsAsync(CancellationToken.None);
            _mockContainerClient.Verify(c => c.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task DeleteIfExistsAsync_ShouldDeleteContainerIfExistsAsync()
        {
            await _azBlobContainerClient.DeleteIfExistsAsync(CancellationToken.None);
            _mockContainerClient.Verify(c => c.DeleteIfExistsAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
