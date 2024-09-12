// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommonTest.Fakes.Messaging.EventHub;
using CommonTest.Fakes.Storage.InMemory;
using CommonTest.Messages;
using Microsoft.DecisionService.Common.Trainer;
using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using reinforcement_learning.messages.flatbuff.v2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCore.Tests.Common.Trainer;

/// <summary>
/// Tests for the TrainerPipeline class
/// </summary>
/// <remarks>
/// TrainerPipelineTests uses CommonTest.Fakes.Message.EventHub and
/// CommonTest.Fakes.Storage.InMemory to simulate end-to-end processing
/// </remarks>
[TestClass]
public class TrainerPipelineTests
{
    private const int TestTimeout_10s = 10_000;
    private const int TestTimeout_30s = 30_000;
    private readonly string _testAppId = BasicPipelineMockProvider.DefaultAppId;
    private readonly string _testModelId = "test-model-1";

    #region Test Data
    private class TestEvent
    {
        public string Context;
        public ulong[] ActionIds;
        public float[] Probabilities;
        public float Outcome;
    }

    private readonly List<TestEvent> _cbEvents = new()
    {
        new()
        {
            Context = "{ \"GUser\":{\"id\":\"mk\",\"major\":\"psychology\",\"hobby\":\"kids\",\"favorite_character\":\"7of9\"}, \"_multi\": [ { \"TAction\":{\"topic\":\"SkiConditions-VT\"} }, { \"TAction\":{\"topic\":\"HerbGarden\"} }, { \"TAction\":{\"topic\":\"BeyBlades\"} }, { \"TAction\":{\"topic\":\"NYCLiving\"} }, { \"TAction\":{\"topic\":\"MachineLearning\"} } ] }",
            ActionIds = new ulong[] { 4, 2, 3, 1, 5 },
            Probabilities = new float[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f },
            Outcome = 0.0f
        },
        new()
        {
            Context = "{ \"GUser\":{\"id\":\"rnc\",\"major\":\"engineering\",\"hobby\":\"hiking\",\"favorite_character\":\"spock\"}, \"_multi\": [ { \"TAction\":{\"topic\":\"SkiConditions-VT\"} }, { \"TAction\":{\"topic\":\"HerbGarden\"} }, { \"TAction\":{\"topic\":\"BeyBlades\"} }, { \"TAction\":{\"topic\":\"NYCLiving\"} }, { \"TAction\":{\"topic\":\"MachineLearning\"} } ] }",
            ActionIds = new ulong[] { 4, 2, 3, 1, 5 },
            Probabilities = new float[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f },
            Outcome = 0.0f
        },
        new()
        {
            Context = "{ \"GUser\":{\"id\":\"mk\",\"major\":\"psychology\",\"hobby\":\"kids\",\"favorite_character\":\"7of9\"}, \"_multi\": [ { \"TAction\":{\"topic\":\"SkiConditions-VT\"} }, { \"TAction\":{\"topic\":\"HerbGarden\"} }, { \"TAction\":{\"topic\":\"BeyBlades\"} }, { \"TAction\":{\"topic\":\"NYCLiving\"} }, { \"TAction\":{\"topic\":\"MachineLearning\"} } ] }",
            ActionIds = new ulong[] { 2, 1, 3, 4, 5 },
            Probabilities = new float[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f },
            Outcome = 1.0f
        },
        new()
        {
            Context = "{ \"GUser\":{\"id\":\"rnc\",\"major\":\"engineering\",\"hobby\":\"hiking\",\"favorite_character\":\"spock\"}, \"_multi\": [ { \"TAction\":{\"topic\":\"SkiConditions-VT\"} }, { \"TAction\":{\"topic\":\"HerbGarden\"} }, { \"TAction\":{\"topic\":\"BeyBlades\"} }, { \"TAction\":{\"topic\":\"NYCLiving\"} }, { \"TAction\":{\"topic\":\"MachineLearning\"} } ] }",
            ActionIds = new ulong[] { 2, 1, 3, 4, 5 },
            Probabilities = new float[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f },
            Outcome = 0.0f
        },
        new()
        {
            Context = "{ \"GUser\":{\"id\":\"mk\",\"major\":\"psychology\",\"hobby\":\"kids\",\"favorite_character\":\"7of9\"}, \"_multi\": [ { \"TAction\":{\"topic\":\"SkiConditions-VT\"} }, { \"TAction\":{\"topic\":\"HerbGarden\"} }, { \"TAction\":{\"topic\":\"BeyBlades\"} }, { \"TAction\":{\"topic\":\"NYCLiving\"} }, { \"TAction\":{\"topic\":\"MachineLearning\"} } ] }",
            ActionIds = new ulong[] { 1, 2, 3, 4, 5 },
            Probabilities = new float[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f },
            Outcome = 0.0f
        },
        new()
        {
            Context = "{ \"GUser\":{\"id\":\"rnc\",\"major\":\"engineering\",\"hobby\":\"hiking\",\"favorite_character\":\"spock\"}, \"_multi\": [ { \"TAction\":{\"topic\":\"SkiConditions-VT\"} }, { \"TAction\":{\"topic\":\"HerbGarden\"} }, { \"TAction\":{\"topic\":\"BeyBlades\"} }, { \"TAction\":{\"topic\":\"NYCLiving\"} }, { \"TAction\":{\"topic\":\"MachineLearning\"} } ] }",
            ActionIds = new ulong[] { 4, 2, 3, 1, 5 },
            Probabilities = new float[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f },
            Outcome = 0.0f
        },
        new()
        {
            Context = "{ \"GUser\":{\"id\":\"rnc\",\"major\":\"engineering\",\"hobby\":\"hiking\",\"favorite_character\":\"spock\"}, \"_multi\": [ { \"TAction\":{\"topic\":\"SkiConditions-VT\"} }, { \"TAction\":{\"topic\":\"HerbGarden\"} }, { \"TAction\":{\"topic\":\"BeyBlades\"} }, { \"TAction\":{\"topic\":\"NYCLiving\"} }, { \"TAction\":{\"topic\":\"MachineLearning\"} } ] }",
            ActionIds = new ulong[] { 2, 1, 3, 4, 5 },
            Probabilities = new float[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f },
            Outcome = 0.0f
        },
        new()
        {
            Context = "{ \"GUser\":{\"id\":\"mk\",\"major\":\"psychology\",\"hobby\":\"kids\",\"favorite_character\":\"7of9\"}, \"_multi\": [ { \"TAction\":{\"topic\":\"SkiConditions-VT\"} }, { \"TAction\":{\"topic\":\"HerbGarden\"} }, { \"TAction\":{\"topic\":\"BeyBlades\"} }, { \"TAction\":{\"topic\":\"NYCLiving\"} }, { \"TAction\":{\"topic\":\"MachineLearning\"} } ] }",
            ActionIds = new ulong[] { 2, 1, 3, 4, 5 },
            Probabilities = new float[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f },
            Outcome = 0.0f
        },
        new()
        {
            Context = "{ \"GUser\":{\"id\":\"mk\",\"major\":\"psychology\",\"hobby\":\"kids\",\"favorite_character\":\"7of9\"}, \"_multi\": [ { \"TAction\":{\"topic\":\"SkiConditions-VT\"} }, { \"TAction\":{\"topic\":\"HerbGarden\"} }, { \"TAction\":{\"topic\":\"BeyBlades\"} }, { \"TAction\":{\"topic\":\"NYCLiving\"} }, { \"TAction\":{\"topic\":\"MachineLearning\"} } ] }",
            ActionIds = new ulong[] { 4, 2, 3, 1, 5 },
            Probabilities = new float[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f },
            Outcome = 0.0f
        },
        new()
        {
            Context = "{ \"GUser\":{\"id\":\"mk\",\"major\":\"psychology\",\"hobby\":\"kids\",\"favorite_character\":\"7of9\"}, \"_multi\": [ { \"TAction\":{\"topic\":\"SkiConditions-VT\"} }, { \"TAction\":{\"topic\":\"HerbGarden\"} }, { \"TAction\":{\"topic\":\"BeyBlades\"} }, { \"TAction\":{\"topic\":\"NYCLiving\"} }, { \"TAction\":{\"topic\":\"MachineLearning\"} } ] }",
            ActionIds = new ulong[] { 4, 2, 3, 1, 5 },
            Probabilities = new float[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f },
            Outcome = 0.0f
        }
    };

