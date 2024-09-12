// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Join;
using Microsoft.DecisionService.Common.Trainer;
using Microsoft.DecisionService.Common.Trainer.Data;
using Microsoft.DecisionService.Common.Trainer.Join;
using Microsoft.DecisionService.Common.Trainer.Operations;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Microsoft.DecisionService.OnlineTrainer.Join
{
    public sealed class JoinerEventHub : IJoiner
    {
        /// <summary>
        /// Forwarding main loops for interaction/observation receivers.
        /// </summary>
        private readonly List<Task> _receiverTasks;

        /// <summary>
        /// The settings currently being used
        /// </summary>
        private readonly JoinerConfig _config;

        private readonly ITargetBlock<JoinedBatch> _targetBlock;
        private readonly ILogger _logger;
        private readonly IMeterFactory _meterFactory;
        private readonly ITimeProvider _timeProvider;
        private readonly IDataClientFactory _dataClientFactory;

        private readonly EventHubCheckpoint? _checkpoint;


        public Task Completion { get; private set; }

        /// <summary>
        /// constructor
        /// </summary>
        public JoinerEventHub(JoinerConfig config, IDataClientFactory dataClientFactory, EventHubCheckpoint? position, ITargetBlock<JoinedBatch> targetBlock, ITimeProvider timeProvider, IMeterFactory meterFactory, ILogger logger)
        {
            Contract.Requires(config != null);

            this._config = config;
            this._receiverTasks = new List<Task>();
            this._meterFactory = meterFactory;
            this._timeProvider = timeProvider;
            this._logger = logger;
            this._dataClientFactory = dataClientFactory;

            ModelCheckpoint checkpoint = new ModelCheckpoint
            {
                WarmstartStartDateTime = config.WarmstartStartDateTime,
            };

            _checkpoint = position;

            this._targetBlock = targetBlock;
            this.Completion = Task.CompletedTask;
        }

        /// <summary>
        /// Starts joining interactions and observations if it's not already started
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // don't start if we're already running
            if (this._receiverTasks.Count > 0)
                throw new InvalidOperationException("Cannot start joining, a join operation is currently in progress!");

            var interactionReceivers = await CreateAsync(
                    _config.InteractionHubName,
                    _dataClientFactory,
                    (eventHubReceiverClient, partitionId) => new JoinerReceiver(
                        "interaction",
                        eventHubReceiverClient,
                        this?._checkpoint?.PartitionCheckpoints.GetValueOrDefault(partitionId, null),
                        this._config, new JoinerReceiverMetrics(_config.AppId, _meterFactory),_logger
                        ),   _logger);

            // Calculate the observation offset.
            DateTime? observationTimeValue = null;
            if (this?._checkpoint?.PartitionCheckpoints != null)
            {
                observationTimeValue = this?._checkpoint?.PartitionCheckpoints.Values.Min(x => x.EnqueuedTimeUtc) -
                    _config.BackwardEventJoinWindowTimeSpan;
            }
            else
            {
                observationTimeValue = _config.WarmstartStartDateTime;
            }

            PartitionCheckpoint observationCheckpoint = new PartitionCheckpoint
            {
                EnqueuedTimeUtc = observationTimeValue
            };

            var observationReceivers = await CreateAsync(
                    _config.ObservationHubName,
                    _dataClientFactory,
                    (eventHubReceiverClient, _) => new JoinerReceiver(
                        "observation",
                        eventHubReceiverClient,
                        observationCheckpoint,
                        this._config, new JoinerReceiverMetrics(_config.AppId, _meterFactory),_logger
                        ), _logger);

            // validation
            if (interactionReceivers.Length != observationReceivers.Length)
            {
                var interactionPartitions = string.Join(",", interactionReceivers.Select(ir => ir.PartitionId));
                var observationPartitions = string.Join(",", observationReceivers.Select(ir => ir.PartitionId));

                throw new InvalidOperationException($"Interaction and observation partitions differ. Interactions '{interactionPartitions}' vs Observations '{observationPartitions}'");
            }

            var sortingBlockInteraction = await this.CreateSortingBlockAsync("interaction", interactionReceivers, cancellationToken);
            var sortingBlockObservation = await this.CreateSortingBlockAsync("observation", observationReceivers, cancellationToken);

            var flatMapped = new TransformManyBlock<MessageBatch, Message>(batch => batch.Messages);
            sortingBlockObservation.Output.LinkTo(flatMapped, new DataflowLinkOptions { PropagateCompletion = true });
            var loj = new LeftOuterJoinBlock(
                sortingBlockInteraction.Output,
                flatMapped,
                this._targetBlock,
                this._config,
                this._timeProvider,
                this._meterFactory,
                _logger,
                cancellationToken);

            // run sorting blocks
            this._receiverTasks.Add(loj.Completion);
            this._receiverTasks.Add(loj.Completion.TraceAsync(_logger, "LOJ", "LOJ.OnExit"));

            this.Completion = Task.WhenAny(this._receiverTasks).Unwrap();

#pragma warning disable 4014
            this.Completion.TraceAsync(_logger, "JoinerEventHub", "JoinerEventHub.OnExit");
        }

        private async Task<EventMergeSortBlock> CreateSortingBlockAsync(string name, JoinerReceiver[] receivers, CancellationToken cancellationToken)
        {
            var sortingBlock = new EventMergeSortBlock(
                name,
                this._logger,
                receivers.Length,
                this._config,
                this._timeProvider);

            // add inputs
            List<PartitionSource> sources = new List<PartitionSource>();
            foreach (var receiver in receivers)
            {
                var source = sortingBlock.Add(receiver.PartitionId);
                var recvTask = receiver.ForwardAsync(source, cancellationToken);
                sources.Add(source);
                this._receiverTasks.Add(recvTask.TraceAsync(_logger, $"Receiver Partition={receiver.PartitionId}", "JoinerEventHub.OnExit"));
            }

            // Wait for all sources to be ready.
            var waitForDataTasks = sources.Select(source => source.WaitForDataAsync(cancellationToken)).ToArray();
            await Task.WhenAll(waitForDataTasks);

            // run main loop
            var sortTask = sortingBlock.RunAsync(cancellationToken);
            this._receiverTasks.Add(sortTask.TraceAsync(_logger, $"SortBock", "SortBlock.OnExit"));
            return sortingBlock;
        }

        private static async Task<T[]> CreateAsync<T>(
            string entityPath,
            IDataClientFactory factory,
            Func<IReceiverClient, string, T> selector, ILogger logger)
        {
            return
                (await factory.GetPartitionsIdAsync(entityPath))
                .Select(partitionId =>
                   selector(factory.CreateEventHubReceiver(partitionId, entityPath, logger),
                   partitionId))
                .ToArray();
        }
    }
}
