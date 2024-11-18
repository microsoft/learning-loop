// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Storage
{
    /// <summary>
    /// Storage for blocks of data. Supports read, write, and commit operations.
    /// </summary>
    public interface IBlockStore
    {
        /// <summary>
        /// The name of the <see cref="IBlockStore"/>
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The maximum block size in bytes that can be written.
        /// </summary>
        int MaxBlockSizeInBytes { get; }

        /// <summary>
        /// The minimum block size in bytes that can be written.
        /// </summary>
        int MinBlockSizeInBytes { get; }

        /// <summary>
        /// Checks if the <see cref="IBlockStore"/> exists.
        /// </summary>
        /// <param name="cancelToken"></param>
        /// <returns>true if store exists, false otherwise.</returns>
        Task<bool> ExistsAsync(CancellationToken cancelToken = default);

        /// <summary>
        /// Writes a block uncommited to the store.
        /// </summary>
        /// <param name="blockId">An identifier or name for the block.</param>
        /// <param name="readStream">The stream that contains the data to write to the block store. Will be read.</param>
        /// <param name="cancellationToken"></param>
        Task WriteBlockAsync(string blockId, Stream readStream, CancellationToken cancellationToken);

        Task WriteAsync(Stream readStream, CancellationToken cancellationToken);

        /// <summary>
        /// Commits a list of blocks to the store. Finalizes put/write operations.
        /// </summary>
        /// <param name="blockIds">The list of block IDs or names to commit.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task CommitBlocksAsync(IEnumerable<string> blockIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a list of block information from the block store.
        /// </summary>
        /// <param name="blockListType">The type of block list to retrieve. Default is "Committed". Valid values are All, Committed, Uncommitted</param>
        /// <param name="cancelToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains an enumerable of <see cref="IBlockInfo"/> representing the block information.
        /// </returns>
        Task<IEnumerable<IBlockInfo>> GetBlockInfoListAsync(string blockListType = "Committed", CancellationToken cancelToken = default);

        /// <summary>
        /// Gets the block from the block store associated with an <see cref="IBlockInfo"/>.
        /// </summary>
        /// <param name="block">The info for the block to read.</param>
        /// <param name="writeStream">The stream where the range of bytes will be written.</param>
        /// <param name="cancellationToken"/>
        Task ReadBlockAsync(IBlockInfo block, Stream writeStream, CancellationToken cancellationToken);
        
        Task ReadBlockToAsync(Stream writeStream, CancellationToken cancellationToken);

        Task<IBlobProperties> GetPropertiesAsync(CancellationToken cancellationToken = default);
        
        Task DeleteIfExistsAsync(CancellationToken cancellationToken = default);
   }
}