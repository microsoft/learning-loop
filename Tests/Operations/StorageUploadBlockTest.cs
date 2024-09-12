// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// using CommonTest;
// using Google.FlatBuffers;
// using Microsoft.Azure.Storage;
// using Microsoft.Azure.Storage.Blob;
// using Microsoft.DecisionService.Common;
// using Microsoft.DecisionService.Common.Data;
// using Microsoft.DecisionService.Common.Trainer.Data;
// using Microsoft.DecisionService.Common.Trainer.Join;
// using Microsoft.DecisionService.Common.Trainer.Operations;
// using Microsoft.DecisionService.OnlineTrainer;
// using Microsoft.DecisionService.OnlineTrainer.Data;
// using Microsoft.DecisionService.OnlineTrainer.Operations;
// using Microsoft.VisualStudio.TestTools.UnitTesting;
// using Moq;
// using Newtonsoft.Json;
// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Threading;
// using System.Threading.Tasks;
// using System.Threading.Tasks.Dataflow;
// using Azure.Storage.Blobs;
//
// namespace Tests.Operations
// {
//     [TestClass]
//     public class StorageUploadBlockTest
//     {
//         private const int time_5s = 5 * 1000;
//         private const int time_10s = 10 * 1000;
//         private const int time_15s = 15 * 1000;
//         //azure mocks
//         private Mock<BlobContainerClient> mockTenantBlobContainer;
//         private Mock<BlobClient> mockTenantBlockBlob;
//         private Mock<BlobClient> mockTenantCheckpointBlob;
//         private Mock<BlobContainerClient> mockClientBlobContainer;
//         private Mock<BlobClient> mockClientBlockBlob;
//         private Mock<BlobClient> mockClientCheckpointBlob;
//
//         private UploadHelper uploadHelper;
//         private StorageBlockOptions options;
//         private StorageBlockOptions optionsWithBinLog;
//         private string storageCheckpointBlobName;
//         private DateTime lastConfigurationEditDate;
//         private OnlineTrainerOptions onlineTrainerOptions;
//
//         [TestInitialize]
//         public void TestInit()
//         {
//             lastConfigurationEditDate = new DateTime(2017, 8, 14, 0, 0, 0);
//             storageCheckpointBlobName = PathHelper.BuildCheckpointName(lastConfigurationEditDate, AzureBlobConstants.TenantStorageCheckpointBlobName);
//
//             //the mock construction requires an URI
//             var tenantBloburi = new Uri("http://bogus/myaccount/blob");
//             this.mockTenantBlobContainer = new Mock<BlobContainerClient>(MockBehavior.Loose, tenantBloburi);
//             this.mockTenantBlockBlob = new Mock<BlobClient>(MockBehavior.Loose, tenantBloburi);
//             this.mockTenantCheckpointBlob = new Mock<BlobClient>(MockBehavior.Loose, tenantBloburi);
//
//             var clientBloburi = new Uri("http://bogus/myaccount/mirrorblob");
//             this.mockClientBlobContainer = new Mock<BlobContainerClient>(MockBehavior.Loose, clientBloburi);
//             this.mockClientBlockBlob = new Mock<BlobClient>(MockBehavior.Loose, clientBloburi);
//             this.mockClientCheckpointBlob = new Mock<BlobClient>(MockBehavior.Loose, clientBloburi);
//
//             //(common setup to all tests) setup the client: verify that methods are called on the correct container name
//             SetupMockedBlobContainer(this.mockTenantBlobContainer, this.mockTenantBlockBlob, this.mockTenantCheckpointBlob);
//             SetupMockedBlobContainer(this.mockClientBlobContainer, this.mockClientBlockBlob, this.mockClientCheckpointBlob);
//
//             this.onlineTrainerOptions = new OnlineTrainerOptions()
//             {
//                 LastConfigurationEditDate = lastConfigurationEditDate,
//                 MaximumFlushLatency = TimeSpan.FromSeconds(10),
//                 CancellationToken = new CancellationToken(),
//             };
//             this.mockTenantBlockBlob.Setup(blob => blob.Name).Returns(PathHelper.BuildBlobName(onlineTrainerOptions.LastConfigurationEditDate.Date,
//                 onlineTrainerOptions.LastConfigurationEditDate.Date,
//                 0));
//
//             this.options = new StorageBlockOptions()
//             {
//                 LastConfigurationEditDate = lastConfigurationEditDate,
//                 MaximumFlushLatency = TimeSpan.FromSeconds(10),
//             };
//             
//             this.uploadHelper = new UploadHelper();
//         }
//
//         [TestMethod, Description("Joiner creates new blob if it's configured with a file format different than the checkpoint")]
//         [Timeout(time_10s)]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task StorageUploadBlock_CreatesNewFileIfFormatNotTheSameAsTheCheckpointAsync()
//         {
//             StorageCheckpoint cp = new StorageCheckpoint()
//             {
//                 BlockPosition = new BlockPosition()
//                 {
//                     BlobName = "existing-blob",
//                     BlockName = "0000",
//                     FileFormat = JoinedLogFormat.DSJSON
//                 },
//                 EventPosition = new EventPosition()
//                 {
//                     Offset = 1
//                 }
//             };
//             string initialCheckpointString = JsonConvert.SerializeObject(cp);
//             List<string> cpStrings = new List<string>();
//
//             var checkpointBlob = new Mock<BlobClient>(MockBehavior.Loose, new Uri("http://bonus/existing-blob") /*?*/);
//             mockTenantCheckpointBlob.Setup(m => m.ExistsAsync()).Returns(Task<bool>.FromResult(true));
//             mockTenantCheckpointBlob.Setup(m => m.DownloadContentAsync()).Returns(Task<string>.FromResult(initialCheckpointString));
//             mockTenantCheckpointBlob.Setup(m => m.UploadTextAsync(It.IsAny<string>())).Callback<string>(str =>
//             {
//                 cpStrings.Add(str);
//             });
//
//             mockTenantBlobContainer
//                 .Setup(m => m.GetBlobClient(It.Is<string>(s => s == "existing-blob")))
//                 .Returns(checkpointBlob.Object);
//
//             checkpointBlob
//                 .Setup(cp => cp.Name).Returns("/myappid/20170814000000/data/2017/9/21_00.json");
//             checkpointBlob
//                 .Setup(cp => cp.ExistsAsync()).Returns(Task<bool>.FromResult(false)); //this is a shortcut, less stuff to mock
//             var batch = TestUtil.GenerateInteractionEvents(count: 1, startOffset: 1000, startDateTime: new DateTime(2017, 9, 21, 18, 24, 45)).First();
//
//             var block = new StorageUploadBlock(this.optionsWithBinLog, mockTenantBlobContainer.Object, mockTenantCheckpointBlob.Object, StorageUploadType.Tenant, SystemTimeProvider.Instance, cancellationToken:CancellationToken.None);
//             await block.ResumeAsync();
//             await block.Input.SendAsync(new List<InteractionEvent>() { uploadHelper.Serialize(batch) });
//
//             block.Input.Complete();
//             await block.Completion;
//
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.IsAny<string>()), Times.Exactly(2));
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.Is<string>(s => s == "existing-blob")), Times.Once);
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.Is<string>(s => s == "20170814000000/data/2017/09/21_0000000001.fb")), Times.Once);
//             this.mockTenantBlockBlob.Verify(blob => blob.PutBlockListAsync(
//                  It.IsAny<IEnumerable<string>>(),
//                  null,
//                  It.IsAny<BlobRequestOptions>(),
//                  It.IsAny<OperationContext>(),
//                  It.IsAny<CancellationToken>()), Times.Exactly(1));
//
//             Assert.AreEqual(1, cpStrings.Count);
//             cp = JsonConvert.DeserializeObject<StorageCheckpoint>(cpStrings[0]);
//             Assert.IsNotNull(cp.BlockPosition);
//             Assert.AreEqual(JoinedLogFormat.Binary, cp.BlockPosition.FileFormat);
//         }
//
//         [TestMethod, Description("Append to an existing blob")]
//         [Timeout(time_10s)]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task StorageUploadBlock_AppendAsync()
//         {
//             // ========================================== Arrange ==============================================
//             var block = new StorageUploadBlock(this.options, mockTenantBlobContainer.Object, mockTenantCheckpointBlob.Object, StorageUploadType.Tenant, SystemTimeProvider.Instance, cancellationToken:CancellationToken.None);
//
//             var firstEvent = TestUtil.GenerateInteractionEvents(count: 1, startOffset: 1000, startDateTime: new DateTime(2017, 9, 21, 18, 24, 45)).First();
//
//             //second event happens the same day
//             var secondEvent = TestUtil.GenerateInteractionEvents(count: 1, startOffset: 2000, startDateTime: new DateTime(2017, 9, 21, 19, 21, 13)).First();
//
//             // ========================================== Action ===============================================
//             await block.ResumeAsync();
//             await block.Input.SendAsync(new List<InteractionEvent>() {
//                 uploadHelper.Serialize(firstEvent),
//                 uploadHelper.Serialize(secondEvent) });
//             block.Input.Complete();
//             await block.Completion;
//
//             // ========================================== Assert ===============================================
//             UploadBlockAppendAssertHelper(this.mockTenantBlobContainer, this.mockTenantBlockBlob, this.mockTenantCheckpointBlob);
//         }
//
//         [TestMethod, Description("Append to an existing blob with output target to forward events")]
//         [Timeout(time_10s)]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task StorageUploadBlock_Append_With_TargetBlockAsync()
//         {
//             // ========================================== Arrange ==============================================
//             var mirrorBlock = new StorageUploadBlock(this.options, mockClientBlobContainer.Object, mockClientCheckpointBlob.Object, StorageUploadType.Mirror, SystemTimeProvider.Instance, cancellationToken:CancellationToken.None);
//
//             var block = new StorageUploadBlock(this.options, mockTenantBlobContainer.Object, mockTenantCheckpointBlob.Object, StorageUploadType.Tenant, SystemTimeProvider.Instance, mirrorBlock.Input, cancellationToken:CancellationToken.None);
//
//             var firstEvent = TestUtil.GenerateInteractionEvents(count: 1, startOffset: 1000, startDateTime: new DateTime(2017, 9, 21, 18, 24, 45)).First();
//
//             //second event happens the same day
//             var secondEvent = TestUtil.GenerateInteractionEvents(count: 1, startOffset: 2000, startDateTime: new DateTime(2017, 9, 21, 19, 21, 13)).First();
//
//             // ========================================== Action ===============================================
//             await block.ResumeAsync();
//             await block.Input.SendAsync(new List<InteractionEvent>() {
//                 uploadHelper.Serialize(firstEvent),
//                 uploadHelper.Serialize(secondEvent) });
//             block.Input.Complete();
//             await block.Completion;
//
//             // ========================================== Assert ===============================================
//             UploadBlockAppendAssertHelper(this.mockTenantBlobContainer, this.mockTenantBlockBlob, this.mockTenantCheckpointBlob);
//             UploadBlockAppendAssertHelper(this.mockClientBlobContainer, this.mockClientBlockBlob, this.mockClientCheckpointBlob); ;
//         }
//
//         [TestMethod, Description("Create and append to a new blob because the day changed")]
//         [Timeout(time_5s)]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task StorageUploadBlock_CreateNewBlob_WhenReceivingDataFromANewDayAsync()
//         {
//             // ========================================== Arrange ==============================================
//             var block = new StorageUploadBlock(this.options, mockTenantBlobContainer.Object, mockTenantCheckpointBlob.Object, StorageUploadType.Tenant, SystemTimeProvider.Instance, cancellationToken:CancellationToken.None);
//
//             var firstEvent = TestUtil.GenerateInteractionEvents(count: 1, startOffset: 1000, startDateTime: new DateTime(2017, 9, 21, 18, 24, 45)).First();
//
//             //second event happens the next day
//             var secondEvent = TestUtil.GenerateInteractionEvents(count: 1, startOffset: 2000, startDateTime: new DateTime(2017, 9, 22, 19, 21, 13)).First();
//
//             // ========================================== Action ===============================================
//             await block.Input.SendAsync(new List<InteractionEvent>() { uploadHelper.Serialize(firstEvent) });
//             await block.Input.SendAsync(new List<InteractionEvent>() { uploadHelper.Serialize(secondEvent) });
//
//             block.Input.Complete();
//             await block.Completion;
//
//             // ========================================== Assert ===============================================
//             //verify GetBlobClient, twice for uploading events
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.IsAny<string>()), Times.Exactly(2));
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.Is<string>(s => s == "20170814000000/data/2017/09/21_0000000000.json")), Times.Once);
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.Is<string>(s => s == "20170814000000/data/2017/09/22_0000000000.json")), Times.Once);
//
//             //verify PutBlockListAsync
//             this.mockTenantBlockBlob.Verify(blob => blob.PutBlockListAsync(
//                  It.IsAny<IEnumerable<string>>(),
//                  null,
//                  It.IsAny<BlobRequestOptions>(),
//                  It.IsAny<OperationContext>(),
//                  It.IsAny<CancellationToken>()), Times.Exactly(2));
//         }
//
//         [TestMethod, Description("Create and append to a new blob because the hour changed")]
//         [Timeout(time_5s)]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task StorageUploadBlock_CreateNewBlob_WhenReceivingDataFromANewHourAsync()
//         {
//             // ========================================== Arrange ==============================================
//             var options = new StorageBlockOptions()
//             {
//                 LastConfigurationEditDate = lastConfigurationEditDate,
//                 MaximumFlushLatency = TimeSpan.FromSeconds(10),
//                 // LogRetentionDays = 1
//
//             };
//             Assert.IsTrue(options.HourlyIndexIncrement);
//             var block = new StorageUploadBlock(options, mockTenantBlobContainer.Object, mockTenantCheckpointBlob.Object, StorageUploadType.Tenant, SystemTimeProvider.Instance, cancellationToken:CancellationToken.None);
//
//             var firstEvent = TestUtil.GenerateInteractionEvents(count: 1, startOffset: 1000, startDateTime: new DateTime(2017, 9, 21, 18, 24, 45)).First();
//
//             //second event happens the next day
//             var secondEvent = TestUtil.GenerateInteractionEvents(count: 1, startOffset: 2000, startDateTime: new DateTime(2017, 9, 21, 23, 21, 13)).First();
//
//             // ========================================== Action ===============================================
//             await block.Input.SendAsync(new List<InteractionEvent>() { uploadHelper.Serialize(firstEvent) });
//             await block.Input.SendAsync(new List<InteractionEvent>() { uploadHelper.Serialize(secondEvent) });
//
//             block.Input.Complete();
//             await block.Completion;
//
//             // ========================================== Assert ===============================================
//             //verify GetBlobClient, twice for uploading events
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.IsAny<string>()), Times.Exactly(2));
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.Is<string>(s => s == "20170814000000/data/2017/09/21_0000000000_18.json")), Times.Once);
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.Is<string>(s => s == "20170814000000/data/2017/09/21_0000000001_23.json")), Times.Once);
//
//             //verify PutBlockListAsync
//             this.mockTenantBlockBlob.Verify(blob => blob.PutBlockListAsync(
//                  It.IsAny<IEnumerable<string>>(),
//                  null,
//                  It.IsAny<BlobRequestOptions>(),
//                  It.IsAny<OperationContext>(),
//                  It.IsAny<CancellationToken>()), Times.Exactly(2));
//         }
//
//         [TestMethod, Description("Try to resume a blob and continue to append to it")]
//         [Timeout(time_15s)]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task StorageUploadBlock_CreatesNewBlob_WhenFirstEventReceivedAsync()
//         {
//             this.mockTenantBlockBlob.Setup(blob => blob.Name).Returns(PathHelper.BuildBlobName(
//                 this.onlineTrainerOptions.LastConfigurationEditDate,
//                 this.onlineTrainerOptions.LastConfigurationEditDate.Date,
//                 0));
//
//             // ========================================== Arrange ==============================================
//             var block = new StorageUploadBlock(this.options, mockTenantBlobContainer.Object, mockTenantCheckpointBlob.Object, StorageUploadType.Tenant, SystemTimeProvider.Instance, cancellationToken:CancellationToken.None);
//             await block.ResumeAsync();
//
//             //event happens the blob day
//             var firstEvent = TestUtil.GenerateInteractionEvents(count: 1, startOffset: 1000, startDateTime: new DateTime(2017, 9, 21, 18, 24, 45)).First();
//             // ========================================== Action ===============================================
//             await block.Input.SendAsync(new List<InteractionEvent>() { uploadHelper.Serialize(firstEvent) });
//             block.Input.Complete();
//             await block.Completion;
//
//             // ========================================== Assert ===============================================
//             //verify GetBlobClient, once for creating new blob
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.IsAny<string>()), Times.Once);
//
//             //verify PutBlockListAsync
//             this.mockTenantBlockBlob.Verify(blob => blob.PutBlockListAsync(
//                 It.IsAny<IEnumerable<string>>(),
//                 null,
//                 It.IsAny<BlobRequestOptions>(),
//                 It.IsAny<OperationContext>(),
//                 It.IsAny<CancellationToken>()), Times.Once);
//         }
//
//         [TestMethod, Description("Create a new blob with the date of first non-dangling event")]
//         [Timeout(time_10s)]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task StorageUploadBlock_CreateNewBlobWithDateOfFirstNonDanglingEventDateAsync()
//         {
//             // ========================================== Arrange ==============================================
//             var block = new StorageUploadBlock(this.options, mockTenantBlobContainer.Object, mockTenantCheckpointBlob.Object, StorageUploadType.Tenant, SystemTimeProvider.Instance, cancellationToken:CancellationToken.None);
//
//             var danglingEvent = TestUtil.GenerateInteractionEvents(isDanglingObservation: true, count: 1, startOffset: 1000, startDateTime: new DateTime(2017, 9, 21, 18, 24, 45)).First();
//
//             //second event happens the same day
//             var joinedEvent = TestUtil.GenerateInteractionEvents(isDanglingObservation: false, count: 1, startOffset: 2000, startDateTime: new DateTime(2017, 09, 25, 19, 21, 13)).First();
//
//             // ========================================== Action ===============================================
//             await block.Input.SendAsync(new List<InteractionEvent>() { uploadHelper.Serialize(danglingEvent), uploadHelper.Serialize(joinedEvent) });
//             block.Input.Complete();
//             await block.Completion;
//
//             // ========================================== Assert ===============================================
//             //verify GetBlobClient once for event upload
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.IsAny<string>()), Times.Exactly(2));
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.Is<string>(s => s == "20170814000000/data/2017/09/25_0000000000.json")), Times.Once);
//             // Defaults to config edit date if there are no events/checkpoint
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.Is<string>(s => s == "20170814000000/skipped-data/2017/09/21_0000000000.json")), Times.Once);
//
//             //verify PutBlockListAsync
//             this.mockTenantBlockBlob.Verify(blob => blob.PutBlockListAsync(
//                  It.IsAny<IEnumerable<string>>(),
//                  null,
//                  It.IsAny<BlobRequestOptions>(),
//                  It.IsAny<OperationContext>(),
//                  It.IsAny<CancellationToken>()), Times.Exactly(2));
//         }
//
//         [TestMethod, Description("Dangling events go to skipped blob instead of learnable events blob.")]
//         [Timeout(time_10s)]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task StorageUploadBlock_SkippedBlob_WhenDanglingAfterInteractionAsync()
//         {
//             // ========================================== Arrange ==============================================
//             var block = new StorageUploadBlock(this.options, mockTenantBlobContainer.Object, mockTenantCheckpointBlob.Object, StorageUploadType.Tenant, SystemTimeProvider.Instance, cancellationToken:CancellationToken.None);
//             var joinedEvent = TestUtil.GenerateInteractionEvents(isDanglingObservation: false, count: 1, startOffset: 1000, startDateTime: new DateTime(2017, 9, 21, 18, 24, 45)).First();
//             var firstDangling = TestUtil.GenerateInteractionEvents(isDanglingObservation: true, count: 1, startOffset: 1000, startDateTime: new DateTime(2017, 9, 20, 18, 24, 45)).First();
//             var secondDangling = TestUtil.GenerateInteractionEvents(isDanglingObservation: true, count: 1, startOffset: 2000, startDateTime: new DateTime(2017, 09, 28, 19, 21, 13)).First();
//             var thirdDangling = TestUtil.GenerateInteractionEvents(isDanglingObservation: true, count: 1, startOffset: 3000, startDateTime: new DateTime(2017, 9, 29, 16, 42, 12)).First();
//             var skippedEvent = TestUtil.GenerateInteractionEvents(deferredAction: true, count: 1, startOffset: 3000, startDateTime: new DateTime(2017, 9, 30, 16, 42, 12)).First();
//
//             // ========================================== Action ===============================================
//             await block.Input.SendAsync(
//                 new List<InteractionEvent>() {
//                     uploadHelper.Serialize(joinedEvent),
//                     uploadHelper.Serialize(firstDangling),
//                     uploadHelper.Serialize(secondDangling),
//                     uploadHelper.Serialize(thirdDangling),
//                     uploadHelper.Serialize(skippedEvent)
//                 });
//             block.Input.Complete();
//             await block.Completion;
//
//             // ========================================== Assert ===============================================
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.IsAny<string>()), Times.Exactly(2));
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient("20170814000000/data/2017/09/21_0000000000.json"), Times.Once);
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient("20170814000000/skipped-data/2017/09/20_0000000000.json"), Times.Once);
//
//             //verify PutBlockListAsync
//             this.mockTenantBlockBlob.Verify(blob => blob.PutBlockListAsync(
//                 It.IsAny<IEnumerable<string>>(),
//                 null,
//                 It.IsAny<BlobRequestOptions>(),
//                 It.IsAny<OperationContext>(),
//                 It.IsAny<CancellationToken>()), Times.Exactly(2));
//         }
//
//         [TestMethod, Description("List of all dangling events will update skipped blob.")]
//         [Timeout(time_10s)]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task StorageUploadBlock_UpdatesSkippedBlob_WhenAllDanglingAsync()
//         {
//             // ========================================== Arrange ==============================================
//             var block = new StorageUploadBlock(this.options, mockTenantBlobContainer.Object, mockTenantCheckpointBlob.Object, StorageUploadType.Tenant, SystemTimeProvider.Instance, cancellationToken:CancellationToken.None);
//
//             var firstDangling = TestUtil.GenerateInteractionEvents(isDanglingObservation: true, count: 1, startOffset: 1000, startDateTime: new DateTime(2017, 9, 20, 18, 24, 45)).First();
//             var secondDangling = TestUtil.GenerateInteractionEvents(isDanglingObservation: true, count: 1, startOffset: 2000, startDateTime: new DateTime(2017, 09, 28, 19, 21, 13)).First();
//             var thirdDangling = TestUtil.GenerateInteractionEvents(isDanglingObservation: true, count: 1, startOffset: 3000, startDateTime: new DateTime(2017, 9, 29, 16, 42, 12)).First();
//
//             // ========================================== Action ===============================================
//             await block.ResumeAsync();
//             await block.Input.SendAsync(
//                 new List<InteractionEvent>() {
//                     uploadHelper.Serialize(firstDangling),
//                     uploadHelper.Serialize(secondDangling),
//                     uploadHelper.Serialize(thirdDangling)
//                 });
//             block.Input.Complete();
//             await block.Completion;
//
//             // ========================================== Assert ===============================================
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.IsAny<string>()), Times.Once);
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.Is<string>(s => s == "20170814000000/skipped-data/2017/09/20_0000000000.json")), Times.Once);
//
//             //verify cooked logs block appended and committed
//             this.mockTenantBlockBlob.Verify(blob => blob.PutBlockListAsync(
//                 It.IsAny<IEnumerable<string>>(),
//                 null,
//                 It.IsAny<BlobRequestOptions>(),
//                 It.IsAny<OperationContext>(),
//                 It.IsAny<CancellationToken>()), Times.Once);
//         }
//
//         [TestMethod, Description("List of skipped interactions will create new blob")]
//         [Timeout(time_10s)]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task StorageUploadBlock_CreatesNewSkippedBlob_WhenSkippedInteractionAsync()
//         {
//             // ========================================== Arrange ==============================================
//             var block = new StorageUploadBlock(this.options, mockTenantBlobContainer.Object, mockTenantCheckpointBlob.Object, StorageUploadType.Tenant, SystemTimeProvider.Instance, cancellationToken:CancellationToken.None);
//
//             var firstSkipped = TestUtil.GenerateInteractionEvents(deferredAction: true, count: 1, startOffset: 1000, startDateTime: new DateTime(2017, 9, 20, 18, 24, 45)).First();
//             var secondSkipped = TestUtil.GenerateInteractionEvents(deferredAction: true, count: 1, startOffset: 2000, startDateTime: new DateTime(2017, 9, 28, 19, 21, 13)).First();
//             var thirdSkipped = TestUtil.GenerateInteractionEvents(deferredAction: true, count: 1, startOffset: 3000, startDateTime: new DateTime(2017, 9, 29, 16, 42, 12)).First();
//
//             // ========================================== Action ===============================================
//             await block.ResumeAsync();
//             await block.Input.SendAsync(
//                 new List<InteractionEvent>() {
//                     uploadHelper.Serialize(firstSkipped),
//                     uploadHelper.Serialize(secondSkipped),
//                     uploadHelper.Serialize(thirdSkipped)
//                 });
//             block.Input.Complete();
//             await block.Completion;
//
//             // ========================================== Assert ===============================================
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.IsAny<string>()), Times.Once);
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient("20170814000000/skipped-data/2017/09/20_0000000000.json"), Times.Once);
//
//             //verify cooked logs block appended and committed
//             this.mockTenantBlockBlob.Verify(blob => blob.PutBlockListAsync(
//                 It.IsAny<IEnumerable<string>>(),
//                 null,
//                 It.IsAny<BlobRequestOptions>(),
//                 It.IsAny<OperationContext>(),
//                 It.IsAny<CancellationToken>()), Times.Once);
//         }
//
//         [TestMethod, Description("List of skipped events will not be uploaded when option is false")]
//         [Timeout(time_10s)]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task StorageUploadBlock_DoNotUploadSkippedLogs_WhenOptionIsTurnedOffAsync()
//         {
//             // ========================================== Arrange ==============================================
//             this.options.UploadSkippedLogs = false;
//             var block = new StorageUploadBlock(this.options, mockTenantBlobContainer.Object, mockTenantCheckpointBlob.Object, StorageUploadType.Tenant, SystemTimeProvider.Instance, cancellationToken:CancellationToken.None);
//             var firstSkipped = TestUtil.GenerateInteractionEvents(deferredAction: true, count: 1, startOffset: 1000, startDateTime: new DateTime(2017, 9, 20, 18, 24, 45)).First();
//             var secondSkipped = TestUtil.GenerateInteractionEvents(deferredAction: true, count: 1, startOffset: 2000, startDateTime: new DateTime(2017, 9, 28, 19, 21, 13)).First();
//             var thirdSkipped = TestUtil.GenerateInteractionEvents(deferredAction: true, count: 1, startOffset: 3000, startDateTime: new DateTime(2017, 9, 29, 16, 42, 12)).First();
//             var dangling = TestUtil.GenerateInteractionEvents(isDanglingObservation: true, count: 1, startOffset: 3000, startDateTime: new DateTime(2017, 9, 29, 16, 42, 12)).First();
//
//             // ========================================== Action ===============================================
//             await block.ResumeAsync();
//             await block.Input.SendAsync(
//                 new List<InteractionEvent>() {
//                     uploadHelper.Serialize(firstSkipped),
//                     uploadHelper.Serialize(secondSkipped),
//                     uploadHelper.Serialize(thirdSkipped),
//                     uploadHelper.Serialize(dangling)
//                 });
//             block.Input.Complete();
//             await block.Completion;
//
//             // ========================================== Assert ===============================================
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.IsAny<string>()), Times.Never);
//
//             //verify cooked logs block appended and committed
//             this.mockTenantBlockBlob.Verify(blob => blob.PutBlockListAsync(
//                 It.IsAny<IEnumerable<string>>(),
//                 It.IsAny<AccessCondition>(),
//                 It.IsAny<BlobRequestOptions>(),
//                 It.IsAny<OperationContext>(),
//                 It.IsAny<CancellationToken>()), Times.Never);
//         }
//
//         [TestMethod, Description("List of skipped events will not be uploaded when option is false")]
//         [Timeout(time_10s)]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task StorageUploadBlock_UploadInteractions_WhenNotUploadingSkippedLogs()
//         {
//             // ========================================== Arrange ==============================================
//             this.options.UploadSkippedLogs = false;
//             var block = new StorageUploadBlock(this.options, mockTenantBlobContainer.Object, mockTenantCheckpointBlob.Object, StorageUploadType.Tenant, SystemTimeProvider.Instance, cancellationToken:CancellationToken.None);
//             var joinedEvent = TestUtil.GenerateInteractionEvents(isDanglingObservation: false, count: 1, startOffset: 1000, startDateTime: new DateTime(2017, 9, 21, 18, 24, 45)).First();
//             var firstSkipped = TestUtil.GenerateInteractionEvents(deferredAction: true, count: 1, startOffset: 1000, startDateTime: new DateTime(2017, 9, 20, 18, 24, 45)).First();
//             var secondSkipped = TestUtil.GenerateInteractionEvents(deferredAction: true, count: 1, startOffset: 2000, startDateTime: new DateTime(2017, 9, 28, 19, 21, 13)).First();
//             var thirdSkipped = TestUtil.GenerateInteractionEvents(deferredAction: true, count: 1, startOffset: 3000, startDateTime: new DateTime(2017, 9, 29, 16, 42, 12)).First();
//             var dangling = TestUtil.GenerateInteractionEvents(isDanglingObservation: true, count: 1, startOffset: 3000, startDateTime: new DateTime(2017, 9, 29, 16, 42, 12)).First();
//
//             // ========================================== Action ===============================================
//             await block.ResumeAsync();
//             await block.Input.SendAsync(
//                 new List<InteractionEvent>() {
//                     uploadHelper.Serialize(joinedEvent),
//                     uploadHelper.Serialize(firstSkipped),
//                     uploadHelper.Serialize(secondSkipped),
//                     uploadHelper.Serialize(thirdSkipped),
//                     uploadHelper.Serialize(dangling)
//                 });
//             block.Input.Complete();
//             await block.Completion;
//
//             // ========================================== Assert ===============================================
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.IsAny<string>()), Times.Once);
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient("20170814000000/data/2017/09/21_0000000000.json"), Times.Once);
//
//             //verify PutBlockListAsync
//             this.mockTenantBlockBlob.Verify(blob => blob.PutBlockListAsync(
//                 It.IsAny<IEnumerable<string>>(),
//                 null,
//                 It.IsAny<BlobRequestOptions>(),
//                 It.IsAny<OperationContext>(),
//                 It.IsAny<CancellationToken>()), Times.Once);
//         }
//
//         [TestMethod, Description("We can resume from a checkpoint with no skipped data section because it was created before the skipped data split.")]
//         [Timeout(time_10s)]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task StorageUploadBlock_ResumeFromCheckpoint_LegacyCheckpointAsync()
//         {
//             string initialCheckpointString = "{\"BlockPosition\":{\"BlobName\":\"20170814000000/data/2017/09/14_0000000000.json\",\"BlockName\":\"0001\"},\"EventPosition\":{\"Offset\":\"1\",\"EnqueuedTimeUtc\":\"0001-01-01T00:00:00\"}}";
//
//             var mockTenantCheckpointBlob = new Mock<BlobClient>(MockBehavior.Loose, new Uri("http://bogus/myaccount/blob"));
//             mockTenantCheckpointBlob.Setup(m => m.ExistsAsync()).Returns(Task<bool>.FromResult(true));
//             mockTenantCheckpointBlob.Setup(m => m.DownloadTextAsync()).Returns(Task<string>.FromResult(initialCheckpointString));
//             mockTenantCheckpointBlob.Setup(m => m.UploadTextAsync(It.IsAny<string>()));
//
//             var block = new StorageUploadBlock(this.options, mockTenantBlobContainer.Object, mockTenantCheckpointBlob.Object, StorageUploadType.Tenant, SystemTimeProvider.Instance, cancellationToken:CancellationToken.None);
//
//             var dangling = TestUtil.GenerateInteractionEvents(isDanglingObservation: true, count: 1, startOffset: 1000, startDateTime: new DateTime(2017, 9, 20, 18, 24, 45)).First();
//             var joined = TestUtil.GenerateInteractionEvents(isDanglingObservation: false, count: 1, startOffset: 2000, startDateTime: new DateTime(2017, 09, 28, 19, 21, 13)).First();
//
//             await block.ResumeAsync();
//             await block.Input.SendAsync(
//                 new List<InteractionEvent>() {
//                     uploadHelper.Serialize(dangling),
//                     uploadHelper.Serialize(joined),
//                 });
//             block.Input.Complete();
//             await block.Completion;
//
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.IsAny<string>()), Times.Exactly(3));
//             // One call is to resume the checkpoint, the others are to update it.
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient("20170814000000/data/2017/09/14_0000000000.json"), Times.Once);
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient("20170814000000/skipped-data/2017/09/20_0000000000.json"), Times.Once);
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient("20170814000000/data/2017/09/28_0000000000.json"), Times.Once);
//
//             //verify cooked logs block appended and committed
//             this.mockTenantBlockBlob.Verify(blob => blob.PutBlockListAsync(
//                 It.IsAny<IEnumerable<string>>(),
//                 null,
//                 It.IsAny<BlobRequestOptions>(),
//                 It.IsAny<OperationContext>(),
//                 It.IsAny<CancellationToken>()), Times.Exactly(2));
//
//             var expectedCheckpoint = new StorageCheckpoint()
//             {
//                 // This BlobName should be 20170814000000/data/2017/09/28_0000000000.json for learnable data
//                 // and 20170814000000/skipped-data/2017/08/14_0000000000.json for skipped data.
//                 // In this validation we use the default returned by the mock.
//                 // We verified above for the GetBlobClient call that the path is correct.
//                 BlockPosition = new BlockPosition()
//                 {
//                     BlobName = "20170814000000/data/2017/08/14_0000000000.json",
//                     BlockName = "0001",
//                     FileFormat = JoinedLogFormat.DSJSON
//                 },
//                 BlobProperty = new BlobProperty()
//                 {
//                     BlobName = "20170814000000/data/2017/08/14_0000000000.json",
//                     Length = 220
//                 },
//                 SkippedDataBlockPosition = new BlockPosition()
//                 {
//                     BlobName = "20170814000000/data/2017/08/14_0000000000.json",
//                     BlockName = "0001",
//                     FileFormat = JoinedLogFormat.DSJSON
//                 },
//                 SkippedDataBlobProperty = new BlobProperty()
//                 {
//                     BlobName = "20170814000000/data/2017/08/14_0000000000.json",
//                     Length = 2
//                 },
//                 EventPosition = new EventPosition()
//                 {
//                     Offset = 2000,
//                     EnqueuedTimeUtc = new DateTime(2017, 09, 28, 19, 21, 13)
//                 }
//             };
//
//             mockTenantCheckpointBlob.Verify(m => m.UploadTextAsync(JsonConvert.SerializeObject(expectedCheckpoint)), Times.Once);
//         }
//
//         [TestMethod, Description("We can resume from a checkpoint with no skipped data.")]
//         [Timeout(time_10s)]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task StorageUploadBlock_ResumeFromCheckpoint_WhenAllLearnableAsync()
//         {
//             StorageCheckpoint cp = new StorageCheckpoint()
//             {
//                 BlockPosition = new BlockPosition()
//                 {
//                     BlobName = "20170814000000/data/2017/08/14_0000000000.json",
//                     BlockName = "0001",
//                     FileFormat = JoinedLogFormat.DSJSON
//                 },
//                 EventPosition = new EventPosition()
//                 {
//                     Offset = 1
//                 }
//             };
//
//             string initialCheckpointString = JsonConvert.SerializeObject(cp);
//
//             var mockTenantCheckpointBlob = new Mock<BlobClient>(MockBehavior.Loose, new Uri("http://bogus/myaccount/blob"));
//             mockTenantCheckpointBlob.Setup(m => m.ExistsAsync()).Returns(Task<bool>.FromResult(true));
//             mockTenantCheckpointBlob.Setup(m => m.DownloadTextAsync()).Returns(Task<string>.FromResult(initialCheckpointString));
//             mockTenantCheckpointBlob.Setup(m => m.UploadTextAsync(It.IsAny<string>()));
//
//             var block = new StorageUploadBlock(this.options, mockTenantBlobContainer.Object, mockTenantCheckpointBlob.Object, StorageUploadType.Tenant, SystemTimeProvider.Instance, cancellationToken:CancellationToken.None);
//
//             var dangling = TestUtil.GenerateInteractionEvents(isDanglingObservation: true, count: 1, startOffset: 1000, startDateTime: new DateTime(2017, 9, 20, 18, 24, 45)).First();
//             var joined = TestUtil.GenerateInteractionEvents(isDanglingObservation: false, count: 1, startOffset: 2000, startDateTime: new DateTime(2017, 09, 28, 19, 21, 13)).First();
//
//             await block.ResumeAsync();
//             await block.Input.SendAsync(
//                 new List<InteractionEvent>() {
//                     uploadHelper.Serialize(dangling),
//                     uploadHelper.Serialize(joined),
//                 });
//             block.Input.Complete();
//             await block.Completion;
//
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.IsAny<string>()), Times.Exactly(3));
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient("20170814000000/data/2017/08/14_0000000000.json"), Times.Once);
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient("20170814000000/skipped-data/2017/09/20_0000000000.json"), Times.Once);
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient("20170814000000/data/2017/09/28_0000000000.json"), Times.Once);
//
//             //verify cooked logs block appended and committed
//             this.mockTenantBlockBlob.Verify(blob => blob.PutBlockListAsync(
//                 It.IsAny<IEnumerable<string>>(),
//                 null,
//                 It.IsAny<BlobRequestOptions>(),
//                 It.IsAny<OperationContext>(),
//                 It.IsAny<CancellationToken>()), Times.Exactly(2));
//
//             var expectedCheckpoint = new StorageCheckpoint()
//             {
//                 // This BlobName should be 20170814000000/data/2017/09/28_0000000000.json for learnable data
//                 // and 20170814000000/skipped-data/2017/08/14_0000000000.json for skipped data.
//                 // In this validation we use the default returned by the mock.
//                 // We verified above for the GetBlobClient call that the path is correct.
//                 BlockPosition = new BlockPosition()
//                 {
//                     BlobName = "20170814000000/data/2017/08/14_0000000000.json",
//                     BlockName = "0001",
//                     FileFormat = JoinedLogFormat.DSJSON
//                 },
//                 BlobProperty = new BlobProperty()
//                 {
//                     BlobName = "20170814000000/data/2017/08/14_0000000000.json",
//                     Length = 220
//                 },
//                 SkippedDataBlockPosition = new BlockPosition()
//                 {
//                     BlobName = "20170814000000/data/2017/08/14_0000000000.json",
//                     BlockName = "0001",
//                     FileFormat = JoinedLogFormat.DSJSON
//                 },
//                 SkippedDataBlobProperty = new BlobProperty()
//                 {
//                     BlobName = "20170814000000/data/2017/08/14_0000000000.json",
//                     Length = 2
//                 },
//                 EventPosition = new EventPosition()
//                 {
//                     Offset = 2000,
//                     EnqueuedTimeUtc = new DateTime(2017, 09, 28, 19, 21, 13)
//                 }
//             };
//
//             mockTenantCheckpointBlob.Verify(m => m.UploadTextAsync(JsonConvert.SerializeObject(expectedCheckpoint)), Times.Once);
//         }
//
//         [TestMethod, Description("We can resume from a checkpoint with no learnable data.")]
//         [Timeout(time_10s)]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task StorageUploadBlock_ResumeFromCheckpoint_WhenAllSkippedAsync()
//         {
//             StorageCheckpoint cp = new StorageCheckpoint()
//             {
//                 SkippedDataBlockPosition = new BlockPosition()
//                 {
//                     BlobName = "20170814000000/skipped-data/2017/08/14_0000000000.json",
//                     BlockName = "0001",
//                     FileFormat = JoinedLogFormat.DSJSON
//                 },
//                 EventPosition = new EventPosition()
//                 {
//                     Offset = 1
//                 }
//             };
//
//             string initialCheckpointString = JsonConvert.SerializeObject(cp);
//
//             var mockTenantCheckpointBlob = new Mock<BlobClient>(MockBehavior.Loose, new Uri("http://bogus/myaccount/blob"));
//             mockTenantCheckpointBlob.Setup(m => m.ExistsAsync()).Returns(Task<bool>.FromResult(true));
//             mockTenantCheckpointBlob.Setup(m => m.DownloadTextAsync()).Returns(Task<string>.FromResult(initialCheckpointString));
//             mockTenantCheckpointBlob.Setup(m => m.UploadTextAsync(It.IsAny<string>()));
//
//             var block = new StorageUploadBlock(this.options, mockTenantBlobContainer.Object, mockTenantCheckpointBlob.Object, StorageUploadType.Tenant, SystemTimeProvider.Instance, cancellationToken:CancellationToken.None);
//
//             var dangling = TestUtil.GenerateInteractionEvents(isDanglingObservation: true, count: 1, startOffset: 1000, startDateTime: new DateTime(2017, 9, 20, 18, 24, 45)).First();
//             var joined = TestUtil.GenerateInteractionEvents(isDanglingObservation: false, count: 1, startOffset: 2000, startDateTime: new DateTime(2017, 09, 28, 19, 21, 13)).First();
//
//             await block.ResumeAsync();
//             await block.Input.SendAsync(
//                 new List<InteractionEvent>() {
//                     uploadHelper.Serialize(dangling),
//                     uploadHelper.Serialize(joined),
//                 });
//             block.Input.Complete();
//             await block.Completion;
//
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient(It.IsAny<string>()), Times.Exactly(3));
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient("20170814000000/skipped-data/2017/08/14_0000000000.json"), Times.Once);
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient("20170814000000/skipped-data/2017/09/20_0000000000.json"), Times.Once);
//             this.mockTenantBlobContainer.Verify(container => container.GetBlobClient("20170814000000/data/2017/09/28_0000000000.json"), Times.Once);
//
//             //verify cooked logs block appended and committed
//             this.mockTenantBlockBlob.Verify(blob => blob.PutBlockListAsync(
//                 It.IsAny<IEnumerable<string>>(),
//                 null,
//                 It.IsAny<BlobRequestOptions>(),
//                 It.IsAny<OperationContext>(),
//                 It.IsAny<CancellationToken>()), Times.Exactly(2));
//
//             var expectedCheckpoint = new StorageCheckpoint()
//             {
//                 // This BlobName should be 20170814000000/data/2017/09/28_0000000000.json for learnable data
//                 // and 20170814000000/skipped-data/2017/08/14_0000000000.json for skipped data.
//                 // In this validation we use the default returned by the mock.
//                 // We verified above for the GetBlobClient call that the path is correct.
//                 BlockPosition = new BlockPosition()
//                 {
//                     BlobName = "20170814000000/data/2017/08/14_0000000000.json",
//                     BlockName = "0001",
//                     FileFormat = JoinedLogFormat.DSJSON
//                 },
//                 BlobProperty = new BlobProperty()
//                 {
//                     BlobName = "20170814000000/data/2017/08/14_0000000000.json",
//                     Length = 220
//                 },
//                 SkippedDataBlockPosition = new BlockPosition()
//                 {
//                     BlobName = "20170814000000/data/2017/08/14_0000000000.json",
//                     BlockName = "0001",
//                     FileFormat = JoinedLogFormat.DSJSON
//                 },
//                 SkippedDataBlobProperty = new BlobProperty()
//                 {
//                     BlobName = "20170814000000/data/2017/08/14_0000000000.json",
//                     Length = 2
//                 },
//                 EventPosition = new EventPosition()
//                 {
//                     Offset = 2000,
//                     EnqueuedTimeUtc = new DateTime(2017, 09, 28, 19, 21, 13)
//                 }
//             };
//
//             mockTenantCheckpointBlob.Verify(m => m.UploadTextAsync(JsonConvert.SerializeObject(expectedCheckpoint)), Times.Once);
//         }
//
//
//         [TestMethod, Description("We should retain events when we receive a StorageException rather than discard them.")]
//         [Timeout(time_15s)]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task StorageUploadBlock_EventsRetained_WhenStorageUploadFailsAsync()
//         {
//             // ========================================== Arrange ==============================================
//             var block = new StorageUploadBlock(this.options, mockTenantBlobContainer.Object, mockTenantCheckpointBlob.Object, StorageUploadType.Tenant, SystemTimeProvider.Instance, cancellationToken:CancellationToken.None);
//
//             List<InteractionEvent> events = TestUtil.GenerateInteractionEvents(count: 5);
//             var serializedEvents = events.Select(x => uploadHelper.Serialize(x)).ToList();
//             var blockStream = new ConcatenatedByteStreams(serializedEvents.SelectMany(e => e.Segments).ToArray());
//
//             this.mockTenantBlockBlob.SetupSequence(blob => blob.PutBlockAsync(
//                     It.IsAny<string>(),
//                     It.IsAny<Stream>(),
//                     It.IsAny<string>(),
//                     It.IsAny<AccessCondition>(),
//                     It.IsAny<BlobRequestOptions>(),
//                     It.IsAny<OperationContext>(),
//                     It.IsAny<CancellationToken>()
//                 )).Throws(new StorageException())
//                   .Returns(Task.CompletedTask);
//
//             // ========================================== Action ===============================================
//             await block.Input.SendAsync(serializedEvents);
//             block.Input.Complete();
//             await block.Completion;
//
//             // ========================================== Assert ===============================================
//             // Verify that we are retrying PutBlock with the same number of events after catching an exception 
//             this.mockTenantBlockBlob.Verify(blob => blob.PutBlockAsync(
//                     It.IsAny<string>(),
//                     It.Is<ConcatenatedByteStreams>(stream => stream.Length == blockStream.Length),
//                     It.IsAny<string>(),
//                     It.IsAny<AccessCondition>(),
//                     It.IsAny<BlobRequestOptions>(),
//                     It.IsAny<OperationContext>(),
//                     It.IsAny<CancellationToken>()
//                 ), Times.Exactly(2));
//         }
//
//         private void SetupMockedBlobContainer(Mock<BlobContainerClient> mockedBlobContainer, Mock<BlobClient> mockedBlockBlob, Mock<BlobClient> mockedCheckpointBlob)
//         {
//             mockedBlobContainer.Setup(client => client.CreateIfNotExistsAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(true));
//             mockedBlobContainer.Setup(container => container.GetBlobClient(It.IsAny<string>())).Returns(mockedBlockBlob.Object);
//             mockedBlobContainer.Setup(container => container.ListBlobsSegmentedAsync(
//                 It.IsAny<string>(),
//                 It.IsAny<bool>(),
//                 It.IsAny<BlobListingDetails>(),
//                 It.IsAny<int?>(),
//                 It.IsAny<BlobContinuationToken>(),
//                 It.IsAny<BlobRequestOptions>(),
//                 It.IsAny<OperationContext>(),
//                 It.IsAny<CancellationToken>())).Returns(Task.FromResult(new BlobResultSegment(new List<IListBlobItem>(), null)));
//
//             //(common setup to all tests) setup the client: verify that methods are called on the correct container name
//             mockedBlobContainer
//                 .Setup(client => client.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
//                 .Returns(Task.FromResult(true));
//             mockedBlobContainer
//                 .Setup(container => container.GetBlobClient(storageCheckpointBlobName))
//                 .Returns(mockedCheckpointBlob.Object);
//             mockedBlobContainer
//                 .Setup(container => container.GetBlobClient(It.Is<string>(s => s != storageCheckpointBlobName)))
//                 .Returns(mockedBlockBlob.Object);
//         }
//
//         private void UploadBlockAppendAssertHelper(Mock<BlobContainerClient> mockedBlobContainer,
//             Mock<BlobClient> mockedCloudBlockBlob, Mock<BlobClient> mockedCheckpointBlob = null)
//         {
//             // in tenant storage, once for creating new cooked log blob
//             // in mirror storage, once for creating new cooked log blob
//             mockedBlobContainer.Verify(container => container.GetBlobClient(It.IsAny<string>()), Times.Once);
//
//             //verify new blob created
//             mockedBlobContainer.Verify(container => container.GetBlobClient(It.Is<string>(s => s == "20170814000000/data/2017/09/21_0000000000.json")), Times.Once);
//             //verify events uploaded
//             mockedCloudBlockBlob.Verify(blob => blob.PutBlockListAsync(
//                 It.IsAny<IEnumerable<string>>(),
//                 null,
//                 It.IsAny<BlobRequestOptions>(),
//                 It.IsAny<OperationContext>(),
//                 It.IsAny<CancellationToken>()), Times.Once);
//
//             if (mockedCheckpointBlob != null)
//             {
//                 // verify checkpoint uploaded
//                 mockedCheckpointBlob.Verify(blob => blob.UploadTextAsync(It.IsAny<string>()), Times.Once);
//             }
//         }
//     }
// }