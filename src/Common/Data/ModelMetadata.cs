// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;
using System;

namespace Microsoft.DecisionService.Common
{
    public class ModelMetadata
    {
        [JsonProperty(PropertyName = "modelId", Required = Required.Always)]
        public string ModelId { get; set; }
        [JsonProperty(PropertyName = "userDescription", Required = Required.Default)]
        public string UserDescription { get; set; }
        [JsonProperty(PropertyName = "creationDate", Required = Required.Always)]
        public DateTime CreationDate { get; set; }
        [JsonProperty(PropertyName = "lastConfigEditDate", Required = Required.Default)]
        public DateTime LastConfigEditDate { get; set; }
        [JsonProperty(PropertyName = "firstEventId", Required = Required.Default)]
        public string FirstEventId { get; set; }
        [JsonProperty(PropertyName = "lastEventId", Required = Required.Default)]
        public string LastEventId { get; set; }
        [JsonProperty(PropertyName = "savedInHistory", Required = Required.Default)]
        public bool SavedInHistory { get; set; }
        [JsonProperty(PropertyName = "numberOfEventsLearnedSinceLastExport", Required = Required.Default)]
        public int NumberOfEventsLearnedSinceLastExport { get; set; }
    }
}
