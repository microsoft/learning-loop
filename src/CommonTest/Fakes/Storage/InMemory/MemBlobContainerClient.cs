// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static CommonTest.Fakes.Storage.InMemory.MemBlobClient;

namespace CommonTest.Fakes.Storage.InMemory
{
    /// <summary>
    /// MemBlobContainerClient is an in-memory implementation of IBlobContainerClient.
    /// </summary>
    /// <remarks>
    /// This class is used for testing purposes only and provider a very simple in-memory storage.
    /// While the internal lists may be thread-safe, the class itself is not thread-safe and access
    /// to memory is not thread-safe (it's just enough to test).
    /// 
    /// It is the responsibility of the caller to ensure that tests are coordinated in a way that
    /// does not cause threading issues.  This may be overcome in future versions providing
    /// a thread-safe implementation.
    /// </remarks>
    public class MemBlobContainerClient : IBlobContainerClient
    {
        private readonly ConcurrentDictionary<Uri, MemBlobClient> _blobs = new();

        public enum MemStoreAction
        {
            Commit,
            Delete,
            Read,
            Write
        };

        public MemBlobContainerClient(string name, Uri uri, bool readOnly, IStorageFactory factory)
        {
            this.Factory = factory;
            this.Name = name;
            this.Uri = uri;
            this.ReadOnly = readOnly;
            this.Exists = false;
        }

        public MemBlobContainerClient(Uri containerUri, bool readOnly, IStorageFactory factory)
        {
            this.Factory = factory;
            this.Name = containerUri.ToString();
            this.Uri = containerUri;
            this.ReadOnly = readOnly;
            this.Exists = false;
        }

        public string Name { get; private set; }

        public Uri Uri { get; private set; }
        
        public bool ReadOnly { get; private set; }
        
        public bool Exists { get; private set; }

        public IStorageFactory Factory { get; private set; }

        public async Task<IBlobLeaseHolder> AcquireLeaseAsync(string appId, string lockBlobName, DateTime lastConfigEditDate, ILogger logger, CancellationToken outerCancellationToken)
        {
            await Task.CompletedTask;
            return new MemBlobLeaseHolder(Task.CompletedTask);
        }

        public async Task CreateIfNotExistsAsync(CancellationToken cancellationToken = default)
        {
            if (this.ReadOnly)
            {
                throw new StorageException("Container is read-only");
            }
            this.Exists = true;
            await Task.CompletedTask;
        }

        public async Task DeleteBlobAsync(string name, CancellationToken cancellationToken)
        {
            if (this.ReadOnly)
            {
                throw new StorageException("Container is read-only");
            }
            var fullUri = MemUriHelper.AppendUri(this.Uri, name);
            if (_blobs.TryRemove(fullUri, out var blob))
            {
                await blob.DeleteIfExistsAsync(null, cancellationToken);
            }
        }

        public async Task DeleteIfExistsAsync(CancellationToken cancellationToken = default)
        {
            if (!this.Exists)
            {
                await Task.CompletedTask;
            }
            if (this.ReadOnly)
            {
                throw new StorageException("Container is read-only");
            }
            var all_blobs = _blobs.ToList();
            _blobs.Clear();
            foreach (var blob in all_blobs)
            {
                await blob.Value.DeleteIfExistsAsync(null, cancellationToken);
            }
            await Task.CompletedTask;
        }

        public IBlobClient GetBlobClient(string blobName)
        {
            var fullUri = MemUriHelper.AppendUri(this.Uri, blobName);
            return _blobs.GetOrAdd(fullUri, (k) => new MemBlobClient(blobName, fullUri, this));
        }

        public async Task<IList<IBlobItem>> GetBlobsAsync(string prefix, CancellationToken cancellationToken)
        {
            var blobs = _blobs.Values.Where(b => b.Name.StartsWith(prefix)).Select(b =>new MemBlobItem(b.Properties)).ToList<IBlobItem>();
            return await Task.FromResult(blobs);
        }

        public async Task<IList<IBlobHierarchyItem>> GetBlobsByHierarchyAsync(string prefix, string delimiter, CancellationToken cancellationToken = default)
        {
            var items = new Dictionary<string, MemBlobHierarchyItem>();
            var filteredBlobs = _blobs.Values.Where(b => b.Name.StartsWith(prefix)).ToList();
            foreach (var blob in filteredBlobs)
            {
                var parts = blob.Name.Substring(prefix.Length).Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);
                var next = parts.FirstOrDefault();
                var isPrefix = parts.Length > 1;
                var name = prefix + (isPrefix ? $"{next}{delimiter}" : next);
                if (!items.ContainsKey(name))
                {
                    items.Add(name, new MemBlobHierarchyItem
                    {
                        Name = name,
                        Uri = MemUriHelper.CreateUri($"{Uri.ToString().TrimEnd('/')}/{name}"),
                        IsPrefix = isPrefix,
                        Prefix = name,
                        Items = isPrefix ? new List<IBlobItem>() : new List<IBlobItem> { new MemBlobItem(blob.Properties) }
                    });
                }
            }
            return await Task.FromResult(items.Values.ToList<IBlobHierarchyItem>());
        }

        public IBlockStoreProvider CreateBlockStoreProvider()
        {
            if (this.ReadOnly)
            {
                throw new StorageException("Container is read-only");
            }
            return new MemBlockStoreProvider(this);
        }

        public IBlockStore GetBlockBlobClient(string blobName)
        {
            var fullUri = MemUriHelper.AppendUri(this.Uri, blobName);
            return _blobs.GetOrAdd(fullUri, (k) => new MemBlobClient(blobName, fullUri, this));
        }

        public void OnBlobStoreEvent(BlobStoreEventArgs args)
        {
            BlobStoreEvent?.Invoke(this, args);
        }

        public event EventHandler<BlobStoreEventArgs> BlobStoreEvent;

        public IDictionary<Uri, MemBlobClient> Blobs { get { return _blobs; } }
    }

    internal class MemBlobItem : IBlobItem
    {
        private readonly MemBlobProperties _properties;

        internal MemBlobItem(string name, Uri uri)
        {
            _properties = new MemBlobProperties(name, uri);
        }

        internal MemBlobItem(MemBlobProperties properties)
        {
            _properties = properties;
        }

        public string Name { get { return _properties.Name; } }

        public Uri Uri { get { return _properties.Uri; } }

        public IBlobItemProperties Properties { get { return _properties; } }
    }

    internal class MemBlobHierarchyItem : IBlobHierarchyItem
    {
        public string Name { get; set; }

        public Uri Uri { get; set; }

        public bool IsPrefix { get; set; }

        public IList<IBlobItem> Items { get; set; }

        public string Prefix { get; set; }
    }
}
