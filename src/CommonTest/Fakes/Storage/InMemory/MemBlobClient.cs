// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static CommonTest.Fakes.Storage.InMemory.MemBlobContainerClient;

namespace CommonTest.Fakes.Storage.InMemory
{
    /// <summary>
    /// MemBlobClient is an in-memory implementation of IBlobClient and IBlockStore.
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
    public class MemBlobClient : IBlobClient, IBlockStore
    {
        private readonly MemBlobContainerClient _container;
        private readonly MemBlobProperties _properties;
        private List<MemBlock> _blocks = new();

        public MemBlobClient(string name, Uri blobUri, MemBlobContainerClient container)
        {
            _properties = new MemBlobProperties(name, blobUri);
            _container = container;
        }

        public string Name { get { return _properties.Name; } }

        public Uri Uri { get { return _properties.Uri; } }

        public int MaxBlockSizeInBytes { get { return _properties.MaxBlockSizeInBytes; } }

        public int MinBlockSizeInBytes { get { return _properties.MinBlockSizeInBytes; } }

        public async Task CommitBlocksAsync(IEnumerable<string> blockIds, CancellationToken cancellationToken = default)
        {
            if (!_container.Exists)
            {
                throw new StorageException("container does not exist");
            }
            if (_container.ReadOnly)
            {
                throw new StorageException("Blob is read-only");
            }
            var committed_blocks = new List<MemBlock>();
            lock (_blocks)
            {
                _blocks.ForEach(b => { if (blockIds.Contains(b.Id)) { b.Committed = true; committed_blocks.Add(b); } });
                _blocks = committed_blocks.ToList();
            }
            await Task.CompletedTask;
            if (committed_blocks.Count > 0)
            {
                _container.OnBlobStoreEvent(new BlobStoreEventArgs(_properties, MemStoreAction.Commit, committed_blocks));
            }
        }

        public async Task<bool> DeleteIfExistsAsync(string options, CancellationToken cancellationToken = default)
        {
            if (!_container.Exists)
            {
                return await Task.FromResult(false);
            }
            if (_container.ReadOnly)
            {
                throw new StorageException("Blob is read-only");
            }
            List<MemBlock> deleted_blocks;
            lock (_blocks)
            {
                deleted_blocks = _blocks;
                _blocks = new List<MemBlock>();
            }
            return await Task.FromResult(deleted_blocks != null);
        }

        public async Task DeleteIfExistsAsync(CancellationToken cancellationToken = default)
        {
            await DeleteIfExistsAsync(null, cancellationToken);
        }

        public async Task<BinaryData> DownloadAsync(CancellationToken cancellationToken = default)
        {
            using var data = new MemoryStream();
            await DownloadToAsync(data, cancellationToken);
            return await Task.FromResult<BinaryData>(new BinaryData(data.ToArray()));
        }

        public async Task DownloadToAsync(Stream stm, CancellationToken cancellationToken = default)
        {
            if (!_container.Exists)
            {
                throw new StorageException("constiner does not exist");
            }
            lock (_blocks)
            {
                foreach (var block in _blocks)
                {
                    block.Data.Position = 0;
                    block.Data.CopyTo(stm);
                }
            }
            await Task.CompletedTask;
        }

        public async Task<bool> ExistsAsync(CancellationToken cancelToken = default)
        {
            if (!_container.Exists)
            {
                throw new StorageException("container does not exist");
            }
            return await Task.FromResult<bool>(_blocks.Count > 0);
        }

        public async Task<IEnumerable<IBlockInfo>> GetBlockInfoListAsync(string blockListType = "Committed", CancellationToken cancelToken = default)
        {
            if (!_container.Exists)
            {
                throw new StorageException("container does not exist");
            }
            List<IBlockInfo> blockInfoList;
            lock (_blocks)
            {
                blockInfoList = _blocks.ToList().Cast<IBlockInfo>().ToList();
            }
            return await Task.FromResult(blockInfoList);
        }

        public async Task<IBlobProperties> GetPropertiesAsync(CancellationToken cancellationToken = default)
        {
            if (!_container.Exists)
            {
                throw new StorageException("container does not exist");
            }
            return await Task.FromResult<IBlobProperties>(_properties);
        }

