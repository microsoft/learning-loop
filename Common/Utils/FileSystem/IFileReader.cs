// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Utils.FileSystem
{
    public interface IFileReader
    {
        /// <summary>
        /// Reads the contents of a file at <paramref name="path"/> to <paramref name="targetStream"/>.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="targetStream">The target stream</param>
        void Read(string path, Stream targetStream);

        /// <summary>
        /// Reads the contents of a file at <paramref name="path"/> to <paramref name="targetStream"/> asynchronously.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="targetStream">The target stream</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task ReadAsync(
            string path,
            Stream targetStream,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
