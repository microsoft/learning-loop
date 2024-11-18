// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.DecisionService.Common.Storage
{
    /// <summary>
    /// Information about a block stored in an <see cref="IBlockStore"/>.
    /// </summary>
    public interface IBlockInfo
    {
        /// <summary>
        /// Gets the name or ID of the block.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the name of the block in encoded format.
        /// Can return the same value as <see cref="Name"/> if no encoding is used.
        /// </summary>
        string EncodedName { get; }

        /// <summary>
        /// Gets the size of block in bytes.
        /// </summary>
        long SizeInBytes { get; }

        /// <summary>
        /// True is the block is committed, false otherwise (uncommitted).
        /// </summary>
        bool IsCommitted { get; }

        /// <summary>
        /// True is the block is uncommitted, false otherwise (committed).
        /// </summary>
        bool IsUncommitted { get; }
    }
}
