// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Error;
using Microsoft.DecisionService.Common.Trainer.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using BlockData = Microsoft.DecisionService.Common.Data.BlockData;
using Microsoft.DecisionService.Common.Storage;

namespace Microsoft.DecisionService.OnlineTrainer.Storage
{
    /// <summary>
    /// The Downloader runs an infinite loop, where it downloads all block from all blobs.
    /// </summary>
    public class BlockDownloader : ISourceBlock<BlockData>
    {
        private readonly TimeSpan _checkpointTimeout = TimeSpan.FromSeconds(5);
        private readonly IBlobContainerClient _container;
        private readonly BufferBlock<BlockData> queue;

        private readonly ILogger _logger;

        //ensure that blob and blocks are visited once only
        public HashSet<string> VisitedBlobs { get; private set; }
        // ensure that blocks are visited and downloaded
        public HashSet<string> VisitedBlocks { get; private set; }

        private readonly DateTime _lastConfigurationEditDate;

        public void Fault(Exception exception)
        {
            (queue as ISourceBlock<BlockData>).Fault(exception);
        }

        public Task Completion { get; private set; }

        public BlockDownloader(BlockPosition startPosition, IBlobContainerClient container, CancellationToken cancellationToken, DateTime lastConfigurationEditDate, int blockBufferCapacity, ILogger logger, TimeSpan? checkpointTimeout = null)
        {
            _logger = logger;
            _lastConfigurationEditDate = lastConfigurationEditDate;
            _checkpointTimeout = checkpointTimeout ?? _checkpointTimeout;

            // TODO pipe in imeterfactory
            // var defaultMetricProperties = MetricsUtil.GetDefaultProperties(appId: _options?.AppId, problemType: _options?.ProblemType.ToString());
            // _downloadSpeedKbPerSecondMetric = _options?.TelemetryService?.RegisterMetric<double>("DownloadSpeed_Kb_Per_Second", defaultMetricProperties);

            this.queue = new BufferBlock<BlockData>(
                new DataflowBlockOptions
                {
                    CancellationToken = cancellationToken,
                    BoundedCapacity = blockBufferCapacity,
                });

            _container = container;

            VisitedBlobs = new HashSet<string>();
            VisitedBlocks = new HashSet<string>();

            _logger?.LogInformation(
                "BlockDownloader start position: blob={BlobName}, block={BlockName}", startPosition?.BlobName, startPosition?.BlockName);

            Completion = this.RunAsync(startPosition, cancellationToken)
                .ContinueWith(t => CompleteQueue(t), TaskScheduler.Default)
                .TraceAsync(_logger, "BlockDownloader", "BlockDownloader.OnExit");
        }

        public ISourceBlock<BlockData> Output => this.queue;

        public void Complete() => this.queue.Complete();

        private async Task RunAsync(BlockPosition startPosition, CancellationToken cancellationToken)
        {
            //if a start position was set, use it
            if (!string.IsNullOrEmpty(startPosition?.BlobName))
            {
                if (await FindVisitedBlobsAsync(startPosition, cancellationToken)
                    && !string.IsNullOrEmpty(startPosition?.BlockName))
                {
                    await FindVisitedBlocksAsync(startPosition, cancellationToken);
                }
            }

            bool logMissingCheckpoint = true;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    BlockPosition storageCheckpoint = await FetchLatestBlockPositionAsync(this._container, _lastConfigurationEditDate, cancellationToken);
                    if (storageCheckpoint == null)
                    {
                        logMissingCheckpoint = HandleMissingCheckpoint(this._container, _lastConfigurationEditDate, logMissingCheckpoint);
                    }
                    else
                    {
                        logMissingCheckpoint = true;
                        await IterateOnBlobsAsync(_container, _lastConfigurationEditDate, storageCheckpoint, cancellationToken);
                    }
                    // wait for storage checkpoint to be updated
                    await Task.Delay(_checkpointTimeout, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("Operation was canceled. Exiting RunAsync");
                    return;
                }
                catch (StorageException e)
                {
                    /*
                     * Check if the exception is a result of canceling an operation. Bail out if it was.
                     * Note we need to check that the cancellation token was actually canceled since the
                     * Storage SDK can throw TaskCancelledException for other reasons.
                    */
                    if (IsOperationCanceled(e, cancellationToken))
                    {
                        _logger?.LogInformation("Storage operation was canceled. Exiting RunAsync");
                        return;
                    }
                    _logger?.LogError(e, "{ErrorCode}", PersonalizerInternalErrorCode.TrainerCheckpointFailure.ToString());
                    await Task.Delay(1000, cancellationToken); // When hit exception on Azure storage service, log, wait and retry.
                }
                catch (Exception e)
                {
                    _logger?.LogError(e, "{ErrorCode}", PersonalizerInternalErrorCode.TrainerExecutionFailure.ToString());
                    throw;
                }
            }
        }

