// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Error;
using Microsoft.DecisionService.Common.Storage;
using Microsoft.DecisionService.Common.Trainer.Data;
using Microsoft.DecisionService.Common.Trainer.FlatBuffers;
using Microsoft.DecisionService.Common.Trainer.Operations;
using Microsoft.DecisionService.OnlineTrainer.Operations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Microsoft.DecisionService.OnlineTrainer
{
    /// <summary>
    /// The <see cref="StorageUploadBlock"/> class is responsible for managing the upload
    /// of serialized event batches to Blob Storage. It handles the configuration, initialization,
    /// and execution of the upload process, including retry mechanisms and checkpointing.
    /// </summary>
    public class StorageUploadBlock
    {
        /// <summary>
        /// Defines the strategies for resuming the upload process.
        /// </summary>
        public enum ResumeStrategy
        {
            /// <summary>
            /// Resume from the last known checkpoint position.  If the checkpoint position
            /// is different from underlying storage, data may be lost.
            /// </summary>
            UseCheckpointPosition,

            /// <summary>
            /// Synchronize with the current state of the storage.
            /// </summary>
            SynchronizeWithStorage,

            /// <summary>
            /// Reset the position to zero and start from the beginning.  This will overwrite
            /// existing data in the storage.
            /// </summary>
            ResetToZero
        }

        /// <summary>
        /// The <see cref="StorageUploadLocationInfo"/> class contains information about the storage upload location,
        /// including blob details, size, block count, and file format.
        /// </summary>
        private class StorageUploadLocationInfo
        {
            public IBlockStore Blob;
            public long BlobSize;
            public int BlockCount;
            public int BlobIndex;
            public int BlobHour;
            public DateTime BlobDay = default;
            public string SubPath;
            public JoinedLogFormat FileFormat = JoinedLogFormat.DSJSON;

            /// <summary>
            /// Gets the block position information for the current storage upload location.
            /// </summary>
            /// <returns>A <see cref="BlockPosition"/> object containing the block position details.</returns>
            public BlockPosition GetBlockPosition()
            {
                return new BlockPosition()
                {
                    BlobName = this.Blob?.Name,
                    BlockName = this.BlockCount.ToString("x4"),
                    FileFormat = this.FileFormat,
                };
            }

            /// <summary>
            /// Gets the blob properties for the current storage upload location.
            /// </summary>
            /// <returns>A <see cref="BlobProperty"/> object containing the blob properties.</returns>
            public BlobProperty GetBlockProperties()
            {
                return new BlobProperty()
                {
                    BlobName = this.Blob?.Name,
                    Length = this.BlobSize,
                };
            }
        }

        private readonly IBlobContainerClient container;

        private readonly StorageBlockOptions options;

        private readonly ILogger appIdLogger;

        private readonly StorageUploadMetrics _metrics;

        private readonly StorageUploadType storageType;

        // number of attempts to upload events to storage before discarding the data
        private readonly int maxStorageUploadRetries = 10;
        private readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(5);

        private IBlobClient storageCheckpointBlob;

        private readonly CancellationToken cancellationToken;

        // These properties are mainly used for unit tests
        public int BlockCount => this.LearnableEventsUploadLocationInfo.BlockCount;
        public int BlobIndex => this.LearnableEventsUploadLocationInfo.BlobIndex;
        public int BlobHour => this.LearnableEventsUploadLocationInfo.BlobHour;
        public DateTime BlobDay => this.LearnableEventsUploadLocationInfo.BlobDay;

        // NOTE: We upload learnable events to a separate location from events
        // that cannot be used for learning (skipped).
        // This improves performance for the trainer since it will only download
        // and process the events that will be used in training.
        private readonly StorageUploadLocationInfo LearnableEventsUploadLocationInfo;

        private readonly ITimeProvider timeProvider;

        private EventHubCheckpoint _eventHubCheckpoint = new();

        public ITargetBlock<IList<SerializedBatch>> Input { get; }
        public ITargetBlock<IList<SerializedBatch>>? Output { get; }
        public Task Completion { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageUploadBlock"/> class.
        /// Sets up the necessary configurations and initializes the upload block.
        /// </summary>
        /// <param name="options">The storage block options.</param>
        /// <param name="container">The blob container client.</param>
        /// <param name="storageCheckpointBlob">The blob client for storage checkpoint.</param>
        /// <param name="type">The type of storage upload.</param>
        /// <param name="timeProvider">The time provider.</param>
        /// <param name="meterFactory">The meter factory for metrics.</param>
        /// <param name="targetBlock">The target block for serialized batches.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="retryDelay">The retry delay duration.</param>
        public StorageUploadBlock(
            StorageBlockOptions options,
            IBlobContainerClient container,
            IBlobClient storageCheckpointBlob,
            StorageUploadType type,
            ITimeProvider timeProvider,
            IMeterFactory meterFactory,
            ITargetBlock<IList<SerializedBatch>> targetBlock = null,
            ILogger logger = null,
            CancellationToken cancellationToken = default,
            TimeSpan? retryDelay = null)
        {
            Contract.Requires(options != null);

            this.options = options;
            this.cancellationToken = cancellationToken;
            this.timeProvider = timeProvider;

            this.appIdLogger = logger ?? NullLogger.Instance;
            this.storageType = type;
            this.Output = targetBlock;

            string uploadMetricNamePrefix = this.storageType == StorageUploadType.Mirror
                ? "OnlineTrainer.JoinUpload.Mirror"
                : "OnlineTrainer.JoinUpload";

            _metrics = new StorageUploadMetrics(options.AppId, uploadMetricNamePrefix, meterFactory);

            _retryDelay = retryDelay ?? _retryDelay;

            if (container == null)
            {
                this.Input = DataflowBlock.NullTarget<IList<SerializedBatch>>();
                this.Completion = Task.CompletedTask;
                return;
            }

            this.container = container;
            this.LearnableEventsUploadLocationInfo = new StorageUploadLocationInfo()
            {
                SubPath = AzureBlobConstants.CookedLogsDirectoryPrefix
            };
            this.storageCheckpointBlob = storageCheckpointBlob;

            // Don't pass cancellation token to the block because we handle cancellation in UploadAsync and flush necessary calls.
            var uploadBlock = new ActionBlock<IList<SerializedBatch>>(
                this.UploadAsync,
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity =
                        options.BlockBufferCapacity, // careful here, as the batchblock produces 100MB blocks
                    MaxDegreeOfParallelism = 1
                });
            this.appIdLogger?.LogInformation("Storage Upload block uses {capacity} buffer capacity",
                options.BlockBufferCapacity);
            this.Input = uploadBlock;

            var tasks = new List<Task> { uploadBlock.Completion };

            if (this.Output != null)
            {
                tasks.Add(this.Output.Completion);
            }

            _ =this.Input.Completion.ContinueWith(_ => this.Output?.Complete(), TaskScheduler.Default);

            this.Completion = Task.WhenAll(tasks);

            _ = this.Completion.TraceAsync(appIdLogger, $"StorageUploadBlock for {storageType}", "CommonTrainer");
        }

        /// <summary>
        /// Updates the storage information based on the specified resume strategy and checkpoint.
        /// </summary>
        /// <param name="resumeStrategy">The strategy to use for resuming the upload.</param>
        /// <param name="checkpoint">The checkpoint containing the current block position and file format.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This method updates the blob reference, parses the index, hour, and day from the blob name, and retrieves the block count and blob size.
        /// It also logs the information about the blob where the upload will resume.
        /// </remarks>
        private async Task UpdateStorageInfoAsync(ResumeStrategy resumeStrategy, StorageCheckpoint checkpoint)
        {
            var locInfo = LearnableEventsUploadLocationInfo;
            locInfo.Blob = this.container.GetBlockBlobClient(checkpoint.BlockPosition.BlobName);
            PathHelper.ParseIndexAndDate(locInfo.Blob.Name, out var index, out var hour, out var day);
            locInfo.BlobDay = day;
            locInfo.BlobHour = hour;
            locInfo.BlobIndex = index;
            locInfo.FileFormat = checkpoint.BlockPosition.FileFormat;
            (locInfo.BlockCount, locInfo.BlobSize) = await GetBlockCountAndBlobSizeAsync(resumeStrategy, checkpoint, locInfo.Blob);
            var properties = new Dictionary<string, string>()
            {
                { "Blob Name", locInfo.Blob?.Name },
                { "Block Name", locInfo.BlockCount.ToString("x4") }
            };
            this.appIdLogger?.LogInformation(
                "{storageType} storage upload will resume upload at blob {Name} {Properties}", storageType,
                locInfo.Blob.Name,
                properties);
        }

        /// <summary>
        /// Sets the resume upload position from the last storage checkpoint.
        /// </summary>
        /// <param name="resumeStrategy">The strategy to use for resuming the upload. Default is <see cref="ResumeStrategy.SynchronizeWithStorage"/>.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a <see cref="StorageCheckpoint"/> object representing the last storage checkpoint if found; otherwise, <c>null</c>.
        /// </returns>
        /// <remarks>
        /// This method retrieves the last storage checkpoint and updates the storage information accordingly. If no checkpoint is found, it logs an information message and proceeds without a checkpoint.
        /// </remarks>
        public async Task<StorageCheckpoint> SetResumeUploadPositionFromCheckpointAsync(ResumeStrategy resumeStrategy = ResumeStrategy.SynchronizeWithStorage)
        {
            StorageCheckpoint lastCheckpoint =
                await StorageCheckpointHelper.GetLastStorageCheckpointAsync(this.storageCheckpointBlob,
                    this.appIdLogger);
            if (lastCheckpoint == null)
            {
                this.appIdLogger?.LogInformation(
                    "Failed to retrieve storage checkpoint. Proceeding without checkpoint.");
                return null;
            }

            // We could have not uploaded any learnable events yet, so there is no checkpoint for learnable events upload.
            bool hasLearnableEventsCheckpoint = String.IsNullOrWhiteSpace(lastCheckpoint.BlockPosition?.BlobName) == false;
            if (hasLearnableEventsCheckpoint)
            {
                await UpdateStorageInfoAsync(resumeStrategy, lastCheckpoint);
            }

            return lastCheckpoint;
        }

        /// <summary>
        /// Resumes the upload process by setting the resume upload position from the checkpoint.
        /// </summary>
        /// <param name="resumeStrategy">The strategy to use for resuming the upload. Default is <see cref="ResumeStrategy.SynchronizeWithStorage"/>.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains an <see cref="EventHubCheckpoint"/> object if a valid checkpoint is found; otherwise, <c>null</c>.
        /// </returns>
        /// <remarks>
        /// This method updates the event hub checkpoint if a valid checkpoint is found. If no checkpoint is found, it returns <c>null</c> indicating that the checkpoint blob was not found, which could be due to a new loop or new configuration folder.
        /// </remarks>
        public async Task<EventHubCheckpoint?> ResumeAsync(ResumeStrategy resumeStrategy = ResumeStrategy.SynchronizeWithStorage)
        {
            var checkpoint = await SetResumeUploadPositionFromCheckpointAsync(resumeStrategy);
            if (checkpoint != null)
            {
                _eventHubCheckpoint = checkpoint.EventPosition;
                return _eventHubCheckpoint;
            }
            else
            {
                // Checkpoint blob not found. Could be new loop or new config folder.
                return null;
            }
        }

        /// <summary>
        /// Extracts and organizes event data from a list of serialized batches into an <see cref="EventsSubsetData"/> object.
        /// </summary>
        /// <param name="events">The list of serialized batches containing event data.</param>
        /// <returns>
        /// An <see cref="EventsSubsetData"/> object containing the organized event data.
        /// </returns>
        private EventsSubsetData GetEventsData(IList<SerializedBatch> events)
        {
            // We try to minimize the number of iterations of events we have to do since events can be large.
            // We pull all the data/metrics from the list of events we need and split the events into skipped and learnable
            // subsets in one iteration.
            var eventsData = new EventsSubsetData(true, "learnable-events");
            foreach (var batch in events)
            {
                eventsData.Add(batch);
            }
            return eventsData;
        }

        //XXX maybe move this to the options object itself?
        private JoinedLogFormat CurrentJoinedLogFormat => JoinedLogFormat.Binary;

        /// <summary>
        /// Determines if a new index is needed based on the event data, current storage upload location information, and hourly index.
        /// </summary>
        /// <param name="events">The event data containing the batch size information.</param>
        /// <param name="locInfo">The storage upload location information.</param>
        /// <param name="isNewHour">Indicates whether a new hour has started.</param>
        /// <returns>
        /// <c>true</c> if a new index is needed; otherwise, <c>false</c>.
        /// </returns>
        private bool NeedNewIndex(EventsSubsetData events, StorageUploadLocationInfo locInfo, bool isNewHour)
        {
            // avoid using this.blob.FetchAttributes() to check blob size which takes about 0.5s.
            var newBlobSize = locInfo.BlobSize + events.BatchSizeNoHeader;
            var newBlockCount = locInfo.BlockCount + 1;
            return (newBlobSize >= this.options.AzureStorageMaxBlobSizeLimitsInByte) ||
                   (newBlockCount >= this.options.MaximumBlocksNumber) ||
                   isNewHour;
        }

        /// <summary>
        /// Updates the hourly index of the storage upload location information if needed.
        /// Logs information if a new hour is detected.
        /// </summary>
        /// <param name="batchBlobTime">The timestamp of the batch blob.</param>
        /// <param name="events">The event data containing the batch time information.</param>
        /// <param name="locInfo">The storage upload location information.</param>
        /// <returns>
        /// <c>true</c> if a new hour is detected and the index is updated; otherwise, <c>false</c>.
        /// </returns>
        private bool UpdateHourlyIndexIfNeeded(DateTime batchBlobTime, EventsSubsetData events, StorageUploadLocationInfo locInfo)
        {
            if (!this.options.HourlyIndexIncrement)
            {
                return false;
            }
            bool isNewHour = batchBlobTime.Hour > locInfo.BlobHour;
            if (isNewHour)
            {
                this.appIdLogger?.LogInformation(
                    "is New hour as locInfo.BlobHour={BlobHour} and batchBlobTime.Hour={Hour}",
                    locInfo.BlobHour, batchBlobTime.Hour
                );
            }
            locInfo.BlobHour = batchBlobTime.Hour;
            return isNewHour;
        }

        /// <summary>
        /// Generates a new blob reference for the storage upload location information.
        /// Updates the blob day, index, size, block count, and file format as needed.
        /// </summary>
        /// <param name="isNewDay">Indicates whether a new day has started.</param>
        /// <param name="batchBlobTime">The timestamp of the batch blob.</param>
        /// <param name="locInfo">The storage upload location information.</param>
        private void GenerateNewBlobReference(bool isNewDay, DateTime batchBlobTime, StorageUploadLocationInfo locInfo)
        {
            if (isNewDay)
            {
                locInfo.BlobDay = batchBlobTime.Date;
                locInfo.BlobIndex = 0;
            }
            else //new index or file format
            {
                locInfo.BlobIndex++;
            }

            locInfo.BlobSize = 0;
            locInfo.BlockCount = 0;
            locInfo.FileFormat = CurrentJoinedLogFormat;

            int? blobHour = this.options.HourlyIndexIncrement ? locInfo.BlobHour : (int?)null;
            locInfo.Blob = this.container.GetBlockBlobClient(PathHelper.BuildBlobName(
                this.options.LastConfigurationEditDate, locInfo.BlobDay, locInfo.BlobIndex, locInfo.SubPath,
                locInfo.FileFormat, blobHour));
            this.appIdLogger?.LogInformation("blob reference updated to: {Name} in {storageType}",
                locInfo.Blob.Name, storageType);
        }

        /// <summary>
        /// Determines if a new blob reference is needed based on the event data and the current storage upload location information.
        /// Generates a new blob reference if necessary.
        /// </summary>
        /// <param name="locInfo">The storage upload location information.</param>
        /// <param name="events">The event data containing the batch time information.</param>
        /// <returns>
        /// <c>true</c> if a new blob reference was generated; otherwise, <c>false</c>.
        /// </returns>
        private bool GenerateNewBlobReferenceIfNeeded(StorageUploadLocationInfo locInfo, EventsSubsetData events)
        {
            // when blobDay is default value, this means we are creating the first blob of the container.
            // the blob created should either be of the date of the first joined event, or the last config date
            var currentBlobDate = locInfo.BlobDay == default ? this.options.LastConfigurationEditDate : locInfo.BlobDay;
            DateTime batchBlobTime = events.GetBatchTimeOrDefault(currentBlobDate);

            bool isNewDay = locInfo.BlobDay < batchBlobTime.Date;
            bool isNewHour = UpdateHourlyIndexIfNeeded(batchBlobTime, events, locInfo);
            bool isNewIndex = NeedNewIndex(events, locInfo, isNewHour);
            bool isNewFileFormat = locInfo.FileFormat != CurrentJoinedLogFormat;
            bool generateNewBlobReference = isNewDay || isNewIndex || isNewFileFormat;
            if (generateNewBlobReference)
            {
                GenerateNewBlobReference(isNewDay, batchBlobTime, locInfo);
            }
            return generateNewBlobReference;
        }

        /// <summary>
        /// Processes a list of serialized event batches by uploading them to Blob Storage and updating the checkpoint.
        /// If an output block is defined, the events are sent to the output block after processing.
        /// </summary>
        /// <param name="events">The list of serialized event batches to be processed.</param>
        /// <returns>A task that represents the asynchronous upload operation.</returns>
        private async Task UploadAsync(IList<SerializedBatch> events)
        {
            try
            {
                var eventsData = GetEventsData(events);
                await UploadEventsAsync(eventsData, this.LearnableEventsUploadLocationInfo);
                await UpdateCheckpointAsync(events);

                if (this.Output != null)
                {
                    await this.Output.SendAsync(events);
                }
            }
            catch (Exception e)
            {
                this.appIdLogger?.Log(
                    this.storageType == StorageUploadType.Mirror ? LogLevel.Information : LogLevel.Error,
                    e, "{Name} {storageType}",
                    this.LearnableEventsUploadLocationInfo.Blob?.Name,
                    this.storageType);
            }
        }

        /// <summary>
        /// Uploads event data to a blob in Blob Storage and commits the blocks.
        /// If a new blob is needed, it generates a new blob reference.
        /// </summary>
        /// <param name="eventsData">The event data containing the batch size information.</param>
        /// <param name="locInfo">The storage upload location information.</param>
        /// <param name="blockStream">
        /// Optional. The stream containing the concatenated event data blocks. If null, a new stream is created.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous upload operation. The task result contains the stream of concatenated event data blocks.
        /// </returns>
        private async Task<ConcatenatedByteStreams> InternalUploadEventsAsync(EventsSubsetData eventsData,
            StorageUploadLocationInfo locInfo, ConcatenatedByteStreams blockStream = null)
        {
            //sanity check on batch size
            bool newBlobWasGenerated = GenerateNewBlobReferenceIfNeeded(locInfo, eventsData);

            // blockStream is non-null when retrying, in that case we reuse it
            blockStream ??= eventsData.CreateBlockStream(
                newBlob: newBlobWasGenerated,
                checkpointInfo: new CheckpointInfo()
                {
                    DefaultReward = options.DefaultReward.Value,
                    FbRewardType = options.RewardFunction.ToFlatbuffer()
                }
            );

            CheckBatchSize(eventsData, blockStream);

            await UploadEventsToBlobAsync(locInfo, blockStream, this.cancellationToken);
            locInfo.BlockCount++;
            locInfo.BlobSize += blockStream.Length;
            return blockStream;
        }

        /// <summary>
        /// Upload events to storage and retry if hit storage exception.
        /// This is to ensure data won't be discarded.
        /// It puts back-pressure on event hub receiver to halt receiving.
        /// </summary>
        /// <param name="eventsData"></param>
        /// <param name="locInfo">The <see cref="StorageUploadLocationInfo"/> for where the events should be uploaded.</param>
        /// <param name="propagateException">
        /// Optional. Indicates if exceptions should be allowed to propagate out of this method. Default: true.
        /// Set to false for non-critical upload tasks.
        /// </param>
        private async Task<long> UploadEventsAsync(EventsSubsetData eventsData, StorageUploadLocationInfo locInfo,
            bool propagateException = true)
        {
            // If there are no events there is nothing to do.
            if (eventsData.HasNoEvents)
            {
                return 0;
            }

            int retryCount = 0;
            ConcatenatedByteStreams blockStream = null;

            while (ShouldTryUpload(retryCount))
            {
                try
                {
                    blockStream = await InternalUploadEventsAsync(eventsData, locInfo, blockStream);
                    return blockStream.Length;
                }
                catch (OperationCanceledException)
                {
                    this.appIdLogger?.LogInformation($"Operation was canceled.");
                    return 0;
                }
                catch (StorageException se)
                {
                    if (se.IsExceptionOf<OperationCanceledException>() && this.cancellationToken.IsCancellationRequested)
                    {
                        this.appIdLogger?.LogInformation($"Storage operation was canceled.");
                        return 0;
                    }

                    await Task.Delay(_retryDelay); // wait and retry.
                    // We need to reset the stream position to retry
                    blockStream.Seek(0, SeekOrigin.Begin);
                    retryCount++;
                }
                catch (Exception ex)
                {
                    if (propagateException)
                    {
                        throw;
                    }
                    else
                    {
                        this.appIdLogger?.LogError(ex, "{name} {storageType}", locInfo.Blob?.Name, this.storageType);
                        return 0;
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// Validates the batch size of the event data against the expected size and the maximum allowed block size.
        /// </summary>
        /// <param name="eventsData">The event data containing the batch size information.</param>
        /// <param name="blockStream">The stream containing the concatenated event data blocks.</param>
        private void CheckBatchSize(EventsSubsetData eventsData, ConcatenatedByteStreams blockStream)
        {
            // https://docs.microsoft.com/en-us/azure/architecture/best-practices/retry-service-specific#azure-storage-retry-guidelines
            // Count * SegmentCount must be equal to examples.SelectMany(e => e.Segments).Count()
            // this is primarily here so we don't have to either allocate the segments twice or keep the complete collection in memory
            // examples can be 100k elements so it can be quite expensive
            var actualBatchSize = blockStream.Length;
            var countedBatchSize = eventsData.BatchSizeWithHeader;
            if (actualBatchSize != countedBatchSize ||
                actualBatchSize > this.options.AzureStorageMaxBlockSizeLimitsInByte)
            {
                this.appIdLogger?.LogInformation(
                    "batch size error: actual_size = {actualBatchSize}, counted_size = {countedBatchSize}, max_size = {AzureStorageMaxBlockSizeLimitsInByte}",
                    actualBatchSize, countedBatchSize, this.options.AzureStorageMaxBlockSizeLimitsInByte);
            }
        }

        /// <summary>
        /// Determines whether the upload operation should be retried based on the current retry count and cancellation token.
        /// </summary>
        /// <param name="currentRetryCount">The current number of retry attempts.</param>
        /// <returns>
        /// <c>true</c> if the upload operation should be retried; otherwise, <c>false</c>.
        /// The upload operation will be retried if the cancellation token has not been requested and the retry count is less than or equal to the maximum allowed retries.
        /// </returns>
        private bool ShouldTryUpload(int currentRetryCount)
        {
            // If storage is temporarily unavailable, we will retry but no more than maxStorageUploadRetries
            // Unless we are in disaster recovery mode: then retry indefinitely to put back pressure on eventhub
            return !this.cancellationToken.IsCancellationRequested && currentRetryCount <= maxStorageUploadRetries;
        }

        /// <summary>
        /// Uploads event data to a blob in Blob Storage and commits the blocks.
        /// </summary>
        /// <param name="locInfo">The storage upload location information.</param>
        /// <param name="blockStream">The stream containing the concatenated event data blocks.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous upload operation.</returns>
        private async Task UploadEventsToBlobAsync(StorageUploadLocationInfo locInfo,
            ConcatenatedByteStreams blockStream, CancellationToken cancellationToken)
        {
            // x4 ... the number of blocks are limited to 50k
            // making sure this call doesn't allocate the complete stream again by supporting CanSeek on blockStream and disabling transactionalMD5
            // https://github.com/Azure/azure-storage-net/blob/7be6a470cf07c0db3a97f214d139bfe9cf5f623b/Lib/ClassLibraryCommon/Blob/CloudBlockBlob.cs#L1649
            //
            // update the block count after a successfull commit
            int newBlockCount = locInfo.BlockCount + 1;
            await locInfo.Blob.WriteBlockAsync(newBlockCount.ToString("x4"), blockStream, cancellationToken: cancellationToken);

            // start blocks at 0001 (0000 means no blocks)
            var ids = Enumerable.Range(1, newBlockCount).Select(id => id.ToString("x4"));
            await CommitBlocksAsync(locInfo.Blob, ids.ToList());
        }

        private async Task CommitBlocksAsync(IBlockStore blob, IList<string> ids)
        {
            //in case of a StorageException we want to log the id list along with the current BlockCount
            try
            {
                await blob.CommitBlocksAsync(ids, cancellationToken: this.cancellationToken);
            }
            catch (StorageException)
            {
                this.appIdLogger?.LogError(
                    "PutBlockListAsync failed, ids: {Ids}, BlockCount: {count}",
                    String.Join(",", ids), ids.Count);
                throw;
            }
        }

        /// <summary>
        /// Updates the checkpoint with the latest event data and uploads it to Blob Storage.
        /// </summary>
        /// <param name="events">The list of serialized event batches.</param>
        /// <returns>A task that represents the asynchronous update operation.</returns>
        private async Task UpdateCheckpointAsync(IList<SerializedBatch> events)
        {
            try
            {
                _eventHubCheckpoint.Update(events, appIdLogger);
                // We don't pass cancellation token here because we shouldn't cancel uploading a checkpoint.
                await UploadStorageCheckpointAsync(_eventHubCheckpoint);
            }
            catch (StorageException se)
            {
                // No retry on exception for checkpoint update since it will retry for next batch of events.
                this.appIdLogger.LogStorageException(
                    se,
                    "StorageUploadBlock.UpdateCheckpointAsync.StorageException",
                    PersonalizerInternalErrorCode.JoinerCheckpointNotFound.ToString(),
                    null,
                    this.storageType
                );
            }
        }

        /// <summary>
        /// Uploads the serialized checkpoint data to Blob Storage.
        /// </summary>
        /// <param name="eventHubCheckpoint">The checkpoint data from Event Hub.</param>
        /// <returns>A task that represents the asynchronous upload operation.</returns>
        private async Task UploadStorageCheckpointAsync(EventHubCheckpoint eventHubCheckpoint)
        {
            var learnableBlockPosition = this.LearnableEventsUploadLocationInfo.GetBlockPosition();
            var learnableBlobProperty = this.LearnableEventsUploadLocationInfo.GetBlockProperties();

            var checkpoint = new StorageCheckpoint()
            {
                EventPosition = eventHubCheckpoint,
                BlockPosition = learnableBlockPosition,
                BlobProperty = learnableBlobProperty,
            };

            // Serialize the checkpoint
            string checkpointJson = JsonConvert.SerializeObject(checkpoint);

            this.storageCheckpointBlob ??=
                    container.GetBlobClient(AzureBlobConstants.TenantStorageCheckpointBlobName);

            await this.storageCheckpointBlob.UploadAsync(BinaryData.FromString(checkpointJson), overwrite: true);

            var properties = new Dictionary<string, string>()
            {
                { "Blob Name", learnableBlockPosition.BlobName },
                { "Block Name", learnableBlockPosition.BlockName },
                // { "Event Hub Event Position", eventHubCheckpoint.ToJ() }
            };

            this.appIdLogger?.LogInformation(
                $"Storage checkpoint blob uploaded. {properties}",
                properties);
        }

        /// <summary>
        /// Attempts to synchronize the checkpoint with the storage account.
        /// </summary>
        /// <param name="blob">The block store to synchronize with.</param>
        /// <param name="checkpoint">The checkpoint containing the current block count and size.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a tuple with the block count and blob size.
        /// </returns>
        /// <remarks>
        /// This method attempts to read the blob size and latest block from the storage account. If the storage account is writable and the last block is uncommitted, it commits the blocks. If no blocks are found, it starts from zero. If an exception occurs, it logs an information message and resets the checkpoint.
        /// </remarks>
        /// <exception cref="StorageException">Thrown when there is an issue accessing the storage account.</exception>
        private async Task<(int, long)> TrySyncCheckpointWithStorageAsync(IBlockStore blob, StorageCheckpoint checkpoint)
        {
            var (blockCount, blobSize) = GetBlockCountAndSize(checkpoint);
            // attempt to read the blob size and latest block from the storage account
            try
            {
                // always try to read the blob size from the storage account if we have the read permission.
                var canWrite = await StorageUtilities.IsContainerWritableAsync(this.container, this.appIdLogger, this.cancellationToken);
                var blocks = await blob.GetBlockInfoListAsync("All", this.cancellationToken);
                // GetBlockInfoListAsync will return any uncommitted block will be at the end of the list;
                // if there is no last block or only committed blocks, the blob will be reported as not found; check anyway
                var lastBlock = blocks.LastOrDefault();
                if (lastBlock != null)
                {
                    if (canWrite && lastBlock.IsUncommitted)
                    {
                        var blockIds = blocks.Select(b => b.Name).ToList();
                        await CommitBlocksAsync(blob, blockIds);
                        // todo: what if the commit fails? -- set the checkpoint to the last committed block??
                    }
                    blockCount = blocks.Count();
                    blobSize = (await blob.GetPropertiesAsync()).ContentLength;
                }
                else
                {
                    // if there are no blocks start from 0
                    (blockCount, blobSize) = (0, 0);
                }
            }
            catch (StorageException)
            {
                this.appIdLogger?.LogInformation(
                    "StorageUploadBlock is unable to synchronize the checkpoint with storage; the checkpoint will be reset");
            }
            return (blockCount, blobSize);
        }

        /// <summary>
        /// Retrieves the block count and blob size based on the specified resume strategy and checkpoint.
        /// </summary>
        /// <param name="resumeStrategy">The strategy to use for resuming the upload.</param>
        /// <param name="checkpoint">The checkpoint containing the current blob properties and block position.</param>
        /// <param name="blob">The blob store to read the block information and properties from.</param>
        /// <returns>A tuple containing the block count and blob size.</returns>
        /// <remarks>
        /// If the resume strategy is <see cref="ResumeStrategy.SynchronizeWithStorage"/>, the method attempts to read the blob size and latest block from the storage account.
        /// If the resume strategy is <see cref="ResumeStrategy.ResetToZero"/>, the method resets the block count and blob size to zero.
        /// If the resume strategy is <see cref="ResumeStrategy.UseCheckpointPosition"/>, the method uses the block count and blob size from the checkpoint.
        /// </remarks>
        private async Task<(int, long)> GetBlockCountAndBlobSizeAsync(ResumeStrategy resumeStrategy, StorageCheckpoint checkpoint, IBlockStore blob)
        {
            if (resumeStrategy == ResumeStrategy.SynchronizeWithStorage)
            {
                return await TrySyncCheckpointWithStorageAsync(blob, checkpoint);
            }
            else if (resumeStrategy == ResumeStrategy.UseCheckpointPosition)
            {
                return GetBlockCountAndSize(checkpoint);
            }
            else if (resumeStrategy == ResumeStrategy.ResetToZero)
            {
                return (0, 0);
            }
            else
            {
                throw new ArgumentException("Invalid resume strategy");
            }
        }

        private static (int, long) GetBlockCountAndSize(StorageCheckpoint checkpoint)
        {
            long blobSize = checkpoint.BlobProperty == null ? 0 : checkpoint.BlobProperty.Length;
            int blockCount = checkpoint.BlobProperty == null ? 0 :
                int.Parse(checkpoint.BlockPosition.BlockName, System.Globalization.NumberStyles.HexNumber);
            return (blockCount, blobSize);
        }
    }
}