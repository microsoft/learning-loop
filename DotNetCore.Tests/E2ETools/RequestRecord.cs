// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace DotNetCore.Tests.E2ETools
{
    public enum RewardType
    {
        NoReward,//no reward at all
        BestReward,//reward equals to default value 1
        CustomReward,//reward equals to a double value (22.24 for instance)
    }

    public class Reward
    {
        public double Value = 0f;
    }

    public class Action
    {
        public string Id;
    }

    /// <summary>
    /// A record to keep track of a pair of Rank and Reward requests sent during E2E tests.
    /// </summary>
    public class RequestRecord : IEquatable<RequestRecord>, IComparable<RequestRecord>
    {
        public string EventId { get; set; }

        public List<Reward> Rewards { get; set; }

        public RewardType RewardType { get; set; } = RewardType.NoReward;

        public List<Action> RewardActions { get; set; }

        public List<string> BaselineActions { get; set; }

        public int CompareTo(RequestRecord other)
        {
            if (other == null) return 1;

            if (string.IsNullOrEmpty(this.EventId))
            {
                throw new ArgumentException("EventId is invalid. Please check your implementation.");
            }

            return this.EventId.CompareTo(other.EventId);
        }

        public bool Equals(RequestRecord other)
        {
            if (other == null) return false;
            return string.Equals(this.EventId, other.EventId)
                && ((this.Rewards == null && other.Rewards == null) || Enumerable.SequenceEqual(this.Rewards, other.Rewards))
                && this.RewardType == other.RewardType
                && ((this.RewardActions == null && other.RewardActions == null) || Enumerable.SequenceEqual(this.RewardActions, other.RewardActions));
        }
    }
}
