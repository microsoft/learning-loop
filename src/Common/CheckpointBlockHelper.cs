// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Error;
using Microsoft.DecisionService.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.DecisionService.Common.Storage;

namespace Microsoft.DecisionService.Common
{
    /// <summary>
    /// Reads and writes <see cref="ModelCheckpoint"/> as blocks to an <see cref="IBlockStore"/>.
    /// </summary>
    public class CheckpointBlockHelper : ICheckpointBlockHelper
    {
        /*
         * NOTE: block names must be the same length or WriteBlockAsync could throw an error
         * depending on the underlying implementation. e.g. Azure Storage requires this.
         */

        /*
         * NOTE: Original/Legacy block names were not encoded before being written to the block store.
         * This class will migrate legacy names to the new name schema.
         */
        private const string LEGACY_MODEL_BLOCK_NAME = "0000";      // 0000
        private const string LEGACY_METADATA_BLOCK_NAME = "0001";   // 0001
        private const string MODEL_BLOCK_NAME_PREFIX = "model";
        private const string METADATA_BLOCK_PREFIX = "metad";

        private static readonly Comparer<IBlockInfo> blockComparer = Comparer<IBlockInfo>.Create((a, b) => a.Name.CompareTo(b.Name));

        private readonly CheckpointBlockHelperOptions options;
        private readonly ILogger logger;

        public CheckpointBlockHelper(CheckpointBlockHelperOptions options, ILogger logger)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            this.options = options;
            this.logger = logger;
        }

        /// <summary>
        /// Save the checkpoint (the model and the reading position) in the tenant storage account
        /// </summary>
        public async Task SaveCheckpointAsync(ModelCheckpoint checkpoint, DateTime configurationDate, CancellationToken cancellationToken)
        {
            // save checkpoint in tenant storage
            if (this.options.BlockStoreProvider == null)
            {
                this.logger?.LogError("cannot save the checkpoint because no storage provider was provided");
                return;
            }

            if (checkpoint == null)
            {
                this.logger?.LogError("cannot save null checkpoint");
                return;
            }

            var checkpointStore = GetCheckpointStore(configurationDate);
            var tasks = new List<Task<IEnumerable<string>>>();
            string md5Hash = string.Empty;
            if (checkpoint.Model != null)
            {
                md5Hash = MD5HashUtil.GetMd5Hash(checkpoint.Model);
                // send the checkpoint model (uncommitted)
                tasks.Add(WriteBlocksFromByteArrayAsync(checkpointStore, checkpoint.Model, GetModelBlockName, cancellationToken));
            }

            // send the checkpoint metadata (uncommitted)
            byte[] checkpointData = CheckpointToByteArray(checkpoint);
            tasks.Add(WriteBlocksFromByteArrayAsync(checkpointStore, checkpointData, GetMetadataBlockName, cancellationToken));

            var blockIdLists = await Task.WhenAll(tasks);
            var blockIdList = blockIdLists.SelectMany(x => x);

            // commit
            await checkpointStore.CommitBlocksAsync(blockIdList, cancellationToken);

            // Geneva logger
            this.logger?.LogInformation("checkpoint saved in {Name}, modeId={ModelId}, modelSize = {ModelSize} bytes with md5 hash={Md5Hash}", checkpointStore.Name, checkpoint.HistoricalModelInfo?.ModelId, checkpoint.Model?.Length, md5Hash);
        }

        /// <summary>
        /// Download the checkpoint (the model and the reading position) from the tenant storage account
        /// </summary>
        /// <returns>
        /// The <see cref="ModelCheckpoint"/>.<c>null</c> if there was an error.
        /// </returns>
        public async Task<ModelCheckpoint> GetCheckpointAsync(DateTime configurationDate, CancellationToken cancellationToken)
        {
            // save checkpoint in tenant storage
            if (this.options.BlockStoreProvider == null)
            {
                this.logger?.LogError("cannot get a checkpoint because no storage provider was provided");
                return null;
            }

            Func<Exception, bool> doNotRetryIfCancellationRequested = (ex) => {
                if (cancellationToken.IsCancellationRequested)
                    return false;
                return true;
            };

            try
            {
                return await Retry.HandleExceptionAsync<ModelCheckpoint, Exception>(
                    async () => {
                        var checkpointStore = GetCheckpointStore(configurationDate);
                        if (!await checkpointStore.ExistsAsync(cancellationToken))
                        {
                            return null;
                        }

                        return await ReadCheckpointAsync(checkpointStore, cancellationToken);
                    },
                    doNotRetryIfCancellationRequested
                );
            }
            catch (Exception e)
            {
                this.logger?.LogError(e, "{ErrorCode}", PersonalizerInternalErrorCode.TrainerCheckpointFailure.ToString());
                return null;
            }
        }

