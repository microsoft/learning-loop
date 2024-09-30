// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Trainer.Data;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.DecisionService.OnlineTrainer;

namespace Microsoft.DecisionService.Common.Trainer.Join
{
    public class JoinerReceiver
    {
        private readonly string StreamName;

        private readonly IReceiverClient client;
        private readonly JoinerConfig Config;
        private readonly ILogger appIdLogger;
        private PartitionCheckpoint lastOffset;

        private JoinerReceiverMetrics _metrics;

        private readonly RetryDelay receiveMessageRetryDelay = new RetryDelay();

        public JoinerReceiver(string streamName, IReceiverClient client, PartitionCheckpoint? lastOffset, JoinerConfig config, JoinerReceiverMetrics metrics, ILogger logger)
        {
            this.StreamName = streamName;
            this.client = client;
            this.lastOffset = lastOffset ?? new PartitionCheckpoint();
            this.Config = config;
            this._metrics = metrics;
            this.appIdLogger = logger;
        }

        private IReceiver CreatePartitionReceiver()
        {
            return this.client.Connect(this.lastOffset);
        }

        public string PartitionId => this.client.PartitionId;

        private ChannelWriter<MessageBatch> _target;

        public async Task ForwardAsync(PartitionSource source, CancellationToken cancellationToken)
        {
            this._target = source.Input;

            // outer loop to catch inner failures to recreate the receiver

            var retryDelay = new RetryDelay();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await this.ProcessLoopAsync(cancellationToken);

                    retryDelay.Reset();
                }
                catch (Exception ex)
                {
                    // Supplied offset was invalid; a likely reason for this is that we've switched to a new event hub
                    if (ex is ArgumentException)
                    {
                        this.appIdLogger.LogError(ex, "");
                        // Trigger recreation of receiver with LastEnqueuedTime as its checkpoint instead
                        this.lastOffset = null;
                    }
                    else
                    {
                        // stop early
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        // Geneva logging
                        this.appIdLogger?.LogError(
                            ex,
                            $"ParitionId{this.PartitionId}");

                        // this exception handler should only be hit if we loose the connection
                        // delay to avoid busy looping (e.g. if the event hub has a temporary issue)
                        await retryDelay.DelayAsync(cancellationToken);
                    }
                }
            }

            this._target.Complete();
        }

        private async Task ProcessLoopAsync(CancellationToken cancellationToken)
        {
            IReceiver eventReceiver = null;

            try
            {
                eventReceiver = this.CreatePartitionReceiver();

                _metrics.EventHubProcessorsIncrement();

                // outer loop for connection retries
                do
                {
                    //AWAIT: library code
                    await this.ReceiveAndForwardAsync(eventReceiver, cancellationToken);
                } while (!cancellationToken.IsCancellationRequested);
            }
            finally
            {
                // make sure we close the receiver to avoid any quota exceptions
                try
                {
                    if (eventReceiver != null)
                    {
                        //AWAIT: library code
                        await eventReceiver.CloseAsync();
                    }
                }
                catch (Exception)
                {
                }

                this.appIdLogger?.LogTrace(
                    $"PartitionId:{this.PartitionId}, LastOffset: {this.lastOffset} EventHubReceiver.Close.{StreamName}");
                _metrics.EventHubProcessorsDecrement();
            }
        }

        private async Task ReceiveAndForwardAsync(IReceiver eventHubReceiver, CancellationToken cancellationToken)
        {
            IMessageData[] messages;
            try
            {
                //AWAIT: library code
                messages = await eventHubReceiver.ReceiveAsync(this.Config.EventHubReceiveTimeout);

                // reset delay on successful reception
                this.receiveMessageRetryDelay.Reset();

                // if no data is present, this API returns null.
                if (messages == null)
                {
                    return;
                }
            }
            catch (ReceiveException)
            {
                // report reconnect exception
                this._metrics.ReconnectExceptionIncrement();

                // avoid CPU hogging
                await this.receiveMessageRetryDelay.DelayAsync(cancellationToken);

                return;
            }

            foreach (var msg in messages)
            {
                var eventsReceived = this.ParseEvents(msg);
                await this._target.WriteAsync(eventsReceived, cancellationToken);

                // Keep track of last offset.
                this.lastOffset.Offset = msg.StreamOffset;
                this.lastOffset.EnqueuedTimeUtc = msg.EnqueuedTimeUtc;

                // Metrics
                _metrics.JoinerReceivedEventsReceived(eventsReceived.Messages.Count, StreamName);
                foreach (var message in eventsReceived.Messages)
                {
                    _metrics.JoinerReceivedMessageReceivedSize(message.DataSegment.Count, StreamName);
                }
            }
        }

        private MessageBatch ParseEvents(IMessageData eventData)
        {
            return SchemaV2Parser.ProcessEventHubMessage(eventData, appIdLogger);
        }
    }
}