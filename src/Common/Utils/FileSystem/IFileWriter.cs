// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Utils.FileSystem
{
    public interface IFileWriter
    {
        /// <summary>
        /// Writes <paramref name="bytes"/> to the file at <paramref name="path"/>.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="bytes">The bytes to write to the file.</param>
        public void Write(string path, byte[] bytes);

        /// <summary>
        /// Writes <paramref name="bytes"/> to the file at <paramref name="path"/> asynchronously.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="bytes">The bytes to write to the file.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public Task WriteAsync(
            string path,
            byte[] bytes,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Clears the file at <paramref name="path"/> of its contents.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        public void Clear(string path);

        /// <summary>
        /// Clears the file at <paramref name="path"/> of its contents.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public Task ClearAsync(string path, CancellationToken cancellationToken = default(CancellationToken));
    }
}
