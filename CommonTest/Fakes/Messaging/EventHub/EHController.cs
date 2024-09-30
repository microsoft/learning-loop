// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace CommonTest.Fakes.Messaging.EventHub
{
    /// <summary>
    /// InMemory Event Hub Controller
    /// </summary>
    /// <remarks>
    /// EHController can be used to control the behaviour of the InMemory Event Hub
    /// while testing.  It provides access to underlying receivers.
    /// 
    /// Methods/Properties/Events can be used to inject messages into the Event Hub,
    /// control whether an operations should succeed or fail, and generate events
    /// to the test harness for further control.
    /// </remarks>
    public class EHController
    {
        private readonly EHDataClientFactory _dataClientFactory;
        private readonly EHJoinerFactory _joinerFactory;

        public EHController(int partitions)
        {
            _dataClientFactory = new EHDataClientFactory(partitions);
            _joinerFactory = new EHJoinerFactory(DataClientFactory);
        }

        public EHController(EHDataClientFactory dataClientFactory, EHJoinerFactory joinerFactory)
        {
            _dataClientFactory = dataClientFactory ?? throw new ArgumentNullException(nameof(dataClientFactory));
            _joinerFactory = joinerFactory ?? throw new ArgumentNullException(nameof(joinerFactory)); ;
        }

        public EHController(IList<string> partition_ids)
        {
            _dataClientFactory = new EHDataClientFactory(partition_ids);
            _joinerFactory = new EHJoinerFactory(DataClientFactory);
        }

        public EHDataClientFactory DataClientFactory { get { return _dataClientFactory; } }

        public EHJoinerFactory JoinerFactory { get { return _joinerFactory; } }

        public ISet<string> GetEventTypeNames()
        {
            return _dataClientFactory.GetEventTypeNames();
        }

        public IReceiverClientTest GetReceiverClient(string eventTypeName, string partitionId)
        {
            return _dataClientFactory.GetReceiverClient(eventTypeName, partitionId);
        }

        public IList<IReceiverClientTest> ReceiverClients => _dataClientFactory.ReceiverClients;

        /// <summary>
        /// Provides the ability to return unbalanced partition ids for special purposes testing of the JoinerEventHub
        /// implemtation.
        /// </summary>
        /// <remarks>Default is false; The JoinerEventHub implementation creates a receiever for each partition for each
        /// event client.  There is a check to enusre the number of receivers is the same for each event client. This switch
        /// simulates the case where the number of partitions for each event client is different and the JoinerEventHub throws.
        /// </remarks>
        public bool ProvideUnabalancedPartitionIds
        {
            get => _dataClientFactory.ProvideUnabalancedPartitionIds;
            set => _dataClientFactory.ProvideUnabalancedPartitionIds = value;
        }
    }
}
