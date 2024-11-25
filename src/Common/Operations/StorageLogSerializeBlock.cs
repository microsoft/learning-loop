// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Error;
using Microsoft.DecisionService.Common.Trainer.Billing;
using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.DecisionService.OnlineTrainer.Operations;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Trainer.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.DecisionService.Common.Storage;

namespace Microsoft.DecisionService.Common.Trainer.Operations
{
    public class StorageLogSerializeBlock : ITargetBlock<JoinedBatch>
    {
        private readonly TransformBlock<JoinedBatch, SerializedBatch> serializationBlock;
        private readonly BillingBlock billingBlock;
        private readonly ILogger appIdLogger;
        private readonly StorageBlockOptions storageBlockOptions;
        private readonly IBlobContainerClient tenantBlobContainer;
        private readonly LogMirrorSettings logMirrorSettings;
        private readonly IMeterFactory meterFactory;
        private ActionBlock<IList<SerializedBatch>> broadcastBlock;

        public StorageUploadBlock TenantStorageBlock { get; private set; }
        public StorageUploadBlock LogMirrorUploadBlock { get; private set; }
        private ITargetBlock<JoinedBatch> Input { get; }

        private readonly CancellationToken cancellationToken;
        private readonly ITimeProvider timeProvider;

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, JoinedBatch messageValue,
            ISourceBlock<JoinedBatch> source, bool consumeToAccept)
        {
            return this.Input.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        public void Complete()
        {
            this.Input.Complete();
        }

        public void Fault(Exception exception)
        {
            this.Input.Fault(exception);
        }

        public Task Completion { get; private set; }

        public StorageLogSerializeBlock(
            StorageBlockOptions options,
            IBlobContainerClient tenantBlobContainer,
            LogMirrorSettings logMirrorSettings,
            BillingBlock billingBlock,
            IMeterFactory meterFactory,
            ITimeProvider timeProvider = null,
            ILogger logger = null,
            CancellationToken cancellationToken = default(CancellationToken)
        )
        {
            this.appIdLogger = logger ?? NullLogger.Instance;
            this.billingBlock = billingBlock;
            this.timeProvider = timeProvider;
            this.cancellationToken = cancellationToken;
            this.meterFactory = meterFactory;

            this.serializationBlock = CreateSerializationBlock(cancellationToken);
            this.Input = this.serializationBlock;

            this.storageBlockOptions = options;
            this.tenantBlobContainer = tenantBlobContainer;
            this.logMirrorSettings = logMirrorSettings;
        }

        public async Task SetupBlocksAsync()
        {
            this.LogMirrorUploadBlock = await CreateLogMirrorUploadBlockAsync(this.logMirrorSettings,
                this.tenantBlobContainer, meterFactory, this.storageBlockOptions);

            var tenantStorageCheckpointBlob = tenantBlobContainer.GetBlobClient(
                PathHelper.BuildCheckpointName(this.storageBlockOptions.LastConfigurationEditDate,
                    AzureBlobConstants.TenantStorageCheckpointBlobName));
            this.TenantStorageBlock = new StorageUploadBlock(this.storageBlockOptions, tenantBlobContainer,
                tenantStorageCheckpointBlob, StorageUploadType.Tenant, timeProvider, meterFactory,
                LogMirrorUploadBlock?.Input, null, cancellationToken);

            var broadcastTargets = new List<ITargetBlock<IList<SerializedBatch>>>();
            broadcastTargets.Add(TenantStorageBlock.Input);

            AddBillingTarget(broadcastTargets);

            this.broadcastBlock = CreateGuaranteedBroadcastBlock(broadcastTargets, cancellationToken);

            var batchBlock = CreateBatchBlockForTarget(this.storageBlockOptions, broadcastBlock, timeProvider,
                cancellationToken);

            // link blocks, filter events not for upload
            this.serializationBlock.LinkTo(batchBlock.Input, new DataflowLinkOptions { PropagateCompletion = true },
                evt => (evt != null));
            // make sure we consume all events!
            this.serializationBlock.LinkTo(DataflowBlock.NullTarget<SerializedBatch>(),
                new DataflowLinkOptions { PropagateCompletion = true },
                evt => (evt == null));

            this.Completion = this.broadcastBlock.Completion.TraceAsync(this.appIdLogger, "StorageLogSerializeBlock",
                "StorageLogSerializeBlock.OnExit");
        }

        public async Task<EventHubCheckpoint> ResumeByTenantStorageCheckpointAsync()
        {
            var eventPosition = await this.TenantStorageBlock.ResumeAsync();
            if (this.LogMirrorUploadBlock != null)
            {
                await this.LogMirrorUploadBlock.SetResumeUploadPositionFromCheckpointAsync();
            }

            return eventPosition;
        }

        private static ActionBlock<T> CreateGuaranteedBroadcastBlock<T>(IEnumerable<ITargetBlock<T>> targets,
            CancellationToken token)
        {
            return new ActionBlock<T>(
                async item => { await Task.WhenAll(targets.Select(t => t.SendAsync(item)).ToArray()); },
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = 1, // This block holds a whole event batch that can be 100's of Mb of memory.
                    CancellationToken = token,
                    MaxDegreeOfParallelism =
                        1 // Do Not Change this value as it leads to out of order events in downstream blocks e.g StorageUploadBlock 
                });
        }

