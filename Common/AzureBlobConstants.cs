// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.DecisionService.Common
{
    public static class AzureBlobConstants
    {
        /// <summary>
        /// Exported client model blob name in tenant storage
        /// </summary>
        public const string ClientModelBlobName = "exported-models/current";

        /// <summary>
        /// Exported trainer model blob name in tenant storage
        /// </summary>
        public const string TrainerModelBlobName = "exported-models/currenttrainer";

        /// <summary>
        /// Directory containing invalid client/trainer models
        /// </summary>
        public const string InvalidModelsDirectory = "invalid-models";

        /// <summary>
        /// Suffix for client model blob
        /// </summary>
        public const string ClientModelSuffix = "client.vw";

        /// <summary>
        /// Suffix for trainer model blob
        /// </summary>
        public const string TrainerModelSuffix = "trainer.vw";

        /// <summary>
        /// Suffix for model metadata blob
        /// </summary>
        public const string MetadataSuffix = "metadata.json";

        /// <summary>
        /// blob name for exported client model in system storage
        /// </summary>
        public const string BlobNameForExportedClientModel = "current";

        /// <summary>
        /// Serialized checkpoint blob for ModelCheckpoint.
        /// </summary>
        public const string CheckpointBlobName = "current.dat";

        /// <summary>
        /// blob name for exported client model in system storage
        /// </summary>
        public const string BlobNameForExportedTrainerModel = "currenttrainer";

        /// <summary>
        /// Directory containing the client and trainer model
        /// </summary>
        public const string ExportedModelsDirectory = "exported-models";

        /// <summary>
        /// Directory containing the imported models
        /// </summary>
        public const string ImportedModelsDirectory = "imported-models";

        /// <summary>
        /// Directory containing the configuration for the client app
        /// </summary>
        public const string SettingsDirectory = "settings";

        /// <summary>
        /// Prefix for the directory where cooked logs are stored
        /// </summary>
        public const string CookedLogsDirectoryPrefix = "data";

        /// <summary>
        /// Prefix for the directory where skipped event logs are stored
        /// </summary>
        public const string SkippedLogsDirectoryPrefix = "skipped-data";

        /// <summary>
        /// Prefix for the directory where historical models are stored
        /// </summary>
        public const string HistoricalModelsDirectoryPrefix = "model";

        /// <summary>
        /// Blob where storage upload block updates upload checkpoint.
        /// </summary>
        public const string TenantStorageCheckpointBlobName = "storage-checkpoint.json";

        /// <summary>
        /// Blob where mirror storage upload block updates upload checkpoint.
        /// </summary>
        public const string MirrorStorageCheckpointBlobName = "mirror-storage-checkpoint.json";

        /// <summary>
        /// Blob where billing checkpoint is saved.
        /// </summary>
        public const string BillingCheckpointBlobName = "billingCheckpoint.json";

        /// <summary>
        /// Blob where apprentice mode metrics are saved.
        /// </summary>
        public const string ApprenticeModeMetricsBlobName = "apprenticeModeMetrics.json";

        /// <summary>
        /// Blob where configuration is saved.
        /// </summary>
        public const string ConfigurationJson = "config.json";
    }
}
