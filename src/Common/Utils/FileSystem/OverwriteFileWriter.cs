// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Utils.FileSystem
{
    /// <summary>
    /// Writes to files by overwriting file if it exists or creating a new file.
    /// </summary>
    public sealed class OverwriteFileWriter : IFileWriter
    {
        /// <summary>
        /// Creates or overwrites the file at <paramref name="path"/> with <paramref name="bytes"/>.
        /// File is not written if <paramref name="bytes"/> is null or empty.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="bytes">The bytes to write.</param>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="path"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// When <paramref name="path"/> is invalid or whitespace.
        /// </exception>
        public void Write(string path, byte[] bytes)
        {
            CheckParams(path);

            if (bytes == null || bytes.Length <= 0) return;
            WriteInternal(path, bytes);
        }

        /// <summary>
        /// Creates or overwrites the file at <paramref name="path"/> with <paramref name="bytes"/>.
        /// File is not written if <paramref name="bytes"/> is null or empty.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="bytes">The bytes to write.</param>
        /// <param name="cancellationToken"/>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="path"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// When <paramref name="path"/> is invalid or whitespace.
        /// </exception>
        public async Task WriteAsync(
            string path,
            byte[] bytes,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckParams(path);

            if (bytes == null || bytes.Length <= 0) return;
            await WriteInternalAsync(path, bytes, cancellationToken);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="path"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// When <paramref name="path"/> is invalid or whitespace.
        /// </exception>
        public void Clear(string path)
        {
            CheckParams(path);

            if (!File.Exists(path)) return;
            WriteInternal(path, new byte[] { });
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// When path is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// When <paramref name="path"/> is invalid or whitespace.
        /// </exception>
        public Task ClearAsync(string path, CancellationToken cancellationToken = default)
        {
            CheckParams(path);

            if (!File.Exists(path)) return Task.CompletedTask;
            return WriteInternalAsync(path, new byte[] { }, cancellationToken);
        }

        private static void WriteInternal(string path, byte[] bytes)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            fs.Write(bytes, 0, bytes.Length);
        }

        private static async Task WriteInternalAsync(string path, byte[] bytes, CancellationToken cancellationToken)
        {
            await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            await fs.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
        }

        #region Parameter Validation Helpers

        private static void CheckParams(string path)
        {
            PathUtils.CheckPath(path);
        }

        #endregion
    }
}
