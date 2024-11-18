// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Error;
using Microsoft.DecisionService.Common.Trainer.ModelExport;
using Microsoft.DecisionService.OnlineTrainer.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Microsoft.DecisionService.OnlineTrainer.Operations
{
    public sealed class ModelExportBlock
    {
        private readonly ActionBlock<ModelExportEvent> exportBlock;
        private readonly ITargetBlock<ModelExportEvent> inputBlock;
        private readonly ModelExportBlockOptions options;
        private readonly ILogger _logger;
        // private readonly IMetric<long> trainerModelSizeMetric;
        // private readonly IMetric<long> clientModelSizeMetric;
        // private readonly IMetric<long> examplesMetric;

        private readonly IModelExporter modelExporter;

        public ModelExportBlock(ModelExportBlockOptions options, IModelExporter modelExporter, ILogger logger)
        {
            Contract.Requires(options != null);
            this.options = options;
            this.modelExporter = modelExporter;
            this._logger = logger;
            
            // TODO do we want these metrics?
            // var defaultMetricsProperties = MetricsUtil.GetDefaultProperties(appId: options?.AppId, problemType: options?.ProblemType.ToString());
            // this.clientModelSizeMetric = this.options?.TelemetryService?.RegisterCumulativeMetric<long>("OnlineTrainer.ModelExport.Client.ModelSize", MetricAggregators.Average, defaultMetricsProperties);
            // this.trainerModelSizeMetric = this.options?.TelemetryService?.RegisterCumulativeMetric<long>("OnlineTrainer.ModelExport.Trainer.ModelSize", MetricAggregators.Average, defaultMetricsProperties);
            // this.examplesMetric = this.options?.TelemetryService?.RegisterCumulativeMetric<long>("OnlineTrainer.ModelExport.Examples", MetricAggregators.Sum, defaultMetricsProperties);

            var broadcastBlock = new BroadcastBlock<ModelExportEvent>(
                    e => e,
                    new DataflowBlockOptions
                    {
                        BoundedCapacity = 1,
                        CancellationToken = options.CancellationToken
                    });
            this.inputBlock = broadcastBlock;

            this.exportBlock = new ActionBlock<ModelExportEvent>(
                this.ExportAsync,
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = 1,
                    MaxDegreeOfParallelism = 1,
                    CancellationToken = options.CancellationToken
                });

            this.Completion = this.exportBlock.Completion.TraceAsync(logger, "ModelExportBlock", "ModelExportBlock.OnExit");

            broadcastBlock.LinkTo(this.exportBlock, new DataflowLinkOptions { PropagateCompletion = true });
        }

        public ITargetBlock<ModelExportEvent> Input => this.inputBlock;

        public Task Completion { get; }

        private async Task ExportAsync(ModelExportEvent model)
        {
            try
            {
                await this.modelExporter.UploadAsync(model.ClientModelData, model.TrainerModelData, model.JsonMetadata, this.options.CancellationToken);
                
                // this.clientModelSizeMetric?.Track(model.ClientModelData.Length);
                // this.trainerModelSizeMetric?.Track(model.TrainerModelData.Length);
                // this.examplesMetric?.IncrementBy(model.NumberOfEventsLearnedSinceLastExport);
            }
            catch (Exception e)
            {
                this._logger?.LogError(
                    e,
                    "{EventId} {ErrorCode} {Details}",
                    "ModelExportBlock.ExportAsync",
                    PersonalizerInternalErrorCode.TrainerCheckpointFailure.ToString(),
                    $"ModelSize:{model.ClientModelData.Length}");
            }
        }
    }
}
