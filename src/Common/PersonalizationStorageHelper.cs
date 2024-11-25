// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DecisionService.Common.Storage;

namespace Microsoft.DecisionService.Common
{
    public static class PersonalizationStorageHelper
    {
        private static readonly Regex IsModelBlobRegex = new($"^[0-9]+/{AzureBlobConstants.HistoricalModelsDirectoryPrefix}/.*");
        private static readonly Regex IsCookedDataBlobRegex = new($"^[0-9]+/{AzureBlobConstants.CookedLogsDirectoryPrefix}/.*");
        private static readonly Regex ConfigurationFolderNameRegex = new("^[0-9]+$");

        public static async Task<IEnumerable<IBlobItem>> GetAllCookedLogsBlobsAsync(
            IBlobContainerClient appContainer,
            CancellationToken cancellationToken,
            bool includeStorageCheckpoint = false)
        {
            // if deleting cooked log, storage-checkpoint.json must be deleted as well to avoid
            // com.microsoft.azure.storage.StorageException: The specified block list is invalid.
            return (await GetAllDataAndModelBlobsAsync(appContainer, cancellationToken))
                .Where(b => IsCookedDataBlob(b) || (IsStorageCheckpointBlob(b) && includeStorageCheckpoint));
        }

        public static async Task<IEnumerable<IBlobItem>> GetAllModelBlobsAsync(
            IBlobContainerClient appContainer,
            CancellationToken cancellationToken)
        {
            return (await GetAllDataAndModelBlobsAsync(appContainer, cancellationToken))
                .Where(PersonalizationStorageHelper.IsModelBlob);
        }


        public static async Task<IEnumerable<IBlobItem>> GetAllDataAndModelBlobsAsync(
            IBlobContainerClient appContainer,
            CancellationToken cancellationToken)
        {
            var directories = await StorageUtilities.ListDirectoriesAsync(appContainer);
            var blobs = new List<IBlobItem>();
            foreach (var directory in directories)
            {
                if (directory.ToString().Contains(AzureBlobConstants.ExportedModelsDirectory) ||
                    directory.ToString().Contains(AzureBlobConstants.SettingsDirectory))
                {
                    continue;
                }

                if (TryGetConfigurationDate(directory, out string configurationDate))
                {
                    blobs.AddRange(await appContainer.GetBlobsAsync(configurationDate, cancellationToken));
                }
            }

            // Remove duplicates
            blobs = blobs.GroupBy(b => b.Name).Select(g => g.First()).ToList();

            return blobs;
        }

        public static async Task<IList<string>> DeleteExpiredBlobsAsync(
            IBlobContainerClient containerClient,
            string appId,
            double retentionDays,
            DateTime today,
            CancellationToken cancellationToken)
        {
            DateTime cutOffDateTime = DateTimeOpsInputValidation.SafeSubtractDays(today, retentionDays);
            return await DeleteDataAndModelBlobsAsync(containerClient, cutOffDateTime, cancellationToken,
                includeStorageCheckpoint: false);
        }

        /// <summary>
        /// Returns the size of blobs that exist from start day and index (inclusive) to end day and index (inclusive)
        /// </summary>
        /// <returns>Size of the qualifying blobs in bytes</returns>
        public static async Task<long> GetOverlappingCookedLogsSizeAsync(
            IBlobContainerClient appContainer,
            DateTime startDay,
            DateTime endDay,
            CancellationToken cancellationToken = default,
            int startIndex = 0,
            int endIndex = int.MaxValue
        )
        {
            try
            {
                Dictionary<string, bool> configurations =
                    await GetConfigurationsAsync(appContainer, cancellationToken);
                var blobs = await GetAllCookedLogsBlobsAsync(appContainer, cancellationToken);

                return GetOverlappingBlobSize(blobs, startDay, endDay, startIndex, endIndex);
            }
            catch (StorageException e)
            {
                if (e?.InnerException is OperationCanceledException)
                    throw e.InnerException;
                else
                    throw;
            }
        }

