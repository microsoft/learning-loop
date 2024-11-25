// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// using CommonTest;
// using Microsoft.Azure.Storage.Blob;
// using Microsoft.DecisionService.Common;
// using Microsoft.DecisionService.Common.Data;
// using Microsoft.DecisionService.Common.Trainer.Operations;
// using Microsoft.DecisionService.Instrumentation;
// using Microsoft.DecisionService.OnlineTrainer;
// using Microsoft.DecisionService.OnlineTrainer.Join;
// using Microsoft.DecisionService.OnlineTrainer.Operations;
// using Microsoft.VisualStudio.TestTools.UnitTesting;
// using Moq;
// using System;
// using System.Linq;
// using System.Threading;
// using System.Threading.Tasks;
// using System.Threading.Tasks.Dataflow;
// using Azure.Storage.Blobs;
// using Azure.Storage.Blobs.Models;
// using Microsoft.Extensions.Logging;
// using Microsoft.Extensions.Logging.Abstractions;
//
// namespace Tests.Operations
// {
//     [TestClass]
//     public class StorageLogSerializeBlockTest
//     {
//         private Mock<BlobContainerClient> mockBlobContainer;
//         private Mock<BlobClient> mockBlockBlob;
//         private Mock<UploadHelper> uploadHelper;
//         private StorageBlockOptions options;
//
//         [TestInitialize]
//         public void TestInit()
//         {
//             //the mock construction requires an URI
//             var uri = new Uri("http://bogus/myaccount/blob");
//             this.mockBlobContainer = new Mock<BlobContainerClient>(MockBehavior.Loose, uri);
//             this.mockBlockBlob = new Mock<CloudBlockBlob>(MockBehavior.Loose, uri);
//
//             this.mockBlobContainer.Setup(client => client.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>())).Returns(Task.FromResult(true));
//             this.mockBlobContainer.Setup(container => container.GetBlobClient(It.IsAny<string>())).Returns(this.mockBlockBlob.Object);
//             
//             //the mock construction requires an URI
//             this.options = new StorageBlockOptions()
//             {
//                 LastConfigurationEditDate = new DateTime(2017, 8, 14, 0, 0, 0),
//                 MaximumFlushLatency = TimeSpan.FromSeconds(10),
//
//             };
//
//             this.uploadHelper = new Mock<UploadHelper>(MockBehavior.Loose, true, (float?)0, null);
//         }
//
//         [TestMethod, Description("Serialize log without log mirror setting")]
//         [Timeout(10000)]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task StorageLogSerializeBlockTest_Null_LogMirrorSettingAsync()
//         {
//             // ========================================== Arrange ==============================================
//             var block = new StorageLogSerializeBlock(this.options, mockBlobContainer.Object, logMirrorSettings: null, uploadHelper: this.uploadHelper.Object);
//             await block.SetupBlocksAsync();
//             var firstEvent = TestUtil.GenerateInteractionEvents().First();
//
//             // ========================================== Action ===============================================
//             await block.SendAsync(firstEvent);
//             block.Complete();
//             await block.Completion;
//
//             // ========================================== Assert ===============================================
//             //verify PutBlockListAsync
//             this.uploadHelper.Verify(uploadHelper => uploadHelper.Serialize(firstEvent), Times.Once);
//         }
//
//         [TestMethod, Description("Serialize log with log mirror setting")]
//         [Timeout(10000)]
//         [TestCategory("Decision Service/Online Trainer")]
//         public async Task StorageLogSerializeBlockTest_LogMirrorSettingAsync()
//         {
//             var setting = new LogMirrorSettings() { Enabled = true, SasUri = "http://www.microsoft.com" };
//             // ========================================== Arrange ==============================================
//             Mock<AzureStorageBlobHelper> azureStorageBlobHelper = new Mock<AzureStorageBlobHelper>();
//             azureStorageBlobHelper.Setup(x => x.IsContainerSasUriWritableAsync(It.IsAny<Uri>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(true));
//             var block = new StorageLogSerializeBlock(this.options, mockBlobContainer.Object, setting, this.uploadHelper.Object, azureStorageBlobHelper: azureStorageBlobHelper.Object);
//             await block.SetupBlocksAsync();
//             var firstEvent = TestUtil.GenerateInteractionEvents().First();
//
//             // ========================================== Action ===============================================
//             await block.SendAsync(firstEvent);
//             block.Complete();
//             await block.Completion;
//
//             // ========================================== Assert ===============================================
//             //verify PutBlockListAsync
//             this.uploadHelper.Verify(uploadHelper => uploadHelper.Serialize(firstEvent), Times.Once);
//
//             var logMirrorBlock = block.LogMirrorUploadBlock;
//             var tenantStorageBlock = block.TenantStorageBlock;
//             Assert.AreEqual(logMirrorBlock.BlobDay, tenantStorageBlock.BlobDay);
//             Assert.AreEqual(logMirrorBlock.BlobIndex, tenantStorageBlock.BlobIndex);
//             Assert.AreEqual(logMirrorBlock.BlockCount, tenantStorageBlock.BlockCount);
//         }
//     }
// }
