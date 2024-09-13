// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests.E2ETools
{
    public enum EventType
    {
        JoinedEvent,
        RewardEvent,
    }

    public class Outcome
    {
        public double LabelCost = 0D;
        public double LabelAction = 0;
    }

    public class CookedLogLine : IEquatable<CookedLogLine>, IComparable<CookedLogLine>
    {
        public ProblemType ProblemType { get; set; } = ProblemType.CB;

        /// <summary>
        /// Index of the line
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// EventId
        /// </summary>
        public string EventId { get; set; }

        /// <summary>
        /// The type of event logged: observation only, or joined event, ..
        /// </summary>
        public EventType EventType { get; set; }

        /// <summary>
        /// Contains list of outcomes (label cost and label action) for the interaction events
        /// </summary>
        public List<Outcome> Outcomes;

        /// <summary>
        /// The value contains the possible reward (not relevant for JoinedEvent)
        /// </summary>
        public double RewardValue { get; set; } = 0D;

        /// <summary>
        /// List of baseline action for multi slot cooked logs.
        /// default baseline action for single slot is first action.
        /// </summary>
        public List<int> BaselineActions { get; set; } = new List<int> { 1 };

        /// <summary>
        /// The Model ID that correlates with this event
        /// </summary>
        public string VWStateM { get; set; }

        public int CompareTo(CookedLogLine other)
        {
            if (other == null) return 1;

            if (string.IsNullOrEmpty(EventId))
            {
                throw new ArgumentException("EventId is invalid. Please check your implementation.");
            }

            return this.EventId.CompareTo(other.EventId);
        }

        public bool Equals(CookedLogLine other)
        {
            if (other == null) return false;
            return this.Id == other.Id
                && string.Equals(this.EventId, other.EventId)
                && this.EventType == other.EventType
                && ((this.Outcomes == null && other.Outcomes == null) || Enumerable.SequenceEqual(this.Outcomes, other.Outcomes))
                && this.RewardValue == other.RewardValue;
        }
    }
}
