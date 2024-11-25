// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.DecisionService.Common.Storage;
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
    public class AzBlobClientTests
    {
        private Mock<BlobClient> _mockBlobClient;
        private AzBlobClient _azBlobClient;

        [TestInitialize]
        public void Setup()
        {
            _mockBlobClient = new Mock<BlobClient>();
            _azBlobClient = new AzBlobClient(_mockBlobClient.Object);
        }

        [TestMethod]
        public async Task DownloadAsync_ShouldReturnBinaryData_Async()
        {
            var expectedContent = BinaryData.FromString("test content");
            var blobDownloadResult = BlobsModelFactory.BlobDownloadResult(content: expectedContent);

            _mockBlobClient.Setup(x => x.DownloadContentAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(blobDownloadResult, null));

            var result = await _azBlobClient.DownloadAsync();

            Assert.AreEqual(expectedContent, result);
        }

        [TestMethod]
        [ExpectedException(typeof(StorageException))]
        public async Task DownloadAsync_ShouldThrowStorageException_OnRequestFailedExceptionAsync()
        {
            _mockBlobClient.Setup(x => x.DownloadContentAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException("error"));

            await _azBlobClient.DownloadAsync();
        }

        [TestMethod]
        public async Task DownloadToAsync_ShouldCallBlobClientDownloadToAsync()
        {
            var stream = new MemoryStream();
            await _azBlobClient.DownloadToAsync(stream);

            _mockBlobClient.Verify(x => x.DownloadToAsync(stream, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        [ExpectedException(typeof(StorageException))]
        public async Task DownloadToAsync_ShouldThrowStorageException_OnRequestFailedExceptionAsync()
        {
            _mockBlobClient.Setup(x => x.DownloadToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException("error"));

            await _azBlobClient.DownloadToAsync(new MemoryStream());
        }

        [TestMethod]
        public async Task ExistsAsync_ShouldReturnTrue_WhenBlobExistsAsync()
        {
            _mockBlobClient.Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, null));

            var result = await _azBlobClient.ExistsAsync();

            Assert.IsTrue(result);
        }

        [TestMethod]
        [ExpectedException(typeof(StorageException))]
        public async Task ExistsAsync_ShouldThrowStorageException_OnRequestFailedExceptionAsync()
        {
            _mockBlobClient.Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException("error"));

            await _azBlobClient.ExistsAsync();
        }

        [TestMethod]
        public async Task UploadAsync_ShouldCallBlobClientUploadAsync()
        {
            var content = BinaryData.FromString("test content");
            _mockBlobClient.Setup(x => x.UploadAsync(content, It.IsAny<bool>(), It.IsAny<CancellationToken>()));
            await _azBlobClient.UploadAsync(content);
            _mockBlobClient.Verify(x => x.UploadAsync(content, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        [ExpectedException(typeof(StorageException))]
        public async Task UploadAsync_ShouldThrowStorageException_OnRequestFailedExceptionAsync()
        {
            var content = BinaryData.FromString("test content");
            _mockBlobClient.Setup(x => x.UploadAsync(content, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException("error"));
            await _azBlobClient.UploadAsync(content);
        }

        [TestMethod]
        public async Task UploadAsync_WithOverwrite_ShouldCallBlobClientUploadAsync()
        {
            var content = BinaryData.FromString("test content");
            await _azBlobClient.UploadAsync(content, true);

            _mockBlobClient.Verify(x => x.UploadAsync(content, true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task GetPropertiesAsync_ShouldReturnBlobPropertiesAsync()
        {
            var properties = BlobsModelFactory.BlobProperties();
            _mockBlobClient.Setup(x => x.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(properties, null));

            var result = await _azBlobClient.GetPropertiesAsync();

            Assert.IsNotNull(result);
        }

        [TestMethod]
        [ExpectedException(typeof(StorageException))]
        public async Task GetPropertiesAsync_ShouldThrowStorageException_OnRequestFailedExceptionAsync()
        {
            _mockBlobClient.Setup(x => x.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException("error"));

            await _azBlobClient.GetPropertiesAsync();
        }

        [TestMethod]
        public async Task DeleteIfExistsAsync_ShouldReturnTrue_WhenBlobDeletedAsync()
        {
            _mockBlobClient.Setup(x => x.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, null));

            var result = await _azBlobClient.DeleteIfExistsAsync("None");

            Assert.IsTrue(result);
        }

        [TestMethod]
        [ExpectedException(typeof(StorageException))]
        public async Task DeleteIfExistsAsync_ShouldThrowStorageException_OnRequestFailedExceptionAsync()
        {
            _mockBlobClient.Setup(x => x.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException("error"));

            await _azBlobClient.DeleteIfExistsAsync("None");
        }

        [TestMethod]
        public async Task StartCopyFromAsync_ShouldCallBlobClientStartCopyFromUriAsync()
        {
            var sourceBlob = new Mock<IBlobClient>();
            sourceBlob.Setup(x => x.Uri).Returns(new Uri("https://example.com/blob"));

            await _azBlobClient.StartCopyFromAsync(sourceBlob.Object);

            _mockBlobClient.Verify(x => x.StartCopyFromUriAsync(sourceBlob.Object.Uri, It.IsAny<IDictionary<string, string>>(), It.IsAny<AccessTier?>(), It.IsAny<BlobRequestConditions>(), It.IsAny<BlobRequestConditions>(), It.IsAny<RehydratePriority?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        [ExpectedException(typeof(StorageException))]
        public async Task StartCopyFromAsync_ShouldThrowStorageException_OnRequestFailedExceptionAsync()
        {
            var sourceBlob = new Mock<IBlobClient>();
            sourceBlob.Setup(x => x.Uri).Returns(new Uri("https://example.com/blob"));

            _mockBlobClient.Setup(x => x.StartCopyFromUriAsync(sourceBlob.Object.Uri, It.IsAny<IDictionary<string, string>>(), It.IsAny<AccessTier?>(), It.IsAny<BlobRequestConditions>(), It.IsAny<BlobRequestConditions>(), It.IsAny<RehydratePriority?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException("error"));

            await _azBlobClient.StartCopyFromAsync(sourceBlob.Object);
        }
    }
}