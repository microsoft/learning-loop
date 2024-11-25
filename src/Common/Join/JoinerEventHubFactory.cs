// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.DecisionService.Common.Data;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using System.Diagnostics.Metrics;
using Microsoft.DecisionService.Common.Trainer.Data;
using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Trainer;

namespace Microsoft.DecisionService.OnlineTrainer.Join
{
    /// <summary>
    /// This factory instantiate a joiner, depending on the event source: event hubs, local files, or external sources created with EventSourceFactory (using C# reflection)
    /// </summary>
    public class JoinerEventHubFactory : IJoinerFactory
    {
        public IJoiner Create(JoinerConfig config, EventHubCheckpoint position, ITargetBlock<JoinedBatch> joinedTarget, ITimeProvider timeProvider, IMeterFactory meterFactory, ILogger logger)
        {
            if (!string.IsNullOrEmpty(config.FullyQualifiedEventHubNamespace))
            {
                //read events from event hubs
                var dataClientFactory = new EventHubReceiverClientFactory(config.FullyQualifiedEventHubNamespace);
                return new JoinerEventHub(config, dataClientFactory, position, joinedTarget, timeProvider, meterFactory, logger);
            }
            else if (!string.IsNullOrEmpty(config.JoinerFilesInputDirectory))
            {
                //read event from local files
                // TODO update this to use the new Message types
                // return new JoinerFiles(config, joinedTarget,timeProvider, meterFactory, logger);
                throw new NotImplementedException();
            }
            else
            {
               throw new ArgumentException("No event source specified");
            }
        }
    }
}
