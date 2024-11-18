// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Trainer.ModelExport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.ModelExport
{
    [TestClass]
    [DoNotParallelize]
    public class LocalFileSystemModelExportTest
    {
        private string clientModelRelativePath = "./LocalFileSystemModelExportTest/clientModel";
        private string trainerModelAbsolutePath = Path.Combine(Path.GetTempPath(), "LocalFileSystemModelExportTest/trainerModel");
        private string metadataAbsolutePath = Path.Combine(Path.GetTempPath(), "LocalFileSystemModelExportTest/modelMetadata");

        private byte[] clientModel;
        private byte[] trainerModel;
        private string metadata;

        private IModelExporter modelExporter;

        [TestInitialize]
        public void TestInit()
        {
            this.clientModel = Encoding.UTF8.GetBytes("clientModelData");
            this.trainerModel = Encoding.UTF8.GetBytes("trainerModelData");
            this.metadata = "model_metadata";

            this.modelExporter =
                new LocalFileSystemModelExporter(null, this.clientModelRelativePath, this.trainerModelAbsolutePath, this.metadataAbsolutePath);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestExportModelLocal_EnvironmentVariablesNotSet()
        {
            var exporter = new LocalFileSystemModelExporter();
        }

        [TestMethod]
        public async Task TestExportModelLocal_RelativePathAsync()
        {
            await modelExporter.UploadAsync(this.clientModel, this.trainerModel, this.metadata, CancellationToken.None);

            Verify(this.clientModelRelativePath, this.clientModel);
            Verify(this.trainerModelAbsolutePath, this.trainerModel);

            Assert.IsTrue(File.Exists(this.metadataAbsolutePath));
            string actualMetadata = File.ReadAllText(this.metadataAbsolutePath);
            Assert.IsTrue(this.metadata.SequenceEqual(actualMetadata));
        }

        [TestMethod]
        public async Task TestExportModelLocal_OverwriteAsync()
        {
            byte[] earlierClientModel = Encoding.UTF8.GetBytes("clientModelData_earlier");
            byte[] earlierTrainerModel = Encoding.UTF8.GetBytes("trainerModelData_earlier");
            string earlierMetadata = "earlier_model_metadata";

            await modelExporter.UploadAsync(earlierClientModel, earlierTrainerModel, earlierMetadata, CancellationToken.None);
            await modelExporter.UploadAsync(this.clientModel, this.trainerModel, this.metadata, CancellationToken.None);

            Verify(this.clientModelRelativePath, this.clientModel);
            Verify(this.trainerModelAbsolutePath, this.trainerModel);

            Assert.IsTrue(File.Exists(this.metadataAbsolutePath));
            string actualMetadata = File.ReadAllText(this.metadataAbsolutePath);
            Assert.IsTrue(this.metadata.SequenceEqual(actualMetadata));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (File.Exists(this.clientModelRelativePath))
            {
                Directory.Delete(new FileInfo(this.clientModelRelativePath).DirectoryName, true);
            }
            if (File.Exists(this.trainerModelAbsolutePath))
            {
                Directory.Delete(new FileInfo(this.trainerModelAbsolutePath).DirectoryName, true);
            }
        }

        private void Verify(string path, byte[] expectedModel)
        {
            Assert.IsTrue(File.Exists(path));
            byte[] actualModel = File.ReadAllBytes(path);
            Assert.IsTrue(expectedModel.SequenceEqual(actualModel));
        }
    }
}