    private (EventBatch, EventBatch) BuildEventBatches(string appId, string modelId, List<TestEvent> testEvents)
    {
        var interactionEvents = new List<Event>();
        var observationEvents = new List<Event>();
        foreach (var testEvent in testEvents)
        {
            var event_id = Guid.NewGuid().ToString();
            var cbEvent = FBMessageBuilder.CreateEvent(appId, event_id, DateTime.UtcNow, EventEncoding.Identity, 0.0f,
                FBMessageBuilder.CreateCbEvent(false, testEvent.ActionIds, Encoding.UTF8.GetBytes(testEvent.Context), testEvent.Probabilities, modelId, LearningModeType.Online)
            );
            var outcomeEvent = FBMessageBuilder.CreateEvent(appId, event_id, DateTime.UtcNow, EventEncoding.Identity, 0.0f,
                FBMessageBuilder.CreateOutcome(testEvent.Outcome, 0, false)
            );
            interactionEvents.Add(cbEvent);
            observationEvents.Add(outcomeEvent);
        }
        return (FBMessageBuilder.CreateEventBatch(interactionEvents), FBMessageBuilder.CreateEventBatch(observationEvents));
    }

    #endregion // Test Data

    private async Task RunTrainerJoinerTestAsync(BasicPipelineMockProvider mockProvider, TrainerPipeline trainerUnderTest, JoinerPipeline joinerUnderTest, EventBatch interactionBatch, EventBatch observationBatch)
    {
        // setup a test block store event handler to capture the blocks written
        var trainerBlobs = new List<MemBlobClient.MemBlock>();
        var testBlockRx = new Regex(@"exported-models/.+?(client\.vw|trainer\.vw|metadata\.json)");
        var tcs = new TaskCompletionSource<bool>();
        mockProvider.StorageContainerClient.BlobStoreEvent += (sender, e) =>
        {
            if (testBlockRx.IsMatch(e.Properties.Name))
            {
                if (e.Action == MemBlobContainerClient.MemStoreAction.Commit)
                {
                    trainerBlobs.Add(e.Blocks.FirstOrDefault());
                    if (trainerBlobs.Count == 3)
                    {
                        tcs.SetResult(true);
                    }
                }
            }
        };

        // start the trainer and joiner
        var cancellationToken = new CancellationTokenSource();
        var joinerTask = joinerUnderTest.StartAsync(cancellationToken.Token);
        var trainerTask = trainerUnderTest.StartAsync(cancellationToken.Token);
        await Task.WhenAll(joinerTask, trainerTask);

        // inject a CbEvent and Outcome batch
        var partitionId = EHDataClientFactory.MakePartitionId(0);
        mockProvider.EventHubController.GetReceiverClient(EHConstants.EH_INTERACTION, partitionId).InjectMessages(new EventBatch[] { interactionBatch });
        mockProvider.EventHubController.GetReceiverClient(EHConstants.EH_OBSERVATION, partitionId).InjectMessages(new EventBatch[] { observationBatch });

        // wait for the block to be committed or abort
        if (await Task.WhenAny(tcs.Task, Task.Delay(TestTimeout_10s)) != tcs.Task)
        {
            Assert.Fail($"the trainer output was not committed within the expected timeframe of {TestTimeout_10s}ms");
        }

        // stop everything
        cancellationToken.Cancel();
        var stopJoinerTask = joinerUnderTest.StopAsync(cancellationToken.Token);
        var stopTrainerTask = trainerUnderTest.StopAsync(cancellationToken.Token);
        await Task.WhenAll(stopJoinerTask, stopTrainerTask);

        Assert.AreEqual(3, trainerBlobs.Count);
        Assert.IsTrue(trainerBlobs[0].SizeInBytes > 0);
        Assert.IsTrue(trainerBlobs[1].SizeInBytes > 0);
        Assert.IsTrue(trainerBlobs[2].SizeInBytes > 0);
    }

