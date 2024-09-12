// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Storage
{
    public interface IBlobLeaseHolder : IDisposable
    {
        public Task Completion { get; }
    }
}