        private static async Task<IEnumerable<string>> WriteBlocksFromByteArrayAsync(IBlockStore checkpointStore, byte[] bytes, Func<int, string> blockIdGenerator, CancellationToken cancellationToken)
        {
            var blockIdList = new List<string>();
            var tasks = new List<Task>();
            int streamSize;
            for (int index = 0, blockNumber = 0; index < bytes.Length; index += streamSize, blockNumber++)
            {
                // send the blocks to azure (uncommitted)
                int remainingBytes = bytes.Length - index;
                streamSize = Math.Min(remainingBytes, checkpointStore.MaxBlockSizeInBytes);
                var remainingBytesAfterThisWrite = remainingBytes - streamSize;

                // ensure the next write call will meet the minimum number of bytes.
                if (remainingBytesAfterThisWrite > 0 && remainingBytesAfterThisWrite < checkpointStore.MinBlockSizeInBytes)
                {
                    streamSize = remainingBytes - checkpointStore.MinBlockSizeInBytes;
                }

                string blockId = blockIdGenerator.Invoke(blockNumber);
                tasks.Add(WriteBlockFromByteArrayWithOffsetAsync(checkpointStore, bytes, index, streamSize, blockId, cancellationToken));
                blockIdList.Add(blockId);
            }

            await Task.WhenAll(tasks);
            return blockIdList;
        }

        private static async Task WriteBlockFromByteArrayWithOffsetAsync(
            IBlockStore checkpointStore,
            byte[] bytes,
            int offsetIndex,
            int bytesToWrite,
            string blockId,
            CancellationToken cancellationToken)
        {
            using var mstream = new MemoryStream(bytes, offsetIndex, bytesToWrite);
            await checkpointStore.WriteBlockAsync(blockId, mstream, cancellationToken);
        }

        /// <summary>
        /// Reads the <see cref="ModelCheckpoint"/> from an <see cref="IBlockStore"/>.
        /// </summary>
        /// <param name="checkpointStore">The store to read from.</param>
        /// <param name="cancellationToken"/>
        /// <returns>The read and deserialized <see cref="ModelCheckpoint"/></returns>
        /// <exception cref="InvalidModelCheckpointException"/>
        private static async Task<ModelCheckpoint> ReadCheckpointAsync(IBlockStore checkpointStore, CancellationToken cancellationToken)
        {
            var checkpoint = new ModelCheckpoint();
            var blocks = await checkpointStore.GetBlockInfoListAsync(cancelToken: cancellationToken);
            IList<IBlockInfo> modelBlocks;
            IList<IBlockInfo> metadataBlocks;

            SplitBlocksIntoModelAndMetadataBlocks(blocks, out modelBlocks, out metadataBlocks);
            var metadataByteArray = await GetBlocksAsByteArrayAsync(checkpointStore, metadataBlocks, cancellationToken);
            var modelByteArray = await GetBlocksAsByteArrayAsync(checkpointStore, modelBlocks, cancellationToken);

            if (metadataByteArray.Length > 0)
            {
                checkpoint = CheckpointFromByteArray(metadataByteArray);
            }

            checkpoint.Model = modelByteArray.Length > 0 ? modelByteArray : null;

            return checkpoint;
        }

