// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Trainer.ModelExport;
using Microsoft.DecisionService.OnlineTrainer.Data;
using Microsoft.DecisionService.OnlineTrainer.Operations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests.Operations
{
    [TestClass]
    public class ModelExportBlockTest
    {
        [TestMethod]
        public async Task TestExportAsync()
        {
            Mock<IModelExporter> mockModelExporter = new Mock<IModelExporter>(MockBehavior.Loose);
            IModelExporter modelExporter = mockModelExporter.Object;

            byte[] clientModelData = Encoding.ASCII.GetBytes("clientModelData");
            byte[] trainerModelData = Encoding.ASCII.GetBytes("trainerModelData");
            string metadata = "model_metadata";
            int eventsLearnedSinceLastExport = 25;

            var options = new ModelExportBlockOptions();

            ModelExportBlock block = new ModelExportBlock(options, modelExporter, NullLogger.Instance);
            await block.Input.SendAsync(
                    new ModelExportEvent
                    {
                        ClientModelData = clientModelData,
                        TrainerModelData = trainerModelData,
                        NumberOfEventsLearnedSinceLastExport = eventsLearnedSinceLastExport,
                        JsonMetadata = metadata,
                    });

            block.Input.Complete();
            await block.Completion;

            //verify that upload method was called
            mockModelExporter.Verify(exporter => exporter.UploadAsync(It.Is<byte[]>(buffer => buffer.SequenceEqual(clientModelData)), It.Is<byte[]>(buffer => buffer.SequenceEqual(trainerModelData)), It.Is<string>(s => s == metadata), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
