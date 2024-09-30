// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Data;
using System;

namespace Microsoft.DecisionService.OnlineTrainer.Operations
{
    public class StorageBlockOptions
    {
        /// <summary>
        /// Blob API limit: https://docs.microsoft.com/en-us/rest/api/storageservices/understanding-block-blobs--append-blobs--and-page-blobs#about-block-blobs
        /// Modifiable for unit tests only
        /// </summary>
        public int AzureStorageMaxBlockSizeLimitsInByte { get; set; } = 100 * 1024 * 1024;

        /// <summary>
        /// We set the max blob size limit to make it easy to view cooked log.
        /// </summary>
        public int AzureStorageMaxBlobSizeLimitsInByte { get; set; } = 256 * 1024 * 1024;

        /// <summary>
        /// Blob API limit: https://docs.microsoft.com/en-us/rest/api/storageservices/understanding-block-blobs--append-blobs--and-page-blobs#about-block-blobs
        /// Modifiable for unit tests only
        /// </summary>
        public int MaximumBlocksNumber { get; set; } = 50000;

        /// <summary>
        /// Last time the app configuration was edited through the portal
        /// </summary>
        public DateTime LastConfigurationEditDate { get; set; }

        public TimeSpan MaximumFlushLatency { get; set; }

        public string AppId { get; set; }

        /// <summary>
        /// Upload cooked logs with skipped/not-learnable events to storage.
        /// These logs are used for diagnostics.
        /// </summary>
        public bool UploadSkippedLogs { get; set; } = false;

        /// <summary>
        /// The block buffer capacity used when processing a list of events. This number should be small, either 1 or 2 as it may hold 100MB for each block
        /// </summary>
        public int BlockBufferCapacity { get; set; } = 1;

        /// <summary>
        /// Update the blob index on the hour.
        /// </summary>
        public bool HourlyIndexIncrement { get; internal set; } = false;

        public float? DefaultReward { get; set; } = 0;

        public RewardFunction RewardFunction { get; set; }
    }
}