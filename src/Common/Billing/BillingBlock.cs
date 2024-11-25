// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Billing;
using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Instrumentation;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Microsoft.DecisionService.Common.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Microsoft.DecisionService.Common.Trainer.Billing
{
    public class BillingBlock
    {
        public ITargetBlock<IList<SerializedBatch>> Input { get; }

        public Task Completion { get; }
        private readonly IBillingClient billingClient;
        private readonly IBlobContainerClient appContainer;
        private readonly BillingBlockOptions options;
        private IBlobClient billingCheckpointBlob;
        private Dictionary<string, long> billingStatePartitionMap;
        private long billableEventCount;
        private readonly Counter<long> billedEventsMetric;
        private readonly ILogger logger;
        private readonly KeyValuePair<string, object?> appIdProperty;
        private readonly CancellationToken _cancellationToken;

        public static async Task<BillingBlock> CreateAsync(IBillingClient billingClient, IBlobContainerClient container, BillingBlockOptions options, IMeterFactory meterFactory, ILogger logger, CancellationToken cancellationToken)
        {
            var billingBlock = new BillingBlock(billingClient, container, options, meterFactory, logger, cancellationToken);

            await billingBlock.TryResumeAsync(cancellationToken);
            return billingBlock;
        }

        private BillingBlock(IBillingClient billingClient, IBlobContainerClient appContainer, BillingBlockOptions options,IMeterFactory meterFactory, ILogger logger, CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            billableEventCount = 0;
            this.billingClient = billingClient;
            this.appContainer = appContainer;
            this.options = options;
            this.billingStatePartitionMap = new Dictionary<string, long>();
            
            var meter = meterFactory.Create("Microsoft.DecisionService.Common.Trainer.Billing");
            appIdProperty = new KeyValuePair<string, object>(MetricsUtil.AppIdKey, options.AppId);
            billedEventsMetric = meter.CreateCounter<long>("BilledEvents");

            var processEventsForBillingAsync = new ActionBlock<IList<SerializedBatch>>(
                this.ProcessEventsForBillingAsync,
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = options.BlockBufferCapacity,
                    MaxDegreeOfParallelism = 1,
                    CancellationToken = cancellationToken
                }
            );
            this.logger = logger;

            this.logger?.LogInformation($"Billing block uses {options.BlockBufferCapacity} buffer capacity");
            this.Input = processEventsForBillingAsync;

            this.Completion = processEventsForBillingAsync.Completion;
        }

        public async Task TryResumeAsync(CancellationToken cancellationToken)
        {
            //get the billing checkpoint
            this.billingCheckpointBlob = this.appContainer.GetBlobClient(AzureBlobConstants.BillingCheckpointBlobName);
            if (await this.billingCheckpointBlob.ExistsAsync())
            {
                string checkpointJson = (await this.billingCheckpointBlob.DownloadAsync(cancellationToken)).ToString();
                this.billingStatePartitionMap = JsonConvert.DeserializeObject<Dictionary<string, long>>(checkpointJson);
            }
        }

        private async Task ProcessEventsForBillingAsync(IList<SerializedBatch> events)
        {
            foreach (var interaction in events)
            {
                string partitionId = interaction.PartitionId ?? "-1";

                if (!this.billingStatePartitionMap.TryGetValue(partitionId, out var billingPartitionSeqNum))
                {
                    billingPartitionSeqNum = -1;
                    this.billingStatePartitionMap.Add(partitionId, billingPartitionSeqNum);
                }

                if (IsBillableEvent(interaction, partitionId, billingPartitionSeqNum))
                {
                    billableEventCount += interaction.SourceMessageEventCount;
                    this.billingStatePartitionMap[partitionId] = interaction.SequenceNumber;
                }
            }

            await UpdateCheckpointBlobAndReportUsageAsync(_cancellationToken);
        }

        private bool IsBillableEvent(SerializedBatch interaction, string partitionId, long latestBillingCheckpointState)
        {
            if (interaction.PartitionId != null && interaction.PartitionId == partitionId)
            {
                return interaction.SequenceNumber > latestBillingCheckpointState;
            }
            else
            {
                // This should not happen as we store the partition id and its corresponding sequence number in dictionary
                return false;
            }
        }

        private async Task UpdateCheckpointBlobAndReportUsageAsync(CancellationToken cancellationToken)
        {
            if (billableEventCount <= 0)
            {
                return;
            }

            try
            {
                string serializedMap = JsonConvert.SerializeObject(this.billingStatePartitionMap);
                await this.billingCheckpointBlob.UploadAsync(BinaryData.FromString(serializedMap), overwrite: true,
                    cancellationToken);
            }
            catch (StorageException e)
            {
                this.logger?.LogInformation(new Exception($"Failed to upload billing checkpoint data. Billing count: {billableEventCount}", e), "");
                return;
            }

            this.billingClient.ReportUsage(billableEventCount);
            billedEventsMetric.Add(billableEventCount, appIdProperty);
            billableEventCount = 0; // Reset current billable event count only when billing checkpoing update is done. Otherwise, we'll keep the count and retry later.
        }
    }
}
