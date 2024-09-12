// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.DecisionService.Common.Storage
{
    public interface IBlobItem
    {
        string Name { get; }

        IBlobItemProperties Properties { get; }
    }
}
