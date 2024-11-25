// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Storage;
using System;

namespace CommonTest.Fakes.Storage.InMemory
{
    /// <summary>
    /// MemBlobProperties is an in-memory implementation of IBlobProperties
    /// and IBlobItemProperties.
    /// </summary>
    public class MemBlobProperties : IBlobProperties, IBlobItemProperties
    {
        public MemBlobProperties(string name, Uri uri)
        {
            this.Name = name;
            this.Uri = uri;
            this.CreatedOn = DateTimeOffset.UtcNow;
            this.LastModified = this.CreatedOn;
        }

        public string Name { get; set; }

        public Uri Uri { get; set; }

        public DateTimeOffset LastModified { get; set; }

        public DateTimeOffset CreatedOn { get; set; }

        public long ContentLength { get; set; }

        public int MaxBlockSizeInBytes { get; set; } = 100 * 1024 * 1024;

        public int MinBlockSizeInBytes { get; set; } = 8;

        public void OnReset()
        {
            this.ContentLength = 0;
        }

        public void OnUpdated(long contentLength)
        {
            this.ContentLength = contentLength;
            this.LastModified = DateTimeOffset.UtcNow;
        }
    }
}
