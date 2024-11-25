// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Storage.Blobs;
using System;

namespace Microsoft.DecisionService.Common.Storage.Azure
{
    public class AzStorageFactory : IStorageFactory
    {
        private readonly BlobServiceClient _storageClient;

        public AzStorageFactory(BlobServiceClient client)
        {
            _storageClient = client;
        }

        public AzStorageFactory(Uri uri, TokenCredential credential)
        {
            _storageClient = new BlobServiceClient(uri, credential);
        }

        public IBlobContainerClient CreateBlobContainerClient(string name)
        {
            return new AzBlobContainerClient(_storageClient.GetBlobContainerClient(name), this);
        }

        public IBlobContainerClient CreateBlobContainerClient(Uri containerUri)
        {
            return new AzBlobContainerClient(new BlobContainerClient(containerUri), this);
        }
    }
}
