// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Storage.Blobs.Models;

namespace Microsoft.DecisionService.Common.Storage.Azure
{
    internal class AzBlobItem : IBlobItem
    {
        private readonly BlobItem _item;
        private readonly AzBlobItemProperties _azItemProperties;

        public AzBlobItem(BlobItem item)
        {
            _item = item;
            _azItemProperties = new AzBlobItemProperties(item.Properties);
        }

        public string Name => _item.Name;

        public IBlobItemProperties Properties => _azItemProperties;
    }
}