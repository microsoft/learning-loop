// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommonTest;
using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Error;
using Microsoft.DecisionService.Common.Storage;
using Microsoft.DecisionService.Common.Trainer.Operations;
using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.DecisionService.OnlineTrainer.Operations;
using Microsoft.DecisionService.OnlineTrainer.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    [TestCategory("integ")]
    public class StorageIntegrationTest
    {
        public TestContext TestContext { get; set; }

        private const JoinedLogFormat joinedLogFormat = JoinedLogFormat.Binary;
        private const int time_30s = 30 * 1000;
        private const int time_60s = 60 * 1000;

        private static readonly TimeSpan downloadChkPtTimeout = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan downloadTimeout = TimeSpan.FromSeconds(2); 
        private static IStorageFactory storageFactory;
        private IBlobContainerClient tenantContainer;
        private IBlobContainerClient mirrorContainer;
        private string storageCheckpointBlobName;
        private IBlobClient storageCheckpointBlob;
        private StorageBlockOptions storageBlockOptions;
        private DateTime lastConfigUpdateDate;
        private CancellationTokenSource cancellationTokenSource;

        [TestInitialize]
        public async Task TestSetupBlocksAsync()
        {
            storageFactory ??= global::CommonTest.TestUtil.CreateStorageFactory(TestContext);
            lastConfigUpdateDate = DateTime.UtcNow;
            tenantContainer = storageFactory.CreateBlobContainerClient($"stgintegtest{DateTime.UtcNow.Ticks}");
            mirrorContainer = storageFactory.CreateBlobContainerClient($"mirrorintegtest{DateTime.UtcNow.Ticks}");
            storageCheckpointBlobName = PathHelper.BuildCheckpointName(lastConfigUpdateDate, AzureBlobConstants.TenantStorageCheckpointBlobName);
            storageCheckpointBlob = tenantContainer.GetBlobClient(storageCheckpointBlobName);
            await tenantContainer.CreateIfNotExistsAsync();

            cancellationTokenSource = new CancellationTokenSource();
            storageBlockOptions = new StorageBlockOptions()
            {
                LastConfigurationEditDate = lastConfigUpdateDate,
            };
        }

        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            cancellationTokenSource.Cancel();
            await tenantContainer.DeleteIfExistsAsync();
            await mirrorContainer.DeleteIfExistsAsync();
        }

        [TestMethod]
        [Timeout(time_30s)]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task EventDownloader_DownloadsStartingFromAndEndsAtCheckpoint_WhenFirstRunAsync()
        {
            await TestStorageHelper.PrepStorageForDownloadAsync(
                tenantContainer,
                storageCheckpointBlob,
                lastConfigUpdateDate,
                blobIndex: 0,
                new DateTime(2019, 11, 11),
                uploadBlockCount: 6,
                commitBlockCount: 6,
                eventCount: 2 // 12 events in total
            );
            var downloader = new BlockDownloader(null, tenantContainer, cancellationTokenSource.Token, lastConfigUpdateDate, storageBlockOptions.BlockBufferCapacity, NullLogger.Instance, downloadChkPtTimeout);
            (int receiveAttempts, int numDownloadedEvents, Exception exception) = await TestStorageHelper.ReadDownloadEventsAsync(downloader, downloadTimeout);
            Assert.AreEqual(7, receiveAttempts, "We should make 7 read attempts for downloaded blocks.");
            Assert.AreEqual(12, numDownloadedEvents, "We should download 12 events.");
            Assert.IsTrue(exception.IsExceptionOf<TimeoutException>(), "Timeout exception is expected as no data is available.");
        }

        [TestMethod]
        [Timeout(time_30s)]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task BlockDownloader_DownloadsStartingFromAndEndsAtCheckpoint_WhenFirstRunAsync()
        {
            await TestStorageHelper.PrepStorageForDownloadAsync(
                tenantContainer,
                storageCheckpointBlob,
                lastConfigUpdateDate,
                blobIndex: 0,
                new DateTime(2019, 11, 11),
                uploadBlockCount: 6,
                commitBlockCount: 6,
                eventCount: 2 // 12 events in total
            );
            var downloader = new BlockDownloader(null, tenantContainer, cancellationTokenSource.Token, lastConfigUpdateDate, storageBlockOptions.BlockBufferCapacity, NullLogger.Instance, downloadChkPtTimeout);
            (int receiveAttempts, int numDownloadedBlocks, Exception exception) = await TestStorageHelper.ReadDownloadDataBlocksAsync(downloader, downloadTimeout);
            Assert.AreEqual(7, receiveAttempts, "We should make 7 read attempts for downloaded blocks.");
            Assert.AreEqual(6, numDownloadedBlocks, "We should download 6 latest data blocks.");
            Assert.IsTrue(exception.IsExceptionOf<TimeoutException>(), "Timeout exception is expected as no data is available.");
        }

        [TestMethod]
        [Timeout(time_30s)]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task BlockDownloader_DownloadNeverGoesPastCheckpointAsync()
        {
            var startingPosition = await TestStorageHelper.PrepStorageForDownloadAsync(
                tenantContainer,
                storageCheckpointBlob,
                lastConfigUpdateDate,
                blobIndex: 0,
                new DateTime(2019, 11, 11),
                uploadBlockCount: 1,
                commitBlockCount: 1
            );
            var downloader = new BlockDownloader(startingPosition, tenantContainer, cancellationTokenSource.Token, lastConfigUpdateDate, storageBlockOptions.BlockBufferCapacity, NullLogger.Instance, downloadChkPtTimeout);
            var uploadBlock = new StorageUploadBlock(storageBlockOptions, tenantContainer, storageCheckpointBlob, StorageUploadType.Tenant, SystemTimeProvider.Instance, null);
            await uploadBlock.ResumeAsync();

            await TestStorageHelper.GenerateAndSendEventsListToUploadBlockAsync(
                uploadBlock,
                new DateTime[]
                {
                    new(2019, 11, 11, 18, 24, 45),
                    new(2019, 11, 11, 18, 30, 45)
                }
            );

            // Verify that we can download the new data blocks
            (int receiveAttempts, int numDownloadedBlocks, Exception exception) = await TestStorageHelper.ReadDownloadDataBlocksAsync(downloader, downloadTimeout);
            Assert.AreEqual(3, receiveAttempts, "We should make 3 read attempts for downloaded blocks.");
            Assert.AreEqual(2, numDownloadedBlocks, "We should download 2 latest data blocks.");
            Assert.IsTrue(exception.IsExceptionOf<TimeoutException>(), "Timeout exception is expected as no data is available.");
        }

        [TestMethod]
        [Timeout(time_30s)]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task BlockDownloader_DownloadNewBlock_WhenNewBlobIsCreatedAndCheckpointedAsync()
        {
            await TestStorageHelper.PrepStorageForDownloadAsync(
                tenantContainer,
                storageCheckpointBlob,
                lastConfigUpdateDate,
                blobIndex: 0,
                new DateTime(2019, 11, 11),
                uploadBlockCount: 2,
                commitBlockCount: 2,
                eventCount: 2 // 4 events in total
            );
            var downloader = new BlockDownloader(null, tenantContainer, cancellationTokenSource.Token, lastConfigUpdateDate, storageBlockOptions.BlockBufferCapacity, NullLogger.Instance, downloadChkPtTimeout);
            var uploadBlock = new StorageUploadBlock(storageBlockOptions, tenantContainer, storageCheckpointBlob, StorageUploadType.Tenant, SystemTimeProvider.Instance, null);
            await uploadBlock.ResumeAsync();

            await TestStorageHelper.GenerateAndSendEventsListToUploadBlockAsync(
                uploadBlock,
                new DateTime[]
                {
                    new(2019, 11, 11, 18, 24, 45),
                    new(2019, 11, 11, 18, 30, 45),
                    new(2019, 11, 12, 18, 30, 45)
                }
            );
            await Task.Delay(downloadTimeout);

            // Verify that we can download the new data
            (int receiveAttempts, int numDownloadedBlocks, Exception exception) = await TestStorageHelper.ReadDownloadDataBlocksAsync(downloader, downloadTimeout);
            Assert.AreEqual(6, receiveAttempts, "We should make 6 read attempts for downloaded blocks.");
            Assert.AreEqual(5, numDownloadedBlocks, "We should download 5 latest data blocks.");
            Assert.IsTrue(exception.IsExceptionOf<TimeoutException>(), "Timeout exception is expected as no data is available.");
        }

        [TestMethod]
        [Timeout(time_60s)]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task StorageLogSerializeBlock_DoesNotCreateMirrorBlock_WhenLogMirrorSasUriIsInvalidAsync()
        {
            var logMirrorSettings = new LogMirrorSettings()
            {
                Enabled = true,
                SasUri = TestConfiguration.TryGet(TestContext, global::CommonTest.Constants.e2eSasStorageUriKey),
            };
            TestStorageHelper.CreateDataBlobReference(tenantContainer, lastConfigUpdateDate, new DateTime(2019, 11, 11), 0);
            var serializerBlock = new StorageLogSerializeBlock(storageBlockOptions, tenantContainer, logMirrorSettings, null, null);
            await serializerBlock.SetupBlocksAsync();
            Assert.IsNull(serializerBlock.LogMirrorUploadBlock);
        }
   }
}