        /// <summary>
        /// Splits a list of <see cref="IBlockInfo"/>s by the type of block it is.
        /// </summary>
        /// <param name="blocks">The blocks to split.</param>
        /// <param name="modelBlocks">The model blocks.</param>
        /// <param name="metadataBlocks">The metadata blocks.</param>
        /// <exception cref="InvalidModelCheckpointException">When a block with a name in an unknown schema is found in <paramref name="blocks"/></exception>
        private static void SplitBlocksIntoModelAndMetadataBlocks(
            IEnumerable<IBlockInfo> blocks,
            out IList<IBlockInfo> modelBlocks,
            out IList<IBlockInfo> metadataBlocks)
        {
            /*
             * NOTE: We cannot guarantee the order of the blocks if we iterate an IEnumberable directly.
             * To make sure we process and read the blocks in the correct order, we split them by type
             * and sort them by name here. This ensures that blocks are read in the correct order.
             */
            var modelBlocksImpl = new List<IBlockInfo>();
            var metadataBlocksImpl = new List<IBlockInfo>();

            foreach (var block in blocks)
            {
                if (IsModelBlock(block))
                {
                    modelBlocksImpl.Add(block);
                }
                else if (IsMetadataBlock(block))
                {
                    metadataBlocksImpl.Add(block);
                }
                else
                {
                    throw new InvalidModelCheckpointException($"Encountered unexpected block with name {block.Name} in model checkpoint.");
                }
            }

            modelBlocksImpl.Sort(blockComparer);
            metadataBlocksImpl.Sort(blockComparer);

            modelBlocks = modelBlocksImpl;
            metadataBlocks = metadataBlocksImpl;
        }

        private static async Task<byte[]> GetBlocksAsByteArrayAsync(IBlockStore store, IList<IBlockInfo> blocks, CancellationToken cancellationToken)
        {
            var tasks = new List<Task<byte[]>>();
            foreach (var block in blocks)
            {
                tasks.Add(GetBlockAsByteArrayAsync(store, block, cancellationToken));
            }

            return MergeArrays(await Task.WhenAll(tasks));
        }

        private static async Task<byte[]> GetBlockAsByteArrayAsync(IBlockStore store, IBlockInfo block, CancellationToken cancellationToken)
        {
            using (var mstream = new MemoryStream((int)block.SizeInBytes))
            {
                await store.ReadBlockAsync(block, mstream, cancellationToken);
                return mstream.ToArray();
            }
        }

        private static T[] MergeArrays<T>(T[][] arrays)
        {
            return arrays.SelectMany(x => x).ToArray();
        }

        private static byte[] CheckpointToByteArray(ModelCheckpoint checkpoint)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(checkpoint));
        }

        private static ModelCheckpoint CheckpointFromByteArray(byte[] bytes)
        {
            return JsonConvert.DeserializeObject<ModelCheckpoint>(
                Encoding.UTF8.GetString(bytes));
        }

        private static bool IsLegacyModelBlock(IBlockInfo block)
        {
            return BlockHasLegacyName(block, LEGACY_MODEL_BLOCK_NAME);
        }

        private static bool IsLegacyMetadataBlock(IBlockInfo block)
        {
            return BlockHasLegacyName(block, LEGACY_METADATA_BLOCK_NAME);
        }

        private static bool BlockHasLegacyName(IBlockInfo block, string legacyName)
        {
            return block.EncodedName == legacyName;
        }

        private static bool StartsWithModelPrefix(string blockName)
        {
            return StartsWithPrefix(blockName, MODEL_BLOCK_NAME_PREFIX);
        }

        private static bool StartsWithMetadataPrefix(string blockName)
        {
            return StartsWithPrefix(blockName, METADATA_BLOCK_PREFIX);
        }

        private static bool StartsWithPrefix(string blockName, string prefix)
        {
            return blockName.StartsWith(prefix);
        }

        private static bool IsModelBlock(IBlockInfo block)
        {
            return IsLegacyModelBlock(block) || StartsWithModelPrefix(block.Name);
        }

        private static bool IsMetadataBlock(IBlockInfo block)
        {
            return IsLegacyMetadataBlock(block) || StartsWithMetadataPrefix(block.Name);
        }

        public static string GetModelBlockName(int blockNumber)
        {
            return GetBlockName(MODEL_BLOCK_NAME_PREFIX, blockNumber);
        }

        public static string GetMetadataBlockName(int blockNumber)
        {
            return GetBlockName(METADATA_BLOCK_PREFIX, blockNumber);
        }

        private static string GetBlockName(string prefix, int blockNumber)
        {
            return $"{prefix}{blockNumber:000000}";
        }

        private IBlockStore GetCheckpointStore(DateTime configurationDate)
        {
            return options.BlockStoreProvider.GetStore(PathHelper.BuildCheckpointName(configurationDate, AzureBlobConstants.CheckpointBlobName));
        }
    }
}
