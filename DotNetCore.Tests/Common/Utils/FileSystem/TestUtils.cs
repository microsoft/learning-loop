// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DotNetCore.Tests.Common.Utils.FileSystem
{
    public static class TestUtils
    {
        public static IEnumerable<object[]> GenerateInvalidPaths()
        {
            foreach (char c in Path.GetInvalidPathChars())
            {
                yield return new object[] {$"foo{c}bar{Path.PathSeparator}file.txt"};
            }

            // Exclude path separators since it will not result in an invalid path,
            // but just a different file name.
            var invalidFileNameChars = Path.GetInvalidFileNameChars()
                .Except(new[] {'\\', '/', Path.PathSeparator});
            foreach (char c in invalidFileNameChars)
            {
                yield return new object[] {$"foo{Path.PathSeparator}file{c}.txt"};
            }
        }
    }
}
