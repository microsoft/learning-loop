// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Storage;
using System.Threading.Tasks;

namespace CommonTest.Fakes.Storage.InMemory
{
    /// <summary>
    /// MemBlobLeaseHolder is an in-memory implementation of IBlobLeaseHolder that
    /// does nothing but satisfy the interface for testing.
    /// </summary>
    public class MemBlobLeaseHolder : IBlobLeaseHolder
    {
        public MemBlobLeaseHolder(Task completion)
        {
            Completion = completion;
        }

        public Task Completion { get; private set; }

        public void Dispose()
        {
        }
    }
}
