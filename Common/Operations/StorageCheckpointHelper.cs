// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Error;
using Microsoft.DecisionService.Common.Trainer.Data;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.DecisionService.Common.Storage;

namespace Microsoft.DecisionService.Common.Trainer.Operations
{
    public class StorageCheckpointHelper
    {
        public static async Task<StorageCheckpoint> GetLastStorageCheckpointAsync(IBlobClient storageCheckpointBlob, ILogger appIdLogger, StorageUploadType storageType = StorageUploadType.Tenant)
        {
            StorageCheckpoint uploadCheckpoint = null;
            if (storageCheckpointBlob != null && await storageCheckpointBlob.ExistsAsync())
            {
                try
                {
                    string serializedStorageCheckpoint =
                        (await storageCheckpointBlob.DownloadAsync()).ToString();
                    uploadCheckpoint = JsonConvert.DeserializeObject<StorageCheckpoint>(serializedStorageCheckpoint);
                }
                catch (StorageException se)
                {
                    appIdLogger.LogStorageException(
                        se, "StorageUploadBlock.GetLastStorageCheckpointAsync.StorageException",
                        PersonalizerInternalErrorCode.JoinerCheckpointNotFound.ToString(),
                        storageCheckpointBlob,
                        storageType);
                    appIdLogger.LogInformation("Storage checkpoint download failed. Proceeding without checkpoint.");
                }
                catch (JsonException je)
                {
                    appIdLogger.LogError(je, "{EventKey} {ErrorCode}", "StorageUploadBlock.GetLastStorageCheckpointAsync.JsonException", PersonalizerInternalErrorCode.JoinerCheckpointNotFound.ToString());
                    appIdLogger.LogInformation("Storage checkpoint deserialization failed. Proceeding without checkpoint.");
                }
            }
            return uploadCheckpoint;
        }
    }
}