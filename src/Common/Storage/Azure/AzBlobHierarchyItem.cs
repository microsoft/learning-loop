// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Storage.Blobs.Models;

namespace Microsoft.DecisionService.Common.Storage.Azure
{
    public class AzBlobHierarchyItem : IBlobHierarchyItem
    {
        private readonly BlobHierarchyItem _item;

        public AzBlobHierarchyItem(BlobHierarchyItem item)
        {
            _item = item;
        }

        public string Name { get { return _item.Blob.Name; } }
        public bool IsPrefix { get { return _item.IsPrefix; } }
        public string Prefix { get { return _item.Prefix; } }
    }
}