        public async Task ReadBlockAsync(IBlockInfo block, Stream writeStream, CancellationToken cancellationToken)
        {
            if (!_container.Exists)
            {
                throw new StorageException("container does not exist");
            }
            List<MemBlock> blocks;
            lock (_blocks)
            {
                var read_block = _blocks.Find(b => b.Name == block.Name) ?? throw new StorageException("Block not found");
                writeStream.Write(read_block.Data.GetBuffer());
                blocks = new List<MemBlock>() { read_block };
            }
            _container.OnBlobStoreEvent(new BlobStoreEventArgs(_properties, MemStoreAction.Read, blocks));
            await Task.CompletedTask;
        }

        public async Task ReadBlockToAsync(Stream writeStream, CancellationToken cancellationToken)
        {
            if (!_container.Exists)
            {
                throw new StorageException("container does not exist");
            }
            var read_blocks = new List<MemBlock>();
            lock (_blocks)
            {
                foreach (var block in _blocks)
                {
                    writeStream.Write(block.Data.GetBuffer());
                    read_blocks.Add(block);
                }
            }
            if (read_blocks.Count > 0)
            {
                _container.OnBlobStoreEvent(new BlobStoreEventArgs(_properties, MemStoreAction.Read, read_blocks));
            }
            await Task.CompletedTask;
        }

        public async Task StartCopyFromAsync(IBlobClient sourceBlob, CancellationToken cancellationToken = default)
        {
            if (!_container.Exists)
            {
                throw new StorageException("container does not exist");
            }
            await sourceBlob.DownloadAsync().ContinueWith(async (task) =>
            {
                await UploadAsync(task.Result, cancellationToken);
            }, TaskScheduler.Current);
        }

        public async Task UploadAsync(BinaryData content, CancellationToken cancellationToken)
        {
            await this.UploadAsync(content, true, cancellationToken);
        }

        public async Task UploadAsync(BinaryData content, bool overwrite = false, CancellationToken cancellationToken = default)
        {
            if (!_container.Exists)
            {
                throw new StorageException("container does not exist");
            }
            if (_container.ReadOnly)
            {
                throw new StorageException("Blob is read-only");
            }
            lock (_blocks)
            {
                if ((overwrite == false) && (_blocks.Count > 0))
                {
                    throw new StorageException($"memory blob already exists ({this.Uri})", "MemoryBlobAlreadyExists");
                }
                _blocks.Clear();
            }
            await WriteAsync(new MemoryStream(content.ToArray()), cancellationToken);
        }

        public async Task WriteAsync(Stream readStream, CancellationToken cancellationToken)
        {
            await DeleteIfExistsAsync(cancellationToken);
            var blockId = 0.ToString("x4");
            await WriteBlockAsync(blockId, readStream, cancellationToken);
            await CommitBlocksAsync(new List<string>() { blockId }, cancellationToken);
        }

        public async Task WriteBlockAsync(string blockId, Stream readStream, CancellationToken cancellationToken)
        {
            if (!_container.Exists)
            {
                throw new StorageException("container does not exist");
            }
            if (_container.ReadOnly)
            {
                throw new StorageException("Blob is read-only");
            }
            var block = new MemBlock { Id = blockId, Committed = false };
            await readStream.CopyToAsync(block.Data, cancellationToken);
            var blocks_added = new List<MemBlock>() { block };
            lock (_blocks)
            {
                _blocks.Add(block);
            }
            _properties.OnUpdated(_blocks.Select(b => b.SizeInBytes).Sum());
            _container.OnBlobStoreEvent(new BlobStoreEventArgs(_properties, MemStoreAction.Write, blocks_added));
        }

        public MemBlobProperties Properties { get {  return _properties; } }

        public IList<MemBlock> Blocks { get { return _blocks.ToList(); } }

        public class MemBlock : IBlockInfo
        {
            private readonly MemoryStream _blockData = new();

            public string Id { get; set; }
            public bool Committed { get; set; }
            public MemoryStream Data { get { return _blockData; } }

            public string Name { get { return Id; } }

            public string EncodedName { get { return Id; } }

            public long SizeInBytes { get { return _blockData != null ? _blockData.Length : 0; } }

            public bool IsCommitted { get { return Committed; } }

            public bool IsUncommitted { get { return !Committed; } }
        }

        public class BlobStoreEventArgs : EventArgs
        {
            public BlobStoreEventArgs(MemBlobProperties properties, MemStoreAction action, IList<MemBlock> blocks)
            {
                Properties = properties;
                Action = action;
                Blocks = blocks;
            }

            public MemBlobProperties Properties { get; private set; }
            public IList<MemBlock> Blocks { get; private set; }
            public MemStoreAction Action { get; private set; }
        }
    }
}
