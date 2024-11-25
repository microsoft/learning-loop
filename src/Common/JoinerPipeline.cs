// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Trainer.Billing;
using Microsoft.DecisionService.Common.Trainer.Operations;
using Microsoft.DecisionService.OnlineTrainer.Join;
using Microsoft.DecisionService.OnlineTrainer.Operations;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.DecisionService.Common.Billing;
using Microsoft.DecisionService.Common.Trainer.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.DecisionService.Common.Storage;
using System;

namespace Microsoft.DecisionService.Common.Trainer
{
    public sealed class JoinerPipeline : IHostedService
    {
        private readonly IOptionsMonitor<JoinerConfig> _joinerConfig;
        private readonly IOptionsMonitor<StorageBlockOptions> _storageBlockOptions;

        private IJoiner _joiner;
        private ITargetBlock<JoinedBatch> _joinedDataUpload;
        private readonly IJoinerFactory _joinerFactory;
        private readonly ILogger _logger;
        private readonly IMeterFactory _meterFactory;
        private readonly IBillingClient _billingClient;
        private readonly ITimeProvider _timeProvider;
        private readonly IBlobContainerClient _blobContainerClient;
        private CancellationTokenSource _overallCancellationTokenSource;
        private CancellationTokenSource _restartableTasksTokenSource;


        private Task _completion;
        private IBlobLeaseHolder _blobLease;
        private readonly TracerProvider _tracerProvider;

        public JoinerPipeline(
            IOptionsMonitor<JoinerConfig> options,
            IOptionsMonitor<StorageBlockOptions> storageOptions,
            IJoinerFactory joinerFactory,
            IBillingClient billingClient,
            IMeterFactory meterFactory,
            ITimeProvider timeProvider,
            IOptions<JoinerConfig> timeDadasfProvider,
            TracerProvider tracer,
            IBlobContainerClient blobContainerClient,
            ILogger<JoinerPipeline> logger)
        {
            _joinerConfig = options;
            _storageBlockOptions = storageOptions;
            this._joinerFactory = joinerFactory;
            this._logger = logger;
            this._tracerProvider = tracer;
            this._meterFactory = meterFactory;
            this._billingClient = billingClient;
            this._timeProvider = timeProvider;
            this._blobContainerClient = blobContainerClient;
        }

        private Task<BillingBlock> CreateBillingBlockAsync(CancellationToken cancellationToken)
        {
            return BillingBlock.CreateAsync(_billingClient,
                _blobContainerClient,
                new BillingBlockOptions()
                {
                    AppId = _joinerConfig.CurrentValue.AppId,
                    BlockBufferCapacity = _joinerConfig.CurrentValue.BlockBufferCapacityForEventBatch
                }, this._meterFactory, _logger, cancellationToken);
        }

        private async Task<Task> StartTasksInternalAsync(CancellationToken cancellationToken)
        {
            var tracer = _tracerProvider.GetTracer("Joiner.Startup");
            using var span = tracer.StartActiveSpan("Starting JoinerPipeline");
            // Now we have the lease, we can start the joiner
            this._logger?.LogInformation(
                $"JoinerPipeline started with LastConfigEditDate:{_joinerConfig.CurrentValue.LastConfigurationEditDate}");
            EventHubCheckpoint position = null;
            if (string.IsNullOrEmpty(_joinerConfig.CurrentValue.LocalCookedLogsPath))
            {
                var logMirrorSettings = new LogMirrorSettings
                {
                    Enabled = _joinerConfig.CurrentValue.LogMirrorEnabled,
                    SasUri = _joinerConfig.CurrentValue.LogMirrorSasUri
                };

                BillingBlock billingBlock = null;
                if (!this._joinerConfig.CurrentValue.IsBillingEnabled)
                {
                    billingBlock = await CreateBillingBlockAsync(cancellationToken);
                }
                else
                {
                    this._logger?.LogInformation(
                        message: $"App name {_joinerConfig.CurrentValue.AppId} has billing enabled on the frontend.");
                }

                span.AddEvent("Creating storage block");
                var storageLogSerializeBlock = new StorageLogSerializeBlock(
                    _storageBlockOptions.CurrentValue,
                    _blobContainerClient,
                    logMirrorSettings,
                    billingBlock,
                    meterFactory: _meterFactory,
                    timeProvider: _timeProvider);
                span.AddEvent("Setting up blocks");
                await storageLogSerializeBlock.SetupBlocksAsync();
                span.AddEvent("Resuming from checkpoint");
                position = await storageLogSerializeBlock.ResumeByTenantStorageCheckpointAsync();
                span.AddEvent("Done resuming");
                this._joinedDataUpload = storageLogSerializeBlock;
            }
            else
            {
                // this._joinedDataUpload = new FileSystemUploadBlock(_storageBlockOptions.CurrentValue,
                //     _joinerConfig.CurrentValue.LocalCookedLogsPath, uploadHelper, cancellationToken);
                throw new NotImplementedException("LocalCookedLogsPath is not implemented");
            }

            span.AddEvent("Creating joiner");
            this._joiner = _joinerFactory.Create(_joinerConfig.CurrentValue, position, this._joinedDataUpload,
                _timeProvider,
                this._meterFactory, _logger);
            span.AddEvent("Done creating joiner, starting now");
            await this._joiner.StartAsync(cancellationToken);
            span.AddEvent("Done starting joiner");

            span.End();
            // joiner pipeline exits when all components complete or times out
            return Task.WhenAll(_joiner.Completion.CancelOnFaultedAsync(_overallCancellationTokenSource),
                    _joinedDataUpload.Completion.CancelOnFaultedAsync(_overallCancellationTokenSource))
                .TraceAsync(_logger, "Exiting JoinerPipeline.");
        }

        public async Task StartAsync(CancellationToken startCancellationToken)
        {
            this._overallCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(startCancellationToken);
            this._restartableTasksTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(_overallCancellationTokenSource.Token);

            // acquire lease before being able to start.
            // TODO link the cancellationToken passed in here to the internal token source to disentangle initial acquisition from ongoing renewal
            _blobLease = await _blobContainerClient.AcquireLeaseAsync(_joinerConfig.CurrentValue.AppId,
                "JoinerLock",
                _joinerConfig.CurrentValue.LastConfigurationEditDate.Value, _logger, _overallCancellationTokenSource.Token);

            _completion = await StartTasksInternalAsync(_restartableTasksTokenSource.Token);

            // TODO consider a mutex
            _joinerConfig.OnChange((config, _) =>
            {
                _logger.LogInformation($"JoinerPipeline config changed. New config: {config}");
                RestartAsync().GetAwaiter().GetResult();
            });

            _storageBlockOptions.OnChange((config, _) =>
            {
                _logger.LogInformation($"JoinerPipeline config changed. New config: {config}");
                RestartAsync().GetAwaiter().GetResult();
            });
        }

        private async Task RestartAsync()
        {
            _restartableTasksTokenSource.Cancel();
            await Task.WhenAll(_blobLease.Completion, _completion);
            _restartableTasksTokenSource.Dispose();
            _restartableTasksTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(_overallCancellationTokenSource.Token);
            _completion = await StartTasksInternalAsync(_restartableTasksTokenSource.Token);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _overallCancellationTokenSource.Cancel();
            // NOTE: _completion may not be set if it StopAsync is called before the lease is acquired
            //       -- find a better way to handle waiting for _completion to be set
            while (_completion == null) await Task.Yield();
            await Task.WhenAny(Task.WhenAll(_blobLease.Completion, _completion), Task.Delay(-1, cancellationToken));
        }
    }
}