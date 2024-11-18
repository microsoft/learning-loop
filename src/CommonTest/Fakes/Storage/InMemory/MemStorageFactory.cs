// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Storage;
using System;

namespace CommonTest.Fakes.Storage.InMemory
{
    /// <summary>
    /// MemStorageFactory is an in-memory implementation of IStorageFactory.
    /// </summary>
    /// <remarks>
    /// This class is used for testing purposes only and provider a very simple in-memory storage.
    /// While the internal lists may be thread-safe, the class itself is not thread-safe and access
    /// to memory is not thread-safe (it's just enough to test).
    /// 
    /// It is the responsibility of the caller to ensure that tests are coordinated in a way that
    /// does not cause threading issues.  This may be overcome in future versions providing
    /// a thread-safe implementation.
    /// </remarks>
    public class MemStorageFactory : IStorageFactory
    {
        private readonly Uri _uri;

        public MemStorageFactory(Uri uri)
        {
            _uri = uri;
        }

        public IBlobContainerClient CreateBlobContainerClient(string name)
        {
            return CreateMemBlobContainerClient(name);
        }

        public IBlobContainerClient CreateBlobContainerClient(Uri containerUri)
        {
            return CreateMemBlobContainerClient(containerUri);
        }

        public MemBlobContainerClient CreateMemBlobContainerClient(string name)
        {
            var uri = MemUriHelper.AppendUri(_uri, name);
            return CreateMemBlobContainerClient(uri);
        }

        public MemBlobContainerClient CreateMemBlobContainerClient(Uri containerUri)
        {
            var containerUriStr = containerUri.ToString();
            bool readOnly;
            if (containerUriStr.StartsWith(Constants.memStoreSasUri))
            {
                readOnly = containerUriStr.StartsWith(Constants.memStoreSasReadOnlyUri);
            }
            else
            {
                readOnly = containerUriStr.StartsWith(Constants.memStoreReadOnlyUri);
            }
            return new MemBlobContainerClient(containerUri, readOnly, this);
        }
    }
}
