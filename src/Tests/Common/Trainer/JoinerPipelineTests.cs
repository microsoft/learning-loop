// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommonTest;
using CommonTest.Fakes.Messaging.EventHub;
using CommonTest.Fakes.Storage.InMemory;
using CommonTest.Messages;
using Microsoft.DecisionService.Common.Trainer;
using Microsoft.DecisionService.Common.Trainer.FlatBuffers;
using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.DecisionService.OnlineTrainer.Operations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using reinforcement_learning.messages.flatbuff.v2;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Common.Trainer;

/// <summary>
/// Tests for the JoinerPipeline class
/// </summary>
/// <remarks>
/// JoinerPipelineTests uses CommonTest.Fakes.Message.EventHub and
/// CommonTest.Fakes.Storage.InMemory to simulate end-to-end processing
/// </remarks>
[TestClass]
public class JoinerPipelineTests
{
    private const int TestTimeout_10s = 10_000;
    private const int TestTimeout_20s = 20_000;
    private readonly string _testAppId = BasicPipelineMockProvider.DefaultAppId;
    private readonly string _testModelId = "test-model-1";

    private async Task RunCaTestAsync(BasicPipelineMockProvider mockProvider, JoinerPipeline joinerUnderTest)
    {
        var testCompletionTime = TimeSpan.FromSeconds(2);
        // setup a test block store event handler to capture the block that is written
        var testBlockRx = new Regex(@"/data/.+?_.+?\.fb");
        MemBlobClient.MemBlock testBlock = null;
        var tcs = new TaskCompletionSource<bool>();
        mockProvider.StorageContainerClient.BlobStoreEvent += (sender, e) =>
        {
            if (testBlockRx.IsMatch(e.Properties.Name))
            {
                if (e.Action == MemBlobContainerClient.MemStoreAction.Commit)
                {
                    testBlock = e.Blocks.FirstOrDefault();
                    tcs.SetResult(true);
                }
            }
        };

        // start the joiner
        var cancellationToken = new CancellationTokenSource();
        await joinerUnderTest.StartAsync(cancellationToken.Token);

        // inject a CaEvent and Outcome batch
        var eventId = "event-id-1";
        var interationEvents = new Event[] {
            FBMessageBuilder.CreateEvent(_testAppId, eventId, DateTime.UtcNow, EventEncoding.Identity, 1.0f,
                FBMessageBuilder.CreateCaEvent(false, 0.5f, new byte[] { 1, 2, 3, 4 }, 0.5f, _testModelId, LearningModeType.Online)
            ),
        };
        var interactionBatch = FBMessageBuilder.CreateEventBatch(interationEvents);

        var observationEvents = new Event[] {
            FBMessageBuilder.CreateEvent(_testAppId, eventId, DateTime.UtcNow, EventEncoding.Identity, 1.0f,
                FBMessageBuilder.CreateOutcome(0.6f, 1, false)
            ),
        };
        var observationBatch = FBMessageBuilder.CreateEventBatch(observationEvents);

        var partitionId = EHDataClientFactory.MakePartitionId(0);
        mockProvider.EventHubController.GetReceiverClient(EHConstants.EH_INTERACTION, partitionId).InjectMessages(new EventBatch[] { interactionBatch });
        mockProvider.EventHubController.GetReceiverClient(EHConstants.EH_OBSERVATION, partitionId).InjectMessages(new EventBatch[] { observationBatch });

        // wait for the block to be committed or abort
        if (await Task.WhenAny(tcs.Task, Task.Delay(testCompletionTime)) != tcs.Task)
        {
            Assert.Fail($"the joiner blob output was not committed within the expected timeframe of {testCompletionTime.TotalMilliseconds}ms");
        }

        // stop everything
        cancellationToken.Cancel();
        await joinerUnderTest.StopAsync(cancellationToken.Token);

        // validate the block is written correctly
        Assert.IsNotNull(testBlock);
        Assert.IsTrue(testBlock.Committed);
        var buffer = testBlock.Data.GetBuffer();

        var binaryReader = new BinaryReader(new MemoryStream(buffer));
        var logReader = new BinaryLogReader(binaryReader);
        var message = logReader.ReadMessage();
        Assert.IsInstanceOfType(message, typeof(HeaderMessage));
        var headerMessage = message as HeaderMessage;
        Assert.AreEqual(headerMessage.HeaderProperties["joiner"], "Joiner");
        Assert.AreEqual(headerMessage.HeaderProperties["file-type"], "learnable-events");
        message = logReader.ReadMessage();
        Assert.IsInstanceOfType(message, typeof(LogCheckpointInfo));
        message = logReader.ReadMessage();
        Assert.IsInstanceOfType(message, typeof(RegularMessage));
        var regularMessage = message as RegularMessage;
        Assert.AreEqual(2, regularMessage.Events.Length);

        var e1 = regularMessage.Events[0];
        var in_e1 = interationEvents[0];
        Assert.IsTrue(e1.Event.Meta.HasValue);
        Assert.AreEqual(e1.Event.Meta.Value.AppId, mockProvider.JoinerConfigOptMonitor.CurrentValue.AppId);
        Assert.AreEqual(in_e1.Meta.Value.Id, e1.Event.Meta.Value.Id);
        Assert.AreEqual(in_e1.Meta.Value.AppId, e1.Event.Meta.Value.AppId);
        Assert.AreEqual(in_e1.Meta.Value.PayloadType, e1.Event.Meta.Value.PayloadType);
        Assert.AreEqual(in_e1.Meta.Value.Encoding, e1.Event.Meta.Value.Encoding);
        Assert.IsTrue(e1.Event.Meta.Value.ClientTimeUtc.HasValue);
        Assert.AreEqual(in_e1.Meta.Value.PassProbability, e1.Event.Meta.Value.PassProbability);
        Assert.IsInstanceOfType<CaEvent>(e1.UnpackedEvent);
        var caEvent = (CaEvent)e1.UnpackedEvent;
        Assert.AreEqual("test-model-1", caEvent.ModelId);

        var e2 = regularMessage.Events[1];
        var in_e2 = observationEvents[0];
        Assert.IsTrue(e2.Event.Meta.HasValue);
        Assert.AreEqual(e2.Event.Meta.Value.AppId, mockProvider.JoinerConfigOptMonitor.CurrentValue.AppId);
        Assert.AreEqual(in_e2.Meta.Value.Id, e2.Event.Meta.Value.Id);
        Assert.AreEqual(in_e2.Meta.Value.AppId, e2.Event.Meta.Value.AppId);
        Assert.AreEqual(in_e2.Meta.Value.PayloadType, e2.Event.Meta.Value.PayloadType);
        Assert.AreEqual(in_e2.Meta.Value.Encoding, e2.Event.Meta.Value.Encoding);
        Assert.IsTrue(e2.Event.Meta.Value.ClientTimeUtc.HasValue);
        Assert.AreEqual(in_e2.Meta.Value.PassProbability, e2.Event.Meta.Value.PassProbability);
        Assert.IsInstanceOfType<OutcomeEvent>(e2.UnpackedEvent);
        var outcomeEvent = (OutcomeEvent)e2.UnpackedEvent;
        Assert.AreEqual(0.6f, outcomeEvent.ValueAsnumeric().Value);
    }

