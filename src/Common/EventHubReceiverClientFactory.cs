// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Primitives;
using Microsoft.DecisionService.Common.Trainer.Data;
using Microsoft.DecisionService.OnlineTrainer.Data;
using Microsoft.DecisionService.Common.Data;

// using Azure.Messaging.EventHubs.Consumer;
// using AzureEventPosition = Azure.Messaging.EventHubs.Consumer;

namespace Microsoft.DecisionService.Common.Trainer
{
    public class EventHubReceiverClientFactory : IDataClientFactory
    {
        private readonly string _eventHubNamespace;
        
        public EventHubReceiverClientFactory(string eventHubNamespace)
        {
            _eventHubNamespace = eventHubNamespace;
        }
        
        public async Task<string[]> GetPartitionsIdAsync(string eventHubName)
        {
            var client = new EventHubConsumerClient(EventHubConsumerClient.DefaultConsumerGroupName, _eventHubNamespace, eventHubName, new DefaultAzureCredential());

            var ids = await client.GetPartitionIdsAsync();
            return ids.ToArray();
        }

        public IReceiverClient CreateEventHubReceiver(string partitionId, string eventTypeName, ILogger logger)
        {
            var client = new EventHubConnection(_eventHubNamespace, eventTypeName, new DefaultAzureCredential());
            
            // var reader = client.ReadEventsFromPartitionAsync(partitionId, start)
            // var eventHubClient = EventHubClient.CreateFromConnectionString(connectionString);
            
            return new EventHubReceiverClient(client,
                   logger,
                   partitionId, eventTypeName);
        }
    }
    

    class EventHubReceiverClient : IReceiverClient
    {
        private readonly EventHubConnection _connection;
        private readonly ILogger _appIdLogger;
        private readonly string _eventTypeName;

        public EventHubReceiverClient(EventHubConnection connection, ILogger appIdLogger, string partitionId, string eventTypeName)
        {
            this._connection = connection;
            this._appIdLogger = appIdLogger;
            PartitionId = partitionId;
            this._eventTypeName = eventTypeName;
        }

        public string PartitionId { get; private set; }

        public IReceiver Connect(PartitionCheckpoint? position)
        {
            // var pos = Azure.Messaging.
            EventPosition pos;
            if (position?.Offset != null)
            {
                pos = EventPosition.FromOffset(position.Offset.Value);
            }
            else if (position?.EnqueuedTimeUtc != null)
            {
                pos = EventPosition.FromEnqueuedTime(position.EnqueuedTimeUtc.Value);
            }
            else
            {
                pos = EventPosition.Earliest;
            }

            // tuning this value to maximize throughput, for realtime this should not have any effect
            ReadEventOptions readOptions = new ReadEventOptions()
            {
                PrefetchCount = 50,
            };

            var opts = new PartitionReceiverOptions()
            {
                RetryOptions = new EventHubsRetryOptions()
                {
                    Mode = EventHubsRetryMode.Exponential,
                    MaximumRetries = 100,
                    MaximumDelay = TimeSpan.FromSeconds(30),
                }
            };

            var receiver = new PartitionReceiver(EventHubConsumerClient.DefaultConsumerGroupName, PartitionId, pos, _connection, opts);

            return new EventHubReceiver(this, receiver, PartitionId, this._eventTypeName);
        }


        class EventHubReceiver : IReceiver
        {
            private const int EventHubMessageReceiveCount = 8;

            private readonly EventHubReceiverClient _client;
            private readonly string _partitionId;
            private readonly PartitionReceiver _reader;
            private readonly string _eventTypeName;

            public EventHubReceiver(EventHubReceiverClient client, PartitionReceiver reader, string partitionId, string eventTypeName)
            {
                this._client = client;
                this._partitionId = partitionId;
                this._reader = reader;
                this._eventTypeName = eventTypeName;
            }

            public Task CloseAsync()
            {
                // TODO is this possible?
                return Task.CompletedTask;
            }

            public async Task<bool> IsHistoricalModeAsync(DateTime? warmstartStartDateTime)
            {
                //AWAIT: this is library code
                var client = new EventHubConsumerClient(EventHubConsumerClient.DefaultConsumerGroupName, _client._connection);
                var partitionRuntimeInformation =
                    await client.GetPartitionPropertiesAsync(_partitionId);
                var lastEnqueuedOffset = partitionRuntimeInformation.LastEnqueuedOffset;
                var lastEnqueuedTimeUtc = partitionRuntimeInformation.LastEnqueuedTime;

                this._client._appIdLogger?.LogTrace($"PartitionId:{this._partitionId}, WarmStartdate: {warmstartStartDateTime} LastEnqueueTime: {partitionRuntimeInformation.LastEnqueuedTime}", $"EventHubReceiver.Open.{this._client._eventTypeName}");

                // if we read from beginning of time, only use last enqueued time if it is not empty
                if (warmstartStartDateTime == null && lastEnqueuedOffset != -1)
                    return true;

                // make sure the latest event will be read as it must occur after the warmstart date
                if (warmstartStartDateTime < lastEnqueuedTimeUtc)
                    return true;

                // if we read from the beginning or we have a chance to read the events to catch up until LastEnqueuedTimeUtc
                return false;
            }

            public async Task<IMessageData[]> ReceiveAsync(TimeSpan receiveTimeout)
            {
                try
                {
                    //AWAIT: this is library code
                    // TODO timeout
                    var messages = await _reader.ReceiveBatchAsync(EventHubMessageReceiveCount, receiveTimeout).ConfigureAwait(false);
                    if (messages == null)
                        return null;

                    var msgs = messages.Select(m => new EventHubData(m, _partitionId)).ToArray();
                    if (msgs.Length == 0)
                        return null;
                    return msgs;
                }
                catch (Exception ex) when (ex is EventHubsException || ex is TimeoutException || ex is UnauthorizedAccessException)
                {
                    /*
                     * NOTE: PartitionReceiver.ReceiveAsync can throw InavlidOperationException with message "Can't create session when the connection is closing".
                     * This is an issue with the EventHubs SDK.
                     * See Bug 7321949 for more detail.
                     * TODO(7474277): Upgrade to Microsoft.Azure.EventHubs 4.2.1 or later when it is released.
                     */
                    var eventHubException = ex as EventHubsException;
                    if (eventHubException != null && !eventHubException.IsTransient)
                    {
                        this._client._appIdLogger?.LogError(
                            ex,

                            $"PartitionId{this._client.PartitionId}");
                    }

                    //Wrap it to simplify users.
                    throw new ReceiveException(ex);
                }
            }
        }
    }
}
