// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.DecisionService.Common.Storage
{
    /// <summary>
    /// Gets <see cref="IBlockStore"/> objects.
    /// </summary>
    public interface IBlockStoreProvider
    {
        /// <summary>
        /// Gets the <see cref="IBlockStore"/> with <paramref name="name"/>
        /// </summary>
        /// <param name="name">Name of the <see cref="IBlockStore"/> to get.</param>
        /// <returns>The <see cref="IBlockStore"/></returns>
        IBlockStore GetStore(string name);
    }
}
