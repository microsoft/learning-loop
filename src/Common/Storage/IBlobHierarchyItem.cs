// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.DecisionService.Common.Storage
{
    public interface IBlobHierarchyItem
    {
        string Name { get; }
        bool IsPrefix { get; }
        string Prefix { get; }
    }
}
