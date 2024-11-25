// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;

namespace Microsoft.DecisionService.Common.Trainer
{
    /// <summary>
    /// Properties for learn metrics specific to CCB.
    /// </summary>
    // NOTE: For now, the CCB learn metrics are mostly duplicated with the CB learn metrics,
    // but this will not always be the case. We would need to have an intermediary base class
    // instead of direct inheritance if the properties differ too much.
    // The CCB metric support is also a work in progress, so this class is incomplete.
    public class VwCcbLearnMetrics : VwCbLearnMetrics
    {
        [JsonIgnore]
        public bool ApprenticeMode { get; set; } = false;

        [JsonProperty("dsjson_number_label_equal_baseline_first_slot")]
        public override long NumberOfLearnedEventsWithBaselineActionChosen { get; set; }

        [JsonProperty("dsjson_number_label_not_equal_baseline_first_slot")]
        public override long NumberOfLearnedEventsWithBaselineActionNotChosen { get; set; }

        [JsonIgnore]
        public override float SumOfLearnedRewards => ApprenticeMode ? -SumOfOriginalCostFirstSlot : -SumOfOriginalCost;

        [JsonIgnore]
        public override float SumOfLearnedRewardsWithBaselineActionChosen => -SumBaselineChosenOriginalCostFirstSlot;

        [JsonProperty("dsjson_sum_cost_original_label_equal_baseline_first_slot")]
        public float SumBaselineChosenOriginalCostFirstSlot { get; set; }

        [JsonProperty("dsjson_sum_cost_original_first_slot")]
        public float SumOfOriginalCostFirstSlot { get; set; }
    }
}
