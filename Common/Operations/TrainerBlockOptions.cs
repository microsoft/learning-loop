// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Storage;
using Microsoft.DecisionService.Common.Trainer;
using Microsoft.DecisionService.OnlineTrainer.Data;
using System;
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace Microsoft.DecisionService.OnlineTrainer.Operations
{
    public class TrainerBlockOptions
    {
        public byte[] InitialModel { get; set; }
        /// <summary>
        /// Gets or sets the System.Threading.CancellationToken to monitor for cancellation.
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        public ITargetBlock<ModelCheckpoint> ModelCheckpointOutput { get; set; }

        public ITargetBlock<ModelExportEvent> ModelExportOutput { get; set; }

        // Uses a time provider so we can unit test deterministically
        public ITimeProvider TimeProvider { get; set; }

        public string AppId { get; set; }

        public IBlobContainerClient ContainerClient { get; set; }

        public TimeSpan ModelExportFrequency { get; set; }

        public TimeSpan ModelCheckpointFrequency { get; set; }

        public ProblemType ProblemType { get; set; }

        public bool IsLearningMetricsEnabled { get; set; } = false;

        public DateTime LastConfigurationEditDate { get; set; }

        public DateTime LastCheckpointTime { get; set; }

        public IOnlineTrainer OnlineTrainerCmdLine { get; set; }
    }
}
