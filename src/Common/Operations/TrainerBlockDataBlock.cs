// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Trainer;
using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.OnlineTrainer.Data;
using Newtonsoft.Json;
using System.Diagnostics.Contracts;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks.Dataflow;
using System.Threading.Tasks;
using System.Threading;
using System.Timers;
using System;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using Timer = System.Timers.Timer;
using Microsoft.DecisionService.Common.Storage;

namespace Microsoft.DecisionService.OnlineTrainer.Operations
{
    public sealed class TrainerBlockDataBlock : IDisposable
    {
        private readonly ActionBlock<BlockData> learnBlock;

        private readonly TrainerBlockOptions options;
        private int numberOfEventsLearnedSinceLastCheckpoint;
        private int numberOfEventsLearnedSinceLastExport;
        private string firstEventIdSinceLastExport;
        private string lastEventId;
        private BlockPosition lastBlockPosition;
        private DateTime nextExportTimeUtc;
        private DateTime nextCheckpointTimeUtc;
        private readonly Timer timer;
        private IBlobContainerClient appContainer;

        private byte[] _currentModel;

        private IOnlineTrainer onlineTrainer;
        private readonly ILogger appIdLogger;

        private readonly OnlineTrainerMetrics _onlineTrainerMetrics;
        private readonly TracerProvider _tracerProvider;

        private TrainerBlockDataBlock(TrainerBlockOptions options, OnlineTrainerMetrics metrics, TracerProvider tracerProvider, ILogger logger)
        {
            Contract.Requires(options != null);
            _tracerProvider = tracerProvider;

            this.options = options;
            this.appIdLogger = logger;
            this._onlineTrainerMetrics = metrics;
            this.appContainer = this.options.ContainerClient;
            _currentModel = this.options.InitialModel;

            this.timer = new Timer
            {
                AutoReset = false
            };
            
            this.timer.Elapsed += OnTimerElapsed;

            this.appIdLogger?.LogInformation("start trainer block {EventKey}", "TrainerBlock");

            this.onlineTrainer = this.options.OnlineTrainerCmdLine;

            var lastCheckpointTimeUtc = this.options.LastCheckpointTime == default(DateTime)
                ? this.options.TimeProvider.UtcNow
                : this.options.LastCheckpointTime;
            this.nextCheckpointTimeUtc = lastCheckpointTimeUtc + this.options.ModelCheckpointFrequency;

            this.learnBlock = new ActionBlock<BlockData>(
                this.LearnAsync,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 1,
                    // tuned on complex and windows marketing data.
                    BoundedCapacity = 32,
                    // No CancellationToken as we want to cleanup
                });

            // Log completion of block
            Completion = this.learnBlock.Completion.TraceAsync(logger, "LearnBlockOnExit", "TrainerBlock.OnExit");

            // make sure we drain the current examples (it's just 32 due to bounded capacity anyway)
            this.options.CancellationToken.Register(() => { this.learnBlock.Complete(); });

            Completion = Completion.ContinueWith(previousTask =>
                {
                    this.options.ModelCheckpointOutput.Complete();
                    this.options.ModelExportOutput.Complete();

                    // Propagate any exception from the previous task
                    if (previousTask != null && previousTask.IsFaulted && previousTask.Exception != null)
                    {
                        throw new AggregateException(
                            $"Rethrowing exception in ContinueWith. Original message: {previousTask.Exception.Message}",
                            previousTask.Exception);
                    }
                }, TaskScheduler.Default)
                .TraceAsync(logger, "TrainerBlock", "TrainerBlock.OnExit");
        }
        
        public static async Task<TrainerBlockDataBlock> CreateAsync(TrainerBlockOptions options, OnlineTrainerMetrics metrics, TracerProvider tracerProvider, ILogger logger)
        {
            var trainerBlock = new TrainerBlockDataBlock(options, metrics, tracerProvider, logger);
            var lastModelExportTimeUtc =
                await StorageUtilities.GetBlobLastModifyDateAsync(trainerBlock.appContainer, AzureBlobConstants.ClientModelBlobName);
            trainerBlock.nextExportTimeUtc =
                (lastModelExportTimeUtc == default(DateTime)
                    ? trainerBlock.options.TimeProvider.UtcNow
                    : lastModelExportTimeUtc) + trainerBlock.options.ModelExportFrequency;
            return trainerBlock;
        }

