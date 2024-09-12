// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommonTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCore.Tests.Common.Trainer;

/// <summary>
/// Tests for the TrainingMonitoringPipeline class
/// </summary>
/// <remarks>
/// TrainingMonitoringPipeline uses CommonTest.Fakes.Message.EventHub and
/// CommonTest.Fakes.Storage.InMemory to simulate end-to-end processing
/// </remarks>
[TestClass]
public class TrainingMonitoringPipelineTests
{
    private const int TestTimeout_10s = 10_000;
    private const int TestTimeout_20s = 20_000;

    [TestMethod]
    [Description("The trainer monitor pipeline is created successfully")]
    [TestCategory("Decision Service/Online Trainer")]
    public async Task TrainingMonitoringPipeline_Create_WithDefaults_Returns_NonNull_Async()
    {
        var provider = new BasicPipelineMockProvider();
        var pipeline = await provider.CreateTrainingMonitorWithDefaultsAsync();
        Assert.IsNotNull(provider.StorageContainerClient);
        Assert.IsNotNull(pipeline);
    }

    [TestMethod]
    [Description("The trainer monitor pipeline is starts successfully")]
    [TestCategory("Decision Service/Online Trainer")]
    [Timeout(TestTimeout_10s)]
    public async Task TrainingMonitoringPipeline_Create_WithDefaults_Starts_Running_Async()
    {
        var provider = new BasicPipelineMockProvider();
        var pipeline = await provider.CreateTrainingMonitorWithDefaultsAsync();
        Assert.IsNotNull(provider.StorageContainerClient);
        Assert.IsNotNull(pipeline);
        var cancellationToken = new CancellationTokenSource();
        await pipeline.StartAsync(cancellationToken.Token);
        await Task.Delay(1000);
        cancellationToken.Cancel();
        await pipeline.StopAsync(cancellationToken.Token);
    }

    [TestMethod]
    [Description("The trainer monitor pipeline reads test models and updates TrainerLagInKB")]
    [TestCategory("Decision Service/Online Trainer")]
    [DataRow("Common/Trainer/Data/TrainingMonitoringPipelineTests/CheckpointTest")]
    //[Timeout(TestTimeout_20s)]
    public async Task TrainingMonitoringPipeline_Updates_Counter_TrainerLagInKB_Async(string testFilesPath)
    {
        string meterName = "Microsoft.DecisionService.OnlineTrainer.Monitoring";
        var meterListener = new MeterListener();
        meterListener.InstrumentPublished += (instrument, e) =>
        {
            if (instrument.Meter.Name == meterName && instrument.Name == "TrainerLagInKB")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        var tcs = new TaskCompletionSource<long>();
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "TrainerLagInKB")
            {
                if (measurement > 0)
                {
                    tcs.SetResult(measurement);
                }
            }
        });
        meterListener.Start();
        var testDirPath = TestStorageHelper.GetTestFileBasePath(testFilesPath);
        var provider = new BasicPipelineMockProvider();
        var pipelineConfig = BasicPipelineMockProvider.CreateDefaultTrainingMonitoringConfig();
        pipelineConfig.LastConfigurationEditDate = TestStorageHelper.ExtractCheckpointTimestamp(testDirPath);
        var pipeline = await provider.CreateAsync(pipelineConfig);
        await TestStorageHelper.UploadFilesToBlobAsync(testDirPath, provider.StorageContainerClient, CancellationToken.None);
        Assert.IsNotNull(provider.StorageContainerClient);
        Assert.IsNotNull(pipeline);
        var cancellationToken = new CancellationTokenSource();
        await pipeline.StartAsync(cancellationToken.Token);
        await Task.Delay(TestTimeout_10s);
        meterListener.RecordObservableInstruments();
        var lagInKB = await tcs.Task;
        // TODO: get the expected size for the file set (Data/TrainingMonitorPipelineTests/CheckpointTest)
        Assert.AreEqual(57, lagInKB);
        meterListener.Dispose();
        cancellationToken.Cancel();
        await pipeline.StopAsync(cancellationToken.Token);
    }
}