        public static async Task<IList<string>> DeleteDataAndModelBlobsAsync(
            IBlobContainerClient appContainer,
            DateTime? cutOffDateTime,
            CancellationToken cancellationToken,
            bool includeStorageCheckpoint = false)
        {
            var blobs =
                (await GetAllModelBlobsAsync(appContainer, cancellationToken))
                .Union(await GetAllCookedLogsBlobsAsync(appContainer, cancellationToken,
                    includeStorageCheckpoint)).ToList();

            bool shouldBlobBeDeleted(IBlobClient blob)
            {
                if (!cutOffDateTime.HasValue)
                {
                    return true;
                }
                else
                {
                    DateTime partitionDateTime = ExtractUtcDateTimeFromBlobUri($"/{blob.Name}");
                    return partitionDateTime < cutOffDateTime.GetValueOrDefault();
                }
            }

            var deletedBlobs = new List<string>();
            foreach (var blob in blobs)
            {
                if (shouldBlobBeDeleted(appContainer.GetBlobClient(blob.Name)))
                {
                    deletedBlobs.Add(blob.Name);
                    await appContainer.DeleteBlobAsync(blob.Name, cancellationToken);
                }
            }

            return deletedBlobs;
        }
        //
        // public static async Task<LogsProperties> GetCookedLogsPropertiesAsync(
        //     CloudBlobContainer appContainer,
        //     OperationContext operationContext,
        //     CancellationToken cancellationToken)
        // {
        //     LogsProperties properties = new LogsProperties();
        //     IOrderedEnumerable<ICloudBlob> cookedLogblobs =
        //         (await PersonalizationStorageHelper.GetAllCookedLogsBlobsAsync(appContainer, operationContext,
        //             cancellationToken))
        //         .OrderBy(b => b.Name);
        //     if (cookedLogblobs.Count() == 0)
        //     {
        //         return properties;
        //     }
        //
        //     DateRange dateRange = new DateRange();
        //     string fromBlob = cookedLogblobs.First().Uri.ToString();
        //     DateTime from = ExtractUtcDateTimeFromBlobUri(fromBlob);
        //     dateRange.From = from;
        //
        //     string toBlob = cookedLogblobs.Last().Uri.ToString();
        //     DateTime to = ExtractUtcDateTimeFromBlobUri(toBlob);
        //     dateRange.To = to;
        //
        //     properties.DateRange = dateRange;
        //     return properties;
        // }


        private static bool IsModelBlob(IBlobItem blob)
        {
            return IsModelBlobRegex.IsMatch(blob.Name);
        }

        private static bool IsCookedDataBlob(IBlobItem blob)
        {
            return IsCookedDataBlobRegex.IsMatch(blob.Name);
        }

        private static bool IsStorageCheckpointBlob(IBlobItem blob)
        {
            return blob.Name.Contains(AzureBlobConstants.TenantStorageCheckpointBlobName);
        }

        private static long GetOverlappingBlobSize(IEnumerable<IBlobItem> blobs, DateTime startDay, DateTime endDay,
            int startIndex = 0, int endindex = int.MaxValue)
        {
            long sizeInBytes = 0;
            if (blobs.Count() != 0)
            {
                startDay = new DateTime(startDay.Year, startDay.Month, startDay.Day, 0, 0, 0, DateTimeKind.Utc);
                endDay = new DateTime(endDay.Year, endDay.Month, endDay.Day, 0, 0, 0, DateTimeKind.Utc);

                foreach (var blob in blobs)
                {
                    PathHelper.ParseIndexAndDate(blob.Name, out int blobIndex, out int _,
                        out DateTime cookedLogDate);
                    if (cookedLogDate < startDay ||
                        cookedLogDate > endDay ||
                        cookedLogDate == startDay && blobIndex < startIndex ||
                        cookedLogDate == endDay && blobIndex > endindex)
                        continue;

                    sizeInBytes += blob.Properties.ContentLength;
                }
            }

            return sizeInBytes;
        }

        private static async Task<Dictionary<string, bool>> GetConfigurationsAsync(
            IBlobContainerClient appContainer,
            CancellationToken cancellationToken)
        {
            var configurations = new Dictionary<string, bool>();
            var directories = await StorageUtilities.ListDirectoriesAsync(appContainer);
            foreach (var directory in directories)
            {
                if (TryGetConfigurationDate(directory, out string configurationDate))
                {
                    bool isImitiationConfiguration = await appContainer.GetBlobClient(PathHelper.BuildCheckpointName(
                            dateFolder: configurationDate,
                            blobName: AzureBlobConstants.ApprenticeModeMetricsBlobName))
                        .ExistsAsync(cancellationToken);
                    configurations.Add(configurationDate, isImitiationConfiguration);
                }
            }
            return configurations;
        }

        private static bool TryGetConfigurationDate(string blobPath, out string configurationDate)
        {
            configurationDate = string.Empty;
            var selection = blobPath.Trim('/');
            if (ConfigurationFolderNameRegex.IsMatch(selection))
            {
                configurationDate = selection;
                return true;
            }
            return false;
        }

         private static DateTime ExtractUtcDateTimeFromBlobUri(string blobUri)
        {
            PathHelper.ParseIndexAndDate(blobUri, out int _, out int hour, out DateTime date);
            return date.AddHours(hour);
        }
    }
}