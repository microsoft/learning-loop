// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Trainer.Data
{
    public sealed class PartitionSource
    {
        private int _partitionReceiveTimeoutInMs;
        private readonly ILogger _logger;

        private readonly Channel<MessageBatch> _channel;
        private readonly ChannelReader<MessageBatch> _channelReader;

        public PartitionSource(string partitionId, JoinerConfig config, ILogger logger)
        {
            this._logger = logger;
            _partitionReceiveTimeoutInMs = Convert.ToInt32(config?.ActivePartitionReadTimeout.TotalMilliseconds);
            this.PartitionId = partitionId;
            this._channel = Channel.CreateBounded<MessageBatch>(16);
            _channelReader = this._channel.Reader;
        }

        public ChannelWriter<MessageBatch> Input => this._channel.Writer;

        public string PartitionId { get; }

        public MessageBatch? Peek()
        {
            var hasItem = _channelReader.TryPeek(out var batch);
            if (hasItem)
            {
                return batch;
            }
            return null;
        }

        public MessageBatch? Pop()
        {
            var hasItem = _channelReader.TryRead(out var batch);
            if (hasItem)
            {
                return batch;
            }
            return null;
        }

        public Task<bool> WaitForDataAsync(CancellationToken cancellationToken)
        {
            // If we have data, return immediately
            if (Peek() != null)
            {
                return Task.FromResult(true);
            }

            return WaitForDataInternalAsync(cancellationToken);
        }


        private async Task<bool> WaitForDataInternalAsync(CancellationToken cancellationToken)
        {
            try
            {
                // WaitToReadAsync with the partition read timeout
                var receiveTask = _channelReader.WaitToReadAsync(cancellationToken).AsTask();
                if (await Task.WhenAny(receiveTask, Task.Delay(_partitionReceiveTimeoutInMs, cancellationToken)) != receiveTask)
                {
                    // timeout
                    return false;
                }

                var dataReady = await receiveTask;
                if (!dataReady)
                {
                    // channel closed
                    // TODO: should we throw?
                    return false;
                }

                return true;
            }
            catch (TimeoutException)
            {
                //Do nothing;
            }
            catch (ArgumentOutOfRangeException)
            {
                // We should not receive future events... catch for safety
            }
            catch (OperationCanceledException e)
            {
                this._logger?.LogError(e, "");
            }

            return false;
        }
    }
}
