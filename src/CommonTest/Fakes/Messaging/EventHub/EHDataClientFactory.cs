// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Trainer;
using Microsoft.DecisionService.Common.Trainer.Data;
using Microsoft.Extensions.Logging;
using reinforcement_learning.messages.flatbuff.v2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CommonTest.Fakes.Messaging.EventHub
{
    /// <summary>
    /// EHDataClientFactory provider the test implementation of IDataClientFactory
    /// used by the Joiner to create IReceiverClient for EventHub
    /// </summary>
    public class EHDataClientFactory : IDataClientFactory
    {
        private readonly HashSet<string> _eventTypeNames = new();
        private readonly Dictionary<string, EHReceiverClient> _receivers = new();
        private List<string> _partitionIds;
        private List<string> _unbalancedPartitionIds = new();

        /// <summary>
        /// EHDataClientFactory constructor
        /// </summary>
        /// <param name="partitionCount">the number of eventhub partitions to simulate</param>
        public EHDataClientFactory(int partitionCount)
        {
            _partitionIds = Enumerable.Range(0, partitionCount).Select(i => MakePartitionId(i)).ToList();
            _unbalancedPartitionIds.AddRange(_partitionIds.Take(partitionCount - 1));
        }

        /// <summary>
        /// EHDataClientFactory constructor
        /// </summary>
        /// <param name="partitionCount">the number of eventhub partitions to simulate</param>
        /// <remarks>Explicitly create the list of partitions with prefabricated ids</remarks>
        public EHDataClientFactory(IList<string> partitionIds)
        {
            if (_partitionIds == null)
            {
                throw new ArgumentNullException(nameof(partitionIds));
            }
            _partitionIds = partitionIds.ToList();
        }

        /// <summary>
        /// Provides the ability to return unbalanced partition ids for special purposes testing of the JoinerEventHub
        /// implemtation.
        /// </summary>
        /// <remarks>Default is false; The JoinerEventHub implementation creates a receiever for each partition for each
        /// event client.  There is a check to enusre the number of receivers is the same for each event client. This switch
        /// simulates the case where the number of partitions for each event client is different and the JoinerEventHub throws.
        /// </remarks>
        internal bool ProvideUnabalancedPartitionIds { get; set; } = false;

        /// <summary>
        /// Create a partition id string based on the partition number
        /// </summary>
        /// <param name="partition">the partition number</param>
        /// <returns>A partition id string</returns>
        public static string MakePartitionId(int partition)
        {
            return EHConstants.EH_PARTITION_PREFIX + partition;
        }

        #region IDataClientFactory

        /// <summary>
        /// Implement the IDataClientFactory interface to create a receiver client for the event hub
        /// </summary>
        /// <param name="partitionId">the partition id</param>
        /// <param name="eventTypeName">the event hub name</param>
        /// <param name="logger">a logger</param>
        /// <returns></returns>
        public IReceiverClient CreateEventHubReceiver(string partitionId, string eventTypeName, ILogger logger)
        {
            _eventTypeNames.Add(eventTypeName);
            var receiver = new EHReceiverClient(new EHConnection(partitionId, eventTypeName), logger, partitionId, eventTypeName);
            _receivers[receiver.Id] = receiver;
            return receiver;
        }

        /// <summary>
        /// Implement the IDataClientFactory interface to get the partition ids for the event hub
        /// </summary>
        /// <param name="eventTypeName">the event hub name</param>
        /// <returns>A list of partition ids for the event hub</returns>
        public async Task<string[]> GetPartitionsIdAsync(string eventTypeName)
        {
            string[] ids = _partitionIds.ToArray();
            if (ProvideUnabalancedPartitionIds)
            {
                (_unbalancedPartitionIds, _partitionIds) = (_partitionIds, _unbalancedPartitionIds);
            }
            return await Task.FromResult(ids.ToArray());
        }
        #endregion

        /// <summary>
        /// Get the list of event hubs names
        /// </summary>
        /// <returns>a list of eventhub names</returns>
        internal ISet<string> GetEventTypeNames()
        {
            return new HashSet<string>(_eventTypeNames);
        }

        /// <summary>
        /// Get the named receiver client
        /// </summary>
        /// <param name="eventTypeName">the eventhub name</param>
        /// <param name="partitionId">the parition id</param>
        /// <returns></returns>
        internal IReceiverClientTest GetReceiverClient(string eventTypeName, string partitionId)
        {
            var id = MakeReceiverClientId(eventTypeName, partitionId);
            _receivers.TryGetValue(id, out EHReceiverClient receiver);
            return receiver;
        }

        /// <summary>
        /// Get all receiver clients
        /// </summary>
        internal IList<IReceiverClientTest> ReceiverClients => _receivers.Values.ToList<IReceiverClientTest>();

        /// <summary>
        /// Make a receiver client id
        /// </summary>
        /// <param name="eventTypeName">the eventhub name</param>
        /// <param name="partitionId">the partition id</param>
        /// <returns>the receiver client id</returns>
        internal static string MakeReceiverClientId(string eventTypeName, string partitionId)
        {
            return $"{eventTypeName}-{partitionId}";
        }
    }

    /// <summary>
    /// EHConnection represents the event hub connection
    /// </summary>
    internal class EHConnection
    {
        public string PartitionId { get; private set; }
        public string EventTypeName { get; private set; }

        public EHConnection(string partitionId, string eventTypeName)
        {
            PartitionId = partitionId;
            EventTypeName = eventTypeName;
        }

        public string Id => EHDataClientFactory.MakeReceiverClientId(EventTypeName, PartitionId);
    }

    /// <summary>
    /// EHReceiverClient implements IReceiverClient for EventHub and IReceiverClientTest for testing.
    /// This allows IReceiverClient to remain internal to the EventHub namespace while allowing
    /// IReceiverClientTest to be used by the test code.
    /// </summary>
    internal class EHReceiverClient : IReceiverClient, IReceiverClientTest
    {
        private readonly EHConnection _connection;
        private readonly ILogger _appIdLogger;
        private readonly string _eventTypeName;
        private EHReceiver _receiver;
        private long _injectEventOffset;
        private long _injectEventSequenceNumber;

        public EHReceiverClient(EHConnection connection, ILogger appIdLogger, string partitionId, string eventTypeName)
        {
            _connection = connection;
            _appIdLogger = appIdLogger;
            PartitionId = partitionId;
            _eventTypeName = eventTypeName;
        }
        public string PartitionId { get; private set; }

        public IReceiver Connect(PartitionCheckpoint position)
        {
            // TODO: position is used to determine the position of where to start reading from the event hub.
            //       add logic to support this when the tests can be covered
            _receiver = new EHReceiver();
            return _receiver;
        }

        public string Id => _connection.Id;

        public IList<IMessageData> PendingEvents => _receiver.PendingEvents;

        public void InjectMessages(IList<EventBatch> messages)
        {
            var events = new List<IMessageData>();
            foreach (var message in messages)
            {
                events.Add(MakeMessage(message));
            }
            _receiver.InjectEvents(events);
        }

        private IMessageData MakeMessage(EventBatch batch)
        {
            var messagePreamble = new MessagePreamble()
            {
                MsgType = (ushort)MessageType.FlatBuffGenericEventBatch,
                MsgSize = (uint)batch.ByteBuffer.Length,
            };
            var stm = new MemoryStream();
            stm.Write(messagePreamble.ToBytes());
            stm.Write(batch.ByteBuffer.ToSizedArray());
            return new EHMessageData(PartitionId, ++_injectEventOffset, ++_injectEventSequenceNumber, DateTime.UtcNow, stm);
        }
    }

    /// <summary>
    /// EHMessageData providers the necessary data items needed by the Joiner to process the event hub message
    /// </summary>
    internal class EHMessageData : IMessageData
    {
        public EHMessageData(string streamId, long streamOffset, long streamSequenceNumber, DateTime enqueuedTimeUtc, MemoryStream stm)
        {
            StreamId = streamId;
            StreamOffset = streamOffset;
            StreamSequenceNumber = streamSequenceNumber;
            EnqueuedTimeUtc = enqueuedTimeUtc;
            Bytes = new ArraySegment<byte>(stm.ToArray());
        }

        public string StreamId { get; }

        public long StreamOffset { get; }

        public long StreamSequenceNumber { get; }

        public DateTime EnqueuedTimeUtc { get; }

        public ArraySegment<byte> Bytes { get; }
    }

    /// <summary>
    /// EHReceiver implements IReceiver for EventHub required by the Joiner
    /// </summary>
    internal class EHReceiver : IReceiver
    {
        private readonly object _lock = new();
        private readonly List<IMessageData> _pendingEvents = new();

        public EHReceiver()
        {
        }

        public async Task CloseAsync()
        {
            await Task.CompletedTask;
        }

        public async Task<bool> IsHistoricalModeAsync(DateTime? warmstartStartDateTime)
        {
            await Task.Yield();
            return false;
        }

        public async Task<IMessageData[]> ReceiveAsync(TimeSpan receiveTimeout)
        {
            await Task.Yield();
            lock (_lock)
            {
                var events = _pendingEvents.ToArray();
                _pendingEvents.Clear();
                return events;
            }
        }

        internal IList<IMessageData> PendingEvents => _pendingEvents;

        internal void InjectEvents(IList<IMessageData> events)
        {
            lock (_lock)
            {
                _pendingEvents.AddRange(events);
            }
        }
    }
}
