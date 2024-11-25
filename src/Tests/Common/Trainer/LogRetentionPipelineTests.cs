// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommonTest.Fakes.Storage.InMemory;
using Microsoft.DecisionService.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Common.Trainer;

[TestClass]
public class LogRetentionPipelineTests
{
    private const int DelayTimeout_1s = 1_000;
    private const int TestTimeout_10s = 10_000;

    private static async Task CreateTestLogsAsync(MemBlobContainerClient storageContainerClient, DateTime configDate, DateTime date)
    {
        var ckptModel = PathHelper.BuildCheckpointName(date, "current.dat");
        var ckptCk = PathHelper.BuildCheckpointName(date, "storage-checkpoint.json");
        var joinData = PathHelper.BuildBlobName(configDate, date, 0, "data", JoinedLogFormat.Binary);
        await storageContainerClient
            .GetBlockBlobClient(ckptModel)
            .WriteAsync(
                new MemoryStream(Encoding.UTF8.GetBytes("staleLog1")),
                CancellationToken.None
            );
        await storageContainerClient
            .GetBlockBlobClient(ckptCk)
            .WriteAsync(
                new MemoryStream(Encoding.UTF8.GetBytes("staleLog1")),
                CancellationToken.None
            );
        await storageContainerClient
            .GetBlockBlobClient(joinData)
            .WriteAsync(
                new MemoryStream(Encoding.UTF8.GetBytes("staleLog1")),
                CancellationToken.None
            );
    }

    [TestMethod]
    [Description("The log retention pipeline starts successfully with 1 day retention")]
    [TestCategory("Decision Service/Online Trainer")]
    [Timeout(TestTimeout_10s)]
    public async Task LogRetentionPipeline_Create_WithDefaults_Starts_Running_Async()
    {
        var provider = new BasicPipelineMockProvider();
        var pipeline = await provider.CreateLogRetentionPipelineWithDefaultsAsync();
        var cancellationToken = new CancellationTokenSource();
        await pipeline.StartAsync(cancellationToken.Token);
        await Task.Delay(DelayTimeout_1s);
        cancellationToken.Cancel();
        await pipeline.StopAsync(cancellationToken.Token);
        pipeline.Dispose();
    }

    [TestMethod]
    [Description("The log retention pipeline starts successfully with 2 days retention")]
    [TestCategory("Decision Service/Online Trainer")]
    [Timeout(TestTimeout_10s)]
    public async Task LogRetentionPipeline_Create_WithDefaults_Starts_Running_With_2Days_Retention_Async()
    {
        var provider = new BasicPipelineMockProvider();
        var config = provider.CreateDefaultLogRetentionConfig();
        config.LogRetentionDays = 2;
        var pipeline = await provider.CreateAsync(config);
        var cancellationToken = new CancellationTokenSource();
        await pipeline.StartAsync(cancellationToken.Token);
        await Task.Delay(DelayTimeout_1s);
        cancellationToken.Cancel();
        await pipeline.StopAsync(cancellationToken.Token);
        pipeline.Dispose();
    }

    [TestMethod]
    [Description("The log retention pipeline removes old logs")]
    [TestCategory("Decision Service/Online Trainer")]
    [Timeout(TestTimeout_10s)]
    public async Task LogRetentionPipeline_Remove_Old_Logs_Async()
    {
        var configDate = new DateTime(2017, 1, 1);
        var joinDataBlob = PathHelper.BuildBlobName(configDate, configDate, 0, "data", JoinedLogFormat.Binary);
        var provider = new BasicPipelineMockProvider();
        var pipeline = await provider.CreateLogRetentionPipelineWithDefaultsAsync();
        await CreateTestLogsAsync(provider.StorageContainerClient, configDate, configDate);
        Assert.IsTrue(await provider.StorageContainerClient.GetBlockBlobClient(joinDataBlob).ExistsAsync());
        var cancellationToken = new CancellationTokenSource();
        await pipeline.StartAsync(cancellationToken.Token);
        await Task.Delay(DelayTimeout_1s);
        cancellationToken.Cancel();
        await pipeline.StopAsync(cancellationToken.Token);
        pipeline.Dispose();
        Assert.IsFalse(await provider.StorageContainerClient.GetBlockBlobClient(joinDataBlob).ExistsAsync());
    }

    [TestMethod]
    [Description("The log retention pipeline does not remove newer logs")]
    [TestCategory("Decision Service/Online Trainer")]
    [Timeout(TestTimeout_10s)]
    public async Task LogRetentionPipeline_Does_Not_Remove_Newer_Logs_Async()
    {
        var now = DateTime.UtcNow;
        var configDate = new DateTime(2017, 1, 1);
        var checkpointDate1 = new DateTime(now.Year, now.Month, now.Day);
        var checkpointDate2 = new DateTime(now.Year, now.Month, now.Day);
        checkpointDate1 = checkpointDate1.AddDays(-10);
        var joinDataBlob1 = PathHelper.BuildBlobName(configDate, checkpointDate1, 0, "data", JoinedLogFormat.Binary);
        var joinDataBlob2 = PathHelper.BuildBlobName(configDate, checkpointDate2, 0, "data", JoinedLogFormat.Binary);
        var provider = new BasicPipelineMockProvider();
        var pipeline = await provider.CreateLogRetentionPipelineWithDefaultsAsync();
        await CreateTestLogsAsync(provider.StorageContainerClient, configDate, checkpointDate1);
        await CreateTestLogsAsync(provider.StorageContainerClient, configDate, checkpointDate2);
        Assert.IsTrue(await provider.StorageContainerClient.GetBlockBlobClient(joinDataBlob1).ExistsAsync());
        Assert.IsTrue(await provider.StorageContainerClient.GetBlockBlobClient(joinDataBlob2).ExistsAsync());
        var cancellationToken = new CancellationTokenSource();
        await pipeline.StartAsync(cancellationToken.Token);
        await Task.Delay(DelayTimeout_1s);
        cancellationToken.Cancel();
        await pipeline.StopAsync(cancellationToken.Token);
        pipeline.Dispose();
        Assert.IsFalse(await provider.StorageContainerClient.GetBlockBlobClient(joinDataBlob1).ExistsAsync());
        Assert.IsTrue(await provider.StorageContainerClient.GetBlockBlobClient(joinDataBlob2).ExistsAsync());
    }
}
