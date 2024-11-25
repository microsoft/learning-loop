// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Microsoft.DecisionService.Common
{
    ///<summary>Trainer Modes for Personalizer</summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TrainerMode
    {
        VwExe,
        PyVw,
    }
}
