// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common
{
    public interface ICheckpointBlockHelper
    {
        Task<ModelCheckpoint> GetCheckpointAsync(DateTime configurationDate, CancellationToken cancellationToken);
        Task SaveCheckpointAsync(ModelCheckpoint checkpoint, DateTime configurationDate, CancellationToken cancellationToken);
    }
}