    [TestMethod]
    [Description("The joiner fails to start because interaction receivers does not match observation receivers")]
    [TestCategory("Decision Service/Online Trainer")]
    [ExpectedException(typeof(InvalidOperationException))]
    public async Task Joiner_Start_Fails_Due_To_Unbalanced_EventHubs_StartAsync()
    {
        var mockProvider = new BasicPipelineMockProvider();
        var joinerUnderTest = await mockProvider.CreateJoinerWithDefaultsAsync();
        mockProvider.EventHubController.ProvideUnabalancedPartitionIds = true;
        var cancellationToken = new CancellationTokenSource();
        await joinerUnderTest.StartAsync(cancellationToken.Token);
    }

    [TestMethod]
    [Description("Updates to the JoinerConfig causes a restart")]
    [TestCategory("Decision Service/Online Trainer")]
    [Timeout(TestTimeout_10s)]
    public async Task Joiner_Update_JoinerConfig_Causes_Restart_StartAsync()
    {
        int ehCreateCount = 0;
        var mockProvider = new BasicPipelineMockProvider();
        mockProvider.EventHubController.JoinerFactory.OnCreate += (j) => { ehCreateCount++; };
        var joinerUnderTest = await mockProvider.CreateJoinerWithDefaultsAsync();
        var cancellationToken = new CancellationTokenSource();
        await joinerUnderTest.StartAsync(cancellationToken.Token);
        mockProvider.JoinerConfigOptMonitor.CurrentValue.EventHubReceiveTimeout = TimeSpan.FromMilliseconds(2);
        mockProvider.JoinerConfigOptMonitor.TriggerChange(nameof(JoinerConfig.EventHubReceiveTimeout));
        await Task.Delay(1000);
        cancellationToken.Cancel();
        await joinerUnderTest.StopAsync(cancellationToken.Token);
        Assert.AreEqual(2, ehCreateCount);
    }

