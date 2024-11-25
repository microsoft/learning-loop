// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace CommonTest.Fakes.Storage.InMemory
{
    /// <summary>
    /// MemUriHelper is a helper class for creating URIs for in-memory storage.
    /// </summary>
    public class MemUriHelper
    {
        public static Uri CreateUri(string uriString)
        {
            return new Uri(uriString);
        }

        public static Uri AppendUri(Uri uri, string name)
        {
            return new Uri($"{uri}/{name}");
        }
    }
}
