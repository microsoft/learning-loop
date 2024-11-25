// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.DecisionService.Common.Storage
{
    public interface IBlobProperties
    {
        DateTimeOffset LastModified { get; }
        DateTimeOffset CreatedOn { get; }
        long ContentLength { get; }
    }
}
