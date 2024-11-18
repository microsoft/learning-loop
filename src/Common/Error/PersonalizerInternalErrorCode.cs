// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;

namespace Microsoft.DecisionService.Common.Error
{
    public enum PersonalizerInternalErrorCode
    {
        // All errors/failures are caught and ignored unless otherwise stated.

        // FrontEnd
        [Description("Front end exception in creating eventhub client")]
        FrontEndEventHubCreateClientError,

        [Description("Front end exception in writing to eventhub")]
        FrontEndEventHubWriteError,

        // OnlineTrainer
        [Description("Failure in OnlineTrainerHostService RunAsync Method.Exits OnlineTrainer")]
        OnlineTrainerFailure,
        [Description("Failure in OnlineTrainerHostService StopAsync Method.Exits OnlineTrainer")]
        OnlineTrainerCloseError,

        // AutoOptimization
        [Description("Failure in AutoOptimizationHostService RunAsync Method.Exits AutoOptimization")]
        AutoOptimizationFailure,
        [Description("Failure in AutoOptimizationHostService StopAsync Method.Exits AutoOptimization")]
        AutoOptimizationCloseError,

        // Joiner
        [Description("Terminal exception in LeftOuterJoinBlock/EventMergeSortBlock.Exits Joiner/OnlineTrainer")]
        JoinerExecutionFailure,
        [Description("Joiner cannot find/parse storage checkpoint")]
        JoinerCheckpointNotFound,
        [Description("Joiner cannot connect to EventHub")]
        JoinerEventHubConnectionFailure,
        [Description("Joiner cannot deserialize EventHub messages")]
        JoinerEventDeserializationError,
        [Description("Joiner cannot serialize interaction events to write to cooked logs")]
        JoinerEventSerializationError,
        [Description("Joiner exception in uploading to cooked logs")]
        JoinerStorageUploadFailure,
        [Description("Interaction learning mode and trainer learning mode don't match")]
        JoinerLearningModeMismatchError,
        [Description("Interaction event type and trainer problem type don't match")]
        JoinerProblemTypeMismatchError,

        // Trainer
        [Description("Trainer cannot be started.Crashes Trainer")]
        TrainerSetupFailure,
        [Description("Trainer execution has faulted.Crashes Trainer")]
        TrainerExecutionFailure,
        [Description("Trainer failure in event deserialization/learning")]
        TrainerEventTrainingError,
        [Description("Trainer exception in uploading/downloading checkpoints")]
        TrainerCheckpointFailure,
        [Description("Trainer exception in sending null interaction event")]
        TrainerSendNullInteractionFailure,
        [Description("Trainer exception in processing learning mode metrics")]
        TrainerLearningModeFailure,
        [Description("Trainer exception in triggering auto optimization job")]
        AutoOptimizationJobTriggerFailure,
        [Description("Trainer exception in monitoring auto optimization job")]
        AutoOptimizationJobMonitorFailure,
        [Description("Trainer exception in processing learning metrics")]
        LearningMetricsFailure,

        // ModelExport
        [Description("ModelExport cannot be setup. Crashes ModelExport")]
        ModelExportSetupFailure,
        [Description("ModelExport execution has faulted. Crashes ModelExport")]
        ModelExportExecutionFailure,
        [Description("ModelExport error in uploading models")]
        ModelExportUploadError,

        // Billing
        [Description("Billing could not be setup. Crashes Joiner/OnlineTrainer")]
        BillingSetupFailure,
        [Description("Billing checkpoint could not be updated")]
        BillingCheckpointFailure,

        // LogRetentionPipeline
        [Description("Log retention pipeline has faulted")]
        LogRetentionPipelineFailure,

        // TrainerLockPipeline
        [Description("Trainer lock pipeline failed to renew lease. Crashes OnlineTrainer")]
        TrainerLockPipelineLeaseRenewalFailure,

        // TrainingMonitoringPipeline
        [Description("Training monitoring pipeline has faulted")]
        TrainingMonitoringPipelineFailure,

        // Evaluations
        [Description("Evaluation job failed")]
        EvaluationJobFailure,

        // ResourceDeployer
        [Description("ResourceCleanup job failed")]
        ResourceCleanupJobFailure,

        // LockPipeline
        [Description("Lock pipeline failed to renew lease. Crashes the process")]
        LockPipelineLeaseRenewalFailure,

        //PersonalizationConfigHelper
        [Description("Failure in PersonalizationConfigHelper. Unexpected data formats when parsing config table")]
        PersonalizationConfigHelperDataConfigTableError,
            
        [Description("Failed to purge older evaluations in auto optimization job")]
        AutoOptimizationPurgeOldEvaluationsFailure,
    }
}
