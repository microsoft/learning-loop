// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Common
{
    [TestClass]
    public class ModelExportManagerTest
    {
        public TestContext TestContext { get; set; }

        private const string testModelId = "id1";
        private const string clientModelDataTxt = "a client model dummy";
        private const string trainerModelDataTxt = "a trainer model dummy";
        private const string modelMetadataTxtTemplate = @"{
            ""modelId"": ""id1"",
            ""userDescription"": """",
            ""creationDate"": ""2019-01-19T00:00:00Z"",
            ""lastConfigEditDate"": ""2019-01-19T00:00:00Z"",
            ""firstEventId"": ""eventid1"",
            ""lastEventId"": ""eventid2"",
            ""savedInHistory"": false,
            ""numberOfEventsLearnedSinceLastExport"": 42
        }";
        private readonly string modelMetadataTxt = Regex.Replace(modelMetadataTxtTemplate, @"\s+", "");

        private readonly BinaryData clientModelData = BinaryData.FromString(clientModelDataTxt);
        private readonly BinaryData trainerModelData = BinaryData.FromString(trainerModelDataTxt);
        private readonly BinaryData modelMetadataData = BinaryData.FromString(modelMetadataTxtTemplate);

        private IStorageFactory storageFactory;
        private IBlobContainerClient testContainerClient;
        private readonly bool modelAutoPublish = false;
        private readonly int stagedModelHistoryLength = 10;

        private async Task PreloadDefaultTestModelAsync(IBlobContainerClient container)
        {
            var clientBlob = container.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{testModelId}.{AzureBlobConstants.ClientModelSuffix}");
            var trainerBlob = container.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{testModelId}.{AzureBlobConstants.TrainerModelSuffix}");
            var metaDataBlob = container.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{testModelId}.{AzureBlobConstants.MetadataSuffix}");

            await clientBlob.UploadAsync(clientModelData, CancellationToken.None);
            await trainerBlob.UploadAsync(trainerModelData, CancellationToken.None);
            await metaDataBlob.UploadAsync(modelMetadataData, CancellationToken.None);
        }

        private static List<string> CreateMetadataFromTemplate(string metadataTemplate, DateTime startDate, TimeSpan increment, int count)
        {
            var metadata = new List<string>();
            var metaDataObj = JsonConvert.DeserializeObject<ModelMetadata>(metadataTemplate);
            for (int i = 0; i < count; ++i)
            {
                metaDataObj.CreationDate = startDate;
                startDate = startDate.Add(increment);
                metadata.Add(JsonConvert.SerializeObject(metaDataObj));
            }
            return metadata;
        }

        private static string MakeModelIdFromDateTime(DateTime date)
        {
            return $"{testModelId}.{date.Year:D4}{date.Month:D2}{date.Day:D2}{date.Hour:D2}{date.Minute:D2}{date.Second:D2}";
        }

        private static async Task<IList<ModelMetadata>> CreateAndLoadModelsFromTemplateAsync(
            IBlobContainerClient container,
            string metadataTemplate,
            DateTime startDate,
            TimeSpan increment,
            int count,
            int setSaveOnFirstNModels = 0)
        {
            var models = new List<ModelMetadata>();
            var metaDataObj = JsonConvert.DeserializeObject<ModelMetadata>(metadataTemplate);
            for (int i = 0; i < count; ++i)
            {
                metaDataObj.CreationDate = startDate;
                metaDataObj.ModelId = MakeModelIdFromDateTime(startDate);
                if (setSaveOnFirstNModels > 0 && i < count - setSaveOnFirstNModels)
                {
                    metaDataObj.SavedInHistory = true;
                }
                var metaDataTxt = JsonConvert.SerializeObject(metaDataObj);
                models.Add(JsonConvert.DeserializeObject<ModelMetadata>(metaDataTxt));
                await container.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{metaDataObj.ModelId}.{AzureBlobConstants.MetadataSuffix}")
                    .UploadAsync(BinaryData.FromString(metaDataTxt), CancellationToken.None);
                await container.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{metaDataObj.ModelId}.{AzureBlobConstants.ClientModelSuffix}")
                    .UploadAsync(BinaryData.FromString($"client dummy data - {metaDataObj.ModelId}"), CancellationToken.None);
                await container.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{metaDataObj.ModelId}.{AzureBlobConstants.TrainerModelSuffix}")
                    .UploadAsync(BinaryData.FromString($"trainer dummy data - {metaDataObj.ModelId}"), CancellationToken.None);
                startDate = startDate.Add(increment);
            }
            return models;
        }

        [TestInitialize]
        public async Task TestInitializeAsync()
        {
            storageFactory ??= global::CommonTest.TestUtil.CreateStorageFactory(TestContext);
            testContainerClient = storageFactory.CreateBlobContainerClient($"testcontainer{DateTime.UtcNow.Ticks}");
            await testContainerClient.CreateIfNotExistsAsync();
        }

        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            if (testContainerClient != null)
            {
                await testContainerClient.DeleteIfExistsAsync(CancellationToken.None);
            }
        }

        [TestMethod]
        public async Task TestEmptyListModelMetadataAsync()
        {
            var manager = new ModelExportManager(testContainerClient, this.modelAutoPublish, this.stagedModelHistoryLength);
            IEnumerable<ModelMetadata> metadatas = await manager.ListModelMetadataAsync(CancellationToken.None);
            Assert.AreEqual(0, metadatas.Count());
        }

        [TestMethod]
        public async Task TestListModelMetadataAsync()
        {
            var modelCount = 5;
            var metadata = CreateMetadataFromTemplate(modelMetadataTxt, new DateTime(2024, 07, 01), TimeSpan.FromDays(1), modelCount);
            for (int i = 0; i < modelCount; ++i)
            {
                var blob = testContainerClient.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{testModelId}.{i}.{AzureBlobConstants.MetadataSuffix}");
                var data = BinaryData.FromString(metadata[i]);
                await blob.UploadAsync(data);
            }

            var manager = new ModelExportManager(testContainerClient, this.modelAutoPublish, this.stagedModelHistoryLength);
            IEnumerable<ModelMetadata> models = await manager.ListModelMetadataAsync(CancellationToken.None);

            Assert.AreEqual(modelCount, models.Count());
            DateTime? previousDate = null;
            foreach (var model in models)
            {
                if (previousDate.HasValue)
                {
                    Assert.IsTrue(model.CreationDate <= previousDate.Value, "Metadata are not in order by CreationDate");
                }
                previousDate = model.CreationDate;
            }
        }

        [TestMethod]
        public async Task TestGetMetadataAsync()
        {
            await PreloadDefaultTestModelAsync(testContainerClient);

            var manager = new ModelExportManager(testContainerClient, this.modelAutoPublish, this.stagedModelHistoryLength);
            ExportedModel exportedModel = await manager.GetExportedModelAsync(testModelId, CancellationToken.None);

            string actualMetadataTxt = JsonConvert.SerializeObject(exportedModel.Metadata);
            string actualClientModelTxt = Encoding.UTF8.GetString(exportedModel.ClientModel);
            string actualTrainerModelTxt = Encoding.UTF8.GetString(exportedModel.TrainerModel);

            Assert.AreEqual(modelMetadataTxt, actualMetadataTxt);
            Assert.AreEqual(clientModelDataTxt, actualClientModelTxt);
            Assert.AreEqual(trainerModelDataTxt, actualTrainerModelTxt);
        }

        [TestMethod]
        public async Task TestSetModelMetadataAsync()
        {
            var manager = new ModelExportManager(testContainerClient, this.modelAutoPublish, this.stagedModelHistoryLength);
            string modelMetadata = "{\"modelId\":\"" + testModelId + "\",\"savedInHistory\":\"True\"}";
            await manager.SetModelMetadataAsync(testModelId, modelMetadata, CancellationToken.None);

            var metaDataBlob = testContainerClient.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{testModelId}.{AzureBlobConstants.MetadataSuffix}");

            MemoryStream memoryStream = new();
            await metaDataBlob.DownloadToAsync(memoryStream, CancellationToken.None);
            var actualMetaData = Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int) memoryStream.Position);
            Assert.AreEqual(modelMetadata, actualMetaData);
        }

        [TestMethod]
        public async Task TestDeleteModelAsync()
        {
            await PreloadDefaultTestModelAsync(testContainerClient);

            var clientBlob = testContainerClient.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{testModelId}.{AzureBlobConstants.ClientModelSuffix}");
            var trainerBlob = testContainerClient.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{testModelId}.{AzureBlobConstants.TrainerModelSuffix}");
            var metaDataBlob = testContainerClient.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{testModelId}.{AzureBlobConstants.MetadataSuffix}");

            Assert.IsTrue(await clientBlob.ExistsAsync());
            Assert.IsTrue(await trainerBlob.ExistsAsync());
            Assert.IsTrue(await metaDataBlob.ExistsAsync());

            var manager = new ModelExportManager(testContainerClient, this.modelAutoPublish, this.stagedModelHistoryLength);
            await manager.DeleteExportedModelAsync(testModelId, CancellationToken.None);

            Assert.IsFalse(await clientBlob.ExistsAsync());
            Assert.IsFalse(await trainerBlob.ExistsAsync());
            Assert.IsFalse(await metaDataBlob.ExistsAsync());
        }

        [TestMethod]
        public async Task TestUpdateCurrentAsync()
        {
            await PreloadDefaultTestModelAsync(testContainerClient);

            var currentClientBlob = testContainerClient.GetBlobClient(AzureBlobConstants.ClientModelBlobName);
            var currentTrainerBlob = testContainerClient.GetBlobClient(AzureBlobConstants.TrainerModelBlobName);

            Assert.IsFalse(await currentClientBlob.ExistsAsync());
            Assert.IsFalse(await currentTrainerBlob.ExistsAsync());

            var manager = new ModelExportManager(testContainerClient, this.modelAutoPublish, this.stagedModelHistoryLength);
            Assert.IsTrue(await manager.TryUpdateCurrentAsync(testModelId, CancellationToken.None));

            Assert.IsTrue(await currentClientBlob.ExistsAsync());
            Assert.IsTrue(await currentTrainerBlob.ExistsAsync());

            var currentClientData = await currentClientBlob.DownloadAsync();
            var currentTrainerData = await currentTrainerBlob.DownloadAsync();
            Assert.AreEqual(clientModelDataTxt, ASCIIEncoding.UTF8.GetString(currentClientData));
            Assert.AreEqual(trainerModelDataTxt, ASCIIEncoding.UTF8.GetString(currentTrainerData));
        }

        [TestMethod]
        [DataRow(20, 10, 0)] // test with default history length (3) and no saved models
        [DataRow(20, 10, 10)] // test with default history length (3) but with 10 saved models
        public async Task TestCleanModelsAsync(int modelCount, int modelHistoryLength, int savedModelHistoryLength)
        {
            var expectedBlobCount = modelCount * 3; // 3 blobs per model
            var alwaysKeepCount = Math.Max(savedModelHistoryLength, 3); // 3 hard coded in ModelExportManager
            var expectedBlobCountAfterClean = alwaysKeepCount * 3; // 3 blobs per model
            var modelStartDate = DateTime.UtcNow;
            var modelDateSpan = TimeSpan.FromDays(-1);
            var models = await CreateAndLoadModelsFromTemplateAsync(testContainerClient, modelMetadataTxt, modelStartDate, modelDateSpan, modelCount, savedModelHistoryLength);
            Assert.AreEqual(modelCount, models.Count);

            var blobs = await testContainerClient.GetBlobsByHierarchyAsync($"{AzureBlobConstants.ExportedModelsDirectory}/", "/", CancellationToken.None);
            Assert.AreEqual(expectedBlobCount, blobs.Count);

            var manager = new ModelExportManager(testContainerClient, this.modelAutoPublish, modelHistoryLength);
            await manager.CleanStagedModelsAsync(CancellationToken.None);

            var blobsAfterClean = new List<IBlobHierarchyItem>();
            foreach (var blob in blobs)
            {
                var blobClient = testContainerClient.GetBlobClient(blob.Name);
                if (await blobClient.ExistsAsync())
                {
                    blobsAfterClean.Add(blob);
                }
            }
            var minDate = modelStartDate.Add(TimeSpan.FromDays(-alwaysKeepCount));
            Assert.AreEqual(expectedBlobCountAfterClean, blobsAfterClean.Count);
            foreach (var blob in blobsAfterClean)
            {
                var m = Regex.Matches(blob.Name, @"[^.]*?(\d{4})(\d{2})(\d{2})(\d{2})(\d{2})(\d{2})[^.]*");
                Assert.AreEqual(1, m.Count);
                var modelDate = new DateTime(int.Parse(m[0].Groups[1].Value), int.Parse(m[0].Groups[2].Value), int.Parse(m[0].Groups[3].Value),
                    int.Parse(m[0].Groups[4].Value), int.Parse(m[0].Groups[5].Value), int.Parse(m[0].Groups[6].Value));
                Assert.IsTrue(modelDate >= minDate, "Model date is older than expected");
            }
        }
    }
}
