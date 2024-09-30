// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Storage;
using Microsoft.DecisionService.Common.Trainer.ModelExport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.ModelExport
{
    [TestClass]
    public class BlobModelExportTest
    {
        private const string modelId = "id1";

        private Mock<IBlobContainerClient> mockBlobContainer;
        private Mock<IBlobClient> mockBlob;
        private readonly int stagedModelHistoryLength = 10;
        private readonly string modelMetadata = "{\"modelId\":\"" + modelId + "\"," +
            "\"userDescription\":\"\"," +
            "\"creationDate\":\"2019-01-19T00:00:00Z\"," +
            "\"lastConfigEditDate\":\"2019-01-19T00:00:00Z\"," +
            "\"firstEventId\":\"eventid1\"," +
            "\"lastEventId\":\"eventid2\"," +
            "\"savedInHistory\":false," +
            "\"numberOfEventsLearnedSinceLastExport\":42}";

        [TestInitialize]
        public void TestInit()
        {
            this.mockBlobContainer = new Mock<IBlobContainerClient>(MockBehavior.Loose);
            this.mockBlob = new Mock<IBlobClient>(MockBehavior.Loose);
        }

        [TestMethod]
        [DataRow(false, false, DisplayName = "uploads the model but does not autopublish")]
        [DataRow(true, true, DisplayName = "uploads the model and autopublish succeeds")]
        [DataRow(true, false, DisplayName = "uploads the model and autopublish fails")]
        public async Task TestExportModelAsync(bool autoPublish, bool autoPublishSucceeds)
        {
            //fake model
            byte[] clientModel = Encoding.UTF8.GetBytes("clientModelData");
            byte[] trainerModel = Encoding.UTF8.GetBytes("trainerModelData");

            var mockExportManager = new Mock<ModelExportManager>(MockBehavior.Loose, this.mockBlobContainer.Object, autoPublish, this.stagedModelHistoryLength);
            mockExportManager.Setup(manager => manager.TryUpdateCurrentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(autoPublishSucceeds));

            this.mockBlobContainer.Setup(container => container.GetBlobClient(It.IsAny<string>())).Returns(this.mockBlob.Object);

            var modelExporter = new BlobModelExporter(null, mockBlobContainer.Object, mockExportManager.Object);
            await modelExporter.UploadAsync(clientModel, trainerModel, modelMetadata, CancellationToken.None);
            
            //check that the container was called with correct blob names:
            // 3 times for the upload (client model blob, trainer model blob and metadata blob)
            this.mockBlobContainer.Verify(container => container.GetBlobClient(It.Is<string>(s => s == $"{AzureBlobConstants.ExportedModelsDirectory}/{modelId}.{AzureBlobConstants.ClientModelSuffix}")), Times.Once);
            this.mockBlobContainer.Verify(container => container.GetBlobClient(It.Is<string>(s => s == $"{AzureBlobConstants.ExportedModelsDirectory}/{modelId}.{AzureBlobConstants.TrainerModelSuffix}")), Times.Once);
            this.mockBlobContainer.Verify(container => container.GetBlobClient(It.Is<string>(s => s == $"{AzureBlobConstants.ExportedModelsDirectory}/{modelId}.{AzureBlobConstants.MetadataSuffix}")), Times.Once);

            // check that upload was called 3 times (once for each blob)
            this.mockBlob.Verify(blob => blob.UploadAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()), Times.Exactly(3));

            if (autoPublish)
            {
                // check that TryUpdateCurrentAsync was called
                mockExportManager.Verify(manager => manager.TryUpdateCurrentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }
    }
}
