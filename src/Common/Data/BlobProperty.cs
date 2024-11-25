// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Serialization;

namespace Microsoft.DecisionService.Common.Data
{
    public class BlobProperty
    {
        [DataMember]
        public string BlobName { get; set; }

        [DataMember]
        public long Length { get; set; }
    }
}
