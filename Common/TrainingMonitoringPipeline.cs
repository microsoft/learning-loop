// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Error;
using Microsoft.DecisionService.Common.Storage;
using Microsoft.DecisionService.Common.Trainer.Data;
using Microsoft.DecisionService.Common.Trainer.Operations;
using Microsoft.DecisionService.Instrumentation;
using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Trainer
{
    public class TrainingMonitoringConfig
    {
        [Required]
        public string AppId { get; set; }

        public bool TrainingMonitoringEnabled { get; set; } = false;

        [Required]
        public TimeSpan? ExperimentalUnitDuration { get; set; } = null;
        public TimeSpan ModelExportFrequency { get; set; }

        [Required]
        public DateTime? LastConfigurationEditDate { get; set; } = null;
    }

    public class TrainingMonitoringPipeline : IDisposable, IHostedService
    {
        private readonly TrainingMonitoringConfig _trainingMonitoringConfig;
        private readonly string _appId;
        private readonly ILogger<TrainingMonitoringPipeline> _logger;

        private readonly ObservableGauge<double> _trainerLagInDays;
        private readonly ObservableGauge<long> _trainerLagInKb;
        private readonly ObservableGauge<int> _trainerExperimentalUnitDurationInSeconds;
        private readonly ObservableGauge<int> _trainerModelExportFrequencyInSeconds;

        private readonly ICheckpointBlockHelper _checkpointBlockHelper;

        private readonly IBlobContainerClient _blobContainerClient;

        private double _trainerLagInDaysValue = 0;
        private long _trainerLagInKbValue = 0;

        private readonly CancellationTokenSource _tokenSource;

        private IBlobLeaseHolder _blobLease;

        public Task Completion { get; private set; }


        public TrainingMonitoringPipeline(
            IOptions<TrainingMonitoringConfig> options,
            IMeterFactory meterFactory,
            IBlobContainerClient blobContainerClient,
            LoggerWithAppId<TrainingMonitoringPipeline> logger
        )
        {
            var defaultMetricsProperties = MetricsUtil.GetDefaultPropertiesV2(appId: options.Value.AppId);
            var meter = meterFactory.Create("Microsoft.DecisionService.OnlineTrainer.Monitoring");
            _trainerLagInKb = meter.CreateObservableGauge("TrainerLagInKB", () => _trainerLagInKbValue, null, null,
                defaultMetricsProperties);
            _trainerLagInDays = meter.CreateObservableGauge("TrainerLagInDays", () => _trainerLagInDaysValue, null,
                null,
                defaultMetricsProperties);
            _trainerExperimentalUnitDurationInSeconds = meter.CreateObservableGauge(
                "TrainerExperimentalUnitDurationInSeconds", () => options.Value.ExperimentalUnitDuration.Value.Seconds, null,
                null, defaultMetricsProperties);
            _trainerModelExportFrequencyInSeconds = meter.CreateObservableGauge("TrainerModelExportFrequencyInSeconds",
                () => options.Value.ModelExportFrequency.Seconds, null, null, defaultMetricsProperties);

            _logger = logger.Instance;
            _trainingMonitoringConfig = options.Value;
            _appId = options.Value.AppId;
            _blobContainerClient = blobContainerClient;

            _tokenSource = new CancellationTokenSource();

            _checkpointBlockHelper = new CheckpointBlockHelper(
                new CheckpointBlockHelperOptions()
                {
                    AppId = _appId,
                    BlockStoreProvider = _blobContainerClient.CreateBlockStoreProvider()
                }, logger.Instance);
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger?.LogInformation("Training monitoring pipeline started");

                await RunMonitoringTaskAsync(cancellationToken);

                TimeSpan ts = TimeSpan.FromHours(1);
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(ts, cancellationToken).ContinueWith(
                        async (x) => { await RunMonitoringTaskAsync(cancellationToken); }, cancellationToken,
                        TaskContinuationOptions.None, TaskScheduler.Default);
                }
            }
            catch (TaskCanceledException)
            {
                _logger?.LogInformation("TaskCanceledException Exiting TrainingMonitoringPipeline RunAsync()");
            }
            catch (Exception ex)
            {
                _logger?.LogInformation(ex, "{ErrorCode}",
                    PersonalizerInternalErrorCode.TrainingMonitoringPipelineFailure.ToString());
            }
            finally
            {
                _logger?.LogInformation("finally Exiting TrainingMonitoringPipeline RunAsync()");
            }
        }

        public void Dispose()
        {
            _tokenSource.Cancel();
        }

        private async Task RunMonitoringTaskAsync(CancellationToken cancellationToken)
        {
            try
            {
                DateTime now = DateTime.UtcNow;
                _logger?.LogInformation("Executing training monitoring task at {now} Utc", now);

                // Extract Joiner Checkpoint Date and Index
                var storageCheckpointBlob = _blobContainerClient.GetBlobClient(PathHelper.BuildCheckpointName(
                    _trainingMonitoringConfig.LastConfigurationEditDate.Value,
                    AzureBlobConstants.TenantStorageCheckpointBlobName));
                StorageCheckpoint storageCheckpoint =
                    await StorageCheckpointHelper.GetLastStorageCheckpointAsync(storageCheckpointBlob, _logger);
                if (storageCheckpoint == null)
                {
                    _logger?.LogInformation("Storage checkpoint blob not found");
                    return;
                }

                PathHelper.ParseIndexAndDate(storageCheckpoint.BlockPosition.BlobName, out int storageCheckpointIndex,
                    out int _, out DateTime storageCheckpointDate);

                // Extract Trainer Checkpoint Date and Index
                DateTime modelCheckpointDate = storageCheckpointDate;
                int modelCheckpointIndex = 0;
                ModelCheckpoint modelCheckpoint =
                    await _checkpointBlockHelper.GetCheckpointAsync(_trainingMonitoringConfig.LastConfigurationEditDate.Value,
                        cancellationToken);
                if (modelCheckpoint == null)
                {
                    // Default modelCheckpointDate to a day before storage checkpoint date creation time if training has not started
                    var storageCheckpointCreatedTime =
                        (await storageCheckpointBlob.GetPropertiesAsync()).CreatedOn;
                    modelCheckpointDate = new DateTime(storageCheckpointCreatedTime.Year,
                        storageCheckpointCreatedTime.Month, storageCheckpointCreatedTime.Day).AddDays(-1);
                    _logger?.LogInformation(
                        message:
                        "Model checkpoint blob not found. Using {modelCheckpointDate} as ModelCheckpointDate for Trainer monitoring",
                        modelCheckpointDate);
                }
                else
                {
                    PathHelper.ParseIndexAndDate(modelCheckpoint.ReadingPosition.BlobName, out modelCheckpointIndex,
                        out int _, out modelCheckpointDate);
                    modelCheckpointIndex += 1; //Skip blob on which trainer is currently working
                }

                // Track number of days between checkpoint dates
                TrackLagInDaysMetric(storageCheckpointDate.Subtract(modelCheckpointDate).TotalDays);

                // Compute size of blobs between trainer and joiner checkpoints
                long sizeOfCookedLogsInBytes = await PersonalizationStorageHelper.GetOverlappingCookedLogsSizeAsync(
                    _blobContainerClient,
                    modelCheckpointDate,
                    storageCheckpointDate,
                    cancellationToken,
                    modelCheckpointIndex,
                    storageCheckpointIndex);
                //Track metric of the above blobs size in KB
                TrackLagInKBMetric(sizeOfCookedLogsInBytes / 1024);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "");
            }
        }

        public virtual void TrackLagInDaysMetric(double days)
        {
            _trainerLagInDaysValue = days;
        }

        public virtual void TrackLagInKBMetric(long sizeOfCookedLogsInBytes)
        {
            _trainerLagInKbValue = sizeOfCookedLogsInBytes;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // TODO link the cancellationToken passed in here to the internal token source to disentangle initial acquisition from ongoing renewal
            _blobLease = await _blobContainerClient.AcquireLeaseAsync(_appId, "TrainingMonitoringLock",
                _trainingMonitoringConfig.LastConfigurationEditDate.Value, _logger, _tokenSource.Token);
            Completion = Task.Run(() => this.RunAsync(_tokenSource.Token));
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _tokenSource.Cancel();
            await Task.WhenAny(Task.WhenAll(_blobLease.Completion, Completion), Task.Delay(-1, cancellationToken));
        }
    }
}