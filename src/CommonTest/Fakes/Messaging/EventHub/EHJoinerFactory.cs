// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Trainer;
using Microsoft.DecisionService.Common.Trainer.Data;
using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.DecisionService.OnlineTrainer.Join;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics.Metrics;
using System.Threading.Tasks.Dataflow;

namespace CommonTest.Fakes.Messaging.EventHub
{
    /// <summary>
    /// EHJoinerFactory is the InMemory Event Hub Joiner Factory.
    /// </summary>
    /// <remarks>
    /// EHJoinerFactory implements IJoinerFactory and is used to create
    /// an instance of JoinerEventHub that uses the InMemory Event Hub.
    /// </remarks>
    public class EHJoinerFactory : IJoinerFactory
    {
        private readonly IDataClientFactory _dataClientFactory;

        public EHJoinerFactory(IDataClientFactory dataClientFactory)
        {
            _dataClientFactory = dataClientFactory;
        }

        public event Action<IJoiner> OnCreate;

        public IJoiner Create(JoinerConfig config, EventHubCheckpoint position, ITargetBlock<JoinedBatch> joinedTarget, ITimeProvider timeProvider, IMeterFactory meterFactory, ILogger logger)
        {
            var eh = new JoinerEventHub(config, _dataClientFactory, position, joinedTarget, timeProvider, meterFactory, logger);
            OnCreate?.Invoke(eh);
            return eh;
        }
    }
}