        private bool HandleMissingCheckpoint(IBlobContainerClient container, DateTime lastConfigDate, bool logMissingCheckpoint)
        {
            if (logMissingCheckpoint)
            {
                _logger?.LogError("cannot find storage checkpoint in {containerName} for configuration date {lastConfigDate:yyyyy-MM-dd HH}", container.Name, lastConfigDate);
                return false;
            }
            return logMissingCheckpoint;
        }

        private static bool IsOperationCanceled(StorageException e, CancellationToken cancellationToken)
        {
            return e.InnerException is OperationCanceledException && cancellationToken.IsCancellationRequested;
        }

        private void CompleteQueue(Task runTask)
        {
            if (runTask.IsFaulted)
            {
                if (this.queue != null && queue is IDataflowBlock)
                {
                    ((IDataflowBlock)queue).Fault(runTask.Exception);
                }

                throw runTask.Exception;
            }
            else
            {
                this.queue?.Complete();
            }
        }

        private async Task IterateOnBlobsAsync(IBlobContainerClient container, DateTime lastConfigEditDate, BlockPosition storageCheckpoint, CancellationToken cancellationToken)
        {
            string checkpointBlobName = storageCheckpoint?.BlobName; // this is the last blob we should read on this round
            if (checkpointBlobName == null)
            {
                _logger?.LogInformation($"StorageCheckpoint has no blob name");
                return;
            }
            bool parseSuccess = PathHelper.ParseIndexAndDateFromBlob(checkpointBlobName, out int checkpointIndex, out int _, out DateTime checkpointDate);
            if (!parseSuccess)
            {
                return;
            }

            var blobList = await container.GetBlobsAsync(prefix: PathHelper.BuildBlobListPrefix(lastConfigEditDate),
                cancellationToken: cancellationToken);
            _logger?.LogInformation("Fetched {TotalCount} blobs, {VisitedCount} blobs are visited already", blobList.Count, VisitedBlobs.Count);
            foreach (var blob in blobList)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                parseSuccess = PathHelper.ParseIndexAndDateFromBlob(blob.Name, out int blobIndex, out int _, out DateTime blobDate);
                if (!parseSuccess)
                {
                    continue;
                }
                if (blobDate > checkpointDate || (blobDate == checkpointDate && blobIndex > checkpointIndex))
                {
                    break;
                }
                var blockBlobClient = container.GetBlockBlobClient(blob.Name);
                if (!await blockBlobClient.ExistsAsync(cancellationToken) || VisitedBlobs.Contains(blob.Name))
                {
                    continue;
                }

                _logger?.LogInformation("Processing blob {Name}", blob.Name);
                await IterateOnBlocksAsync(blockBlobClient, lastConfigEditDate, cancellationToken);

                VisitedBlobs.Add(blob.Name);
            }
        }

        /// <summary>
        /// Scan and look for new blocks
        /// </summary>
        private async Task IterateOnBlocksAsync(IBlockStore blob, DateTime lastConfigEditDate, CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Starting iteration on blob {Name}", blob.Name);

            bool blobIsGrowing = false;
            var sw = new Stopwatch();
            int iteration = 0;
            bool recentIterationHasAction = true;
            do
            {
                iteration++;
                BlockPosition latestBlockPosition = await FetchLatestBlockPositionAsync(_container, lastConfigEditDate, cancellationToken);
                if (latestBlockPosition == null)
                {
                    _logger?.LogInformation(" {Name}--{Iteration} latest block position is null", blob.Name, iteration);
                    continue;
                }
                int checkpointBlockIndex = int.Parse(latestBlockPosition.BlockName, System.Globalization.NumberStyles.HexNumber);
                blobIsGrowing = (blob.Name == latestBlockPosition.BlobName);

                long blobOffset = 0;
                long downloadedBlocks = 0;
                long totalDownloadedSize = 0;
                long totalDownloadTimeMs = 0;

                //iterate on a fresh list blocks
                var blockList = (await blob.GetBlockInfoListAsync(cancelToken: cancellationToken));
                if (recentIterationHasAction || iteration % 600 == 0)
                {
                    _logger?.LogInformation("Latest block position: {latestBlockPositionBlobName}-{latestBlockPositionBlockName}, {blobName}-{iteration}: block count {blockListCount}, VisitedBlocks count {VisitedBlocksCount}, Last block: {blockListLastOrDefaultName}",
                        latestBlockPosition.BlobName,
                        latestBlockPosition.BlockName,
                        blob.Name,
                        iteration,
                        blockList?.Count(),
                        VisitedBlocks.Count,
                        blockList?.LastOrDefault().Name
                        );
                }

                foreach (var block in blockList)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger?.LogInformation("Cancellation requested.");
                        return;
                    }

                    // we don't want to download blocks until they are checkpointed.
                    if (IsBlockCheckpointed(block.Name, blobIsGrowing, checkpointBlockIndex))
                    {
                        _logger?.LogInformation("{blobName}-{iteration}: {blockName} not checkpointed",
                            blob.Name,
                            iteration,
                            block.Name);
                        continue;
                    }

                    if (VisitedBlocks.Contains(block.Name))
                    {
                        blobOffset += block.SizeInBytes;
                        continue;
                    }

                    // new block found: download it
                    sw.Restart();

                    var content = new MemoryStream();
                    await blob.ReadBlockAsync(block, content, cancellationToken);

                    var downloadTimeMs = sw.ElapsedMilliseconds;
                    totalDownloadTimeMs += downloadTimeMs;
                    totalDownloadedSize += block.SizeInBytes;
                    downloadedBlocks++;
                    blobOffset += block.SizeInBytes;

                    await this.queue.SendAsync(
                        new BlockData()
                        {
                            Data = content.ToArray(),
                            Position = new BlockPosition() { BlobName = blob.Name, BlockName = block.Name, FileFormat = PathHelper.GetLogFormatFromFilePath(blob.Name) }
                        },
                        cancellationToken);
                    sw.Stop();

                    VisitedBlocks.Add(block.Name);
                }

                //self throttle for the case of no new blocks
                if (totalDownloadedSize == 0)
                {
                    await Task.Delay(1000, cancellationToken);
                    recentIterationHasAction = false;
                }
                else
                {
                    recentIterationHasAction = true;
                    _logger?.LogInformation("{blobName}-{iteration}: finished downloading {downloadedBlocks} blocks, size {totalDownloadedSize} bytes, took {totalDownloadTimeMs} ms",
                        blob.Name,
                        iteration,
                        downloadedBlocks,
                        totalDownloadedSize,
                        totalDownloadTimeMs);
                }
            }
            while (blobIsGrowing);

            //clean visited blocks, this blob is done
            VisitedBlocks.Clear();

            _logger?.LogInformation("Completed iteration on blob {blobName}", blob.Name);
        }

        private static bool IsBlockCheckpointed(string blockName, bool blobIsGrowing, int checkpointBlockIndex)
        {
            int blockIndex = int.Parse(blockName, System.Globalization.NumberStyles.HexNumber);
            return blobIsGrowing && blockIndex > checkpointBlockIndex;
        }

        /// <summary>
        /// Finds visited blobs, meaning all blobs that precede chronologically the given blob, put the names into VisitedBlobs to avoid re-processing
        /// The given blob is EXCLUDED
        /// </summary>
        private async Task<bool> FindVisitedBlobsAsync(BlockPosition checkpoint, CancellationToken cancellationToken)
        {
            var blobList = await _container.GetBlobsAsync(
                prefix: PathHelper.BuildBlobListPrefix(_lastConfigurationEditDate),
                cancellationToken: cancellationToken
            );
            if (blobList.Count == 0)
            {
                _logger?.LogInformation("no blob is found");
            }

            //create visited blob list
            bool found = false;
            var visited = new HashSet<string>();
            foreach (var blob in blobList)
            {
                if (blob.Name.CompareTo(checkpoint.BlobName) > 0)
                {
                    _logger?.LogInformation("the given blob name {CheckpointBlobName} cannot be found. Use {Name} to resume.", checkpoint.BlobName, blob.Name);
                    found = false;
                    break;
                }
                if (blob.Name == checkpoint.BlobName)
                {
                    found = true;
                    break;
                }
                visited.Add(blob.Name);
            }
            VisitedBlobs = visited;
            return found;
        }

        /// <summary>
        /// Finds the set of blocks that precedes chronologically the given block, in the given blob. Put the names into VisitedBlocks to avoid re-processing
        /// The given block is INCLUDED in the set
        /// </summary>
        private async Task<bool> FindVisitedBlocksAsync(BlockPosition checkpoint, CancellationToken cancellationToken)
        {
            var blob = _container.GetBlockBlobClient(checkpoint.BlobName);
            if (!await blob.ExistsAsync(cancellationToken))
            {
                _logger?.LogInformation("{Name} is not found", checkpoint.BlobName);
                return false;
            }

            //mark block as visited until blockName is found
            bool found = false;//check if the given block name is found
            var visited = new HashSet<string>();
            var blockList = await blob.GetBlockInfoListAsync(cancelToken: cancellationToken);
            foreach (var block in blockList)
            {
                visited.Add(block.Name);
                if (block.Name == checkpoint.BlockName)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                _logger?.LogInformation("the given block name {Name} cannot be found.", checkpoint.BlockName);
                return false;
            }
            VisitedBlocks = visited;
            return found;
        }

        private async Task<BlockPosition> FetchLatestBlockPositionAsync(
            IBlobContainerClient container,
            DateTime lastConfigEditDate,
            CancellationToken cancellationToken)
        {
            StorageCheckpoint checkpoint = null;
            // checkpoint blob should be the source of truth here.
            // if a new blob has been created but not synced, we should wait until it is synced to start downloading.
            var checkpointBlob = container.GetBlockBlobClient(PathHelper.BuildCheckpointName(lastConfigEditDate, AzureBlobConstants.TenantStorageCheckpointBlobName));
            if (await checkpointBlob.ExistsAsync(cancellationToken))
            {
                string serializedCheckpoint = null;
                try
                {
                    using var mstream = new MemoryStream();
                    await checkpointBlob.ReadBlockToAsync(mstream, cancellationToken);
                    serializedCheckpoint = Encoding.UTF8.GetString(mstream.ToArray());
                    checkpoint = JsonConvert.DeserializeObject<StorageCheckpoint>(serializedCheckpoint);
                }
                catch (JsonException je)
                {
                    _logger?.LogError(
                        je,
                        "Failed to deserialize storage checkpoint:{serializedCheckpoint}. Skipping download.", serializedCheckpoint);
                }
            }

            return checkpoint?.BlockPosition;
        }

        public BlockData ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<BlockData> target, out bool messageConsumed)
        {
            return (queue as ISourceBlock<BlockData>).ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public IDisposable LinkTo(ITargetBlock<BlockData> target, DataflowLinkOptions linkOptions)
        {
            return queue.LinkTo(target, linkOptions);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<BlockData> target)
        {
            (queue as ISourceBlock<BlockData>).ReleaseReservation(messageHeader, target);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<BlockData> target)
        {
            return (queue as ISourceBlock<BlockData>).ReserveMessage(messageHeader, target);
        }
    }
}
