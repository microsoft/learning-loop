// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Storage;
using Microsoft.DecisionService.Common.Trainer.ModelExport;
using Microsoft.DecisionService.Common.Trainer.Operations;
using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.DecisionService.OnlineTrainer.Operations;
using Microsoft.DecisionService.OnlineTrainer.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OpenTelemetry.Trace;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Microsoft.DecisionService.Common.Trainer
{
    public sealed class TrainerPipeline : IHostedService
    {
        public const string TrainerLockBlobName = "trainer-only.lock";

        private CheckpointBlock checkpointBlock;
        private ModelExportBlock modelExportBlock;
        private Task trainerTask;
        private Task joinedDataDownloadTask;

        private readonly IOptionsMonitor<TrainerConfig> _trainerConfig;
        private readonly ILogger _logger;
        private readonly TracerProvider _tracerProvider;
        private readonly IOnlineTrainer _trainer;
        private readonly IMeterFactory _meterFactory;
        private readonly IBlobContainerClient _containerClient;

        private CancellationTokenSource _overallCancellationTokenSource;
        private CancellationTokenSource _restartableTasksTokenSource;

        private Task _completion;
        private IBlobLeaseHolder _blobLease;

        public TrainerPipeline(IOptionsMonitor<TrainerConfig> options, IOnlineTrainer trainer, IMeterFactory meterFactory, IBlobContainerClient containerClient, TracerProvider tracerProvider, LoggerWithAppId<TrainerPipeline> logger)
        {
            this._trainerConfig = options;
            this._trainer = trainer;
            this._logger = logger.Instance;
            this._meterFactory = meterFactory;
            this._containerClient = containerClient;
            this._tracerProvider = tracerProvider;
        }

        // for testing purposes
        public event Action OnRestarting;
        public event Action OnRestarted;

        public async Task StartAsync(CancellationToken startCancellationToken)
        {
            this._overallCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(startCancellationToken);
            this._restartableTasksTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(_overallCancellationTokenSource.Token);
            // TODO link the cancellationToken passed in here to the internal token source to disentangle initial acquisition from ongoing renewal
            _blobLease = await _containerClient.AcquireLeaseAsync(
                _trainerConfig.CurrentValue.AppId,
                TrainerLockBlobName,
                _trainerConfig.CurrentValue.LastConfigurationEditDate.Value, _logger, _overallCancellationTokenSource.Token);

            _completion = await StartTasksInternalAsync(_restartableTasksTokenSource.Token);

            // TODO consider a mutex
            _trainerConfig.OnChange((config, _) =>
            {
                _logger.LogInformation($"TrainerPipeline config changed. New config: {config}");
                RestartAsync().GetAwaiter().GetResult();
            });
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _overallCancellationTokenSource.Cancel();
            // NOTE: _completion may not be set if it StopAsync is called before the lease is acquired
            //       -- find a better way to handle waiting for _completion to be set
            while (_completion == null) await Task.Yield();
            await Task.WhenAny(Task.WhenAll(_blobLease.Completion, _completion), Task.Delay(-1, cancellationToken));
        }

        private async Task<Task> StartTasksInternalAsync(CancellationToken cancellationToken)
        {
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await SetupTrainerPipelineAsync(tokenSource.Token);

            var trainerTasks = new List<Task>()
            {
                joinedDataDownloadTask,
                trainerTask,
                modelExportBlock?.Completion,
                checkpointBlock?.Completion
            };

            // Filter out any tasks that did not get initialized.
            // This can happen when we reached our max number of error retries
            // or if the pipeline is canceled.
            var validTrainerTasks = trainerTasks.Where(x => x != null);

            if (validTrainerTasks.Any())
            {
                // Ensure that any exceptions cause the pipeline to stop.
                _ = Task.WhenAny(validTrainerTasks)
                    .CancelOnFaultedAsync(tokenSource);
            }
            else
            {
                _logger?.LogInformation("Trainer pipeline had no tasks to run. Exiting trainer pipeline.");
            }

            return Task.WhenAll(validTrainerTasks);
        }

        private async Task RestartAsync()
        {
            OnRestarting?.Invoke();
            _restartableTasksTokenSource.Cancel();
            await Task.WhenAll(_blobLease.Completion, _completion);
            _restartableTasksTokenSource.Dispose();
            _restartableTasksTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(_overallCancellationTokenSource.Token);
            _completion = StartTasksInternalAsync(_restartableTasksTokenSource.Token);
            OnRestarted?.Invoke();
        }

        private async Task SetupTrainerPipelineAsync(CancellationToken cancellationToken)
        {
            var options = this._trainerConfig.CurrentValue;
            // have some minimums
            if (options.ModelCheckpointFrequency < TimeSpan.FromMinutes(1) && !options.IgnoreCheckpointAndExportFrequencyCapping)
                options.ModelCheckpointFrequency = TimeSpan.FromMinutes(1);

            if (options.ModelExportFrequency < TimeSpan.FromSeconds(10) && !options.IgnoreCheckpointAndExportFrequencyCapping)
                options.ModelExportFrequency = TimeSpan.FromSeconds(10);

            var timeProvider = SystemTimeProvider.Instance; // 'The' time provider for all blocks
            string containerName = StorageUtilities.BuildValidContainerName(options.AppId);

            // Create building blocks for trainer pipeline
            this.checkpointBlock = CreateCheckpointBlock(containerName, cancellationToken);

            ModelCheckpoint resumedModelCheckpoint = await DehydrateModelFromCheckpointAsync(checkpointBlock);

            this.modelExportBlock = CreateModelExportBlock(containerName, cancellationToken);


            var trainerBlock = await CreateTrainerBlockDataBlockAsync(modelExportBlock, checkpointBlock, timeProvider, _trainer,  _tracerProvider, new OnlineTrainerMetrics(options.AppId, _meterFactory), resumedModelCheckpoint.Model, resumedModelCheckpoint.Timestamp, cancellationToken);

            this.trainerTask = trainerBlock.Completion;

            var joinedDataDownloadBlock = CreateJoinedBlockDataDownloadBlock(resumedModelCheckpoint, cancellationToken);
            this.joinedDataDownloadTask = joinedDataDownloadBlock.Completion;

            joinedDataDownloadBlock.LinkTo(trainerBlock.JoinedDataInput, new DataflowLinkOptions { PropagateCompletion = true });

            _logger.LogInformation(
                message: $"Trainer pipeline started with LastConfigEditDate:{options.LastConfigurationEditDate}");
        }

        private ISourceBlock<BlockData> CreateJoinedBlockDataDownloadBlock(ModelCheckpoint resumedModelCheckpoint, CancellationToken cancellationToken)
        {
            var options = this._trainerConfig.CurrentValue;
            ISourceBlock<BlockData> dataSource = null;
            if (string.IsNullOrEmpty(options.LocalCookedLogsPath))
            {
                dataSource = new BlockDownloader(resumedModelCheckpoint?.ReadingPosition, _containerClient,
                    cancellationToken, options.LastConfigurationEditDate.Value, options.BlockBufferCapacityForEventBatch, _logger);
            }
            else
            {
                dataSource = new FileSystemDownloadBlock(resumedModelCheckpoint, options.LocalCookedLogsPath, _logger, cancellationToken);
            }

            return dataSource;
        }

        private TrainerBlockOptions CreateTrainerBlockOptions(
            byte[] initialModel,
            ModelExportBlock modelExportBlock,
            CheckpointBlock checkpointBlock,
            ITimeProvider timeProvider,
            IOnlineTrainer trainer,
            DateTime lastCheckpointTime,
            CancellationToken cancellationToken)
        {
            var options = this._trainerConfig.CurrentValue;
            return new TrainerBlockOptions
            {
                InitialModel = initialModel,
                CancellationToken = cancellationToken,
                ModelExportOutput = modelExportBlock.Input,
                ModelCheckpointOutput = checkpointBlock.Input,
                TimeProvider = timeProvider,
                AppId = options.AppId,
                ContainerClient = _containerClient,
                ModelExportFrequency = options.ModelExportFrequency,
                ModelCheckpointFrequency = options.ModelCheckpointFrequency,
                ProblemType = options.ProblemType,
                LastConfigurationEditDate = options.LastConfigurationEditDate.Value,
                OnlineTrainerCmdLine = trainer,
                IsLearningMetricsEnabled = true,
                LastCheckpointTime = lastCheckpointTime,
            };
        }

        private Task<TrainerBlockDataBlock> CreateTrainerBlockDataBlockAsync(
            ModelExportBlock modelExportBlock,
            CheckpointBlock checkpointBlock,
            ITimeProvider timeProvider,
            IOnlineTrainer trainer,
            TracerProvider tracerProvider,
            OnlineTrainerMetrics trainerMetrics,
            byte[] initialModel,
            DateTime lastCheckpointTime, CancellationToken cancellationToken)
        {
            var options = CreateTrainerBlockOptions(
                initialModel,
                modelExportBlock,
                checkpointBlock,
                timeProvider,
                trainer,
                lastCheckpointTime, cancellationToken);

            return TrainerBlockDataBlock.CreateAsync(options, trainerMetrics, tracerProvider, _logger);
        }

        private async Task<ModelCheckpoint> DehydrateModelFromCheckpointAsync(CheckpointBlock checkpointBlock)
        {
            var options = this._trainerConfig.CurrentValue;
            var resumedModelCheckpoint = await checkpointBlock.GetOrUpdateAsync(
               options.WarmstartModelUrl,
               options.WarmstartModelSource,
               options.WarmstartStartDateTime
            );
            _logger.LogInformation("resume trainer on checkpoint: {Checkpoint}", JsonConvert.SerializeObject(resumedModelCheckpoint));
            return resumedModelCheckpoint;
        }

        // Get checkpoint and historical models
        private CheckpointBlock CreateCheckpointBlock(string containerName, CancellationToken cancellationToken)
        {
            var options = this._trainerConfig.CurrentValue;
            CheckpointBlockHelper checkpointBlockHelper = new CheckpointBlockHelper(
                new CheckpointBlockHelperOptions()
                {
                    AppId = options.AppId,
                    BlockStoreProvider = _containerClient.CreateBlockStoreProvider()
                }, _logger);

            return new CheckpointBlock(
                new CheckpointBlockOptions
                {
                    CancellationToken = cancellationToken,
                    ContainerClient = _containerClient,
                    LastConfigurationEditDate = options.LastConfigurationEditDate.Value,
                    AppId = options.AppId,
                    ProblemType = options.ProblemType,
                    ContainerName = containerName,
                    CheckpointBlockHelper = checkpointBlockHelper
                }, _logger);
        }

        private ModelExportBlock CreateModelExportBlock(string containerName, CancellationToken cancellationToken)
        {
            var options = this._trainerConfig.CurrentValue;
            var modelExportBlockOptions = new ModelExportBlockOptions
            {
                AppId = options.AppId,
                CancellationToken = cancellationToken,
                ProblemType = options.ProblemType,
                ContainerName = containerName,
                ContainerClient = _containerClient,
                ModelAutoPublish = options.ModelAutoPublish,
                StagedModelHistoryLength = options.StagedModelHistoryLength
            };

            IModelExporter modelExporter = ResolveModelExporter(modelExportBlockOptions);
            return new ModelExportBlock(modelExportBlockOptions, modelExporter, _logger);
        }

        private IModelExporter ResolveModelExporter(ModelExportBlockOptions modelExportBlockOptions)
        {
            return ModelExporterFactory.Create(modelExportBlockOptions, _logger);
        }
    }
}
