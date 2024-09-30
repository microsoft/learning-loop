// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Storage;

namespace Microsoft.DecisionService.Common
{
    public class CheckpointBlockHelperOptions
    {
        public string AppId { get; set; }

        public IBlockStoreProvider BlockStoreProvider { get; set; }
    }
}
