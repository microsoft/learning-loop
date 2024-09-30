// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;

namespace Microsoft.DecisionService.Common.Trainer.FlatBuffers
{
    public class ObservationSerializer
    {
        [JsonProperty]
        public string EventId { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Index { get; set; }

        [JsonProperty]
        public bool ActionTaken { get; set; }
    }

    public class NumericObservationSerializer : ObservationSerializer
    {
        [JsonProperty("v")]
        public float NumericObservation { get; set; }
    }

    public class StringObservationSerializer : ObservationSerializer
    {
        [JsonProperty("v", NullValueHandling = NullValueHandling.Ignore)]
        public string StringObservation { get; set; }
    }

    public class JsonObservationSerializer : ObservationSerializer
    {
        [JsonProperty("v", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(JsonRawStringConverter))]
        public string JsonObservation { get; set; }
    }
}