        private TransformBlock<JoinedBatch, SerializedBatch> CreateSerializationBlock(
            CancellationToken cancellationToken)
        {
            return new TransformBlock<JoinedBatch, SerializedBatch>(
                batch => batch.Serialize(),
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = 32,
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = 4
                });
        }

        private async Task<StorageUploadBlock> CreateLogMirrorUploadBlockAsync(LogMirrorSettings logMirrorSettings,
            IBlobContainerClient mirrorCheckpointContainer, IMeterFactory meterFactory, StorageBlockOptions options)
        {
            var logMirrorSettingsEnabled = logMirrorSettings != null ? logMirrorSettings.Enabled : false;
            this.appIdLogger?.LogInformation("Log Mirror Settings - present: {isPresent}, " +
                                             "enabled : {logMirrorSettingsEnabled}", logMirrorSettings != null,
                logMirrorSettingsEnabled);

            if (logMirrorSettings != null)
            {
                if (!logMirrorSettings.Enabled)
                {
                    return null;
                }

                var clientCloudContainer = mirrorCheckpointContainer.Factory.CreateBlobContainerClient(new Uri(logMirrorSettings.SasUri));
                CancellationToken cancellationToken = CancellationToken.None;
                if (!await StorageUtilities.IsContainerWritableAsync(clientCloudContainer,
                        appIdLogger, cancellationToken))
                {
                    this.appIdLogger?.LogError(
                        new PersonalizerException("Log mirroring SAS Uri is unwritable or invalid."),
                        "{EventKey} {ErrorCode}", "StorageSerializeBlock"
                        , PersonalizerInternalErrorCode.JoinerStorageUploadFailure.ToString());
                    return null;
                }

                var mirrorCheckpointBlob = mirrorCheckpointContainer.GetBlobClient(
                    PathHelper.BuildCheckpointName(options.LastConfigurationEditDate,
                        AzureBlobConstants.MirrorStorageCheckpointBlobName));
                this.appIdLogger?.LogInformation("Log Mirror Settings container is valid");
                return new StorageUploadBlock(options, clientCloudContainer, mirrorCheckpointBlob,
                    StorageUploadType.Mirror, timeProvider, meterFactory, cancellationToken: cancellationToken);
            }

            return null;
        }

        private void AddBillingTarget(List<ITargetBlock<IList<SerializedBatch>>> broadcastTargets)
        {
            if (this.billingBlock != null)
            {
                broadcastTargets.Add(this.billingBlock.Input);
            }
        }

        /// <summary>
        /// Create a batch block to combine single inputs to a list then feed to target
        /// </summary>
        private BatchBlockEx<SerializedBatch> CreateBatchBlockForTarget(StorageBlockOptions options,
            ActionBlock<IList<SerializedBatch>> target, ITimeProvider timeProvider,
            CancellationToken cancellationToken)
        {
            var previousTime = DateTime.MinValue;
            int capacityMinusHeader = options.AzureStorageMaxBlockSizeLimitsInByte;
            //We want to make sure we can always inject header
            capacityMinusHeader -= ApplicationConstants.BinaryLogMaxBatchHeaderSize;

            var batchBlock = new BatchBlockEx<SerializedBatch>(
                new BatchBlockExOptions<SerializedBatch>
                {
                    MaximumFlushLatency = options.MaximumFlushLatency,
                    BoundedCapacity = capacityMinusHeader,
                    MeasureItem = ex => ex.payload.Count,
                    CancellationToken = cancellationToken,
                    TimeProvider = timeProvider,
                    StartNewPredicate = item =>
                    {
                        var itemTime = item.EnqueuedTimeUtc;

                        if (previousTime > itemTime)
                            return false;

                        // split if the examples are on different days, let's split the batch
                        var result = previousTime.Day != itemTime.Day ||
                                     previousTime.Month != itemTime.Month ||
                                     previousTime.Year != itemTime.Year;

                        previousTime = itemTime;

                        return result;
                    },
                },
                target);

            return batchBlock;
        }
    }
}