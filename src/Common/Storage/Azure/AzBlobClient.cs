// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Storage.Azure
{
    public class AzBlobClient : IBlobClient
    {
        private readonly BlobClient _blobClient;

        public AzBlobClient(BlobClient blobClient)
        {
            _blobClient = blobClient;
        }

        public string? Name => _blobClient.Name;

        public Uri Uri => _blobClient.Uri;

        public async Task<BinaryData> DownloadAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return (await _blobClient.DownloadContentAsync(cancellationToken)).Value.Content;
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        public async Task DownloadToAsync(Stream stm, CancellationToken cancellationToken = default)
        {
            try
            {
                await _blobClient.DownloadToAsync(stm, cancellationToken);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        public async Task<bool> ExistsAsync(CancellationToken cancelToken = default)
        {
            try
            {
                return (await _blobClient.ExistsAsync(cancelToken)).Value;
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        public async Task UploadAsync(BinaryData content, CancellationToken cancellationToken)
        {
            try
            {
                await _blobClient.UploadAsync(content, cancellationToken);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        public async Task UploadAsync(BinaryData content, bool overwrite = false, CancellationToken cancellationToken = default)
        {
            try
            {
                await _blobClient.UploadAsync(content, overwrite, cancellationToken);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        public async Task<IBlobProperties> GetPropertiesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return new AzBlobProperties((await _blobClient.GetPropertiesAsync(cancellationToken: cancellationToken)).Value);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        public async Task<bool> DeleteIfExistsAsync(string options, CancellationToken cancellationToken = default)
        {
            try
            {
                var deleteSnapshotsOption = DeleteSnapshotsOption.None;
                if (!string.IsNullOrEmpty(options))
                {
                    deleteSnapshotsOption = Enum.Parse<DeleteSnapshotsOption>(options);
                }
                return (await _blobClient.DeleteIfExistsAsync(deleteSnapshotsOption, cancellationToken: cancellationToken)).Value;
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        public async Task StartCopyFromAsync(IBlobClient sourceBlob, CancellationToken cancellationToken = default)
        {
            try
            {
                await _blobClient.StartCopyFromUriAsync(sourceBlob.Uri, cancellationToken: cancellationToken);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }
    }
}