        public Task Completion { get; private set; }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.timer.Dispose();
            }
        }

        ~TrainerBlockDataBlock()
        {
            Dispose(false);
        }


        public ITargetBlock<BlockData> JoinedDataInput => this.learnBlock;

        private async Task LearnAsync(BlockData data)
        {
            var tracer = _tracerProvider.GetTracer("Trainer.LearnAsync");
            using var span = tracer.StartActiveSpan("LearnAsync");
            if (data != null)
            {
                span.AddEvent("DataReceived");
                _onlineTrainerMetrics.CookedLogSizeInBytes(data.Data.Length);

                var learnTimeStopWatch = Stopwatch.StartNew();
                IOnlineTrainer.LearnResult learnResult;
                try
                {
                    learnResult = await this.onlineTrainer.LearnAsync(data.Data, _currentModel,data.Position.FileFormat,
                        this.options.CancellationToken);
                }
                catch (Exception e)
                {
                    this.appIdLogger?.LogError(e,
                        "LearnAsync threw exception while processing Blob: {BlobName} Block: {BlockName} Size: {Length}",
                        data.Position?.BlobName, data.Position?.BlockName, data.Data?.Length);
                    _onlineTrainerMetrics.LearnException();
                    this.appIdLogger?.LogWarning(
                        "Trainer skipping Blob: {BlobName} Block: {BlockName} Size: {Length} due to Exception",
                        data.Position?.BlobName, data.Position?.BlockName, data.Data?.Length
                    );

                    span.RecordException(e);

                    // Propagate an invalid model exception so we can restart the trainer with LKG model.
                    if (e is InvalidModelException) throw;
                    return;
                }

                learnTimeStopWatch.Stop();
                
                // Update our current model based on this iteration.
                _currentModel = learnResult.FinalModel;

                _onlineTrainerMetrics.LearnTimeMs(learnTimeStopWatch.ElapsedMilliseconds);
                _onlineTrainerMetrics.LearnThroughputInEventsPerMS(learnTimeStopWatch.ElapsedMilliseconds > 0
                    ? (double)learnResult.Metrics.NumberOfLearnedEvents / (double)learnTimeStopWatch.ElapsedMilliseconds
                    : 0.0);
                _onlineTrainerMetrics.LearnThroughputInBytesPerMS(learnTimeStopWatch.ElapsedMilliseconds > 0
                    ? (double)data.Data.Length / (double)learnTimeStopWatch.ElapsedMilliseconds
                    : 0.0);
                _onlineTrainerMetrics.LearnedEvents(learnResult.Metrics.NumberOfLearnedEvents);
                _onlineTrainerMetrics.FaultyExamples(learnResult.Metrics.NumberOfFaultyEvents);
                _onlineTrainerMetrics.ActionsPerEvent(learnResult.Metrics.AverageActionsPerEvent);
                _onlineTrainerMetrics.NamespacesPerEvent(learnResult.Metrics.AverageNamespacesPerEvent);
                _onlineTrainerMetrics.NamespacesPerExample(learnResult.Metrics.AverageNamespacesPerExample);
                _onlineTrainerMetrics.FeaturesPerExample(learnResult.Metrics.AverageFeaturesPerExample);
                _onlineTrainerMetrics.FeaturesPerEvent(learnResult.Metrics.AverageActionsPerEvent);

                this.numberOfEventsLearnedSinceLastCheckpoint += (int)learnResult.Metrics.NumberOfLearnedEvents;
                this.numberOfEventsLearnedSinceLastExport += (int)learnResult.Metrics.NumberOfLearnedEvents;

                this.firstEventIdSinceLastExport = firstEventIdSinceLastExport ?? learnResult.Metrics.FirstEventId;
                this.lastEventId = learnResult.Metrics.LastEventId;
                this.lastBlockPosition = data.Position;

                this.appIdLogger?.LogInformation(
                    "Trainer finished processing Blockname: {BlockName} Blobname: {BlobName} Size: {Length}",
                    data.Position?.BlockName, data.Position?.BlobName, data.Data?.Length);
            }
            else
            {
                span.AddEvent("NonDataEvent");
                this.appIdLogger?.LogInformation("Trainer processing null data");
                timer.Enabled = false;
            }

            if (ShouldCheckpointNow(out bool shouldExportToo))
            {
                span.AddEvent("Checkpointing");
                await SendCheckpointAsync(shouldExportToo, this.lastBlockPosition, tracer);
            }

            if (!timer.Enabled &&
                (this.numberOfEventsLearnedSinceLastExport >
                 0)) // If its a new event or there is a pending model export, enable checkpoint timer
            {
                span.AddEvent("CheckpointTimerEnabled");
                EnableCheckpointTimer();
            }
        }

        /// <summary>
        /// Return true if a checkpoint is required
        /// </summary>
        /// <param name="shouldExportToo">true if an export is also required</param>
        private bool ShouldCheckpointNow(out bool shouldExportToo)
        {
            var now = this.options.TimeProvider.UtcNow;

            var shouldCheckpoint =
                this.nextCheckpointTimeUtc < now && this.numberOfEventsLearnedSinceLastCheckpoint > 0;
            shouldExportToo = this.nextExportTimeUtc < now && this.numberOfEventsLearnedSinceLastExport > 0;

            return shouldExportToo || shouldCheckpoint;
        }

        private void EnableCheckpointTimer()
        {
            timer.Interval = FindShortestInterval();
            timer.Enabled = true;
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void OnTimerElapsed(object sender, ElapsedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            // Catch all exceptions so we don't crash the process if one is thrown.
            try
            {
                await this.learnBlock.SendAsync(null);
            }
            catch (Exception ex)
            {
                this.appIdLogger?.LogError(ex,
                    "{eventKey}", "OnModelExportTimerElapsedAsync.Exception");
                if (timer != null)
                {
                    timer.Enabled = true;
                }
            }
        }

        /// <summary>
        /// Find the interval of time since last model export or model checkpoint, whichever is shorter.
        /// </summary>
        /// <returns></returns>
        private double FindShortestInterval()
        {
            var utcNow = this.options.TimeProvider.UtcNow;

            // Occasionally, the time it takes between checking if we should advance the checkpoint/export time and enabling the timer
            // will mean that utcNow is a few milliseconds ahead of closerIntervalEndpoint
            // This section guards against having a negative time interval and will find the next closest suitable interval

            var checkpointDiff = this.nextCheckpointTimeUtc - utcNow;
            var checkpointIntervalEndpoint = this.nextCheckpointTimeUtc.Ticks;
            if (checkpointDiff.TotalMilliseconds <= 0)
            {
                // Calculate the smallest step needed to make interval endpoint > utcNow that is still a multiple of model checkpoint frequency
                var checkpointIncrement =
                    (((utcNow - this.nextCheckpointTimeUtc).Ticks / this.options.ModelCheckpointFrequency.Ticks) + 1) *
                    this.options.ModelCheckpointFrequency.Ticks;
                checkpointIntervalEndpoint = checkpointIncrement + this.nextCheckpointTimeUtc.Ticks;
            }

            var exportDiff = this.nextExportTimeUtc - utcNow;
            var exportIntervalEndpoint = this.nextExportTimeUtc.Ticks;
            if (exportDiff.TotalMilliseconds <= 0)
            {
                var exportIncrement =
                    (((utcNow - this.nextExportTimeUtc).Ticks / this.options.ModelExportFrequency.Ticks) + 1) *
                    this.options.ModelExportFrequency.Ticks;
                exportIntervalEndpoint = exportIncrement + this.nextExportTimeUtc.Ticks;
            }

            var intervalEndpoint = new DateTime(Math.Min(checkpointIntervalEndpoint, exportIntervalEndpoint));
            TimeSpan interval = intervalEndpoint - utcNow;
            return interval.TotalMilliseconds;
        }

        /// <summary>
        /// Save a checkpoint, possibly export the model
        /// </summary>
        /// <param name="exportModel">true if a model export is also requested</param>
        /// <param name="readingPosition"></param>
        /// <param name="tracer"></param>
        private async Task SendCheckpointAsync(bool exportModel, BlockPosition readingPosition, Tracer tracer)
        {
            using var span = tracer.StartActiveSpan("SendCheckpointAsync");
            //important get a deep-copy otherwise we'll continue to update the model
            string modelId = $"{this.firstEventIdSinceLastExport}/{this.lastEventId}";
            span.AddEvent("ExportingModel");
            var trainerModel =
                await this.onlineTrainer.ExportTrainerModelAsync(_currentModel, modelId, this.options.CancellationToken);
            var result = await this.onlineTrainer.ValidateModelAsync(trainerModel, this.options.CancellationToken);
            var isTrainerModelValid = result.IsValid;
            if (!result.IsValid)
            {
                span.RecordException(new TrainerException($"Invalid exported model {result.Errors}"));
                appIdLogger.LogError("Invalid exported model {}", result.Errors);
            }

            if (isTrainerModelValid)
            {
                span.AddEvent("SendingExportedModelToOutput");

                await this.options.ModelCheckpointOutput.SendAsync(
                    new ModelCheckpoint
                    {
                        Timestamp = this.options.TimeProvider.UtcNow,
                        Model = trainerModel,
                        NumberOfExamplesLearnedSinceLastCheckpoint = this.numberOfEventsLearnedSinceLastCheckpoint,
                        HistoricalModelInfo = new HistoricalModelInfo
                        {
                            FirstEventId = this.firstEventIdSinceLastExport,
                            LastEventId = this.lastEventId,
                            ModelId = modelId,
                            WasExported = exportModel,
                        },
                        ReadingPosition = readingPosition,
                    },
                    this.options.CancellationToken);

                this.numberOfEventsLearnedSinceLastCheckpoint = 0;
                _onlineTrainerMetrics.ModelCheckpointed();
            }
            else
            {
                string invalidTrainerBlobName =
                    $"{AzureBlobConstants.InvalidModelsDirectory}/trainer-{DateTime.UtcNow:yyyyMMddHHmmss}";
                this.appIdLogger?.LogWarning(
                    "Skipping checkpoint. VW generated trainer model {invalidTrainerBlobName} is invalid",
                    invalidTrainerBlobName);
                await UploadByteArrayToBlobAsync(invalidTrainerBlobName, trainerModel, this.options.CancellationToken);
            }

            //Model checkpoint occurs every minute and is not configurable by the user
            this.nextCheckpointTimeUtc = this.options.TimeProvider.UtcNow + this.options.ModelCheckpointFrequency;

            // conditionally export the model
            if (exportModel)
            {
                byte[] clientModel;
                await using (var memStream = new MemoryStream())
                {
                    clientModel = await
                        this.onlineTrainer.ConvertToInferenceModelAsync(_currentModel, modelId,
                            this.options.CancellationToken);

                    this.appIdLogger?.LogInformation(
                        "End OnlineTrainer.SnapshotExportModel, ModelSize: {Length}, ModelId: {modelId}",
                        clientModel.Length, modelId);
                }

                bool isClientModelValid = (await
                    this.onlineTrainer.ValidateModelAsync(clientModel)).IsValid;
                if (isTrainerModelValid && isClientModelValid)
                {
                    var metadata = new ModelMetadata
                    {
                        ModelId = DateTime.UtcNow.ToString(Common.ApplicationConstants.DateTimeStringFormat),
                        UserDescription = string.Empty,
                        CreationDate = this.options.TimeProvider.UtcNow,
                        LastConfigEditDate = this.options.LastConfigurationEditDate.ToUniversalTime(),
                        FirstEventId = this.firstEventIdSinceLastExport ?? string.Empty,
                        LastEventId = this.lastEventId ?? string.Empty,
                        SavedInHistory = false,
                        NumberOfEventsLearnedSinceLastExport = this.numberOfEventsLearnedSinceLastExport
                    };

                    await this.options.ModelExportOutput.SendAsync(
                        new ModelExportEvent
                        {
                            ClientModelData = clientModel,
                            TrainerModelData = trainerModel,
                            NumberOfEventsLearnedSinceLastExport = this.numberOfEventsLearnedSinceLastExport,
                            JsonMetadata = JsonConvert.SerializeObject(metadata)
                        });

                    this.numberOfEventsLearnedSinceLastExport = 0;
                    this.nextExportTimeUtc = this.options.TimeProvider.UtcNow + this.options.ModelExportFrequency;
                    this.firstEventIdSinceLastExport = this.lastEventId = null;
                    _onlineTrainerMetrics.ModelExported();
                    this.appIdLogger?.LogInformation("Exporting Model, ModelId: {modelId}", modelId);
                }
                else
                {
                    //if model export fails due to invalid model generation try again after a minute
                    this.nextExportTimeUtc = this.options.TimeProvider.UtcNow.AddMinutes(1);
                    this.appIdLogger?.LogInformation(
                        $"Skipping model exports. VW generated trainer/client model is invalid");
                    if (!isClientModelValid)
                    {
                        string invalidClientBlobName =
                            $"{AzureBlobConstants.InvalidModelsDirectory}/client-{DateTime.UtcNow:yyyyMMddHHmmss}";
                        this.appIdLogger?.LogInformation(
                            $"VW generated client model {invalidClientBlobName} is invalid", invalidClientBlobName);
                        await UploadByteArrayToBlobAsync(invalidClientBlobName, clientModel,
                            this.options.CancellationToken);
                    }
                }
            }

            this.appIdLogger?.LogInformation(
                "Next model export time (UTC): {nextExportTimeUtc}, next model checkpoint time (UTC): {nextCheckpointTimeUtc}",
                this.nextExportTimeUtc, this.nextCheckpointTimeUtc);
        }

        private async Task UploadByteArrayToBlobAsync(string blobName, byte[] buffer,
            CancellationToken cancellationToken)
        {
            try
            {
                var modelCurrent = appContainer.GetBlobClient(blobName);
                await modelCurrent.UploadAsync(BinaryData.FromBytes(buffer), cancellationToken);
            }
            catch (Exception ex)
            {
                this.appIdLogger?.LogError(ex, "UploadByteArrayToBlobAsync");
            }
        }
    }
}