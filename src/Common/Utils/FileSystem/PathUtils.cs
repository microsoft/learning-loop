// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;

namespace Microsoft.DecisionService.Common.Utils.FileSystem
{
    public static class PathUtils
    {
        /// <summary>
        /// Checks if <paramref name="path"/> is valid.
        /// Throws appropriate exception if not valid.
        /// </summary>
        /// <param name="path">Path to a file</param>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="path"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// When <paramref name="path"/> is whitespace or invalid.
        /// </exception>
        public static void CheckPath(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException($"{nameof(path)} cannot be whitespace");
            }

            // NOTE: Path.GetFileName will throw ArgumentException if path is invalid
            if (Path.GetFileName(path).IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
            {
                throw new ArgumentException("Path is not valid", nameof(path));
            }
        }
    }
}
