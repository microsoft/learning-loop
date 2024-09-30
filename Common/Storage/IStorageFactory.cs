// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.DecisionService.Common.Storage
{
    public interface IStorageFactory
    {
        IBlobContainerClient CreateBlobContainerClient(string name);
        IBlobContainerClient CreateBlobContainerClient(Uri containerUri);
    }
}
