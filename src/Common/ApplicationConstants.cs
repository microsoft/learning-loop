// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.DecisionService.Common
{
    public static class ApplicationConstants
    {
        /// <summary>
        /// Default learning policy
        /// </summary>

        // Learning policy used by rlclient to initialize vw (CB + Large action spaces) when the model is not available.
        public static readonly string CBLargeActionSpaceInitialCommandLine = "--cb_explore_adf --json --quiet --epsilon 1.0 --first_only --id N/A --large_action_space";

        // Learning policy used by rlclient to initialize vw (CCB + Large action spaces) when the model is not available.
        public static readonly string CCBLargeActionSpaceInitialCommandLine = "--ccb_explore_adf --json --quiet --epsilon 1.0 --first_only --id N/A --large_action_space";

        // Learning policy used by rlclient to initialize vw (CB) when the model is not available.
        public static readonly string CBInitialCommandLine = "--cb_explore_adf --json --quiet --epsilon 1.0 --first_only --id N/A";

        // MachineLearningArguments for a new loop (which is CB by default) are set to below learning policy. Deleting policy (CB mode) defaults to below.
        public static readonly string CBDefaultLearningPolicy = "--cb_explore_adf --epsilon 0.2 --power_t 0 -l 0.001 --cb_type mtr -q ::";

        // MachineLearningArguments and DecoderArguments for an IGL experiment loop
        public static readonly string IGLDefaultLearningPolicy = "--cb_explore_adf --quiet -q Fi -q Fj --coin;--quiet --link=logistic --loss_function=logistic --cubic Fiv --cubic Fjv --coin;--label_negative";

        // Learning policy used by rlclient to initialize vw (CCB) when the model is not available.
        public static readonly string CCBInitialCommandLine = "--ccb_explore_adf --json --quiet --epsilon 1.0 --first_only --id N/A";

        // Deleting policy (CCB) defaults to below.
        public static readonly string CCBDefaultLearningPolicy = "--ccb_explore_adf --epsilon 0.2 --power_t 0 -l 0.001 --cb_type mtr -q ::";

        // Note that CA is not implemented on the front end. Initial command line and default learning policy for CA should be revisited before enabling this feature.
        // Learning policy used by rlclient to initialize vw (CA) when the model is not available.
        public static readonly string CAInitialCommandLine = "--cats 4 --min_value=185 --max_value=23959 --bandwidth 1 --coin --loss_option 1 --json --quiet --epsilon 0.1 --id N/A";

        // Deleting policy (CATS) defaults to below.
        public static readonly string CADefaultLearningPolicy = "--cats 4 --min_value 0 --max_value 100 --bandwidth 1 --epsilon 0.2 --power_t 0 -l 0.001 -q ::";

        public static readonly int V1ProtocolVersion = 1;

        public static readonly int V2ProtocolVersion = 2;

        public static readonly int ApprenticeBaselineAction = 1;

        public static readonly int ApprenticeMetricsBatchSize = 1000;

        /// <summary>
        /// String format used to stringify DateTimes
        /// </summary>
        public static readonly string DateTimeStringFormat = "yyyyMMddHHmmss";

        public static readonly double Tolerance = 0.0001D;

        public static readonly float InitialExplorationEpsilon = 1.0f;

        public static readonly string TableStorageConfig = "TableStorageConfig";

        public static readonly string ApplicationIdTag = "InternalId";

        public static readonly string ResourceIdTagName = "PersonalizerResourceId";

        public static readonly string MarkForDeletionTag = "MarkForDeletion";


        public static readonly string KeyVaultResourceType = "Microsoft.KeyVault/vaults";

        public static readonly string ServiceBusFrontendTopic = "DecisionServiceWebApp";

        public static readonly string ServiceBusOnlineTrainerTopic = "OnlineTrainer";

        public static readonly string ServiceBusAutoOptimizationTopic = "AutoOptimization";

        public static readonly int DeploymentNameMaxLength = 64;

        /// <summary>
        /// Content encoding
        /// </summary>

        public static readonly string DedupBatch = "DEDUP";
        public static readonly string DictionaryEventId = "3defd95a-0122-4aac-9068-0b9ac30b66d8";
        public static readonly string IdentityBatch = "IDENTITY";

        public static readonly TimeSpan DefaultAutoOptimizationFrequency = TimeSpan.FromDays(28);
        public static readonly TimeSpan DefaultAutoOptimizationInitialStartPeriod = TimeSpan.FromDays(15);
        public static readonly TimeSpan DefaultModelExportFrequency = TimeSpan.FromMinutes(5);
        public static readonly float DefaultReward = 0;
        public static readonly TimeSpan DefaultExperimentalUnitDuration = TimeSpan.FromMinutes(10);
        public static readonly bool DefaultIsAutoOptimizationEnabled = true;
        public static readonly int DefaultLogRetentionDays = 90;
        public static readonly int DefaultModelRetrainDays = 2;
        public static readonly bool DefaultNeedsRetrain = false;

        // This is a pessimistic upper bound guess on BinaryLogBuilder.HEADER_SIZE_GUESTIMATE (currently 208)
        public const int BinaryLogMaxBatchHeaderSize = 1024;

        /// <summary>
        /// VW Exe Retry limits
        /// </summary>
        public const int VwExeIoRetries = 3;
        public const float VwExeIoRetryDelay = 0.1f; // Retry delay in seconds

        /// <summary>
        /// VW Exe args to get trainer model and prediction models
        /// </summary>
        public const string trainerModelArgs = " --save_resume --preserve_performance_counters";
        public const string predictionModelArgs = " --predict_only_model";
        
        public const string JoinerName = "Joiner";
    }
}