    [TestMethod]
    [Description("Updates to the StorageBlockOptions causes a restart")]
    [TestCategory("Decision Service/Online Trainer")]
    [Timeout(TestTimeout_10s)]
    public async Task Joiner_Update_StorageBlockOptions_Causes_Restart_StartAsync()
    {
        int ehCreateCount = 0;
        var mockProvider = new BasicPipelineMockProvider();
        mockProvider.EventHubController.JoinerFactory.OnCreate += (j) => { ehCreateCount++; };
        var joinerUnderTest = await mockProvider.CreateJoinerWithDefaultsAsync();
        var cancellationToken = new CancellationTokenSource();
        await joinerUnderTest.StartAsync(cancellationToken.Token);
        mockProvider.StorageBlockOptMonitor.CurrentValue.DefaultReward = 0.0f;
        mockProvider.StorageBlockOptMonitor.TriggerChange(nameof(StorageBlockOptions.DefaultReward));
        await Task.Delay(1000);
        cancellationToken.Cancel();
        await joinerUnderTest.StopAsync(cancellationToken.Token);
        Assert.AreEqual(2, ehCreateCount);
    }

    [TestMethod]
    [Description("The joiner starts with config option LocalCookedLogsPath that is NOT currently implemented")]
    [TestCategory("Decision Service/Online Trainer")]
    [ExpectedException(typeof(NotImplementedException))]
    public async Task Joiner_Start_With_LocalCookedLogsPath_StartAsync()
    {
        var mockProvider = new BasicPipelineMockProvider();
        var config = BasicPipelineMockProvider.CreateDefaultJoinerConfig();
        config.LocalCookedLogsPath = "doesnt_exist";
        var joinerUnderTest = await mockProvider.CreateAsync(config);
        var cancellationToken = new CancellationTokenSource();
        await joinerUnderTest.StartAsync(cancellationToken.Token);
    }

    [TestMethod]
    [Description("The joiner receives a CaEvent followed by 1 Outcome and generates an EventBatch in block storage")]
    [TestCategory("Decision Service/Online Trainer")]
    [Timeout(TestTimeout_10s)]
    public async Task Joiner_Captures_1_CaEvent_And_1_Outcome_StartAsync()
    {
        var mockProvider = new BasicPipelineMockProvider();
        var joinerUnderTest = await mockProvider.CreateJoinerWithDefaultsAsync();
        await RunCaTestAsync(mockProvider, joinerUnderTest);
    }

    [TestMethod]
    [Description("The joiner receives a CaEvent followed by 1 Outcome and generates an EventBatch in block storage with an existing checkpoint")]
    [TestCategory("Decision Service/Online Trainer")]
    [DataRow("Common/Trainer/Data/JoinerPipelineTests/CheckpointTest")]
    [Timeout(TestTimeout_20s)]
    public async Task Joiner_Captures_1_CaEvent_And_1_Outcome_With_Existing_Checkpoint_StartAsync(string testFilesPath)
    {
        var mockProvider = new BasicPipelineMockProvider();
        var joinerUnderTest = await mockProvider.CreateJoinerWithDefaultsAsync();
        await TestStorageHelper.UploadFilesToBlobAsync(testFilesPath, mockProvider.StorageContainerClient, CancellationToken.None);
        await RunCaTestAsync(mockProvider, joinerUnderTest);
    }
}