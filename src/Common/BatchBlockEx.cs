// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Microsoft.DecisionService.Common
{
    /// <summary>
    /// A batching TPL block outputting based on function of the available items (e.g. sum of size of each item).
    /// </summary>
    /// <typeparam name="T">Type of item.</typeparam>
    public sealed class BatchBlockEx<T>
    {
        private readonly BufferBlock<T> input;
        private readonly BatchBlockExOptions<T> options;
        private readonly ITargetBlock<IList<T>> target;
        private DateTime lastSendTime = DateTime.MinValue;
        private List<T> outputBuffer = new List<T>();
        private int outputBufferSize = 0;

        public BatchBlockEx(BatchBlockExOptions<T> options, ITargetBlock<IList<T>> target)
        {
            this.options = options;
            this.target = target;

            this.input = new BufferBlock<T>(new DataflowBlockOptions
            {
                CancellationToken = options.CancellationToken,
                BoundedCapacity = 16 // bound in number of items
            });

            var scheduler = new ConcurrentExclusiveSchedulerPair();
            this.Completion = Task.Factory.StartNew(this.ProcessCoreLoopAsync,
                this.options.CancellationToken,
                TaskCreationOptions.LongRunning,
                scheduler.ExclusiveScheduler)
                .Unwrap();

            _ = Task.Factory.StartNew(this.FlushTimerAsync,
                this.options.CancellationToken,
                TaskCreationOptions.LongRunning,
                scheduler.ExclusiveScheduler);
        }

        private ITimeProvider TimeProvider => this.options.TimeProvider ?? SystemTimeProvider.Instance;

        private async Task SendIfNotEmptyAsync()
        {
            // TODO: what to do if downstream rejects. Completed?
            if (this.outputBuffer.Count > 0)
            {
                var temp = this.outputBuffer;

                this.outputBuffer = new List<T>();
                this.outputBufferSize = 0;

                this.lastSendTime = this.TimeProvider.UtcNow;

                await this.target.SendAsync(temp, this.options.CancellationToken);
            }
        }

        /// <summary>
        /// Trigger flushing from time to time.
        /// </summary>
        /// <remarks>
        /// Will not run concurrently at this.ProcessCoreLoopAsync.
        /// </remarks>
        private async Task FlushTimerAsync()
        {
            while (!this.options.CancellationToken.IsCancellationRequested && !this.input.Completion.IsCompleted)
            {
                await Task.Delay(200, this.options.CancellationToken);

                if (this.lastSendTime < this.TimeProvider.UtcNow - this.options.MaximumFlushLatency &&
                    this.outputBuffer.Count > 0)
                {
                    // make sure we start waiting from the time we get the first event
                    if (this.lastSendTime == DateTime.MinValue)
                    {
                        this.lastSendTime = this.TimeProvider.UtcNow;
                        continue;
                    }

                    await this.SendIfNotEmptyAsync();
                }
            }
        }

        private async Task SendIfLastIsFlushItemAsync()
        {
            if (this.options.IsFlushItem(this.outputBuffer.Last()))
                await this.SendIfNotEmptyAsync();
        }

        private async Task ProcessCoreLoopAsync()
        {
            try
            {
                var maxBatchOutputSize = this.options.BoundedCapacity;
                var measureItem = this.options.MeasureItem;
                var startNewPredicate = this.options.StartNewPredicate;

                while (!this.options.CancellationToken.IsCancellationRequested && !this.input.Completion.IsCompleted)
                {
                    T item;

                    try
                    {
                        item = await this.input.ReceiveAsync(this.options.CancellationToken);
                    }
                    catch (Exception)
                    {
                        // ReceiveAsync() will throw if input completed. 
                        if (this.input.Completion.IsCompleted)
                        {
                            // send the left overs
                            await this.SendIfNotEmptyAsync();
                            return;
                        }

                        throw;
                    }

                    var itemSize = measureItem(item);
                    var newOutputBufferSize = this.outputBufferSize + itemSize;

                    if (newOutputBufferSize > maxBatchOutputSize || startNewPredicate(item))
                    {
                        await this.SendIfNotEmptyAsync();

                        this.outputBuffer.Add(item);
                        this.outputBufferSize += itemSize;

                        if (itemSize > maxBatchOutputSize)
                            await this.SendIfNotEmptyAsync();
                        else
                            await this.SendIfLastIsFlushItemAsync();
                    }
                    else
                    {
                        // this will send the item even if it's larger than bounded capacity
                        // for the use-case of batching together log entries, they can't be bigger than 256KB anyway due to EventHub limit
                        this.outputBuffer.Add(item);
                        this.outputBufferSize = newOutputBufferSize;

                        await this.SendIfLastIsFlushItemAsync();
                    }
                }

                // flush any left over messages
                await this.SendIfNotEmptyAsync();
            }
            catch (Exception ex)
            {
                this.target.Fault(ex);
                throw;
            }
            finally
            {
                this.target.Complete();
            }
        }

        public Task Completion { get; private set; }

        public ITargetBlock<T> Input => this.input;
    }
}
