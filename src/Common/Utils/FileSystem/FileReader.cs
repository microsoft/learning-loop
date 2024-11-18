// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Utils.FileSystem
{
    public sealed class FileReader : IFileReader
    {
        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="path"/> is null.
        /// When <paramref name="targetStream"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// When <paramref name="path"/> is invalid or whitespace.
        /// </exception>
        public void Read(string path, Stream targetStream)
        {
            CheckParams(path, targetStream);

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            fs.CopyTo(targetStream);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="path"/> is null.
        /// When <paramref name="targetStream"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// When <paramref name="path"/> is invalid or whitespace.
        /// </exception>
        public async Task ReadAsync(string path, Stream targetStream, CancellationToken cancellationToken = default)
        {
            CheckParams(path, targetStream);

            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            await fs.CopyToAsync(targetStream, cancellationToken);
        }

        #region Parameter Validation Helpers

        private static void CheckParams(string path, Stream targetStream)
        {
            CheckPathParam(path);
            CheckTargetStreamParam(targetStream);
        }

        private static void CheckPathParam(string path)
        {
            PathUtils.CheckPath(path);
        }

        private static void CheckTargetStreamParam(Stream targetStream)
        {
            if (targetStream == null) throw new ArgumentNullException(nameof(targetStream));
        }

        #endregion
    }
}
