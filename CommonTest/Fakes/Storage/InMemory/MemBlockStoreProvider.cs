// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Storage;

namespace CommonTest.Fakes.Storage.InMemory
{
    /// <summary>
    /// MemBlockStoreProvider is an in-memory implementation of IBlockStoreProvider.
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
    public class MemBlockStoreProvider : IBlockStoreProvider
    {
        private readonly MemBlobContainerClient _containerClient;

        public MemBlockStoreProvider(MemBlobContainerClient containerClient)
        {
            _containerClient = containerClient;
        }

        public IBlockStore GetStore(string name)
        {
            return _containerClient.GetBlockBlobClient(name);
        }
    }
}
