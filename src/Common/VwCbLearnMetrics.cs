// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;

namespace Microsoft.DecisionService.Common.Trainer
{
    /// <summary>
    /// Properties for learn metrics specific to CB.
    /// </summary>
    public class VwCbLearnMetrics : VwLearnMetricsBase, ILearnMetrics
    {
        [JsonIgnore]
        public long NumberOfLearnedEvents => NumberOfLearnCalls;

        [JsonProperty("cbea_labeled_ex")]
        public long NumberOfEventsWithObservation { get; set; }

        [JsonProperty("cbea_label_first_action")]
        public virtual long NumberOfLearnedEventsWithBaselineActionChosen { get; set; }

        [JsonProperty("cbea_label_not_first")]
        public virtual long NumberOfLearnedEventsWithBaselineActionNotChosen { get; set; }

        [JsonIgnore]
        public virtual float SumOfLearnedRewards => -SumOfOriginalCost;

        [JsonIgnore]
        public virtual float SumOfLearnedRewardsWithBaselineActionChosen => -SumBaselineChosenOriginalCost;

        [JsonProperty("dsjson_sum_cost_original_baseline")]
        public float SumBaselineChosenOriginalCost { get; set; }

        [JsonProperty("dsjson_sum_cost_original")]
        public float SumOfOriginalCost { get; set; }
    }
}
