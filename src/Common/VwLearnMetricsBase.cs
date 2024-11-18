// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Newtonsoft.Json;

namespace Microsoft.DecisionService.Common.Trainer
{
    /// <summary>
    /// Properties common to metrics for all problem types.
    /// </summary>
    public class VwLearnMetricsBase
    {
        [JsonProperty("first_event_id")]
        public string FirstEventId { get; set; }

        [JsonProperty("last_event_id")]
        public string LastEventId { get; set; }

        [JsonProperty("first_event_time", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime FirstEventTime { get; set; }

        [JsonProperty("last_event_time", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime LastEventTime { get; set; }

        [JsonIgnore]
        public long NumberOfEvents => NumberOfLearnCalls + NumberOfSkippedEvents;

        [JsonIgnore]
        public long NumberOfFaultyEvents => NumberOfEventsMissingActions;

        [JsonProperty("number_events_zero_actions")]
        public long NumberOfEventsMissingActions { get; set; }

        [JsonProperty("total_learn_calls")]
        public long NumberOfLearnCalls { get; set; }

        [JsonProperty("number_skipped_events")]
        public long NumberOfSkippedEvents { get; set; }

        [JsonProperty("cbea_avg_feat_per_event")]
        public float AverageFeaturesPerEvent { get; set; }

        [JsonProperty("cbea_avg_feat_per_action")]
        public float AverageFeaturesPerExample { get; set; }

        [JsonProperty("cbea_avg_ns_per_event")]
        public float AverageNamespacesPerEvent { get; set; }

        [JsonProperty("cbea_avg_ns_per_action")]
        public float AverageNamespacesPerExample { get; set; }

        [JsonProperty("cbea_avg_actions_per_event")]
        public float AverageActionsPerEvent { get; set; }
    }
}
