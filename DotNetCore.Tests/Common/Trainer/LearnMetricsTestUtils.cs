// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.DecisionService.Common.Trainer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCore.Tests.Common.Trainer
{
    internal static class LearnMetricsTestUtils
    {
        internal static void VerifyLearnMetrics(
            ILearnMetrics metrics,
            string firstEventId = null,
            string lastEventId = null,
            DateTime firstEventTime = default(DateTime),
            DateTime lastEventTime = default(DateTime),
            long numberOfEvents = 0,
            long numberOfLearnedEvents = 0,
            long numberOfEventsWithObservation = 0,
            long numberOfFaultyEvents = 0,
            long numberOfLearnedEventsWithBaselineActionChosen = 0,
            long numberOfLearnedEventsWithBaselineActionNotChosen = 0,
            float sumOfLearnedRewards = 0.0f,
            float sumOfLearnedRewardsWithBaselineActionChosen = 0.0f,
            float averageFeaturesPerEvent = 0.0f,
            float averageFeaturesPerExample = 0.0f,
            float averageNamespacesPerEvent = 0.0f,
            float averageNamespacesPerExample = 0.0f,
            float averageActionsPerEvent = 0.0f
        )
        {
            Assert.IsNotNull(metrics);
            Assert.AreEqual(firstEventId, metrics.FirstEventId, "FirstEventId");
            Assert.AreEqual(lastEventId, metrics.LastEventId, "LastEventId");
            Assert.AreEqual(firstEventTime, metrics.FirstEventTime, "FirstEventTime");
            Assert.AreEqual(lastEventTime, metrics.LastEventTime, "LastEventTime");
            Assert.AreEqual(numberOfEvents, metrics.NumberOfEvents, "NumberOfEvents");
            Assert.AreEqual(numberOfLearnedEvents, metrics.NumberOfLearnedEvents, "NumberOfLearnedEvents");
            Assert.AreEqual(numberOfEventsWithObservation, metrics.NumberOfEventsWithObservation, "NumberOfEventsWithObservation");
            Assert.AreEqual(numberOfFaultyEvents, metrics.NumberOfFaultyEvents, "NumberOfFaultyEvents");
            Assert.AreEqual(numberOfLearnedEventsWithBaselineActionChosen,
                metrics.NumberOfLearnedEventsWithBaselineActionChosen, "NumberOfLearnedEventsWithBaselineActionChosen");
            Assert.AreEqual(numberOfLearnedEventsWithBaselineActionNotChosen,
                metrics.NumberOfLearnedEventsWithBaselineActionNotChosen, "NumberOfLearnedEventsWithBaselineActionNotChosen");
            Assert.AreEqual(sumOfLearnedRewards, metrics.SumOfLearnedRewards, "SumOfLearnedRewards");
            Assert.AreEqual(sumOfLearnedRewardsWithBaselineActionChosen,
                metrics.SumOfLearnedRewardsWithBaselineActionChosen, "SumOfLearnedRewardsWithBaselineActionChosen");
            Assert.AreEqual(averageFeaturesPerEvent, metrics.AverageFeaturesPerEvent, "AverageFeaturesPerEvent");
            Assert.AreEqual(averageFeaturesPerExample, metrics.AverageFeaturesPerExample, "AverageFeaturesPerExample");
            Assert.AreEqual(averageNamespacesPerEvent, metrics.AverageNamespacesPerEvent, "AverageNamespacesPerEvent");
            Assert.AreEqual(averageNamespacesPerExample, metrics.AverageNamespacesPerExample, "AverageNamespacesPerExample");
            Assert.AreEqual(averageActionsPerEvent, metrics.AverageActionsPerEvent, "AverageActionsPerEvent");
        }
    }
}
