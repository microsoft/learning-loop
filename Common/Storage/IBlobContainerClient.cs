// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Storage
{
    public interface IBlobContainerClient
    {
        string Name { get; }
        Uri Uri { get; }
        IStorageFactory Factory { get; }
        IBlobClient GetBlobClient(string blobName);
        IBlockStoreProvider CreateBlockStoreProvider();
        IBlockStore GetBlockBlobClient(string blobName);
        Task<IBlobLeaseHolder> AcquireLeaseAsync(string appId, string lockBlobName,
            DateTime lastConfigEditDate, ILogger logger, CancellationToken outerCancellationToken);
        Task<IList<IBlobItem>> GetBlobsAsync(string prefix, CancellationToken cancellationToken);
        Task DeleteBlobAsync(string name, CancellationToken cancellationToken);
        Task<IList<IBlobHierarchyItem>> GetBlobsByHierarchyAsync(string prefix, string delimiter, CancellationToken cancellationToken = default);
        Task CreateIfNotExistsAsync(CancellationToken cancellationToken = default);
        Task DeleteIfExistsAsync(CancellationToken cancellationToken = default);
    }
}
