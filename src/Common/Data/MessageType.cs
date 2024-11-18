// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.DecisionService.Common.Trainer.Data
{
    public enum MessageType:ushort
    {
        Unknown = 0,
        FlatBuffRankingEventBatch = 1,
        FlatBuffOutcomeEventBatch = 2,
        JsonRankingEventBatch = 3,
        JsonOutcomeEventBatch = 4,
        FlatBuffOutcomeEvent = 5,
        FlatBuffRankingEvent = 6,
        FlatBuffDecisionEvent = 7,
        FlatBuffDecisionEventBatch = 8,
        FlatBuffRankingLearningModeEventBatch = 9,
        FlatBuffRankingLearningModeEvent = 10,
        FlatBuffSlateEvent = 11,
        FlatBuffSlateEventBatch = 12,
        FlatBuffGenericEventBatch = 13,
    }
}
