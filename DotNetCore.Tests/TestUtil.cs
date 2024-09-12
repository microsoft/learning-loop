// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Storage.Blob;
using Microsoft.DecisionService.OnlineTrainer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

namespace DotNetCore.Tests
{
    public static class TestUtil
    {
        public const string aModelId = "a-model-id";

        public static CloudBlockBlob GetAzureBlockBlobReference(string sasUri, string blobName)
        {
            // Retrieve reference to a previously created container
            CloudBlobContainer clientCloudContainer = new CloudBlobContainer(new Uri(sasUri));

            // Retrieve reference to a blob
            return clientCloudContainer.GetBlockBlobReference(blobName);
        }


        public static TrainerConfig GetDefaultTrainerOptions(TestContext context, string appId = null,
            DateTime lastConfigurationEditDate = default(DateTime),
            CancellationTokenSource cancellationTokenSource = default(CancellationTokenSource))
        {
            return new TrainerConfig()
            {
                AppId = appId ?? "defaultappId",
                LastConfigurationEditDate = lastConfigurationEditDate.Equals(default(DateTime))
                    ? DateTime.UtcNow
                    : lastConfigurationEditDate,
                WarmstartStartDateTime = DateTime.UtcNow, // reading start date in the event hub
                ModelCheckpointFrequency = TimeSpan.FromMinutes(1),
            };
        }
    }
}