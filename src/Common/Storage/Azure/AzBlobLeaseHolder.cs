// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;

namespace Microsoft.DecisionService.Common.Storage.Azure
{
    public sealed class AzBlobLeaseHolder : IBlobLeaseHolder, IDisposable
    {
        // Set this to true when debugging to avoid acquiring a lease. That way
        // you don't need to wait for the lease to expire when rerunning
        const bool DebugIgnoreLease = false;
        private readonly CancellationTokenSource _internalTokenSource;
        public Task Completion { get; private set; }

        private AzBlobLeaseHolder(CancellationTokenSource internalTokenSource, Task completion)
        {
            _internalTokenSource = internalTokenSource;
            Completion = completion;
        }

        public static async Task<AzBlobLeaseHolder> AcquireBlobLeaseV2Async(
            BlobContainerClient containerClient, string appId, string lockBlobName,
            DateTime lastConfigEditDate, ILogger logger, CancellationToken outerCancellationToken)
        {
            var internalTokenSource = new CancellationTokenSource();
            var cancellationToken = CancellationTokenSource
                .CreateLinkedTokenSource(internalTokenSource.Token, outerCancellationToken).Token;

            if (DebugIgnoreLease)
            {
#pragma warning disable CS0162 // Unreachable code detected
                return new AzBlobLeaseHolder(internalTokenSource, Task.CompletedTask);
#pragma warning restore CS0162 // Unreachable code detected
            }

            var blobClient = containerClient.GetBlobClient(lockBlobName);
            BlobLease leaseId = null;
            while (!cancellationToken.IsCancellationRequested && leaseId == null)
            {
                try
                {
                    if (!await blobClient.ExistsAsync(cancellationToken))
                    {
                        await blobClient.UploadAsync(
                            BinaryData.FromString($"{Environment.MachineName} acquired lease at {DateTime.UtcNow} UTC")
                            , cancellationToken);
                    }

                    var blobLeaseClient = blobClient.GetBlobLeaseClient();
                    leaseId = await blobLeaseClient.AcquireAsync(
                        duration: TimeSpan.FromMinutes(1),
                        cancellationToken: cancellationToken);

                    logger?.LogInformation(
                        $"TrainerLockPipeline started with LastConfigEditDate:{lastConfigEditDate} with lock on {lockBlobName}");
                }
                catch (RequestFailedException ex)
                {
                    if (ex.ErrorCode == BlobErrorCode.LeaseAlreadyPresent)
                    {
                        logger?.LogInformation(
                            $"TrainerLockPipeline.LeaseAlreadyPresent. LastConfigEditDate:{lastConfigEditDate} with lock on {lockBlobName}");
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                    else
                    {
                        logger?.LogError(ex, "Lease could not be acquired. LeaseName={LeaseName} AppId={AppId}",
                            lockBlobName, appId);
                        throw;
                    }
                }
            }

            var completion = RenewLeaseAsync(leaseId, blobClient, logger, cancellationToken)
                .ContinueWith(prev =>
                {
                    var leaseClient = blobClient.GetBlobLeaseClient(prev.Result.LeaseId);
                    leaseClient.Release();
                    logger?.LogInformation("Lock released");
                }, TaskScheduler.Default);

            return new AzBlobLeaseHolder(internalTokenSource, completion);
        }

        private static async Task<BlobLease> RenewLeaseAsync(BlobLease existingLease, BlobClient blobClient,
            ILogger logger, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                    var leaseClient = blobClient.GetBlobLeaseClient(existingLease.LeaseId);
                    existingLease = await leaseClient.RenewAsync(cancellationToken: cancellationToken);
                    BlobUploadOptions blobUploadOptions = new BlobUploadOptions()
                    {
                        Conditions = new BlobRequestConditions()
                        {
                            LeaseId = existingLease.LeaseId
                        }
                    };

                    await blobClient.UploadAsync(
                        BinaryData.FromString($"{Environment.MachineName} renewed lease at {DateTime.UtcNow} UTC"),
                        blobUploadOptions, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    logger?.LogError(message: $"Cancellation requested. Exiting RenewLeaseAsync");
                    return existingLease;
                }
                catch (RequestFailedException ex)
                {
                    logger?.LogError(ex, "TrainerLockPipeline.RenewLeaseAsync.Exit");

                    if (BlobErrorCode.LeaseIdMismatchWithLeaseOperation.Equals(ex.ErrorCode))
                    {
                        logger?.LogError(ex,
                            "TrainerLockPipeline.RenewLeaseAsync.LeaseIdMismatchWithLeaseOperation.Exit");
                        throw; // Throw if lease has been acquired by some other trainer
                    }
                }
            }

            return existingLease;
        }

        public void Dispose()
        {
            _internalTokenSource.Cancel();
        }
    }
}