// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace Microsoft.DecisionService.Common
{
    [DataContract]
    public sealed class ModelCheckpoint : IExtensibleDataObject
    {
        [DataMember]
        public DateTime Timestamp { get; set; }

        [DataMember]
        [JsonIgnore] // don't want to have the binary model in the JSON
        public byte[] Model { get; set; }

        [DataMember]
        public string WarmstartModelUrl { get; set; }

        [DataMember]
        public DateTime? WarmstartStartDateTime { get; set; }

        // Just for logging
        [IgnoreDataMember]
        [JsonIgnore]
        public int NumberOfExamplesLearnedSinceLastCheckpoint { get; set; }

        [JsonIgnore]
        public ExtensionDataObject ExtensionData { get; set; }

        /// <summary>
        /// Store additional info for historical models
        /// </summary>
        [DataMember]
        public HistoricalModelInfo HistoricalModelInfo { get; set;}

        /// <summary>
        /// Track the reading position in Azure logs (ds-json)
        /// </summary>
        [DataMember]
        public BlockPosition ReadingPosition { get; set; }
    }

    /// <summary>
    /// In order to replay the history, model id and first/last event ids are required
    /// </summary>
    public class HistoricalModelInfo
    {
        [DataMember]
        public string FirstEventId { get; set; }
        [DataMember]
        public string LastEventId { get; set; }
        [DataMember]
        public string ModelId { get; set; }
        [DataMember]
        public bool WasExported { get; set; }
    }

}
