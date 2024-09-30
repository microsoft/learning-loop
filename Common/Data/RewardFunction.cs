// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using reinforcement_learning.messages.flatbuff.v2;

namespace Microsoft.DecisionService.Common.Data
{
    /// <summary>
    /// The reward aggregation function used to process rewards.
    /// </summary>
    public enum RewardFunction : byte
    {
        earliest = 0,
        average = 1,
        median = 2,
        sum = 3,
        min = 4,
        max = 5
    }
    
    public static class RewardFunctionExtensions
    {
        public static RewardFunctionType ToFlatbuffer(this RewardFunction other)
        {
            switch (other)
            {
                case RewardFunction.earliest:
                    return RewardFunctionType.Earliest;
                case RewardFunction.average:
                    return RewardFunctionType.Average;
                case RewardFunction.median:
                    return RewardFunctionType.Median;
                case RewardFunction.sum:
                    return RewardFunctionType.Sum;
                case RewardFunction.min:
                    return RewardFunctionType.Min;
                case RewardFunction.max:
                    return RewardFunctionType.Max;
                default:
                    throw new ArgumentOutOfRangeException(nameof(other), other, null);
            }
        }
    }
}
