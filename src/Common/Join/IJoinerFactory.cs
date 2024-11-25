// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Data;
using Microsoft.DecisionService.Common.Trainer.Data;
using Microsoft.Extensions.Logging;
using System.Diagnostics.Metrics;
using System.Threading.Tasks.Dataflow;

namespace Microsoft.DecisionService.OnlineTrainer.Join
{
    public interface IJoinerFactory
    {
        IJoiner Create(JoinerConfig config, EventHubCheckpoint position, ITargetBlock<JoinedBatch> joinedTarget, ITimeProvider timeProvider, IMeterFactory meterFactory, ILogger logger);
    }
}
