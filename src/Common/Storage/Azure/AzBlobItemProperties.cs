// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Storage.Blobs.Models;

namespace Microsoft.DecisionService.Common.Storage.Azure
{
    public class AzBlobItemProperties : IBlobItemProperties
    {
        private readonly BlobItemProperties _properties;

        public AzBlobItemProperties(BlobItemProperties properties)
        {
            _properties = properties;
        }

        public long ContentLength => _properties.ContentLength ?? 0;
    }
}
