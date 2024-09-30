// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Storage.Blobs;

namespace Microsoft.DecisionService.Common.Storage.Azure
{
    /// <summary>
    /// Adapter for using Azure Blob Storage as block storage.
    /// </summary>
    public sealed class AzureBlockStoreProviderAdapter : IBlockStoreProvider
    {
        private readonly BlobContainerClient container;

        private AzureBlockStoreProviderAdapter(BlobContainerClient container)
        {
            this.container = container;
        }

        public static AzureBlockStoreProviderAdapter CreateWithAppId(BlobServiceClient azureClient, string appId)
        {
            return CreateWithContainerName(azureClient, StorageUtilities.BuildValidContainerName(appId));
        }

        public static AzureBlockStoreProviderAdapter CreateWithContainerName(BlobServiceClient azureClient, string containerName)
        {
            return CreateWithContainer(azureClient?.GetBlobContainerClient(containerName));
        }

        public static AzureBlockStoreProviderAdapter CreateWithContainer(BlobContainerClient container)
        {
            return new AzureBlockStoreProviderAdapter(container);
        }

        /// <inheritdoc/>
        public IBlockStore GetStore(string name)
        {
            return AzureBlockStoreAdapter.CreateWithName(container, name);
        }
    }
}
