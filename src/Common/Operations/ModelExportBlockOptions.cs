// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using System.Threading;
using Microsoft.DecisionService.Common.Storage;

namespace Microsoft.DecisionService.OnlineTrainer.Operations
{
    public class ModelExportBlockOptions
    {
        public IBlobContainerClient ContainerClient { get; set; }

        public CancellationToken CancellationToken { get; set; }

        public string AppId { get; set; }

        public ProblemType ProblemType { get; set; }

        public string ContainerName { get; set; }

        public bool ModelAutoPublish { get; set; }

        public int StagedModelHistoryLength { get; set; }
    }
}
