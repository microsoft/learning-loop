// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Storage;
using Microsoft.DecisionService.Common.Trainer.ModelExport;
using Microsoft.DecisionService.OnlineTrainer.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;

namespace Tests.ModelExport
{
    [TestClass]
    public class ModelExportFactoryTest
    {
        private Mock<IBlobContainerClient> mockBlobContainer;

        [TestInitialize]
        public void TestInit()
        {
            //init mocks, and specify a default ctor parameter to make it valid
            this.mockBlobContainer = new Mock<IBlobContainerClient>(MockBehavior.Loose);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestCreateInvalidBlobModelExporter()
        {
            //both storage client are null: this is not valid, at least one of the 2 must be set
            var options = new ModelExportBlockOptions {};
            //assert that the invalid instantiation throws
            ModelExporterFactory.Create(options, NullLogger.Instance);
        }

        [TestMethod]
        public void TestCreateBlobModelExporter()
        {
            var options = new ModelExportBlockOptions
            {
                ContainerClient = mockBlobContainer.Object,
            };
            IModelExporter modelExporter = ModelExporterFactory.Create(options, NullLogger.Instance);
            Assert.AreEqual(typeof(BlobModelExporter), modelExporter.GetType());
        }

        [TestMethod]
        public void TestCreateModelExportManager_WithNegativeHistoryLength()
        {
            //both storage client are null: this is not valid, at least one of the 2 must be set
            var options = new ModelExportBlockOptions
            {
                StagedModelHistoryLength = -1,
                ContainerClient = mockBlobContainer.Object,
            };
            //assert that the invalid instantiation throws
            var exportManager = new ModelExportManager(options.ContainerClient, options.ModelAutoPublish, options.StagedModelHistoryLength);
            Assert.AreEqual(1, exportManager.HistoryLength);
            Assert.AreEqual(options.ModelAutoPublish, exportManager.AutoPublish);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestCreateBlobModelExporter_WithNullContainer()
        {
            //both storage client are null: this is not valid, at least one of the 2 must be set
            var options = new ModelExportBlockOptions
            {
                StagedModelHistoryLength = -1,
                ContainerClient = mockBlobContainer.Object,
            };
            //assert that the invalid instantiation throws
            var exportManager = new ModelExportManager(options.ContainerClient, options.ModelAutoPublish, options.StagedModelHistoryLength);
            _ = new BlobModelExporter(null, null, exportManager);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestCreateBlobModelExporter_WithNullExportManager()
        {
            _ = new BlobModelExporter(null, mockBlobContainer.Object, null);
        }
    }
}
