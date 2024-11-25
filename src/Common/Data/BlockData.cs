// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.DecisionService.Common.Data
{
    /// <summary>
    /// The data and metadata from a storage block.
    /// </summary>
    public class BlockData
    {
        public byte[] Data { get; set; }
        public BlockPosition Position { get; set; }
    }
}