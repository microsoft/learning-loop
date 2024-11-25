// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.DecisionService.Common.Trainer
{
    /// <summary>
    /// Contains information about the results of a learn/train iteration.
    /// </summary>
    public interface ILearnMetrics
    {
        /// <summary>
        /// The ID of the event in the cooked logs that has the
        /// earliest time.
        /// </summary>
        string FirstEventId { get; }

        /// <summary>
        /// The ID of the event in the cooked logs that has the
        /// latest time.
        /// </summary>
        string LastEventId { get; }

        /// <summary>
        /// The earliest event time.
        /// </summary>
        DateTime FirstEventTime { get; }

        /// <summary>
        /// The latest event time.
        /// </summary>
        DateTime LastEventTime { get; }

        /// <summary>
        /// The number of events present in the cooked logs.
        /// This includes events that may not be used for learning.
        /// </summary>
        long NumberOfEvents { get; }

        /// <summary>
        /// The number of events present in the cooked logs that are used for learning.
        /// Does not include events that are skipped.
        /// </summary>
        long NumberOfLearnedEvents { get; }

        /// <summary>
        /// The number of events that have an observation
        /// </summary>
        long NumberOfEventsWithObservation { get; }

        /// <summary>
        /// The number of events in the cooked logs that are faulty or have an error.
        /// </summary>
        long NumberOfFaultyEvents { get; }

        /// <summary>
        /// The number of events learned where the first/baseline action was the chosen action.
        /// Does not include events that are skipped.
        /// </summary>
        long NumberOfLearnedEventsWithBaselineActionChosen { get; }

        /// <summary>
        /// The number of events learned where the first/baseline action was not the chosen action.
        /// Does not include events that are skipped.
        /// </summary>
        long NumberOfLearnedEventsWithBaselineActionNotChosen { get; }

        /// <summary>
        /// Sum of rewards for events that were used for learning.
        /// Does not included skipped events or dangling observations.
        /// </summary>
        float SumOfLearnedRewards { get; }

        /// <summary>
        /// Sum of rewards for events where the chosen action is the first/baseline action.
        /// Does not included skipped events or dangling observations.
        /// </summary>
        float SumOfLearnedRewardsWithBaselineActionChosen { get; }

        float AverageFeaturesPerEvent { get; }

        float AverageFeaturesPerExample { get; }

        float AverageNamespacesPerEvent { get; }

        float AverageNamespacesPerExample { get; }

        float AverageActionsPerEvent { get; }
    }
}
