// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Storage
{
    public interface IBlobClient
    {
        string? Name { get; }
        Uri Uri { get; }
        Task<bool> ExistsAsync(CancellationToken cancelToken = default);
        Task UploadAsync(BinaryData content, CancellationToken cancellationToken);
        Task UploadAsync(BinaryData content, bool overwrite = false, CancellationToken cancellationToken = default);
        Task<BinaryData> DownloadAsync(CancellationToken cancellationToken = default);
        Task DownloadToAsync(Stream stm, CancellationToken cancellationToken = default);
        Task<IBlobProperties> GetPropertiesAsync(CancellationToken cancellationToken = default);
        Task<bool> DeleteIfExistsAsync(string options, CancellationToken cancellationToken = default);
        Task StartCopyFromAsync(IBlobClient sourceBlob, CancellationToken cancellationToken = default);
    }
}
