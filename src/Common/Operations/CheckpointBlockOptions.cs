// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Storage;

namespace Microsoft.DecisionService.OnlineTrainer.Operations
{
    public sealed class CheckpointBlockOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public string ContainerName { get; set; }

        public IBlobContainerClient ContainerClient { get; set; }

        /// <summary>
        /// Last time the app configuration was edited through the portal
        /// </summary>
        public DateTime LastConfigurationEditDate { get; set; }

        public string AppId { get; set; }

        public ProblemType ProblemType { get; set; }

        public ICheckpointBlockHelper CheckpointBlockHelper { get; set; }
    }
}
