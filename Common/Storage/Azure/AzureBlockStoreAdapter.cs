// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace Microsoft.DecisionService.Common.Storage.Azure
{
    /// <summary>
    /// Adapter for using Azure Blob Storage as a block store.
    /// </summary>
    public sealed class AzureBlockStoreAdapter : IBlockStore
    {
        private readonly BlockBlobClient blob;

        private AzureBlockStoreAdapter(BlockBlobClient blob)
        {
            this.blob = blob;
        }

        /// <inheritdoc/>
        public string Name => blob.Name;

        /// <inheritdoc/>
        public int MaxBlockSizeInBytes => blob.BlockBlobMaxStageBlockBytes;

        /// <inheritdoc/>
        public int MinBlockSizeInBytes => 1;

        public static AzureBlockStoreAdapter CreateWithName(BlobContainerClient container, string name)
        {
            return CreateWithBlob(container.GetBlockBlobClient(name));
        }

        public static AzureBlockStoreAdapter CreateWithBlob(BlockBlobClient blob)
        {
            return new AzureBlockStoreAdapter(blob);
        }

        /// <inheritdoc/>
        public async Task<bool> ExistsAsync(CancellationToken cancelToken)
        {
            try
            {
                return await blob.ExistsAsync(cancelToken);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        /// <inheritdoc/>
        public async Task WriteBlockAsync(string blockName, Stream readStream, CancellationToken cancellationToken)
        {
            if (readStream.Length < MinBlockSizeInBytes)
            {
                throw new ArgumentOutOfRangeException("readStream", $"readStream.Length = {readStream.Length} is smaller than MinBlockSizeInBytes = {MinBlockSizeInBytes}");
            }

            if (readStream.Length > MaxBlockSizeInBytes)
            {
                throw new ArgumentOutOfRangeException("readStream", $"readStream.Length = {readStream.Length} is larger than MaxBlockSizeInBytes = {MaxBlockSizeInBytes}");
            }

            try
            {
                await blob.StageBlockAsync(EncodeBlockId(blockName), readStream, cancellationToken: cancellationToken);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
            catch (AuthenticationFailedException e)
            {
                throw new StorageException(e.Message, e);
            }
        }

        public async Task WriteAsync(Stream readStream, CancellationToken cancellationToken)
        {
            try
            {
                await blob.UploadAsync(readStream, cancellationToken: cancellationToken);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
            catch (AuthenticationFailedException e)
            {
                throw new StorageException(e.Message, e);
            }
        }

        /// <inheritdoc/>
        public async Task CommitBlocksAsync(IEnumerable<string> blockIds, CancellationToken cancellationToken = default)
        {
            var encodedBlockIds = blockIds.Select(name => EncodeBlockId(name));
            try
            {
                await blob.CommitBlockListAsync(encodedBlockIds, cancellationToken: cancellationToken);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        private void AddBlockToBlockList(List<IBlockInfo> blockItems, IEnumerable<BlobBlock> listBlockItems, ref long offset, bool committed)
        {
            if (listBlockItems == null)
            {
                return;
            }
            foreach (var block in listBlockItems)
            {
                blockItems.Add(GetBlockInfo(block, ref offset, committed));
            }
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<IBlockInfo>> GetBlockInfoListAsync(string blockListType = "Committed", CancellationToken cancelToken = default)
        {
            if (!Enum.TryParse<BlockListTypes>(blockListType, out var blockListTypeEnum))
            {
                blockListTypeEnum = BlockListTypes.Committed;
            }
            var blockItems = new List<IBlockInfo>();
            try
            {
                var listBlockItems = (await blob.GetBlockListAsync(blockListTypeEnum, cancellationToken: cancelToken)).Value;
                if (listBlockItems != null)
                {
                    long offset = 0;
                    blockItems = new List<IBlockInfo>();
                    AddBlockToBlockList(blockItems, listBlockItems.CommittedBlocks, ref offset, true);
                    AddBlockToBlockList(blockItems, listBlockItems.UncommittedBlocks, ref offset, false);
                }
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
            return blockItems;
        }

        /// <inheritdoc/>
        public async Task ReadBlockAsync(IBlockInfo block, Stream writeStream, CancellationToken cancellationToken)
        {
            AzureListBlockItemAdapter azureBlock = block as AzureListBlockItemAdapter;
            if (azureBlock == null)
            {
                throw new ArgumentException($"block is not runtime type {nameof(AzureListBlockItemAdapter)}", nameof(block));
            }

            try
            {
                var result = await blob.DownloadContentAsync(null, null, new HttpRange(azureBlock.Offset, azureBlock.SizeInBytes), cancellationToken: cancellationToken);
                await writeStream.WriteAsync(result.Value.Content, cancellationToken);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        public async Task ReadBlockToAsync(Stream writeStream, CancellationToken cancellationToken)
        {
            try
            {
                await blob.DownloadToAsync(writeStream, cancellationToken);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        private string EncodeBlockId(string blockId)
        {
            try
            {
                return AsciiBase64Converter.Encode(blockId);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        private IBlockInfo GetBlockInfo(BlobBlock listBlockItem, ref long offset, bool committed = true)
        {
            var blockInfo = AzureListBlockItemAdapter.Create(listBlockItem, offset, committed);
            offset += blockInfo.SizeInBytes;
            return blockInfo;
        }

        public async Task<IBlobProperties> GetPropertiesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return new AzBlobProperties(await blob.GetPropertiesAsync(null, cancellationToken));
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }

        public async Task DeleteIfExistsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            }
            catch (RequestFailedException e)
            {
                throw new StorageException(e.Message, e.ErrorCode, e);
            }
        }
    }
}