    [TestMethod]
    [Description("Updates to the TrainerConfig causes a restart")]
    [TestCategory("Decision Service/Online Trainer")]
    [Timeout(TestTimeout_10s)]
    public async Task Trainer_Update_TrainerConfig_Causes_Restart_StartAsync()
    {
        int restartCount = 0;
        var mockProvider = new BasicPipelineMockProvider();
        var trainerUnderTest = await mockProvider.CreateTrainerWithDefaultsAsync();
        trainerUnderTest.OnRestarted += () => restartCount++;
        var cancellationToken = new CancellationTokenSource();
        await trainerUnderTest.StartAsync(cancellationToken.Token);
        mockProvider.TrainerConfigOpt.CurrentValue.DefaultReward = -1;
        mockProvider.TrainerConfigOpt.TriggerChange(nameof(TrainerConfig.DefaultReward));
        await Task.Delay(1000);
        cancellationToken.Cancel();
        await trainerUnderTest.StopAsync(cancellationToken.Token);
        Assert.AreEqual(1, restartCount);
    }

    [TestMethod]
    [Description("The trainer starts with config option LocalCookedLogsPath")]
    [TestCategory("Decision Service/Online Trainer")]
    [Timeout(TestTimeout_10s)]
    public async Task Trainer_Start_With_LocalCookedLogsPath_StartAsync()
    {
        bool startedAndStoppedSuccessfully = false;
        string cookedLogPath = Path.Combine(Directory.GetCurrentDirectory(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(cookedLogPath);
        try
        {
            var mockProvider = new BasicPipelineMockProvider();
            var config = BasicPipelineMockProvider.CreateDefaultTrainerConfig();
            config.LocalCookedLogsPath = cookedLogPath;
            var trainerUnderTest = await mockProvider.CreateAsync(config);
            var cancellationToken = new CancellationTokenSource();
            await trainerUnderTest.StartAsync(cancellationToken.Token);
            await Task.Delay(1000);
            cancellationToken.Cancel();
            await trainerUnderTest.StopAsync(cancellationToken.Token);
            startedAndStoppedSuccessfully = true;
        }
        finally
        {
            if (Directory.Exists(cookedLogPath))
            {
                Directory.Delete(cookedLogPath, true);
            }
        }
        Assert.IsTrue(startedAndStoppedSuccessfully);
    }

    [TestMethod]
    [Description("Trainer and Joiner pipeline with CB events generates a model")]
    [TestCategory("Decision Service/Online Trainer")]
    [Timeout(TestTimeout_30s)]
    public async Task Trainer_Train_with_CBEvents_StartAsync()
    {
        var mockProvider = new BasicPipelineMockProvider();
        var joinerUnderTest = await mockProvider.CreateJoinerWithDefaultsAsync();
        var trainerUnderTest = await mockProvider.CreateTrainerWithDefaultsAsync();
        var (interactionBatch, observationBatch) = BuildEventBatches(_testAppId, _testModelId, _cbEvents);
        await RunTrainerJoinerTestAsync(mockProvider, trainerUnderTest, joinerUnderTest, interactionBatch, observationBatch);
    }
}
