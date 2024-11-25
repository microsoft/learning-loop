// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Error;
using Microsoft.DecisionService.Instrumentation;
using Newtonsoft.Json;
using System;
using System.Diagnostics.Contracts;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;

namespace Microsoft.DecisionService.OnlineTrainer.Operations
{
    public sealed class CheckpointBlock
    {
        private readonly ActionBlock<ModelCheckpoint> persistBlock;
        private readonly BroadcastBlock<ModelCheckpoint> inputBlock;
        private readonly CheckpointBlockOptions options;
        private readonly ICheckpointBlockHelper checkpointBlockHelper;
        private readonly ILogger appIdLogger;

        public CheckpointBlock(CheckpointBlockOptions options, ILogger logger)
        {
            Contract.Requires(options != null);
            this.options = options;
            this.appIdLogger = logger;
            this.checkpointBlockHelper = options.CheckpointBlockHelper;
            var defaultMetricsProperties = MetricsUtil.GetDefaultProperties(appId: options?.AppId, problemType: options?.ProblemType.ToString());

            // TODO renable if we want?
            // this.histBlobModelSizeMetric = this.options?.TelemetryService?.RegisterCumulativeMetric<long>("OnlineTrainer.Historical.Blob.ModelSize", MetricAggregators.Average, defaultMetricsProperties);
            // this.histBlobExamplesMetric = this.options?.TelemetryService?.RegisterCumulativeMetric<long>("OnlineTrainer.Historical.Blob.Examples", MetricAggregators.Sum, defaultMetricsProperties);
            
            this.Input = this.inputBlock = new BroadcastBlock<ModelCheckpoint>(
                    e => e,
                    new DataflowBlockOptions
                    {
                        BoundedCapacity = 1,
                        CancellationToken = options.CancellationToken
                    });

            this.persistBlock = new ActionBlock<ModelCheckpoint>(
                this.PersistAsync,
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = 1,
                    MaxDegreeOfParallelism = 1,
                    CancellationToken = options.CancellationToken
                });

            this.Completion = this.persistBlock.Completion.TraceAsync(logger, "CheckpointBlock", "CheckpointBlock.OnExit");

            this.inputBlock.LinkTo(this.persistBlock, new DataflowLinkOptions { PropagateCompletion = true });
        }

        public ITargetBlock<ModelCheckpoint> Input { get; }

        public Task Completion { get; }

        /// <summary>
        /// Get current checkpoint or return warmstart values
        /// </summary>
        public async Task<ModelCheckpoint> GetOrUpdateAsync(Uri warmstartModelUrl, DateTime? warmstartStartDateTime)
        {
            //try to download the existing checkpoint
            ModelCheckpoint checkpoint = await this.checkpointBlockHelper.GetCheckpointAsync(this.options.LastConfigurationEditDate, this.options.CancellationToken);

            //if a checkpoint is found, just return it
            if (checkpoint != null)
            {
                this.appIdLogger?.LogInformation("Last checkpoint model: {ModelId}", checkpoint.HistoricalModelInfo?.ModelId);
                return checkpoint;
            }

            //otherwise, use the warmstart parameters
            checkpoint = new ModelCheckpoint();

            //download the warmstart model
            if (warmstartModelUrl != null)
            {
                try
                {
                    // download model
                    using (var client = new HttpClient())
                    {
                        checkpoint.Model = await client.GetByteArrayAsync(warmstartModelUrl);
                    }

                    checkpoint.WarmstartModelUrl = warmstartModelUrl.ToString();
                    this.appIdLogger?.LogInformation("Last warmstart model: {Url}", checkpoint.WarmstartModelUrl);
                }
                catch (Exception e)
                {
                    this.appIdLogger?.LogError(e, "{EventId} {Details} {ErrorCode}",  "CheckpointBlock.GetOrUpdateAsync",  "Issue reading model from warmstart URL. Using default checkpoint",  PersonalizerInternalErrorCode.TrainerCheckpointFailure.ToString());
                }
            }

            //set the warmstart date
            if (warmstartStartDateTime.HasValue)
            {
                checkpoint.WarmstartStartDateTime = warmstartStartDateTime;
                // Geneva logger
                this.appIdLogger?.LogInformation("Last warmstart date: {Date}", checkpoint.WarmstartStartDateTime);
            }

            return checkpoint;
        }

        /// <summary>
        /// Save the checkpoint and the historical model
        /// </summary>
        private async Task PersistAsync(ModelCheckpoint checkpoint)
        {
            try
            {
                await this.checkpointBlockHelper.SaveCheckpointAsync(checkpoint, this.options.LastConfigurationEditDate, this.options.CancellationToken);
                await SaveHistoricalModelAsync(checkpoint);
            }
            catch (Exception e)
            {
                // Geneva logger
                this.appIdLogger?.LogError(e, "{EventId} {ErrorCode}", "CheckpointBlock.PersistAsync", PersonalizerInternalErrorCode.TrainerCheckpointFailure.ToString());
            }
        }

        /// <summary>
        /// Save 1 model per day in the tenant storage
        /// </summary>
        private async Task SaveHistoricalModelAsync(ModelCheckpoint checkpoint)
        {
            if (checkpoint.HistoricalModelInfo == null)
            {
                // nothing to save, noop
                return;
            }
            if (!checkpoint.HistoricalModelInfo.WasExported)
            {
                // nothing was exported, noop
                return;
            }

            ////create blob name of historical model
            var baseName = checkpoint.ReadingPosition.BlobName
                .Replace("/data/", "/model/")
                .Replace(".json", "");

            var container = this.options.ContainerClient;
            if (container == null)
            {
                this.appIdLogger?.LogError("attempt to saved historical model failed for {BlobName}: modeId={ModelId}", baseName, checkpoint.HistoricalModelInfo?.ModelId);
                return;
            }

            var modelBlob = container.GetBlobClient(baseName + ".vw");
            var infoBlob = container.GetBlobClient(baseName + ".json");
            if (!await modelBlob.ExistsAsync(this.options.CancellationToken) || !await infoBlob.ExistsAsync(this.options.CancellationToken))
            {
                await modelBlob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots.ToString(), cancellationToken:this.options.CancellationToken);
                await modelBlob.UploadAsync(BinaryData.FromBytes(checkpoint.Model), this.options.CancellationToken);
                await infoBlob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots.ToString(),  cancellationToken:this.options.CancellationToken);
                await infoBlob.UploadAsync(BinaryData.FromString(JsonConvert.SerializeObject(checkpoint.HistoricalModelInfo)), this.options.CancellationToken);

                // Geneva Logger
                // histBlobModelSizeMetric?.Track(checkpoint.Model.Length);
                // histBlobExamplesMetric?.IncrementBy(checkpoint.NumberOfExamplesLearnedSinceLastCheckpoint);
                this.appIdLogger?.LogInformation("saved historical model: {Name}, modeId={ModelId}", modelBlob.Name, checkpoint.HistoricalModelInfo?.ModelId);
            }
        }
    }
}
