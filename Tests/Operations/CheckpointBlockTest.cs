// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.OnlineTrainer.Operations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.DecisionService.Common.Storage;
using System.IO;
using System.Threading;
using System.Collections.Generic;

namespace Tests.Operations
{
    [TestClass]
    public class CheckpointBlockTest
    {
        private Mock<IStorageFactory> mockBlobClient;
        private Mock<IBlobContainerClient> mockBlobContainer;
        private Mock<IBlobClient> mockModelBlob;
        private Mock<IBlockStoreProvider> mockBlockStoreProvider;
        private Mock<IBlockStore> mockBlockStore;

        [TestInitialize]
        public void TestInit()
        {
            //init mocks, and specify a default ctor parameter to make it valid
            this.mockBlobClient = new Mock<IStorageFactory>(MockBehavior.Loose);
            this.mockBlobContainer = new Mock<IBlobContainerClient>(MockBehavior.Loose);
            this.mockModelBlob = new Mock<IBlobClient>(MockBehavior.Loose);
            this.mockBlockStoreProvider = new Mock<IBlockStoreProvider>(MockBehavior.Loose);
            this.mockBlockStore = new Mock<IBlockStore>(MockBehavior.Loose);

            //(common setup to all tests) setup the client: verify that methods are called on the correct container name
            this.mockBlobClient.Setup(client => client.CreateBlobContainerClient(It.IsAny<string>())).Returns(this.mockBlobContainer.Object);
            this.mockBlobContainer.Setup(container => container.GetBlobClient(It.IsAny<string>())).Returns(this.mockModelBlob.Object);
            this.mockBlobContainer.Setup(container => container.CreateBlockStoreProvider()).Returns(this.mockBlockStoreProvider.Object);
            this.mockBlockStoreProvider.Setup(IBlockStoreProvider => IBlockStoreProvider.GetStore(It.IsAny<string>())).Returns(this.mockBlockStore.Object);
            this.mockBlockStore.Setup(IBlockStore => IBlockStore.MaxBlockSizeInBytes).Returns(1000);
            this.mockBlockStore.Setup(IBlockStore => IBlockStore.MinBlockSizeInBytes).Returns(1);
        }

        [TestMethod, Description("Post a checkpoint, but do not create a historical model")]
        public async Task TestCheckpointAsync()
        {
            var checkpoint = new ModelCheckpoint
            {
                Model = Array.Empty<byte>(), //an empty model
            };
            // mock existence of historical model (when a historical model already exists, it is not created)
            // this.mockBlockBlob.Setup(modelblob => modelblob.ExistsAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(true));
            var block = CreateCheckpointBlock(new DateTime(2017, 08, 14, 0, 0, 0));
            //post the checkpoint
            block.Input.Post(checkpoint);
            block.Input.Complete();
            await block.Completion;

            this.mockBlockStoreProvider.Verify(blockStore => blockStore.GetStore(It.Is<string>(s => s == "20170814000000/checkpoint/current.dat")), Times.Once);
            this.mockBlockStore.Verify(blockStore => blockStore.WriteBlockAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
            this.mockBlockStore.Verify(blockStore => blockStore.CommitBlocksAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);

            // verify that historization is not called (no historical model is created)
            this.mockBlobContainer.Verify(container => container.GetBlobClient(It.Is<string>(s => s.StartsWith("20170814000000/model/2017/09/30"))), Times.Never);
        }

        [TestMethod, Description("Post a checkpoint, and create a historical model")]
        public async Task TestHistorizationAsync()
        {
            CheckpointBlock block = CreateCheckpointBlock(new DateTime(2017, 08, 14, 0, 0, 0));
            byte[] aModel = new byte[] { 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20 };
            var checkpoint = new ModelCheckpoint
            {
                Model = aModel,
                HistoricalModelInfo = new HistoricalModelInfo
                {
                    FirstEventId = "FirstEventId",
                    LastEventId = "LastEventId",
                    ModelId = "ModelId",
                    WasExported = true,
                },
                ReadingPosition = new BlockPosition
                {
                    BlobName = "20170814000000/model/2017/09/30_0.json",
                }
            };

            // capture the uploaded data
            var uploadedData = new List<BinaryData>();
            this.mockModelBlob.Setup(modelblob => modelblob.UploadAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
                .Callback<BinaryData, CancellationToken>((data, token) => uploadedData.Add(data));

            block.Input.Post(checkpoint);
            block.Input.Complete();
            await block.Completion;

            // the model and the checkpoint are saved so 2 calls to WriteBlockAsync and 1 commit
            this.mockBlockStore.Verify(blockStore => blockStore.WriteBlockAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            this.mockBlockStore.Verify(blockStore => blockStore.CommitBlocksAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);

            // the historical model is saved
            this.mockBlobContainer.Verify(container => container.GetBlobClient(It.Is<string>(s => s == "20170814000000/model/2017/09/30_0.vw")), Times.Once);
            this.mockBlobContainer.Verify(container => container.GetBlobClient(It.Is<string>(s => s == "20170814000000/model/2017/09/30_0.json")), Times.Once);

            //verify upload content for the historical model
            this.mockModelBlob.Verify(modelblob => modelblob.UploadAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

            //verify upload content
            var sss = JsonConvert.SerializeObject(checkpoint.HistoricalModelInfo);
            Assert.AreEqual(sss, uploadedData[1].ToString());
        }

        [TestMethod, Description("Make sure invalid warmstart model url is handled and replaced with default checkpoint.")]
        public async Task CheckpointBlock_HandlesInvalidWarmstartModelUrlAsync()
        {
            var block = CreateCheckpointBlock(new DateTime(2017, 08, 14, 0, 0, 0));
            var checkpoint = await block.GetOrUpdateAsync(new Uri("https://fake.blob.core.windows.net/fake/fake.trainer.vw"), null);
            var defaultCheckpoint = new ModelCheckpoint();
            Assert.AreEqual(defaultCheckpoint.Timestamp, checkpoint.Timestamp);
            Assert.AreEqual(defaultCheckpoint.Model, checkpoint.Model);
        }

        private CheckpointBlock CreateCheckpointBlock(DateTime configurationDate)
        {

            return new CheckpointBlock(
                new CheckpointBlockOptions
                {
                    ContainerClient = this.mockBlobContainer.Object,
                    LastConfigurationEditDate = configurationDate,
                    AppId = "app1",
                    CheckpointBlockHelper = new CheckpointBlockHelper(new CheckpointBlockHelperOptions()
                    {
                        AppId = "app1",
                        BlockStoreProvider = mockBlobContainer.Object.CreateBlockStoreProvider(),
                    }, NullLogger.Instance),
                }, NullLogger.Instance);
        }
    }
}
