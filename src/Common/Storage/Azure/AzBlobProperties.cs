// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Storage.Blobs.Models;
using System;

namespace Microsoft.DecisionService.Common.Storage.Azure
{
    public class AzBlobProperties : IBlobProperties
    {
        private readonly BlobProperties _properites;

        public AzBlobProperties(BlobProperties properties)
        {
            _properites = properties ?? throw new ArgumentNullException(nameof(properties));
        }

        public long ContentLength => _properites.ContentLength;

        public DateTimeOffset LastModified => _properites.LastModified;

        public DateTimeOffset CreatedOn => _properites.CreatedOn;
    }
}
