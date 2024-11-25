// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// using CommonTest;
// using Microsoft.DecisionService.Common;
// using Microsoft.DecisionService.Common.Data;
// using Microsoft.DecisionService.Common.Trainer.Operations;
// using Microsoft.DecisionService.Instrumentation;
// using Microsoft.DecisionService.OnlineTrainer;
// using Microsoft.DecisionService.OnlineTrainer.Data;
// using Microsoft.DecisionService.OnlineTrainer.Operations;
// using Microsoft.VisualStudio.TestTools.UnitTesting;
// using Moq;
// using System;
// using System.Threading;
// using System.Threading.Tasks;
// using System.Threading.Tasks.Dataflow;
// using Microsoft.Extensions.Logging.Abstractions;
//
// namespace Tests.Operations
// {
//     [TestClass]
//     public class FileSystemUploadBlockTests
//     {
//         private StorageBlockOptions options;
//         private Mock<UploadHelper> mockUploadHelper;
//         private UploadHelper uploadHelper;
//
//         [TestInitialize]
//         public void TestInit()
//         {
//             this.options = new StorageBlockOptions()
//             {
//                 LastConfigurationEditDate = new DateTime(2019, 09, 21),
//                 MaximumFlushLatency = TimeSpan.FromSeconds(10),
//                 UploadSkippedLogs = true
//             };
//             this.uploadHelper = new UploadHelper();
//             this.mockUploadHelper = new Mock<UploadHelper>(MockBehavior.Loose, true, 0.0f, null);
//             this.mockUploadHelper.Setup(x => x.Serialize(It.IsAny<InteractionEvent>())).Returns((InteractionEvent evt) => this.uploadHelper.Serialize(evt));
//         }
//
//         [DataTestMethod]
//         [DataRow(true)]
//         [DataRow(false)]
//         public async Task UploadInteractionsAsync(bool uploadSkippedLogs)
//         {
//             this.options.UploadSkippedLogs = uploadSkippedLogs;
//             var fsUploadBlock = new FileSystemUploadBlock(options, ".", this.mockUploadHelper.Object, CancellationToken.None);
//             var joinedEvent = TestUtil.GenerateInteractionEvents(count: 1);
//
//             fsUploadBlock.OfferMessage(new DataflowMessageHeader(1), joinedEvent[0], null, false);
//             fsUploadBlock.Complete();
//             await fsUploadBlock.Completion;
//
//             this.mockUploadHelper.Verify(x => x.Serialize(It.IsAny<InteractionEvent>()), Times.Once);
//         }
//
//         [DataTestMethod]
//         [DataRow(true)]
//         [DataRow(false)]
//         public async Task UploadMixOfInteractionsAndSkippedAsync(bool uploadSkippedLogs)
//         {
//             this.options.UploadSkippedLogs = uploadSkippedLogs;
//             var fsUploadBlock = new FileSystemUploadBlock(options, ".", this.mockUploadHelper.Object, CancellationToken.None);
//             var joinedEvent = TestUtil.GenerateInteractionEvents(count: 1);
//             var danglingObs = TestUtil.GenerateInteractionEvents(isDanglingObservation: true, count: 1);
//             var skippedEvent = TestUtil.GenerateInteractionEvents(deferredAction: true, count: 1);
//
//             fsUploadBlock.OfferMessage(new DataflowMessageHeader(1), joinedEvent[0], null, false);
//             fsUploadBlock.OfferMessage(new DataflowMessageHeader(2), danglingObs[0], null, false);
//             fsUploadBlock.OfferMessage(new DataflowMessageHeader(3), skippedEvent[0], null, false);
//             fsUploadBlock.Complete();
//             await fsUploadBlock.Completion;
//
//             this.mockUploadHelper.Verify(x => x.Serialize(It.IsAny<InteractionEvent>()), Times.Exactly(uploadSkippedLogs ? 3 : 1));
//         }
//     }
// }
