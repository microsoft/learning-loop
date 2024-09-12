// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.DecisionService.Common
{
    public class LogMirrorSettings
    {
        /// <summary>
        /// Azure Storage Account Container Sas Uri for mirroring data logs to.
        /// </summary>
        [JsonProperty(Required = Required.AllowNull)]
        public string SasUri { get; set; }

        /// <summary>
        /// Flag indicating whether or not mirroring is enabled.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public bool Enabled { get; set; }

        public override bool Equals(object obj)
        {
            var settings = obj as LogMirrorSettings;
            return settings != null &&
                   SasUri == settings.SasUri &&
                   Enabled == settings.Enabled;
        }

        public override int GetHashCode()
        {
            var hashCode = -132298322;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(SasUri);
            hashCode = hashCode * -1521134295 + Enabled.GetHashCode();
            return hashCode;
        }
    }
}
