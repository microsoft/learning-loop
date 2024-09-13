// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommonTest;
using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Storage;
using Microsoft.DecisionService.Common.Trainer.Data;
using Microsoft.DecisionService.Common.Trainer.Operations;
using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.DecisionService.OnlineTrainer.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;


namespace Tests
{
    [TestClass]
    [TestCategory("integ")]
    public class StorageUploadBlockIntegrationTests
    {
        public TestContext TestContext { get; set; }
        private const int time_10s = 10 * 1000;
        private const int time_30s = 30 * 1000;

        private static IStorageFactory storageFactory;
        private static readonly TimeSpan uploadTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan uploadRetryDelay = TimeSpan.FromMilliseconds(500);

        private const JoinedLogFormat joinedLogFormat = JoinedLogFormat.Binary;
        private IBlobContainerClient tenantContainer;
        private IBlobContainerClient mirrorContainer;
        private string storageCheckpointBlobName;
        private IBlobClient storageCheckpointBlob;

        private StorageBlockOptions storageBlockOptions;
        private DateTime lastConfigUpdateDate;
        private CancellationTokenSource cancellationTokenSource;
        private readonly DateTime testBlobDate = new(2019, 11, 11);

        [TestInitialize]
        public async Task TestInitAsync()
        {
            storageFactory ??= global::CommonTest.TestUtil.CreateStorageFactory(TestContext);
            lastConfigUpdateDate = DateTime.UtcNow.Date;
            tenantContainer = storageFactory.CreateBlobContainerClient($"stgintegtest{DateTime.UtcNow.Ticks}");
            mirrorContainer = storageFactory.CreateBlobContainerClient($"mirrorintegtest{DateTime.UtcNow.Ticks}");

            await tenantContainer.CreateIfNotExistsAsync();
            storageCheckpointBlobName = PathHelper.BuildCheckpointName(lastConfigUpdateDate, AzureBlobConstants.TenantStorageCheckpointBlobName);
            storageCheckpointBlob = tenantContainer.GetBlobClient(storageCheckpointBlobName);

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

        [TestMethod, Description("ResumeAsync returns null when no data is present.")]
        [Timeout(time_30s)]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task StorageUploadBlock_ResumeAsyncReturnsNull_WhenStartingNewAsync()
        {
            var uploadBlock = new StorageUploadBlock(storageBlockOptions, tenantContainer, storageCheckpointBlob, StorageUploadType.Tenant, SystemTimeProvider.Instance, null);
            var resumePosition = await uploadBlock.ResumeAsync();
            Assert.IsNull(resumePosition);
        }

        [TestMethod, Description("ResumeAsync returns null when no eventhub position.")]
        [Timeout(time_30s)]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task StorageUploadBlock_ResumeAsyncReturnsNull_WhenStartingWithNoEventhubPositionsAsync()
        {
            string blobName = PathHelper.BuildBlobName(lastConfigUpdateDate, testBlobDate, 0, format: JoinedLogFormat.Binary);
            var checkpoint = TestStorageHelper.CreateStorageCheckpoint(blobName);
            await TestStorageHelper.UploadCheckpointAsync(storageCheckpointBlob, checkpoint);
            var uploadBlock = new StorageUploadBlock(storageBlockOptions, tenantContainer, storageCheckpointBlob, StorageUploadType.Tenant, SystemTimeProvider.Instance, null, retryDelay: uploadRetryDelay);
            var resumePosition = await uploadBlock.ResumeAsync();
            Assert.IsNull(resumePosition);
        }

        [TestMethod, Description("ResumeAsync recovers the last eventhub position.")]
        [Timeout(time_10s)]
        [TestCategory("Decision Service/Online Trainer")]
        public async Task StorageUploadBlock_ResumeAsyncReturnsNull_WhenStartingWithTwoEventhubPositionAsync()
        {
            string blobName = PathHelper.BuildBlobName(lastConfigUpdateDate, testBlobDate, 0, format: JoinedLogFormat.Binary);
            var checkpoint = TestStorageHelper.CreateStorageCheckpoint(blobName, 0, 0,
                new PartitionCheckpoint[] {
                    new() { Offset = 0, EnqueuedTimeUtc = DateTime.UtcNow },
                    new() { Offset = 0, EnqueuedTimeUtc = DateTime.UtcNow }
                }
            );
            await TestStorageHelper.UploadCheckpointAsync(storageCheckpointBlob, checkpoint);
            var uploadBlock = new StorageUploadBlock(storageBlockOptions, tenantContainer, storageCheckpointBlob, StorageUploadType.Tenant, SystemTimeProvider.Instance, null);
            var resumePosition = await uploadBlock.ResumeAsync();
            Assert.IsNotNull(resumePosition);
            Assert.AreEqual(2, resumePosition.PartitionCheckpoints.Count);
        }

        public static IEnumerable<object[]> GetUploadResumeTestCases()
        {
            var testBlobDate = new DateTime(2019, 11, 11, 10, 0, 0);
            var oneAdditionalEvent = new List<DateTime> { new(2019, 11, 11, 18, 24, 45) };
            var nextDayEvent = new List<DateTime> { new(2019, 11, 12, 0, 0, 0) };
            var nextTwoDaysEvents = new List<DateTime> { new(2019, 11, 12, 0, 0, 0), new(2019, 11, 13, 0, 0, 0) };
            return new List<object[]>
            {
                // resume with all blocks committed with the checkpoint up to date
                new object[] { testBlobDate, false, 0, 2, 2, -1, oneAdditionalEvent},
                // resume with some blocks committed with the checkpoint up to date
                new object[] { testBlobDate, false, 0, 4, 2, -1, oneAdditionalEvent },
                // resume with all block committed with the checkpoint is explicitly set to the beginning
                new object[] { testBlobDate, false, 0, 2, 2, 0, oneAdditionalEvent },
                // resume with all block committed with the checkpoint behind by 1
                new object[] { testBlobDate, false, 0, 2, 2, 1, oneAdditionalEvent },
                // resume with all block committed with the checkpoint is explicitly up to date
                new object[] { testBlobDate, false, 0, 2, 2, 2, oneAdditionalEvent },
                // resume with all block committed with the checkpoint ahead by 1
                new object[] { testBlobDate, false, 0, 2, 2, 3, oneAdditionalEvent },
                // resume with no blocks committed with the checkpoint up to date
                new object[] { testBlobDate, false, 0, 4, 0, -1, oneAdditionalEvent },
                // resume with no blocks committed with the checkpoint is explicitly set to the beginning
                new object[] { testBlobDate, false, 0, 4, 0, 0, oneAdditionalEvent },
                // resume with some blocks committed with the checkpoint is explicitly set to the beginning
                new object[] { testBlobDate, false, 0, 4, 1, 0, oneAdditionalEvent },
                // resume with some blocks committed with the checkpoint behind by 1
                new object[] { testBlobDate, false, 0, 4, 2, 1, oneAdditionalEvent },
                // resume with some blocks committed with the checkpoint is explicitly at position 2
                new object[] { testBlobDate, false, 0, 4, 3, 2, oneAdditionalEvent },
                // resume with some blocks committed with the checkpoint is explicitly at position 1
                new object[] { testBlobDate, false, 0, 4, 3, 1, oneAdditionalEvent },
                // resume with some blocks committed with the checkpoint is explicitly far ahead
                new object[] { testBlobDate, false, 0, 4, 3, 100, oneAdditionalEvent },
                // resume with all blocks committed with the checkpoint up to date using index 1
                new object[] { testBlobDate, false, 1, 2, 2, -1, oneAdditionalEvent },
                // resume with all blocks committed with the checkpoint up to date using hourly increment
                new object[] { testBlobDate, true, 0, 2, 2, -1, oneAdditionalEvent },
                // resume with all blocks committed with the checkpoint up to date with events for the next day
                new object[] { testBlobDate, false, 0, 2, 2, -1, nextDayEvent },
                // resume with all blocks committed with the checkpoint up to date with events for the next two days
                new object[] { testBlobDate, false, 0, 2, 2, -1, nextTwoDaysEvents },
                // resume with some blocks committed with the checkpoint up to date with events for the next two days
                new object[] { testBlobDate, false, 0, 4, 2, -1, nextTwoDaysEvents },
            };
        }

        [TestMethod]
        [Timeout(time_30s)]
        [TestCategory("Decision Service/Online Trainer")]
        [DynamicData(nameof(GetUploadResumeTestCases), DynamicDataSourceType.Method)]
        public async Task StorageUploadBlock_EventsResumeFromCheckpointAsync(
            DateTime testBlobDate,
            bool testUseHourlyIndexIncrement,
            int testBlobIndex,
            int testUploadBlockCount,
            int testCommittedBlockCount,
            int testCheckpointStartBlockNo,
            IList<DateTime> postResumeEventDates
        )
        {
            var blockPosition = await TestStorageHelper.PrepStorageForDownloadAsync(
                tenantContainer,
                storageCheckpointBlob,
                lastConfigUpdateDate,
                blobIndex: testBlobIndex,
                blobDate: testBlobDate,
                useHourlyIncrement: testUseHourlyIndexIncrement,
                uploadBlockCount: testUploadBlockCount,
                commitBlockCount: testCommittedBlockCount,
                startAtCheckpointNo: testCheckpointStartBlockNo
            );
            // verify the storage state before resuming
            await Assert_PrepStorageForDownload_IsValid_Async(blockPosition.BlobName, testBlobIndex, testUploadBlockCount, testCommittedBlockCount, testCheckpointStartBlockNo);
            // resume from checkpoint
            storageBlockOptions.HourlyIndexIncrement = testUseHourlyIndexIncrement;
            var uploadBlock = new StorageUploadBlock(storageBlockOptions, tenantContainer, storageCheckpointBlob, StorageUploadType.Tenant, SystemTimeProvider.Instance, null, cancellationToken: cancellationTokenSource.Token, retryDelay: uploadRetryDelay);
            var resumePosition = await uploadBlock.ResumeAsync();
            // verify the state after resume
            await Assert_Resume_IsValid_Async(blockPosition.BlobName, resumePosition, testBlobIndex, testUploadBlockCount, testCommittedBlockCount, testCheckpointStartBlockNo);
            // generate and send more data
            var batchCount = await TestStorageHelper.GenerateAndSendEventsListToUploadBlockAsync(uploadBlock, postResumeEventDates.ToArray());
            // wait for the uploader to catch up
            await WaitForUploadAsync();
            var expectedTotalBlocks = testUploadBlockCount + batchCount;
            var containerBlobDates = new HashSet<DateTime>
            {
                testBlobDate
            };
            containerBlobDates.UnionWith(postResumeEventDates);
            await Assert_StorageUpload_IsValid_Async(containerBlobDates.ToArray(), testUseHourlyIndexIncrement, expectedTotalBlocks);
        }

        private async Task Assert_PrepStorageForDownload_IsValid_Async(string blobName, int testBlobIndex, int testUploadBlockCount, int testCommittedBlockCount, int testCheckpointStartBlockNo)
        {
            var blob = tenantContainer.GetBlockBlobClient(blobName);
            if (testCommittedBlockCount > 0)
            {
                var blocks = await blob.GetBlockInfoListAsync("All", cancelToken: cancellationTokenSource.Token);
                int numCommittedBlocks = blocks.Where(b => b.IsCommitted).Count();
                Assert.AreEqual(testCommittedBlockCount, numCommittedBlocks, $"after storage preperation, there should be {testCommittedBlockCount} committed blocks");
                int numUncommittedBlocks = blocks.Where(b => !b.IsCommitted).Count();
                Assert.AreEqual(testUploadBlockCount - testCommittedBlockCount, numUncommittedBlocks, $"after storage preperation, there should be {testUploadBlockCount - testCommittedBlockCount} uncommitted blocks");
            }
            var startingCheckpoint = await StorageCheckpointHelper.GetLastStorageCheckpointAsync(storageCheckpointBlob, NullLogger.Instance);
            // if the checkpoint is negative, PrepStorageForDownloadAsync will be set to the upload count;
            int expectedCPStartNo = testCheckpointStartBlockNo < 0 ? testUploadBlockCount : testCheckpointStartBlockNo;
            Assert.AreEqual(expectedCPStartNo.ToString("x4"), startingCheckpoint.BlockPosition.BlockName, $"the checkpoint should be at {expectedCPStartNo:x4}");
            PathHelper.ParseIndexAndDate(startingCheckpoint.BlockPosition.BlobName, out var index, out var hour, out var day);
            Assert.AreEqual(testBlobIndex, index, $"the blob index should be {testBlobIndex}");
        }

        private async Task Assert_Resume_IsValid_Async(string blobName, EventHubCheckpoint resumePosition, int testBlobIndex, int testUploadBlockCount, int testCommittedBlockCount, int testCheckpointStartBlockNo)
        {
            var blob = tenantContainer.GetBlockBlobClient(blobName);
            // validate the partition checkpoints (defaulted to 1 partition "0" for testing)
            // todo: add partition generation to the data generation
            resumePosition.PartitionCheckpoints.TryGetValue("0", out var partitionCheckpoint);
            Assert.AreEqual(0, partitionCheckpoint.Offset, "Resume position is incorrect.");

            // validate the committed blocks
            if (testCommittedBlockCount > 0)
            {
                var blocks = await blob.GetBlockInfoListAsync("All", cancelToken: cancellationTokenSource.Token);
                int numCommittedBlocks = blocks.Where(b => b.IsCommitted).Count();
                Assert.AreEqual(testUploadBlockCount, numCommittedBlocks, $"after storage preperation, there should be {testUploadBlockCount} committed blocks");
                int numUncommittedBlocks = blocks.Where(b => !b.IsCommitted).Count();
                Assert.AreEqual(0, numUncommittedBlocks, "after resume, there should be no uncommitted blocks");
            }
        }

        private async Task Assert_StorageUpload_IsValid_Async(DateTime[] blobDates, bool testUseHourlyIndexIncrement, int expectedTotalBlocks)
        {
            // find all blobs, count all of the blocks, and verify they add up to the expected total
            int totalBlocks = 0;
            var allBlobs = await tenantContainer.GetBlobsAsync(PathHelper.BuildBlobListPrefix(lastConfigUpdateDate), CancellationToken.None);
            var fbBlobs = allBlobs.Where(b => Regex.IsMatch(b.Name, @".*\.fb"));
            foreach (var bi in fbBlobs)
            {
                var blob= tenantContainer.GetBlockBlobClient(bi.Name);
                totalBlocks += (await blob.GetBlockInfoListAsync("All", cancelToken: cancellationTokenSource.Token)).Count();
            }
            Assert.AreEqual(expectedTotalBlocks, totalBlocks, "all blocks should be accounted for");
            
            // verify the final checkpoint is at the expected position
            var finalCheckpoint = await StorageCheckpointHelper.GetLastStorageCheckpointAsync(storageCheckpointBlob, NullLogger.Instance);
            var currentBlob = tenantContainer.GetBlockBlobClient(finalCheckpoint.BlockPosition.BlobName);
            var currentBlobBlocks = await currentBlob.GetBlockInfoListAsync("All", cancelToken: cancellationTokenSource.Token);
            Assert.AreEqual(currentBlobBlocks.Count().ToString("x4"), finalCheckpoint.BlockPosition.BlockName);
        }

        private static async Task WaitForUploadAsync()
        {
            await Task.Delay(uploadTimeout);
        }
    }
}
