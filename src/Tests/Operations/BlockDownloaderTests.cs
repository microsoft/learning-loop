// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// using Microsoft.Azure.Storage;
// using Microsoft.Azure.Storage.Blob;
// using Microsoft.DecisionService.Common;
// using Microsoft.DecisionService.Common.Error;
// using Microsoft.DecisionService.Instrumentation;
// using Microsoft.DecisionService.OnlineTrainer;
// using Microsoft.DecisionService.OnlineTrainer.Operations;
// using Microsoft.DecisionService.OnlineTrainer.Storage;
// using Microsoft.VisualStudio.TestTools.UnitTesting;
// using Moq;
// using System;
// using System.Collections.Generic;
// using System.Threading;
// using System.Threading.Tasks;
// using Azure.Storage.Blobs;
// using Microsoft.Extensions.Logging.Abstractions;
//
// namespace Tests.Operations
// {
//     [TestClass]
//     public class BlockDownloaderTests
//     {
//         private const int timeout_10s = 10000;
//
//         [TestMethod]
//         [Timeout(timeout_10s)]
//         public async Task BlockDownloader_GetBlobClient_StorageException_ResumeAsync()
//         {
//             Mock<BlobContainerClient> containerMock = new Mock<BlobContainerClient>(new Uri("http://mytest"));
//             containerMock.Setup(c => c.GetBlobClient(It.IsAny<string>())).Throws<StorageException>();
//
//             var cancellationTokenSource = new CancellationTokenSource();
//
//             // Create BlockDownloader and start to run
//             BlockDownloader downloader = new BlockDownloader(null, containerMock.Object, cancellationTokenSource.Token, DateTime.Now, 1, NullLogger.Instance);
//
//             await Task.Delay(TimeSpan.FromSeconds(3)); // this gives time for BlockDownloader for a few retries.
//
//             // Verification
//             containerMock.Verify(a => a.GetBlobClient(It.IsAny<string>()), Times.AtLeastOnce, "GetBlobClient should be called at least once.");
//             // loggerMock.Verify(l => l.LogException(It.IsAny<StorageException>(), It.IsAny<string>(), PersonalizerInternalErrorCode.TrainerCheckpointFailure.ToString(),
//             //     It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<TracingLevel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.AtLeast(1), "StorageException should be logged.");
//
//             Assert.IsTrue(TaskStatus.WaitingForActivation.Equals(downloader.Completion.Status)
//                 || TaskStatus.WaitingToRun.Equals(downloader.Completion.Status)
//                 || TaskStatus.Running.Equals(downloader.Completion.Status), "BlockDownloader should resume upon StorageException");
//
//             cancellationTokenSource.Cancel();
//             await Task.Delay(TimeSpan.FromSeconds(5)); // this gives time for BlockDownloader to shutdown
//             Assert.AreEqual(TaskStatus.RanToCompletion, downloader.Completion.Status, "BlockDownloader should stop after cancellation");
//         }
//
//         [TestMethod]
//         [Timeout(timeout_10s)]
//         public async Task BlockDownloader_GetBlobClient_Exception_StopAsync()
//         {
//             Mock<BlobContainerClient> containerMock = new Mock<BlobContainerClient>(new Uri("http://mytest"));
//             containerMock.Setup(c => c.GetBlobClient(It.IsAny<string>())).Throws<Exception>();
//
//             var cancellationTokenSource = new CancellationTokenSource();
//             BlockDownloader downloader = new BlockDownloader(null, containerMock.Object, cancellationTokenSource.Token, DateTime.Now, 1, NullLogger.Instance);
//
//             await Task.Delay(TimeSpan.FromSeconds(3)); // this gives time for BlockDownloader for a few retries.
//
//             // Verification
//             containerMock.Verify(a => a.GetBlobClient(It.IsAny<string>()), Times.AtLeastOnce, "GetBlobClient should be called at least once.");
//             // loggerMock.Verify(l => l.LogException(It.IsAny<Exception>(), It.IsAny<string>(), PersonalizerInternalErrorCode.TrainerExecutionFailure.ToString(),
//             //     It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<TracingLevel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.AtLeastOnce, "Exception should be logged.");
//
//             Assert.AreEqual(TaskStatus.Faulted, downloader.Completion.Status, "BlockDownloader should resume stop upon exception");
//         }
//
//         [TestMethod]
//         [Timeout(timeout_10s)]
//         // For integration tests, please check StorageIntegrationTest.cs
//         public async Task BlockDownloader_GetBlobClient_Working_NullCheckPointBlobAsync()
//         {
//             var uri = new Uri("http://bogus/myaccount/blob");
//             Mock<CloudBlockBlob> mockedBlob = new Mock<CloudBlockBlob>(MockBehavior.Loose, uri);
//             mockedBlob.Setup(c => c.ExistsAsync()).Returns(Task.FromResult(false));
//             Mock<BlobContainerClient> containerMock = new Mock<BlobContainerClient>(new Uri("http://mytest"));
//             containerMock.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(mockedBlob.Object);
//
//             var cancellationTokenSource = new CancellationTokenSource();
//             
//             BlockDownloader downloader = new BlockDownloader(null, containerMock.Object, cancellationTokenSource.Token, DateTime.Now, 1, NullLogger.Instance);
//
//             await Task.Delay(TimeSpan.FromSeconds(3)); // this gives time for BlockDownloader for a few retries.
//
//             // Verification
//             containerMock.Verify(a => a.GetBlobClient(It.IsAny<string>()), Times.AtLeastOnce, "GetBlobClient should be called at least once.");
//             // loggerMock.Verify(l => l.LogException(It.IsAny<Exception>(), "BlockDownloader", PersonalizerInternalErrorCode.TrainerCheckpointFailure.ToString(),
//             //     It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<TracingLevel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never, "No StorageException should be logged.");
//             // loggerMock.Verify(l => l.LogException(It.IsAny<Exception>(), "BlockDownloader", PersonalizerInternalErrorCode.TrainerExecutionFailure.ToString(),
//             //     It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<TracingLevel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never, "No Exception should be logged.");
//
//             Assert.IsTrue(TaskStatus.WaitingForActivation.Equals(downloader.Completion.Status)
//                 || TaskStatus.WaitingToRun.Equals(downloader.Completion.Status)
//                 || TaskStatus.Running.Equals(downloader.Completion.Status), "BlockDownloader should be running");
//
//             cancellationTokenSource.Cancel();
//             await Task.Delay(TimeSpan.FromSeconds(1)); // this gives time for BlockDownloader to shutdown
//             Assert.AreEqual(TaskStatus.RanToCompletion, downloader.Completion.Status, "BlockDownloader should stop after cancellation");
//         }
//
//         [TestMethod]
//         [Timeout(timeout_10s)]
//         public async Task BlockDownloader_FindVisitedBlobsAsync()
//         {
//             var cancellationToken = new CancellationTokenSource().Token;
//             BlockDownloader downloader = await SetupAndActOnFindVisitedBlobTestAsync(numberOfBlobs: 3, blobIndexExcluded: null, resumeBlobName: 1, cancellationToken: cancellationToken);
//
//             // Verification
//             Assert.AreEqual(1, downloader.VisitedBlobs.Count, "only one blob should be visited");
//             Assert.IsTrue(downloader.VisitedBlobs.Contains("0"), "first blob should be visited");
//             Assert.AreEqual(0, downloader.VisitedBlocks.Count, "no block is visited");
//         }
//
//         [TestMethod]
//         [Timeout(timeout_10s)]
//         public async Task BlockDownloader_FindVisitedBlobsAsync_MissingLastBlobAsync()
//         {
//             var cancellationToken = new CancellationTokenSource().Token;
//             BlockDownloader downloader = await SetupAndActOnFindVisitedBlobTestAsync(numberOfBlobs: 3, blobIndexExcluded: 2, resumeBlobName: 2, cancellationToken: cancellationToken);
//
//             // Verification
//             Assert.AreEqual(2, downloader.VisitedBlobs.Count, "first 2 blobs should be visited");
//             Assert.IsTrue(downloader.VisitedBlobs.Contains("0"), "first blob should be visited");
//             Assert.IsTrue(downloader.VisitedBlobs.Contains("1"), "second blob should be visited");
//             Assert.AreEqual(0, downloader.VisitedBlocks.Count, "no block is visited");
//         }
//
//         [TestMethod]
//         [Timeout(timeout_10s)]
//         public async Task BlockDownloader_FindVisitedBlobsAsync_MissingMiddleBlobAsync()
//         {
//             var cancellationToken = new CancellationTokenSource().Token;
//             BlockDownloader downloader = await SetupAndActOnFindVisitedBlobTestAsync(numberOfBlobs: 3, blobIndexExcluded: 1, resumeBlobName: 1, cancellationToken: cancellationToken);
//
//             // Verification
//             Assert.AreEqual(1, downloader.VisitedBlobs.Count, "one blob should be visited");
//             Assert.IsTrue(downloader.VisitedBlobs.Contains("0"), "first blob should be visited");
//             Assert.AreEqual(0, downloader.VisitedBlocks.Count, "no block is visited");
//         }
//
//         // Cannot test ResumeVisitedBlocksAsync with missing blocks due to implementation of 
//         // ListBlockItem class in Microsoft.Azure.Storage.Blob, Version=10.0.3.0
//
//         private static Mock<CloudBlockBlob> SetupCloudBlockMock(Uri uri, string blobName)
//         {
//             var blob = new Mock<CloudBlockBlob>(uri);
//             blob.Setup(c => c.Name).Returns(blobName);
//             return blob;
//         }
//
//         // private static async Task<BlockDownloader> SetupAndActOnFindVisitedBlobTestAsync(int numberOfBlobs, int? blobIndexExcluded, int resumeBlobName, CancellationToken cancellationToken)
//         // {
//         //     var uri = new Uri("http://bogus/myaccount/blob");
//         //
//         //     var blobList = new List<CloudBlockBlob>();
//         //
//         //     for (int i = 0; i < numberOfBlobs; i++)
//         //     {
//         //         if (blobIndexExcluded.HasValue && i == blobIndexExcluded.Value)
//         //         {
//         //             continue;
//         //         }
//         //         blobList.Add(SetupCloudBlockMock(new Uri($"http://bogus/myaccount/blob/{i}"), i.ToString()).Object);
//         //     }
//         //
//         //     var blobResultSegment = new BlobResultSegment(blobList, null);
//         //
//         //     Mock<CloudBlockBlob> mockedBlob = new Mock<CloudBlockBlob>(MockBehavior.Loose, uri);
//         //     Mock<BlobContainerClient> containerMock = new Mock<BlobContainerClient>(new Uri("http://mytest"));
//         //     containerMock.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(mockedBlob.Object);
//         //     containerMock.Setup(c => c.ListBlobsSegmentedAsync(
//         //         It.IsAny<string>(), It.IsAny<bool>(), BlobListingDetails.None,
//         //         null, It.IsAny<BlobContinuationToken>(), It.IsAny<BlobRequestOptions>(), It.IsAny<OperationContext>(), cancellationToken))
//         //         .Returns(Task.FromResult(blobResultSegment));
//         //
//         //     var blockPosition = new BlockPosition()
//         //     {
//         //         BlobName = resumeBlobName.ToString(),
//         //         BlockName = resumeBlobName.ToString()
//         //     };
//         //     
//         //
//         //     BlockDownloader downloader = new BlockDownloader(blockPosition, containerMock.Object, cancellationToken, DateTime.Now, 1, NullLogger.Instance);
//         //
//         //     await Task.Delay(TimeSpan.FromSeconds(5)); // this gives time for BlockDownloader for a few retries.
//         //     return downloader;
//         // }
//     }
// }
