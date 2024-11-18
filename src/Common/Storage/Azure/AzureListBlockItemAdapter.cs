// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Configuration;
using Azure.Storage.Blobs.Models;
using Microsoft.DecisionService.Common.Storage;

namespace Microsoft.DecisionService.Common.Storage.Azure
{
    public sealed class AzureListBlockItemAdapter : IBlockInfo
    {
        private readonly BlobBlock _blockItem;
        private readonly bool _committed;

        private AzureListBlockItemAdapter(BlobBlock blockItem, long offset, bool committed = true)
        {
            _blockItem = blockItem;
            _committed = committed;
            Offset = offset;
        }

        public long Offset { get; }

        /// <inheritdoc/>
        public string Name => DecodeName(_blockItem.Name);

        /// <inheritdoc/>
        public string EncodedName => _blockItem.Name;

        /// <inheritdoc/>
        public long SizeInBytes => _blockItem.Size;

        public bool IsCommitted => _committed;

        public bool IsUncommitted => !_committed;

        public static AzureListBlockItemAdapter Create(BlobBlock blockItem, long offset = 0, bool committed = true)
        {
            return new AzureListBlockItemAdapter(blockItem, offset, committed);
        }

        /// <summary>
        /// Decode an encoded block name.
        /// </summary>
        /// <param name="blockName">The encoded block name.</param>
        /// <returns>The decoded block name.</returns>
        /// <remarks>
        /// Should be the inverse of the encoding in <see cref="AzureBlockStoreAdapter"/>.
        /// </remarks>
        private static string DecodeName(string blockName)
        {
            return AsciiBase64Converter.Decode(blockName);
        }
    }
}
