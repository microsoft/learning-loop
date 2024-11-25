// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Serialization;

namespace Microsoft.DecisionService.Common
{
    public class BlockPosition
    {
        [DataMember]
        public string BlobName { get; set; }

        [DataMember]
        public string BlockName { get; set; }

        [DataMember]
        public JoinedLogFormat FileFormat { get; set; }
    }
}