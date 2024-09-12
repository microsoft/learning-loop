// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommonTest;
using Microsoft.DecisionService.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.DecisionService.Common.Storage;
using Microsoft.DecisionService.Common.Storage.Azure;

namespace DotNetCore.Tests
{
    [TestClass]
    [TestCategory("integ")]
    [Ignore("These tests use GetUserDelegationKeyAsync(); the correct permissions need to be worked out for them to succeed.")]
    public class StorageUtilitiesTests
    {
        public TestContext TestContext { get; set; }
        private IStorageFactory StorageFactory { get; set; } = null;
        private BlobServiceClient ServiceClient { get; set; } = null;
        private IBlobContainerClient WriteStorageContainer { get; set; } = null;
        private IBlobContainerClient ReadOnlyStorageContainer { get; set; } = null;
        private readonly string e2eStorageUri = "e2eStorageUri";

        [TestInitialize]
        public async Task TestInitializeAsync()
        {
            var cred = new DefaultAzureCredential();
            var uri = new Uri(TestConfiguration.Get(TestContext, e2eStorageUri).ToString());
            ServiceClient ??= new BlobServiceClient(uri, cred);
            StorageFactory ??= new AzStorageFactory(ServiceClient);
            long timestamp = DateTime.UtcNow.Ticks;

            WriteStorageContainer ??= StorageFactory.CreateBlobContainerClient($"evm-rw-{timestamp}");
            await WriteStorageContainer.CreateIfNotExistsAsync();

            ReadOnlyStorageContainer ??= StorageFactory.CreateBlobContainerClient($"evm-r-{timestamp}");
            await ReadOnlyStorageContainer.CreateIfNotExistsAsync();
        }

        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            await WriteStorageContainer.DeleteIfExistsAsync();
            await ReadOnlyStorageContainer.DeleteIfExistsAsync();
        }

        public async Task<IBlobContainerClient> GenerateContainerSasAsync(
            IBlobContainerClient blob,
            DateTimeOffset? start = null, DateTimeOffset? expiry = null,
            BlobContainerSasPermissions permissions = BlobContainerSasPermissions.Read)
        {
            var userDelegationKey = await ServiceClient.GetUserDelegationKeyAsync(DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddMinutes(10));
            var sasBuilder = new BlobSasBuilder(permissions, expiry ?? DateTimeOffset.UtcNow.AddMinutes(10))
            {
                BlobContainerName = blob.Name,
                Resource = "b",
            };
            var blobUriBuilder = new BlobUriBuilder(blob.Uri)
            {
                Sas = sasBuilder.ToSasQueryParameters(
                    userDelegationKey,
                    ServiceClient.AccountName)
            };
            return StorageFactory.CreateBlobContainerClient(blobUriBuilder.ToUri());
        }

        [TestMethod]
        public async Task IsContainerSasUriWritableSucceedsAsync()
        {
            var permissions = BlobContainerSasPermissions.Read | BlobContainerSasPermissions.Write | BlobContainerSasPermissions.List;
            var sasContainer = await GenerateContainerSasAsync(WriteStorageContainer, permissions: permissions);
            await Task.Delay(TimeSpan.FromSeconds(15)); // wait for the sasUri to be active
            bool isWritable = await StorageUtilities.IsContainerWritableAsync(sasContainer, NullLogger.Instance, CancellationToken.None);
            Assert.IsTrue(isWritable, "Expected Container sasUri with write permissions");
        }

        [TestMethod]
        public async Task IsContainerSasUriWritable_FailsForExpiredSasUriAsync()
        {
            var permissions = BlobContainerSasPermissions.Read | BlobContainerSasPermissions.Write | BlobContainerSasPermissions.List;
            var sasContainer = await GenerateContainerSasAsync(WriteStorageContainer, expiry: DateTimeOffset.Now.AddDays(-7), permissions: permissions);
            bool isWritable = await StorageUtilities.IsContainerWritableAsync(sasContainer, NullLogger.Instance, CancellationToken.None);
            Assert.IsFalse(isWritable, "Expected Container sasUri should have expired.");
        }

        [TestMethod]
        public async Task IsContainerSasUriWritableFailsWithReadOnlyPermissionsAsync()
        {
            var permissions = BlobContainerSasPermissions.Read | BlobContainerSasPermissions.List;
            var sasContainer = await GenerateContainerSasAsync(WriteStorageContainer, permissions: permissions);
            bool isWritable = await StorageUtilities.IsContainerWritableAsync(sasContainer, NullLogger.Instance, CancellationToken.None);
            Assert.IsFalse(isWritable, "Expected Container sasUri without write permissions");
        }

        [TestMethod]
        public void ValidateSasUriDateFormat_Valid()
        {
            // Prepare
            var uriBuilder = new UriBuilder("https://test.blob.core.windows.net/test");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters["st"] = "2021-11-24T17:31:48Z";
            parameters["se"] = "2021-12-10T01:31:48Z";
            uriBuilder.Query = parameters.ToString();

            var sasUrl = uriBuilder.Uri;

            // Act & Assert
            Assert.IsTrue(StorageUtilities.ValidateSasUriDateFormat(sasUrl), $"the input URL is {sasUrl}");
        }

        [TestMethod]
        [DataRow("st")]
        [DataRow("se")]
        public void ValidateSasUriDateFormat_InvalidDate(string queryParamKey)
        {
            // Prepare
            var uriBuilder = new UriBuilder("https://test.blob.core.windows.net/test");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters[queryParamKey] = "2021-11-24";
            uriBuilder.Query = parameters.ToString();

            var sasUrl = uriBuilder.Uri;

            // Act & Assert
            Assert.IsFalse(StorageUtilities.ValidateSasUriDateFormat(sasUrl), $"the input URL is {sasUrl}");
        }
    }
}
