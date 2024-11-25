// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Storage.Azure
{
    internal class AzBlobContainerClient : IBlobContainerClient
    {
        private readonly AzStorageFactory _factory;
        private readonly BlobContainerClient _containerClient;

        public AzBlobContainerClient(BlobContainerClient client, AzStorageFactory factory)
        {
            _containerClient = client;
            _factory = factory;
        }

        public string Name { get { return _containerClient.Name; } }

        public Uri Uri { get { return _containerClient.Uri; } }

        public IStorageFactory Factory { get { return _factory; } }

        public async Task<IBlobLeaseHolder> AcquireLeaseAsync(string appId, string lockBlobName, DateTime lastConfigEditDate, ILogger logger, CancellationToken outerCancellationToken)
        {
            try
            {
                return await AzBlobLeaseHolder.AcquireBlobLeaseV2Async(_containerClient, appId, lockBlobName, lastConfigEditDate, logger, outerCancellationToken);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        public IBlockStoreProvider CreateBlockStoreProvider()
        {
            try
            {
                return AzureBlockStoreProviderAdapter.CreateWithContainer(_containerClient);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        public IBlobClient GetBlobClient(string blobName)
        {
            try
            {
                return new AzBlobClient(_containerClient.GetBlobClient(blobName));
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        public IBlockStore GetBlockBlobClient(string blobName)
        {
            try
            {
                return AzureBlockStoreAdapter.CreateWithName(_containerClient, blobName);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        public async Task<IList<IBlobItem>> GetBlobsAsync(string prefix, CancellationToken cancellationToken)
        {
            try
            {
                var azItems = await _containerClient.GetBlobsAsync(prefix: prefix).ToListAsync(cancellationToken);
                return azItems.Select(b => new AzBlobItem(b)).ToList<IBlobItem>();
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        public async Task DeleteBlobAsync(string name, CancellationToken cancellationToken)
        {
            try
            {
                await _containerClient.DeleteBlobIfExistsAsync(name, cancellationToken: cancellationToken);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        public async Task<IList<IBlobHierarchyItem>> GetBlobsByHierarchyAsync(string prefix, string delimiter, CancellationToken cancellationToken = default)
        {
            try
            {
                var azItems = await _containerClient.GetBlobsByHierarchyAsync(prefix: prefix, delimiter: delimiter).ToListAsync(cancellationToken);
                return azItems.Select(b => new AzBlobHierarchyItem(b)).ToList<IBlobHierarchyItem>();
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        public async Task CreateIfNotExistsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        public async Task DeleteIfExistsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _containerClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }
    }
}
