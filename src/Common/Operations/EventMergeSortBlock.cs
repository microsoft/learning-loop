// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Trainer.Data;
using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Microsoft.DecisionService.Common.Trainer.Operations
{
    public sealed class EventMergeSortBlock
    {
        private readonly string _eventTypeName;
        private readonly ILogger appIdLogger;
        private readonly JoinerConfig _joinerConfig;
        private readonly BufferBlock<MessageBatch> outputBlock;
        private readonly RetryCheck retryCheck;

        private readonly List<PartitionSource> activePartitions;
        private readonly List<PartitionSource> inactivePartitions;

        private DateTime lastEventEnqueuedTimeUtc;

        // private string lastEventId;
        private object priorityQueueLock = new object();

        public EventMergeSortBlock(
            string name,
            ILogger appIdLogger,
            int numberOfPartitions,
            JoinerConfig config,
            ITimeProvider timeProvider
        )
        {
            this._eventTypeName = name;
            this.appIdLogger = appIdLogger;
            this._joinerConfig = config;
            this.outputBlock = new BufferBlock<MessageBatch>(new DataflowBlockOptions
            {
                BoundedCapacity = config.EventMergeSortBlockBufferSize
            });
            this.appIdLogger?.LogInformation(
                $"EventMergeSortBlock uses {config.EventMergeSortBlockBufferSize} buffer capacity");
            this.retryCheck = new RetryCheck(timeProvider);
            this.activePartitions = new List<PartitionSource>();
            this.inactivePartitions = new List<PartitionSource>();
        }

        public PartitionSource Add(string partitionId)
        {
            var evt = new PartitionSource(partitionId, this._joinerConfig, appIdLogger);
            this.activePartitions.Add(evt);
            return evt;
        }

        public IReceivableSourceBlock<MessageBatch> Output => this.outputBlock;

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // If there are inactive partitions and it is time to retry, we will check if they are active again.
                    if (this.inactivePartitions.Count > 0 && this.retryCheck.ShouldRetry())
                    {
                        var tasks = new List<Task>();
                        foreach (var element in this.inactivePartitions.ToList())
                        {
                            if (element == null)
                            {
                                // Moving this.inactivePartitions.Remove(element); into lock protection.
                                // Still keeps this for safety.
                                return;
                            }

                            tasks.Add(Task.Run(async () =>
                            {
                                // when partitions become active from inactive,
                                // add it back to active queue
                                // NOTE: this is going to cause a sudden influx
                                // of all events that are from the last read
                                // point in that partition. This is most likely
                                // going to cause out of order events to flow
                                // through the system.
                                if (await element.WaitForDataAsync(cancellationToken))
                                {
                                    lock (this.priorityQueueLock)
                                    {
                                        appIdLogger?.LogInformation("{kind} {partition} back online", _eventTypeName, element.PartitionId);
                                        this.inactivePartitions.Remove(element);
                                        this.activePartitions.Add(element);
                                    }
                                }
                            }, cancellationToken));
                        }

                        ;
                        await Task.WhenAll(tasks);

                        if (this.inactivePartitions.Count == 0)
                        {
                            this.retryCheck.Reset();
                        }
                        else
                        {
                            this.retryCheck.TryIncrease();
                        }
                    }

                    //if there is no active partition, we will go checking inactive partitions regardless.
                    // First we go over each source and if there is no item ready we try and wait for it.
                    var waitTasks = new List<Task>();
                    lock (this.priorityQueueLock)
                    {
                        if (this.activePartitions.Count == 0)
                        {
                            this.retryCheck.Reset();
                            continue;
                        }

                        foreach (var source in this.activePartitions)
                        {
                            if (source.Peek() == null)
                            {
                                waitTasks.Add(Task.Run(async () =>
                                {
                                    await source.WaitForDataAsync(cancellationToken);
                                }));
                            }
                        }
                    }

                    await Task.WhenAll(waitTasks);

                    // Then we find the min source, and if any partition still has no event then we put it into the inactive list.
                    MessageBatch? minBatch = null;
                    PartitionSource? partitionSourceForMinBatch = null;
                    lock (this.priorityQueueLock)
                    {
                        var toModify = new List<PartitionSource>();
                        foreach (var source in this.activePartitions)
                        {
                            var batch = source.Peek();
                            if (batch == null)
                            {
                                toModify.Add(source);
                            }
                            else if (minBatch == null || batch.EnqueuedTimeUtc < minBatch.EnqueuedTimeUtc)
                            {
                                partitionSourceForMinBatch = source;
                                minBatch = batch;
                            }
                        }

                        foreach (var source in toModify)
                        {
                            appIdLogger?.LogInformation("{kind} {partition} is now offline", _eventTypeName, source.PartitionId);
                            this.inactivePartitions.Add(source);
                            this.activePartitions.Remove(source);
                        }
                    }

                    if (minBatch == null)
                    {
                        continue;
                    }

                    if (minBatch.EnqueuedTimeUtc < lastEventEnqueuedTimeUtc)
                    {
                        this.appIdLogger?.LogError(
                            $"EventMergeSortBlock: {this._eventTypeName} events are out of order. Last event enqueued time: {lastEventEnqueuedTimeUtc}, current event enqueued time: {minBatch.EnqueuedTimeUtc}. Throwing it away...");

                        // Consume the event.
                        partitionSourceForMinBatch.Pop();

                        continue;
                    }

                    lastEventEnqueuedTimeUtc = minBatch.EnqueuedTimeUtc;

                    // Consume the event.
                    partitionSourceForMinBatch.Pop();

                    // Send it.
                    if (!await this.outputBlock.SendAsync(minBatch, cancellationToken))
                    {
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // ignore, this is normal shutdown behavior
            }
            catch (Exception ex)
            {
                this.appIdLogger?.LogError(ex, "");
                throw;
            }
            finally
            {
                this.outputBlock.Complete();
            }
        }
    }
}